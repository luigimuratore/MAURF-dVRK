using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using System.IO;

public class DiffusionPolicyModel : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private NNModel diffusionModelAsset;
    [SerializeField] private TextAsset normalizationParams;
    [SerializeField] private int diffusionSteps = 10;
    [SerializeField] private float noiseScheduleStart = 0.01f;
    [SerializeField] private float noiseScheduleEnd = 0.99f;
    [SerializeField] private int actionDimension = 7; // Update based on your joint count + gripper
    [SerializeField] private int stateDimension = 16; // Update based on your observation space

    [Header("Training Data Collection")]
    [SerializeField] private bool collectTrainingData = false;
    [SerializeField] private string dataCollectionPath = "DiffusionTrainingData";
    [SerializeField] private int maxTrajectoriesToCollect = 1000;
    [SerializeField] private float successDistanceThreshold = 0.05f; // Only collect successful trajectories

    [Header("Diffusion Settings")]
    [SerializeField] private bool useTemperatureSchedule = true;
    [SerializeField] private float initialTemperature = 1.0f;
    [SerializeField] private float finalTemperature = 0.1f;
    
    // Runtime model execution variables
    private Model diffusionRuntimeModel;
    private IWorker diffusionWorker;
    private List<float> currentStateFeatures = new List<float>();
    private Queue<Trajectory> trajectoryBuffer = new Queue<Trajectory>();
    private int trajectoriesCollected = 0;
    private bool isModelLoaded = false;
    
    // Input/output normalization parameters
    private float[] stateMin;
    private float[] stateMax;
    private float[] actionMin;
    private float[] actionMax;
    
    // Smoothing variables for hybrid approach
    private float[] lastActions;
    [SerializeField] private float actionSmoothing = 0.3f;
    [SerializeField] private bool hybridMode = true;
    
    // Class to store trajectory data for training
    [System.Serializable]
    public class Trajectory
    {
        public List<float[]> states = new List<float[]>();
        public List<float[]> actions = new List<float[]>();
        public float finalDistance;
        public bool success;
        
        public Trajectory() { }
    }
    
    private Trajectory currentTrajectory;
    
    void Awake()
    {
        LoadModel();
        LoadNormalizationParams();
        
        if (collectTrainingData)
        {
            // Create directory for training data if it doesn't exist
            string fullPath = Path.Combine(Application.persistentDataPath, dataCollectionPath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            
            currentTrajectory = new Trajectory();
        }
    }
    
    void LoadModel()
    {
        if (diffusionModelAsset != null)
        {
            diffusionRuntimeModel = ModelLoader.Load(diffusionModelAsset);
            diffusionWorker = WorkerFactory.CreateWorker(WorkerFactory.Type.ComputePrecompiled, diffusionRuntimeModel);
            isModelLoaded = true;
            Debug.Log("Diffusion policy model loaded successfully.");
        }
        else
        {
            Debug.LogWarning("No diffusion model asset assigned. Will use fallback methods.");
            isModelLoaded = false;
        }
    }
    
    void LoadNormalizationParams()
    {
        if (normalizationParams != null)
        {
            // Parse the JSON normalization parameters
            // This is a placeholder - you'd need to implement proper JSON parsing
            // Example format: {"state_min": [...], "state_max": [...], "action_min": [...], "action_max": [...]}
            Debug.Log("Loaded normalization parameters.");
        }
        else
        {
            // Default to [-1,1] normalization if no file provided
            stateMin = Enumerable.Repeat(-1f, stateDimension).ToArray();
            stateMax = Enumerable.Repeat(1f, stateDimension).ToArray();
            actionMin = Enumerable.Repeat(-1f, actionDimension).ToArray();
            actionMax = Enumerable.Repeat(1f, actionDimension).ToArray();
            
            Debug.LogWarning("No normalization parameters asset assigned. Using default [-1,1] range.");
        }
    }
    
    public void UpdateStateFeatures(List<float> stateFeatures)
    {
        currentStateFeatures = new List<float>(stateFeatures);
        
        // If we're collecting training data, store this state
        if (collectTrainingData && currentTrajectory != null)
        {
            List<float> stateAsList = new List<float>(stateFeatures);
            currentTrajectory.states.Add(stateFeatures.ToArray());
        }
    }
    
    public void RecordAction(float[] actions)
    {
        // Store the action that was actually executed (for training data collection)
        if (collectTrainingData && currentTrajectory != null)
        {
            currentTrajectory.actions.Add((float[])actions.Clone());
        }
    }
    
    public float[] GenerateActions()
    {
        if (!isModelLoaded || !currentStateFeatures.Any())
        {
            return GenerateFallbackActions();
        }
        
        // Normalize state features based on loaded parameters
        float[] normalizedState = NormalizeState(currentStateFeatures.ToArray());
        
        // Initialize with random noise (starting point for denoising)
        float[] actions = new float[actionDimension];
        for (int i = 0; i < actionDimension; i++)
        {
            actions[i] = Random.Range(-1f, 1f);
        }
        
        // Apply temperature to initial noise if using temperature schedule
        if (useTemperatureSchedule)
        {
            float initialTemp = initialTemperature;
            for (int i = 0; i < actionDimension; i++)
            {
                actions[i] *= initialTemp;
            }
        }
        
        // Progressive denoising over multiple steps
        for (int step = 0; step < diffusionSteps; step++)
        {
            // Calculate noise level for this step (from high to low)
            float t = noiseScheduleStart + (noiseScheduleEnd - noiseScheduleStart) * 
                     (float)step / diffusionSteps;
            
            // Calculate temperature for this step if using temperature schedule
            float temperature = 1.0f;
            if (useTemperatureSchedule)
            {
                temperature = initialTemperature + (finalTemperature - initialTemperature) * 
                             (float)step / diffusionSteps;
            }
            
            // Create input tensor with [noise_level, state_features, current_noisy_actions]
            List<float> modelInputs = new List<float>();
            modelInputs.Add(t); // Add noise level
            modelInputs.AddRange(normalizedState); // Add normalized state features
            modelInputs.AddRange(actions); // Add current noisy actions
            
            Tensor inputTensor = new Tensor(1, modelInputs.Count, modelInputs.ToArray());
            
            // Run inference to predict noise to remove
            diffusionWorker.Execute(inputTensor);
            Tensor outputTensor = diffusionWorker.PeekOutput();
            
            // Apply denoising step
            for (int i = 0; i < actionDimension && i < outputTensor.length; i++)
            {
                // Extract predicted noise and remove it from actions
                float predictedNoise = outputTensor[0, i];
                
                // Apply temperature scaling if enabled
                if (useTemperatureSchedule)
                {
                    predictedNoise *= temperature;
                }
                
                actions[i] = actions[i] - predictedNoise;
            }
            
            // Clean up tensors
            inputTensor.Dispose();
        }
        
        // Denormalize actions to the actual action range
        float[] denormalizedActions = DenormalizeActions(actions);
        
        // Apply smoothing if hybrid mode is enabled
        if (hybridMode && lastActions != null)
        {
            for (int i = 0; i < denormalizedActions.Length && i < lastActions.Length; i++)
            {
                denormalizedActions[i] = lastActions[i] * actionSmoothing + 
                                        denormalizedActions[i] * (1 - actionSmoothing);
            }
        }
        
        // Store current actions for next frame smoothing
        lastActions = (float[])denormalizedActions.Clone();
        
        return denormalizedActions;
    }
    
    private float[] GenerateFallbackActions()
    {
        // Fallback method when no model is loaded
        // Can use ML-Agents actions, simple heuristics, or smooth random actions
        
        // Get the ML-Agents component
        Unity.MLAgents.Agent agent = GetComponent<Unity.MLAgents.Agent>();
        int actionSize = actionDimension;
        
        // Generate smooth random actions
        float[] actions = new float[actionSize];
        
        // If we have previous actions, smooth between them and new random actions
        if (lastActions != null)
        {
            for (int i = 0; i < actionSize && i < lastActions.Length; i++)
            {
                float newRandomAction = Random.Range(-0.5f, 0.5f);
                actions[i] = lastActions[i] * 0.95f + newRandomAction * 0.05f;
                actions[i] = Mathf.Clamp(actions[i], -1f, 1f);
            }
        }
        else
        {
            // First run, initialize with small random values
            for (int i = 0; i < actionSize; i++)
            {
                actions[i] = Random.Range(-0.1f, 0.1f);
            }
            lastActions = new float[actionSize];
        }
        
        // Store current actions for next frame
        lastActions = (float[])actions.Clone();
        
        return actions;
    }
    
    private float[] NormalizeState(float[] state)
    {
        float[] normalized = new float[state.Length];
        for (int i = 0; i < state.Length && i < stateMin.Length; i++)
        {
            // Normalize to [-1, 1] range
            normalized[i] = 2.0f * (state[i] - stateMin[i]) / (stateMax[i] - stateMin[i]) - 1.0f;
        }
        return normalized;
    }
    
    private float[] DenormalizeActions(float[] normalizedActions)
    {
        float[] denormalized = new float[normalizedActions.Length];
        for (int i = 0; i < normalizedActions.Length && i < actionMin.Length; i++)
        {
            // Denormalize from [-1, 1] range to actual action range
            denormalized[i] = 0.5f * (normalizedActions[i] + 1.0f) * (actionMax[i] - actionMin[i]) + actionMin[i];
        }
        return denormalized;
    }
    
    // Call this when an episode ends to save trajectory data
    public void EpisodeEnded(bool success, float finalDistance)
    {
        if (collectTrainingData && currentTrajectory != null && 
            currentTrajectory.states.Count > 0 && currentTrajectory.actions.Count > 0)
        {
            // Only save successful trajectories or if we're under threshold
            if (success || finalDistance <= successDistanceThreshold)
            {
                currentTrajectory.finalDistance = finalDistance;
                currentTrajectory.success = success;
                
                // Save trajectory data
                SaveTrajectory(currentTrajectory);
                trajectoriesCollected++;
                
                Debug.Log($"Saved trajectory #{trajectoriesCollected} - Success: {success}, Final distance: {finalDistance}");
                
                // Check if we've collected enough trajectories
                if (trajectoriesCollected >= maxTrajectoriesToCollect)
                {
                    collectTrainingData = false;
                    Debug.Log($"Completed collecting {trajectoriesCollected} trajectories.");
                }
            }
        }
        
        // Reset for next episode
        currentTrajectory = new Trajectory();
    }
    
    private void SaveTrajectory(Trajectory trajectory)
    {
        try
        {
            string fileName = $"trajectory_{System.DateTime.Now.ToString("yyyyMMdd_HHmmss")}_{trajectoriesCollected}.json";
            string fullPath = Path.Combine(Application.persistentDataPath, dataCollectionPath, fileName);
            
            // Convert trajectory to JSON
            string json = JsonUtility.ToJson(trajectory);
            File.WriteAllText(fullPath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error saving trajectory: {e.Message}");
        }
    }
    
    void OnDestroy()
    {
        diffusionWorker?.Dispose();
    }
}