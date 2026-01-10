using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class RobotController : Agent
{
    [SerializeField] private int maxStepsPerEpisode = 500;

    public int ep = 0; // Episode counter
    public int win = 0; // Win counter
    public int winRate = 1; // Win rate percentage

    [System.Serializable]
    public struct Joint
    {
        public string inputAxis;
        public GameObject robotPart;
        public float initialRotation;
        public float initialPosition;
        public JointType jointType;
    }

    public enum JointType
    {
        Revolute,
        Prismatic
    }

    [System.Serializable]
    public struct GripperJoint
    {
        public GameObject leftFinger;
        public GameObject rightFinger;
        public float initialOpenAmount;
        public float maxOpenDistance;
    }

    public Joint[] joints;
    public GripperJoint gripper;
    public bool hasGripper = false;

    [Header("Heuristic Controls")]
    [SerializeField] private KeyCode gripperOpenKey = KeyCode.M;
    [SerializeField] private KeyCode gripperCloseKey = KeyCode.N;


    [Header("Task Objects")]
    public GameObject endEffector;
    public GameObject needle;
    public GameObject needleTip;
    public GameObject targetPlane;
    public GameObject graspingTrigger;

    [Header("Task Settings")]
    [SerializeField] private float graspThreshold = 0.3f;
    [SerializeField] private float needleGraspDistance = 0.02f;
    [SerializeField] private float planeReachDistance = 0.05f;
    [SerializeField] private bool useNeedleTipOnly = true;

    [Header("RL Rewards")]
    [SerializeField] private float rlStepPenalty = -0.001f;
    [SerializeField] private float rlProximityReward = 0.01f;
    [SerializeField] private float rlGraspReward = 50.0f;
    [SerializeField] private float rlCompletionReward = 20.0f;
    [SerializeField] private float rlDropPenalty = -60.0f;
    [SerializeField] private float rlTimeoutPenalty = -0.2f;

    [Header("RL Dense Reward Shaping")]
    [SerializeField] private float rlDistanceRewardScale = 0.1f;
    [SerializeField] private float rlPlaneDistanceRewardScale = 0.15f;
    [SerializeField] private float rlVelocityPenaltyScale = 0.01f;
    [SerializeField] private float rlEfficiencyBonusScale = 0.01f;
    [SerializeField] private float rlMaxReachDistance = 0.5f;
    [SerializeField] private float rlMaxPlaneDistance = 0.3f;
    [SerializeField] private float rlProgressRewardScale = 0.2f;
    [SerializeField] private float rlOrientationRewardScale = 0.1f;
    [SerializeField] private float rlStabilityBonusScale = 0.05f;
    [SerializeField] private float centerShapingScale = 0.5f;      // overall magnitude for potential-based center reward
    [SerializeField] private float centerHeightScale = 2.0f;       // how close in height before center reward becomes significant



    [Header("IL Rewards")]
    [SerializeField] private float ilGraspReward = 1f;
    [SerializeField] private float ilCompletionReward = 2.0f;

    [Header("Visual Feedback")]
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private Material defaultMaterial;
    [SerializeField] private Material PlanedefaultMaterial;
    [SerializeField] private MeshRenderer floorMeshRenderer;
    [SerializeField] private MeshRenderer planeMeshRenderer;
    [SerializeField] private Material planeWinMaterial;

    [SerializeField] private Material needleGraspableMaterial;
    [SerializeField] private Material needleDefaultMaterial;
    private MeshRenderer needleMeshRenderer;
    private bool isNeedleGraspable = false;


    [Header("Trajectory Visualization")]
    [SerializeField] private bool showTrajectoryLines = true;
    [SerializeField] private Color eeToNeedleColor = Color.red;
    [SerializeField] private Color needleToPlaneColor = Color.green;
    [SerializeField] private float lineWidth = 0.01f;

    [Header("Debug Info")]
    [SerializeField] private bool showDistanceInfo = true;
    [SerializeField] private float eeToNeedleDistance = 0f;
    [SerializeField] private float needleToPlaneDistance = 0f;

    [Header("Learning Mode")]
    [SerializeField] private LearningMode currentLearningMode = LearningMode.ReinforcementLearning;

    [Header("Curriculum Learning")]
    [SerializeField] private bool useCurriculumLearning = true;
    [SerializeField] private bool useCLonGraspingSphere = true;
    [SerializeField] private bool useCLonPlaneReachDistance = true;
    [SerializeField] private bool useCLonTargetSize = true;
    [SerializeField] private bool useCLonNeedleSize = true;
    [SerializeField] private bool useCLonRandomizationCoeff = true;

    private float randomizationCoeff = 0f;

    // Default values (used for IL mode or when curriculum is disabled)
    [SerializeField] private float defaultGraspingSphereRadius = 0.02f;
    [SerializeField] private float defaultPlaneReachDistance = 0.05f;
    [SerializeField] private float defaultTargetPlaneScale = 1.0f;
    [SerializeField] private float defaultNeedleScale = 1.0f;

    // Current curriculum values (set by environment parameters)
    private float currentGraspingSphereRadius;
    private float currentPlaneReachDistance;
    private float currentTargetPlaneScale;
    private float currentNeedleScale;

    // Task state tracking
    //private bool needleGrasped = false;
    private bool taskCompleted = false;
    private bool wasNeedleGraspedPreviously = false;
    private bool needleGraspRewardGiven = false; // Add this new flag
    private GameObject currentGraspableObject = null;
    public bool isGrasping { get; private set; } = false;
    private int stepCount = 0;
    private bool debugMode = false;
    private bool needleTipTouchingPlane = false;
    private bool taskCompletedByCollision = false;

    // Progress tracking for better rewards
    private float previousDistanceToNeedle = float.MaxValue;
    private float previousDistanceToPlane = float.MaxValue;
    private float bestDistanceToNeedle = float.MaxValue;
    private float bestDistanceToPlane = float.MaxValue;
    private int stepsWithoutProgress = 0;
    private const int maxStepsWithoutProgress = 100;
    private float previousCenterDist = float.PositiveInfinity;
    private float previousCenterPotential = 0f; // NEW: store potential for potential-based shaping



    // Grasping state
    private GameObject currentlyGraspedObject = null;
    private Vector3 graspedObjectOriginalPos;
    private Quaternion graspedObjectOriginalRot;
    private Rigidbody graspedObjectRigidbody;
    private bool graspedObjectHadGravity = false;
    private Transform originalParent;

    // Trajectory line renderers
    private LineRenderer eeToNeedleLine;
    private LineRenderer needleToplaneLine;

    // References for curriculum learning scaling
    private SphereCollider graspingSphereCollider;
    private Vector3 originalTargetPlaneScale;
    private Vector3 originalNeedleScale;

    public enum LearningMode
    {
        ReinforcementLearning,
        ImitationLearning
    }

    public enum MainTaskType
    {
        Reaching,        // Reach needle with EE
        Placement,       // Place needle on plane
        CompleteMovement // Full task (current RL)
    }

    [Header("Main Task Selection")]
    [SerializeField] public MainTaskType mainTaskType = MainTaskType.CompleteMovement;

    [Header("Domain Randomization")]
    [SerializeField] private bool useDomainRandomization = true;
    [SerializeField] private string domainRandomizationNote = "When enabled, applies randomization to needle position and rotation for better generalization.";
    [SerializeField] private bool useDRanNEEDLE = true;
    [SerializeField] private bool useDRanPLANE = true;
    [SerializeField] private bool useDRanPSM = true;

    [SerializeField] private float planeRandomizationCoeff = 0.001f;



    [Header("Observation Settings")]
    [SerializeField] private bool useCameraObservations = false;
    [SerializeField] private string observationNote = "When enabled, uses minimal vector observations with camera. When disabled, uses full vector observations.";


    private bool episodeEnding = false;


    [Header("Suturing Automation")]
    [SerializeField] private bool enableSuturingAutomation = true;
    [SerializeField] private KeyCode executeArchKey = KeyCode.PageUp;
    [SerializeField] private float archDuration = 1.0f;
    [SerializeField] private AnimationCurve archCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Suturing motion parameters
    [SerializeField] private float pitchRotationDegrees = -360f; // How much to rotate pitch
    [SerializeField] private float rollRotationDegrees = 0f;  // How much to rotate roll
    [SerializeField] private int pitchJointIndex = 4; // Index of pitch joint in your joints array
    [SerializeField] private int rollJointIndex = 3;  // Index of roll joint in your joints array

    // Add stiffness and force parameters
    [SerializeField] private float archStiffness = 10000f; // Add force to the motion
    [SerializeField] private float archForceLimit = 1000f;
    [SerializeField] private float archDamping = 100f;


    //private bool isExecutingArch = false;
    private Coroutine archCoroutine;


    [Header("Multi-Agent Settings")]
    [SerializeField] private bool isMultiAgentTask = false;
    [SerializeField] private SuturingTaskManager taskManager;
    [SerializeField] private int agentID = 1; // 1 for PSM1, 2 for PSM2

    // Make these public for task manager access:
    public bool needleGrasped { get; private set; }
    public bool isExecutingArch { get; private set; }
    //public GameObject needleTip;


    private int stepsHoldingPosition = 0;
    private Vector3 lastNeedleTipPosition = Vector3.zero;
    private const float maxAllowedMovement = 0.001f; // 1mm tolerance

    // Add public method to make arch coroutine accessible:
    public Coroutine PerformSuturingArchMotion()
    {
        if (archCoroutine != null)
        {
            StopCoroutine(archCoroutine);
        }
        archCoroutine = StartCoroutine(PerformSuturingArchMotionCoroutine());
        return archCoroutine;
    }



    void Start()
    {
        //Academy.Instance.InferenceSeed = 42; // Fissa il seed per l’inferenza

        LogSystemConfiguration();

        ValidateReferences();

        // Validate required references
        if (endEffector == null)
        {
            Debug.LogError("End effector not assigned!");
        }

        if (needle == null)
        {
            Debug.LogError("Needle not assigned!");
        }

        if (targetPlane == null)
        {
            Debug.LogError("Target plane not assigned!");
        }

        // Store initial joint positions
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].robotPart == null)
            {
                Debug.LogError($"Joint at index {i} has a null robotPart reference!");
                continue;
            }

            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();
            if (controller != null)
            {
                Joint joint = joints[i];
                joint.initialRotation = controller.CurrentPrimaryAxisRotation();
                joints[i] = joint;
            }
        }

        // Initialize gripper if it exists
        if (hasGripper)
        {
            if (gripper.leftFinger == null || gripper.rightFinger == null)
            {
                Debug.LogError("Gripper fingers not properly assigned!");
            }
            else
            {
                SetGripperOpenAmount(gripper.initialOpenAmount);
            }
        }

        // Store original floor material
        if (floorMeshRenderer != null)
        {
            defaultMaterial = floorMeshRenderer.material;
        }

        if (needle != null)
        {
            needleMeshRenderer = needle.GetComponent<MeshRenderer>();
            if (needleMeshRenderer != null)
            {
                needleDefaultMaterial = needleMeshRenderer.material;
            }
        }


        // Create trajectory line renderers
        CreateTrajectoryLines();

        // Store original scales before any curriculum modifications
        if (targetPlane != null)
        {
            originalTargetPlaneScale = targetPlane.transform.localScale;
        }

        if (needle != null)
        {
            originalNeedleScale = needle.transform.localScale;
        }

        // Get reference to grasping sphere collider
        if (graspingTrigger != null)
        {
            graspingSphereCollider = graspingTrigger.GetComponent<SphereCollider>();
            if (graspingSphereCollider == null)
            {
                Debug.LogError("Grasping trigger must have a SphereCollider component for curriculum learning!");
            }
        }
        // Initialize curriculum parameters
        InitializeCurriculumParameters();

        // Initial reset the needle
        ResetNeedlePosition();



    }



    private void LogSystemConfiguration()
    {
        Debug.Log("=== ROBOT CONTROLLER CONFIGURATION ===");

        // Task Type Information
        Debug.Log($"TASK TYPE: {mainTaskType}");
        string taskDescription = GetTaskDescription(mainTaskType);
        Debug.Log($"TASK DESCRIPTION: {taskDescription}");

        // Learning Mode Information
        Debug.Log($"LEARNING MODE: {currentLearningMode}");

        // Reward Function Information
        string rewardFunction = GetRewardFunctionName();
        Debug.Log($"REWARD FUNCTION: {rewardFunction}");

        // Curriculum Learning Status
        if (useCurriculumLearning && currentLearningMode == LearningMode.ReinforcementLearning)
        {
            Debug.Log("CURRICULUM LEARNING: ENABLED");
        }
        else
        {
            Debug.Log("CURRICULUM LEARNING: DISABLED");
        }

        // Domain Randomization Status
        Debug.Log($"DOMAIN RANDOMIZATION: {(useDomainRandomization ? "ENABLED" : "DISABLED")}");

        // Observation Type
        Debug.Log($"OBSERVATIONS: {(useCameraObservations ? "CAMERA + MINIMAL VECTOR" : "FULL VECTOR")}");

        // Key Reward Values
        if (currentLearningMode == LearningMode.ReinforcementLearning)
        {
            Debug.Log("--- RL REWARD VALUES ---");
            Debug.Log($"Completion Reward: +{rlCompletionReward}");
            Debug.Log($"Grasp Reward: +{rlGraspReward}");
            Debug.Log($"Drop Penalty: {rlDropPenalty}");
            Debug.Log($"Step Penalty: {rlStepPenalty}");
            Debug.Log($"Max Steps: {maxStepsPerEpisode}");
        }
        else
        {
            Debug.Log("--- IL REWARD VALUES ---");
            Debug.Log($"Completion Reward: +{ilCompletionReward}");
            Debug.Log($"Grasp Reward: +{ilGraspReward}");
        }

        Debug.Log("========================================");
    }

    private string GetTaskDescription(MainTaskType taskType)
    {
        switch (taskType)
        {
            case MainTaskType.Reaching:
                return "Reach needle with end effector";
            case MainTaskType.Placement:
                return "Place needle on target plane (auto-grasped)";
            case MainTaskType.CompleteMovement:
                return "Full task: reach + grasp + place needle";
            default:
                return "Unknown task type";
        }
    }

    private string GetRewardFunctionName()
    {
        string baseName = "";

        if (currentLearningMode == LearningMode.ReinforcementLearning)
        {
            switch (mainTaskType)
            {
                case MainTaskType.Reaching:
                    baseName = "CalculateReachingRLReward()";
                    break;
                case MainTaskType.Placement:
                    baseName = "CalculatePlacementRLReward()";
                    break;
                case MainTaskType.CompleteMovement:
                    baseName = "CalculatePerfectRLReward()";
                    break;
            }
            baseName += " [REINFORCEMENT LEARNING]";
        }
        else
        {
            switch (mainTaskType)
            {
                case MainTaskType.Reaching:
                    baseName = "CalculateReachingILReward()";
                    break;
                case MainTaskType.Placement:
                    baseName = "CalculatePlacementILReward()";
                    break;
                case MainTaskType.CompleteMovement:
                    baseName = "CalculateOptimizedILReward()";
                    break;
            }
            baseName += " [IMITATION LEARNING]";
        }

        return baseName;
    }


    void Update()
    {

        // Update trajectory lines
        UpdateTrajectoryLines();

        // Update distance information
        UpdateDistanceInfo();

        // DEBUG: Press P to print which joint is being moved
        if (Input.GetKeyDown(KeyCode.P))
        {
            for (int i = 0; i < joints.Length; i++)
            {
                Debug.Log($"Joint {i}: inputAxis = '{joints[i].inputAxis}', type = {joints[i].jointType}");
            }
        }



        // Manual gripper control for testing
        if (Input.GetKey(KeyCode.M))
        {
            if (hasGripper)
            {
                float currentAmount = GetCurrentGripperOpenAmount();
                SetGripperOpenAmount(Mathf.Min(currentAmount + 0.05f, 1.0f));
            }
        }
        else if (Input.GetKey(KeyCode.N))
        {
            if (hasGripper)
            {
                float currentAmount = GetCurrentGripperOpenAmount();
                SetGripperOpenAmount(Mathf.Max(currentAmount - 0.05f, -1.0f));
            }
        }

        // Toggle debug mode
        if (Input.GetKeyDown(KeyCode.Backslash))
        {
            debugMode = !debugMode;
            Debug.Log($"Debug mode: {(debugMode ? "ON" : "OFF")}");
        }

        // Toggle trajectory lines
        if (Input.GetKeyDown(KeyCode.C))
        {
            showTrajectoryLines = !showTrajectoryLines;
            SetTrajectoryLinesVisibility(showTrajectoryLines);
            Debug.Log($"Trajectory lines: {(showTrajectoryLines ? "ON" : "OFF")}");
        }

        // Toggle distance info display
        if (Input.GetKeyDown(KeyCode.X))
        {
            showDistanceInfo = !showDistanceInfo;
            Debug.Log($"Distance info: {(showDistanceInfo ? "ON" : "OFF")}");
        }

        // Execute suturing arch on key press
        if (enableSuturingAutomation && Input.GetKeyDown(executeArchKey) && !isExecutingArch)
        {
           
            ExecuteSuturingArch();
            
            
        }

        // Cancel arch execution
        if (isExecutingArch && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelSuturingArch();
        }

    }

    private void CreateTrajectoryLines()
    {
        // Create End Effector to Needle line
        GameObject eeToNeedleObj = new GameObject("EE_to_Needle_Line");
        eeToNeedleObj.transform.parent = transform;
        eeToNeedleLine = eeToNeedleObj.AddComponent<LineRenderer>();
        eeToNeedleLine.material = new Material(Shader.Find("Sprites/Default"));
        eeToNeedleLine.material.color = eeToNeedleColor;
        eeToNeedleLine.startWidth = lineWidth;
        eeToNeedleLine.endWidth = lineWidth;
        eeToNeedleLine.positionCount = 2;
        eeToNeedleLine.useWorldSpace = true;

        // Create Needle to Plane line
        GameObject needleToPlaneObj = new GameObject("Needle_to_Plane_Line");
        needleToPlaneObj.transform.parent = transform;
        needleToplaneLine = needleToPlaneObj.AddComponent<LineRenderer>();
        needleToplaneLine.material = new Material(Shader.Find("Sprites/Default"));
        needleToplaneLine.material.color = needleToPlaneColor;
        needleToplaneLine.startWidth = lineWidth;
        needleToplaneLine.endWidth = lineWidth;
        needleToplaneLine.positionCount = 2;
        needleToplaneLine.useWorldSpace = true;

        // Set initial visibility
        SetTrajectoryLinesVisibility(showTrajectoryLines);
    }

    private void UpdateTrajectoryLines()
    {
        if (!showTrajectoryLines || endEffector == null || needle == null || targetPlane == null)
            return;

        // Update End Effector to Needle line
        if (eeToNeedleLine != null)
        {
            eeToNeedleLine.SetPosition(0, endEffector.transform.position);
            eeToNeedleLine.SetPosition(1, needle.transform.position);

            // Change color based on grasping state
            if (!needleGrasped)
            {
                eeToNeedleLine.material.color = eeToNeedleColor;
            }
            else
            {
                eeToNeedleLine.material.color = Color.gray; // Dim when grasped
            }
        }

        // Update Needle to Plane line (use needle tip if available)
        if (needleToplaneLine != null)
        {
            Vector3 needlePosition = useNeedleTipOnly && needleTip != null ?
                                   needleTip.transform.position :
                                   needle.transform.position;

            needleToplaneLine.SetPosition(0, needlePosition);
            needleToplaneLine.SetPosition(1, targetPlane.transform.position);

            // Change color based on task state
            if (needleGrasped && !taskCompleted)
            {
                needleToplaneLine.material.color = needleToPlaneColor;
            }
            else
            {
                needleToplaneLine.material.color = Color.gray; // Dim when needle not grasped
            }
        }
    }

    private void SetTrajectoryLinesVisibility(bool visible)
    {
        if (eeToNeedleLine != null)
            eeToNeedleLine.enabled = visible;

        if (needleToplaneLine != null)
            needleToplaneLine.enabled = visible;
    }


    public override void OnEpisodeBegin()
    {
        ep++;
        episodeEnding = false;
        taskCompleted = false;

        if (ep % 100 == 1 || ep <= 3)
        {
            Debug.Log($"[EPISODE {ep}] Task: {mainTaskType} | Mode: {currentLearningMode} | " +
                    $"Reward Function: {GetRewardFunctionName().Split('[')[0].Trim()}");
        }

        float newGraspingRadius = currentGraspingSphereRadius;
        float newPlaneReachDistance = currentPlaneReachDistance;
        float newPlaneScale = currentTargetPlaneScale;
        float newNeedleScale = currentNeedleScale;

        SetGripperOpenAmount(0.8f);

        // Update curriculum parameters if needed
        if (useCurriculumLearning)
        {
            if (useCLonGraspingSphere)
            {
                newGraspingRadius = Academy.Instance.EnvironmentParameters.GetWithDefault("grasping_sphere_radius", defaultGraspingSphereRadius);
            }

            if (useCLonPlaneReachDistance)
            {
                newPlaneReachDistance = Academy.Instance.EnvironmentParameters.GetWithDefault("plane_reach_distance", defaultPlaneReachDistance);
            }

            if (useCLonTargetSize)
            {
                newPlaneScale = Academy.Instance.EnvironmentParameters.GetWithDefault("target_plane_scale", defaultTargetPlaneScale);
            }

            if (useCLonNeedleSize)
            {
                newNeedleScale = Academy.Instance.EnvironmentParameters.GetWithDefault("needle_scale", defaultNeedleScale);
            }

            if (useCLonRandomizationCoeff)
            {
                randomizationCoeff = Academy.Instance.EnvironmentParameters.GetWithDefault("randomization_coeff", 0f);
            }

            bool parametersChanged =
                Mathf.Abs(newGraspingRadius - currentGraspingSphereRadius) > 0.001f ||
                Mathf.Abs(newPlaneReachDistance - currentPlaneReachDistance) > 0.001f ||
                Mathf.Abs(newPlaneScale - currentTargetPlaneScale) > 0.001f ||
                Mathf.Abs(newNeedleScale - currentNeedleScale) > 0.001f;

            if (parametersChanged)
            {
                currentGraspingSphereRadius = newGraspingRadius;
                currentPlaneReachDistance = newPlaneReachDistance;
                currentTargetPlaneScale = newPlaneScale;
                currentNeedleScale = newNeedleScale;
                UpdateAllCurriculumParameters();
            }

        }



        // Reset task state flags - but handle placement task differently
        if (mainTaskType != MainTaskType.Placement)
        {
            needleGrasped = false;
            needleGraspRewardGiven = false;

            // Force release any grasped objects
            if (isGrasping || currentlyGraspedObject != null)
                ForceReleaseObject();

        }


        // Reset ALL task state flags
        taskCompleted = false;
        wasNeedleGraspedPreviously = false;
        taskCompletedByCollision = false;
        stepCount = 0;
        isNeedleGraspable = false;
        needleTipTouchingPlane = false;
        currentGraspableObject = null;

        // Reset progress tracking variables
        previousDistanceToNeedle = float.MaxValue;
        previousDistanceToPlane = float.MaxValue;
        bestDistanceToNeedle = float.MaxValue;
        bestDistanceToPlane = float.MaxValue;
        stepsWithoutProgress = 0;



        // Robust reset: repeat several times to ensure physics settle
        for (int i = 0; i < 3; i++)
        {
            SetGripperOpenAmount(gripper.initialOpenAmount);
            ResetJointPositionsRobust();
            ResetVelocities();
            ResetNeedlePosition();
            ResetPlanePosition();
            ResetVelocities();
        }

        UpdateNeedleVisualFeedback();



        Debug.Log("[RESET] Reset sequence completed!");


        // Calculate win rate
        if (ep > 1)
            winRate = (int)(((float)win / ep) * 100);
        else
            winRate = 0;

        Debug.Log($"Episode {ep} started. Win rate: {win}/{ep - 1}: {winRate}% - Mode: {currentLearningMode}");

    }


    private void ForceReleaseObject()
    {
        if (currentlyGraspedObject != null)
        {

            // Restore object properties
            currentlyGraspedObject.transform.parent = originalParent;

            if (graspedObjectRigidbody != null)
            {
                graspedObjectRigidbody.useGravity = graspedObjectHadGravity;
                graspedObjectRigidbody.isKinematic = false;

                // Force stop any movement
                graspedObjectRigidbody.velocity = Vector3.zero;
                graspedObjectRigidbody.angularVelocity = Vector3.zero;

                // Force wake up the rigidbody
                graspedObjectRigidbody.WakeUp();
            }

            currentlyGraspedObject = null;
            graspedObjectRigidbody = null;
        }

        // Reset ALL grasping flags - be thorough
        isGrasping = false;
        needleGrasped = false;
        currentGraspableObject = null;
        wasNeedleGraspedPreviously = false;
        needleGraspRewardGiven = false;

    }


    private void ResetJointPositionsRobust()
    {

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].robotPart == null) continue;

            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();
            ArticulationBody articulationBody = joints[i].robotPart.GetComponent<ArticulationBody>();

            // Force stop all movement first
            if (articulationBody != null)
            {
                articulationBody.velocity = Vector3.zero;
                articulationBody.angularVelocity = Vector3.zero;

                // Reset joint positions through ArticulationBody
                if (joints[i].jointType == JointType.Revolute)
                {
                    var drive = articulationBody.xDrive;
                    drive.target = joints[i].initialRotation * Mathf.Deg2Rad; // Convert to radians
                    articulationBody.xDrive = drive;
                }
                else if (joints[i].jointType == JointType.Prismatic)
                {
                    ArticulationDrive drive = GetPrismaticDrive(articulationBody);
                    drive.target = joints[i].initialPosition;
                    SetPrismaticDrive(articulationBody, drive);
                }
            }

            // Also use controller if available (backup method)
            if (joints[i].jointType == JointType.Revolute && controller != null)
            {
                controller.rotationState = RotationDirection.None;
                controller.RotateTo(joints[i].initialRotation);
            }

        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        if (useCameraObservations)
        {

            Debug.Log($"[CAMERA MODE] NO observations");
        }
        else
        {
            // FULL VECTOR OBSERVATIONS with null checks
            if (endEffector == null || needle == null || targetPlane == null)
            {
                Debug.LogError("Critical GameObjects are null! Cannot collect observations.");
                return;
            }

            // Observe end effector position (3 values)
            sensor.AddObservation(endEffector.transform.localPosition);

            // Observe needle position (3 values)
            sensor.AddObservation(needle.transform.localPosition);

            // Observe needle tip position if available (3 values)
            if (useNeedleTipOnly && needleTip != null)
            {
                sensor.AddObservation(needleTip.transform.localPosition);
            }
            else
            {
                sensor.AddObservation(needle.transform.localPosition); // Fallback to needle center
            }

            // Observe target plane position (3 values)
            sensor.AddObservation(targetPlane.transform.localPosition);

            // Observe joint positions with null checks
            if (joints != null)
            {
                foreach (Joint joint in joints)
                {
                    if (joint.robotPart == null)
                    {
                        sensor.AddObservation(0f);
                        continue;
                    }

                    ArticulationJointController controller = joint.robotPart.GetComponent<ArticulationJointController>();
                    if (controller != null)
                    {
                        if (joint.jointType == JointType.Revolute)
                        {
                            float normalizedRotation = controller.CurrentPrimaryAxisRotation() / 180.0f;
                            sensor.AddObservation(normalizedRotation);
                        }
                        else if (joint.jointType == JointType.Prismatic)
                        {
                            float currentPos = controller.CurrentPosition();
                            ArticulationBody artBody = joint.robotPart.GetComponent<ArticulationBody>();
                            if (artBody != null)
                            {
                                ArticulationDrive drive = GetPrismaticDrive(artBody);
                                float range = drive.upperLimit - drive.lowerLimit;
                                float normalizedPos = range != 0 ? (currentPos - drive.lowerLimit) / range : 0;
                                sensor.AddObservation(normalizedPos);
                            }
                            else
                            {
                                sensor.AddObservation(0f);
                            }
                        }
                    }
                    else
                    {
                        sensor.AddObservation(0f);
                    }
                }
            }

            // Add gripper state observation
            if (hasGripper)
            {
                float gripperOpenAmount = GetCurrentGripperOpenAmount();
                sensor.AddObservation(gripperOpenAmount);
            }

            // Add task state observations
            sensor.AddObservation(needleGrasped ? 1.0f : 0.0f);
            sensor.AddObservation(isGrasping ? 1.0f : 0.0f);
            sensor.AddObservation(taskCompleted ? 1.0f : 0.0f);

            // Add curriculum parameters as observations ONLY for RL mode
            if (useCurriculumLearning)
            {
                sensor.AddObservation(currentGraspingSphereRadius);
                sensor.AddObservation(currentPlaneReachDistance);
                sensor.AddObservation(currentTargetPlaneScale);
                sensor.AddObservation(currentNeedleScale);
            }
            else
            {
                // For IL mode or when curriculum is disabled, use default constant values
                sensor.AddObservation(defaultGraspingSphereRadius);
                sensor.AddObservation(defaultPlaneReachDistance);
                sensor.AddObservation(defaultTargetPlaneScale);
                sensor.AddObservation(defaultNeedleScale);
            }

        }

        // Add multi-agent observations
        if (isMultiAgentTask && taskManager != null)
        {
            // One-hot encode current phase (10 phases)
            int phaseCount = System.Enum.GetValues(typeof(SuturingTaskManager.SuturingPhase)).Length;
            for (int i = 0; i < phaseCount; i++)
            {
                sensor.AddObservation(i == (int)taskManager.GetCurrentPhase() ? 1f : 0f);
            }

            // Agent ID
            sensor.AddObservation(agentID == 1 ? 1f : 0f);
        }
    


    }


    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (episodeEnding) return;

        // Check if task was completed by collision FIRST
        if (taskCompleted)
        {
            //EndEpisode();
            return;
        }

        // Process robot actions
        ProcessJointActions(actionBuffers);

        // Update states
        CheckGraspingConditions();
        UpdateTaskState();

        // Calculate rewards
        CalculateReward();

        // Check for episode end conditions
        stepCount++;
        if (stepCount >= maxStepsPerEpisode)
        {
            ep++;
            if (floorMeshRenderer != null && loseMaterial != null)
                floorMeshRenderer.material = loseMaterial;

            AddReward(rlTimeoutPenalty);
            EndEpisode();
        }
    }




    private void CalculateReward()
    {
        if (currentLearningMode == LearningMode.ReinforcementLearning)
        {
            switch (mainTaskType)
            {
                case MainTaskType.Reaching:
                    CalculateReachingRLReward();
                    break;
                case MainTaskType.Placement:
                    CalculatePlacementRLReward();
                    break;
                case MainTaskType.CompleteMovement:
                    CalculatePerfectRLReward();
                    break;
            }
        }
        else
        {
            switch (mainTaskType)
            {
                case MainTaskType.Reaching:
                    CalculateReachingILReward();
                    break;
                case MainTaskType.Placement:
                    CalculatePlacementILReward();
                    break;
                case MainTaskType.CompleteMovement:
                    CalculateOptimizedILReward();
                    break;
            }
        }
    }

    // private void CalculatePerfectRLReward()
    // {
    //     if (taskCompleted) return;

    //     float reward = rlStepPenalty; // Small time penalty

    //     // Simple jerk limitation - penalize high velocities -- TO TEST
    //     if (endEffector != null)
    //     {
    //         Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
    //         if (eeRb != null)
    //         {
    //             float velocityPenalty = eeRb.velocity.magnitude * 0.05f; // Adjust multiplier as needed
    //             AddReward(-velocityPenalty);
    //         }
    //     }

    //     // PHASE 1: Reach and grasp needle
    //     if (!needleGrasped)
    //     {
    //         float distToNeedle = Vector3.Distance(endEffector.transform.position, needle.transform.position);

    //         // DENSE proximity reward - much weaker
    //         float proximityReward = Mathf.Max(0, 1.0f - distToNeedle / rlMaxReachDistance) * 0.001f;
    //         reward += proximityReward;

    //         // PROGRESS reward - only for improvement
    //         if (previousDistanceToNeedle != float.MaxValue)
    //         {
    //             float progress = previousDistanceToNeedle - distToNeedle;
    //             if (progress > 0)
    //             {
    //                 reward += progress * 0.05f;
    //                 stepsWithoutProgress = 0;
    //             }
    //             else
    //             {
    //                 stepsWithoutProgress++;
    //                 // Penalty for stagnation
    //                 if (stepsWithoutProgress > 50)
    //                     reward -= 0.1f;
    //             }
    //         }
    //         previousDistanceToNeedle = distToNeedle;

    //         // Small bonus when very close
    //         if (distToNeedle <= currentGraspingSphereRadius * 2.0f)
    //         {
    //             reward += 0.01f;
    //         }
    //     }
    //     else
    //     {
    //         // PHASE 2: ONE-TIME grasp reward (+50)
    //         if (!needleGraspRewardGiven)
    //         {
    //             reward += 50.0f;
    //             needleGraspRewardGiven = true;
    //             previousDistanceToPlane = float.MaxValue;
    //             Debug.Log("[RL] Needle grasped! +50 reward");
    //         }

    //         // PHASE 3: Place needle on target plane
    //         Vector3 needlePos = useNeedleTipOnly && needleTip != null ?
    //                            needleTip.transform.position : needle.transform.position;
    //         float distToPlane = Vector3.Distance(needlePos, targetPlane.transform.position);

    //         // DENSE proximity reward for placement (much weaker)
    //         float planeProximityReward = Mathf.Max(0, 1.0f - distToPlane / rlMaxPlaneDistance) * 0.001f;
    //         reward += planeProximityReward;

    //         // PROGRESS reward for placement (only for improvement)
    //         if (previousDistanceToPlane != float.MaxValue)
    //         {
    //             float planeProgress = previousDistanceToPlane - distToPlane;
    //             if (planeProgress > 0)
    //             {
    //                 reward += planeProgress * 0.05f;
    //                 stepsWithoutProgress = 0;
    //             }
    //             else
    //             {
    //                 stepsWithoutProgress++;
    //                 if (stepsWithoutProgress > 50)
    //                     reward -= 0.1f;
    //             }
    //         }
    //         previousDistanceToPlane = distToPlane;

    //         // Small bonus for proximity
    //         if (distToPlane <= currentPlaneReachDistance * 2.0f)
    //         {
    //             reward += 0.05f;
    //         }

    //         // COMPLETION: BIG SUCCESS REWARD (+200)
    //         if (distToPlane <= currentPlaneReachDistance)
    //         {
    //             reward += 200.0f;



    //             if (floorMeshRenderer != null && winMaterial != null)
    //                 floorMeshRenderer.material = winMaterial;

    //             Debug.Log($"[RL COMPLETE] SUCCESS! +200 completion reward.");

    //             AddReward(reward);

    //             taskCompleted = true;
    //             win++;
    //             EndEpisode();
    //             return;
    //         }
    //     }

    //     // BIG PENALTY: Dropping the needle (-8)
    //     if (wasNeedleGraspedPreviously && !needleGrasped && needleGraspRewardGiven)
    //     {
    //         reward -= 8.0f;
    //         Debug.Log("[RL] Needle dropped! -8 penalty");
    //     }

    //     AddReward(reward);
    // }




    private void CalculatePerfectRLReward()
    {
        if (taskCompleted) return;

        float reward = rlStepPenalty; // Small time penalty

        // Simple jerk limitation - penalize high velocities
        if (endEffector != null)
        {
            Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
            if (eeRb != null)
            {
                float velocityPenalty = eeRb.velocity.magnitude * 0.05f;
                AddReward(-velocityPenalty);
            }
        }

        // PHASE 1: Reach and grasp needle
        if (!needleGrasped)
        {
            float distToNeedle = Vector3.Distance(endEffector.transform.position, needle.transform.position);

            // DENSE proximity reward
            float proximityReward = Mathf.Max(0, 1.0f - distToNeedle / rlMaxReachDistance) * 0.001f;
            reward += proximityReward;

            // PROGRESS reward - only for improvement
            if (previousDistanceToNeedle != float.MaxValue)
            {
                float progress = previousDistanceToNeedle - distToNeedle;
                if (progress > 0)
                {
                    reward += progress * 0.05f;
                    stepsWithoutProgress = 0;
                }
                else
                {
                    stepsWithoutProgress++;
                    if (stepsWithoutProgress > 50)
                        reward -= 0.1f;
                }
            }
            previousDistanceToNeedle = distToNeedle;

            // Small bonus when very close
            if (distToNeedle <= currentGraspingSphereRadius * 2.0f)
            {
                reward += 0.01f;
            }
        }
        else
        {
            // PHASE 2: ONE-TIME grasp reward (+50)
            if (!needleGraspRewardGiven)
            {
                reward += 50.0f;
                needleGraspRewardGiven = true;
                previousDistanceToPlane = float.MaxValue;
                Debug.Log("[RL] Needle grasped! +50 reward");
            }

            // PHASE 3: Place needle on target plane
            Vector3 needlePos = useNeedleTipOnly && needleTip != null ?
                               needleTip.transform.position : needle.transform.position;
            float distToPlane = Vector3.Distance(needlePos, targetPlane.transform.position);

            // DENSE proximity reward for placement
            float planeProximityReward = Mathf.Max(0, 1.0f - distToPlane / rlMaxPlaneDistance) * 0.001f;
            reward += planeProximityReward;

            // PROGRESS reward for placement
            if (previousDistanceToPlane != float.MaxValue)
            {
                float planeProgress = previousDistanceToPlane - distToPlane;
                if (planeProgress > 0)
                {
                    reward += planeProgress * 0.05f;
                    stepsWithoutProgress = 0;
                }
                else
                {
                    stepsWithoutProgress++;
                    if (stepsWithoutProgress > 50)
                        reward -= 0.1f;
                }
            }
            previousDistanceToPlane = distToPlane;

            // Small bonus for proximity
            if (distToPlane <= currentPlaneReachDistance * 2.0f)
            {
                reward += 0.05f;
            }

            // COMPLETION: Check collision detection instead of distance
            if (needleTipTouchingPlane)
            {
                reward += 200.0f;

                if (floorMeshRenderer != null && winMaterial != null)
                    floorMeshRenderer.material = winMaterial;

                Debug.Log($"[RL COMPLETE] SUCCESS! Needle tip touched plane. +200 completion reward.");

                AddReward(reward);

                taskCompleted = true;
                win++;
                EndEpisode();
                return;
            }
        }

        // BIG PENALTY: Dropping the needle (-8)
        if (wasNeedleGraspedPreviously && !needleGrasped && needleGraspRewardGiven)
        {
            reward -= 8.0f;
            Debug.Log("[RL] Needle dropped! -8 penalty");
        }

        AddReward(reward);
    }




    // RL: Reaching task (EE to needle)
    private void CalculateReachingRLReward()
    {
        if (taskCompleted || episodeEnding) return;

        float distToNeedle = Vector3.Distance(endEffector.transform.position, needle.transform.position);

        // Small step penalty
        AddReward(-0.001f);

        // Velocity penalty for smoother movement
        if (endEffector != null)
        {
            Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
            if (eeRb != null)
            {
                float velocityPenalty = eeRb.velocity.magnitude * 0.01f;
                AddReward(-velocityPenalty);
            }
        }

        // Initialize tracking
        if (previousDistanceToNeedle == float.MaxValue)
        {
            previousDistanceToNeedle = distToNeedle;
            bestDistanceToNeedle = distToNeedle;
            return;
        }

        // DENSE REWARD: Progress-based shaping
        float progress = previousDistanceToNeedle - distToNeedle; // >0 when getting closer

        if (progress > 0f)
        {
            // Reward improvement with stronger signal when very close
            float proximityMultiplier = 1f + Mathf.Max(0, (currentGraspingSphereRadius * 3f - distToNeedle) / currentGraspingSphereRadius);
            float progressReward = progress * 5.0f * proximityMultiplier; // Stronger than your placement task
            AddReward(progressReward);

            // Track best distance
            if (distToNeedle < bestDistanceToNeedle)
            {
                bestDistanceToNeedle = distToNeedle;
                AddReward(0.1f); // Bonus for new best
            }

            stepsWithoutProgress = 0;
        }
        else
        {
            // Penalty for moving away
            stepsWithoutProgress++;
            AddReward(progress * 2.0f); // Negative penalty

            if (stepsWithoutProgress > 50)
                AddReward(-0.05f); // Stagnation penalty
        }

        // Proximity bonus when getting very close
        if (distToNeedle <= currentGraspingSphereRadius * 2.0f)
        {
            AddReward(0.1f);
        }

        // Update tracking
        previousDistanceToNeedle = distToNeedle;

        // SUCCESS: Terminal reward
        if (distToNeedle <= currentGraspingSphereRadius)
        {
            episodeEnding = true;
            taskCompleted = true;
            win++;

            if (floorMeshRenderer != null && winMaterial != null)
                floorMeshRenderer.material = winMaterial;

            // Large completion reward
            AddReward(50f);
            Debug.Log($"[RL REACH] Success! Distance: {distToNeedle:F4}, Reward: +50");
            //EndEpisode();
        }

        // Optional debug every 50 steps
        if (stepCount % 50 == 0)
        {
            Debug.Log($"[REACH] dist={distToNeedle:F3} prog={progress:F4} best={bestDistanceToNeedle:F3}");
        }
    }



    // // RL: Placement task (needle to plane) - IMPROVED VERSION
    // private void CalculatePlacementRLReward()
    // {
    //     if (taskCompleted || episodeEnding) return;

    //     // Apply small time penalty to encourage efficiency
    //     AddReward(-0.001f);

    //     // Use tip if available
    //     Vector3 needlePos = (useNeedleTipOnly && needleTip != null)
    //         ? needleTip.transform.position
    //         : needle.transform.position;

    //     float dist = Vector3.Distance(needlePos, targetPlane.transform.position);

    //     // Terminal success (single condition)
    //     if (dist <= currentPlaneReachDistance)
    //     {
    //         taskCompleted = true;
    //         win++;
    //         if (floorMeshRenderer != null && winMaterial != null)
    //             floorMeshRenderer.material = winMaterial;

    //         // Larger terminal reward
    //         AddReward(50f);
    //         Debug.Log($"[PLACEMENT SUCCESS] Distance: {dist:F4}, Reward: +50");
    //         EndEpisode();
    //         return;
    //     }

    //     // Initialize baseline
    //     if (previousDistanceToPlane == float.MaxValue)
    //     {
    //         previousDistanceToPlane = dist;
    //         bestDistanceToPlane = dist;
    //         return;
    //     }

    //     // Progress shaping: reward = delta distance
    //     float progress = previousDistanceToPlane - dist; // > 0 when closer
    //     float reward = 0f;

    //     // Balanced progress rewards
    //     if (progress > 0f)
    //     {
    //         // More reasonable scale for progress rewards
    //         reward = progress * 2f;

    //         // Extra bonus for new best distance
    //         if (dist < bestDistanceToPlane)
    //         {
    //             bestDistanceToPlane = dist;
    //             reward += 0.1f; // Small bonus for new best
    //         }

    //         // Proximity bonus when getting very close
    //         if (dist < currentPlaneReachDistance * 2.0f)
    //             reward += 0.05f;
    //     }
    //     else
    //     {
    //         // Balanced penalty (should be proportional to reward)
    //         reward = progress * 1.5f;

    //         // Cap negative rewards to prevent collapse
    //         reward = Mathf.Max(reward, -0.2f);
    //     }

    //     // Velocity penalty to discourage erratic movement
    //     if (endEffector != null)
    //     {
    //         Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
    //         if (eeRb != null)
    //         {
    //             float velocityPenalty = eeRb.velocity.magnitude * 0.01f;
    //             reward -= velocityPenalty;
    //         }
    //     }

    //     previousDistanceToPlane = dist;
    //     AddReward(reward);

    //     // More frequent debug information
    //     if ((stepCount % 50 == 0) || reward > 0.5f || reward < -0.1f)
    //         Debug.Log($"[PLACE] dist={dist:F3} prog={progress:F4} reward={reward:F3} best={bestDistanceToPlane:F3}");
    // }



    // // RL: Placement task (needle to plane) - IMPROVED VERSION with principled centering shaping
    // private void CalculatePlacementRLReward()
    // {
    //     if (taskCompleted || episodeEnding) return;

    //     // small time penalty
    //     AddReward(-0.001f);

    //     // use tip if available
    //     Vector3 needlePos = (useNeedleTipOnly && needleTip != null)
    //         ? needleTip.transform.position
    //         : needle.transform.position;

    //     // height distance to plane (primary)
    //     Vector3 planeCenter = targetPlane.transform.position;
    //     Vector3 planeNormal = targetPlane.transform.up;
    //     float heightDist = Mathf.Abs(Vector3.Dot(needlePos - planeCenter, planeNormal));

    //     // lateral distance to plane center (secondary)
    //     Vector3 projectedPos = needlePos - (Vector3.Dot(needlePos - planeCenter, planeNormal) * planeNormal);
    //     float centerDist = Vector3.Distance(projectedPos, planeCenter);

    //     // Terminal success
    //     if (heightDist <= currentPlaneReachDistance)
    //     {
    //         taskCompleted = true;
    //         win++;
    //         if (floorMeshRenderer != null && winMaterial != null)
    //             floorMeshRenderer.material = winMaterial;

    //         AddReward(50f);
    //         Debug.Log($"[PLACEMENT SUCCESS] HeightDist: {heightDist:F4}, Reward: +50");
    //         EndEpisode();
    //         return;
    //     }

    //     // initialize trackers
    //     if (previousDistanceToPlane == float.MaxValue)
    //     {
    //         previousDistanceToPlane = heightDist;
    //         bestDistanceToPlane = heightDist;
    //         previousCenterDist = float.PositiveInfinity;
    //         previousCenterPotential = 0f;
    //         return;
    //     }

    //     // PRIMARY: progress reward on height (make this dominate)
    //     float heightProgress = previousDistanceToPlane - heightDist; // >0 when getting closer
    //     float reward = 0f;
    //     if (heightProgress > 0f)
    //     {
    //         float heightReward = heightProgress * 3.0f; // stronger weight for approaching plane
    //         reward += heightReward;

    //         if (heightDist < bestDistanceToPlane)
    //         {
    //             bestDistanceToPlane = heightDist;
    //             reward += 0.05f;
    //         }

    //         if (heightDist < currentPlaneReachDistance * 2.0f)
    //             reward += 0.02f;
    //         stepsWithoutProgress = 0;
    //     }
    //     else
    //     {
    //         stepsWithoutProgress++;
    //         reward += Mathf.Max(heightProgress * 1.0f, -0.2f);
    //         if (stepsWithoutProgress > 80)
    //             reward -= 0.05f; // small stagnation penalty
    //     }

    //     // VELOCITY penalty for stability
    //     if (endEffector != null)
    //     {
    //         Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
    //         if (eeRb != null)
    //         {
    //             reward -= eeRb.velocity.magnitude * rlVelocityPenaltyScale;
    //         }
    //     }

    //     // --- CENTERING: potential-based shaping (principled, preserves optimal policy) ---
    //     // approximate plane radius (based on plane scale)
    //     float planeRadiusApprox = Mathf.Max(0.01f, currentTargetPlaneScale * 0.5f);

    //     // normalized lateral distance (0..1)
    //     float centerDistNormalized = Mathf.Clamp01(centerDist / planeRadiusApprox);
    //     // potential: higher when closer to center
    //     float centerPotential = 1f - centerDistNormalized;

    //     // height factor: only reward center when reasonably close in height
    //     float heightFactor = Mathf.Clamp01(1f - (heightDist / (currentPlaneReachDistance * centerHeightScale)));

    //     // potential difference -> potential-based shaping: deltaPhi = centerPotential' - previousCenterPotential
    //     float deltaPotential = centerPotential - previousCenterPotential;
    //     float centerReward = centerShapingScale * deltaPotential * heightFactor;

    //     // clamp to avoid overpowering height objective
    //     centerReward = Mathf.Clamp(centerReward, -0.1f, 0.15f);

    //     reward += centerReward;

    //     // update previous potentials for next step
    //     previousCenterPotential = centerPotential;

    //     // update trackers
    //     previousDistanceToPlane = heightDist;

    //     AddReward(reward);

    //     // debug
    //     if ((stepCount % 50 == 0) || reward > 0.5f || reward < -0.1f)
    //     {
    //         Debug.Log($"[PLACE] height={heightDist:F3} hProg={heightProgress:F4} center={centerDist:F3} centerPot={centerPotential:F3} reward={reward:F3}");
    //     }
    // }



    // RL: Placement task (needle to plane) - VERSION with collision detection
    private void CalculatePlacementRLReward()
    {
        if (taskCompleted || episodeEnding) return;

        // Apply small time penalty
        AddReward(-0.001f);

        // Use tip if available
        Vector3 needlePos = (useNeedleTipOnly && needleTip != null)
            ? needleTip.transform.position
            : needle.transform.position;

        // Get plane center and normal for shaping rewards
        Vector3 planeCenter = targetPlane.transform.position;
        Vector3 planeNormal = targetPlane.transform.up;

        // Calculate height distance to plane (for shaping only, not success condition)
        float heightDist = Mathf.Abs(Vector3.Dot(needlePos - planeCenter, planeNormal));

        // Calculate lateral distance to plane center (for shaping only)
        Vector3 projectedPos = needlePos - (Vector3.Dot(needlePos - planeCenter, planeNormal) * planeNormal);
        float centerDist = Vector3.Distance(projectedPos, planeCenter);

        // SUCCESS CONDITION: Only check collision flag, not distance
        // The collision is set by OnNeedleTipTouchPlane() callback
        if (needleTipTouchingPlane)
        {
            taskCompleted = true;
            win++;
            if (floorMeshRenderer != null && winMaterial != null)
                floorMeshRenderer.material = winMaterial;

            AddReward(50f);
            Debug.Log($"[PLACEMENT SUCCESS] Collision detected! Reward: +50");
            //EndEpisode();
            return;
        }

        // Initialize trackers
        if (previousDistanceToPlane == float.MaxValue)
        {
            previousDistanceToPlane = heightDist;
            bestDistanceToPlane = heightDist;
            previousCenterDist = float.PositiveInfinity;
            previousCenterPotential = 0f;
            return;
        }

        // Progress reward on height (shaping toward collision)
        float heightProgress = previousDistanceToPlane - heightDist;
        float reward = 0f;

        if (heightProgress > 0f)
        {
            float heightReward = heightProgress * 3.0f;
            reward += heightReward;

            if (heightDist < bestDistanceToPlane)
            {
                bestDistanceToPlane = heightDist;
                reward += 0.05f;
            }

            if (heightDist < currentPlaneReachDistance * 2.0f)
                reward += 0.02f;
            stepsWithoutProgress = 0;
        }
        else
        {
            stepsWithoutProgress++;
            reward += Mathf.Max(heightProgress * 1.0f, -0.2f);
            if (stepsWithoutProgress > 80)
                reward -= 0.05f;
        }

        // Velocity penalty
        if (endEffector != null)
        {
            Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
            if (eeRb != null)
            {
                reward -= eeRb.velocity.magnitude * rlVelocityPenaltyScale;
            }
        }

        // Centering shaping (potential-based)
        float planeRadiusApprox = Mathf.Max(0.01f, currentTargetPlaneScale * 0.5f);
        float centerDistNormalized = Mathf.Clamp01(centerDist / planeRadiusApprox);
        float centerPotential = 1f - centerDistNormalized;
        float heightFactor = Mathf.Clamp01(1f - (heightDist / (currentPlaneReachDistance * centerHeightScale)));

        float deltaPotential = centerPotential - previousCenterPotential;
        float centerReward = centerShapingScale * deltaPotential * heightFactor;
        centerReward = Mathf.Clamp(centerReward, -0.1f, 0.15f);
        reward += centerReward;

        previousCenterPotential = centerPotential;
        previousDistanceToPlane = heightDist;

        AddReward(reward);

        if ((stepCount % 50 == 0) || reward > 0.5f || reward < -0.1f)
        {
            Debug.Log($"[PLACE] height={heightDist:F3} collision={needleTipTouchingPlane} reward={reward:F3}");
        }
    }





    // // RL: Placement task (needle to plane) - IMPROVED VERSION with exponential hold-still reward
    // private void CalculatePlacementRLReward()
    // {
    //     if (taskCompleted || episodeEnding) return;

    //     // Apply small time penalty
    //     AddReward(-0.001f);

    //     // Use tip if available
    //     Vector3 needlePos = (useNeedleTipOnly && needleTip != null)
    //         ? needleTip.transform.position
    //         : needle.transform.position;

    //     // Get plane center and normal for shaping rewards
    //     Vector3 planeCenter = targetPlane.transform.position;
    //     Vector3 planeNormal = targetPlane.transform.up;

    //     // Calculate height distance to plane (for shaping only, not success condition)
    //     float heightDist = Mathf.Abs(Vector3.Dot(needlePos - planeCenter, planeNormal));

    //     // Calculate lateral distance to plane center (for shaping only)
    //     Vector3 projectedPos = needlePos - (Vector3.Dot(needlePos - planeCenter, planeNormal) * planeNormal);
    //     float centerDist = Vector3.Distance(projectedPos, planeCenter);

    //     // Check if needle tip is touching the plane
    //     if (needleTipTouchingPlane)
    //     {
    //         // Initialize tracking on first contact
    //         if (lastNeedleTipPosition == Vector3.zero)
    //         {
    //             lastNeedleTipPosition = needlePos;
    //             stepsHoldingPosition = 0;
    //             Debug.Log("[HOLD] Initial contact with plane detected!");
    //         }

    //         float movement = Vector3.Distance(needlePos, lastNeedleTipPosition);

    //         // Exponential reward for staying still
    //         if (movement <= maxAllowedMovement)
    //         {
    //             stepsHoldingPosition++;

    //             // Progressive exponential reward system
    //             // Starts slower, grows faster as agent proves it can maintain position
    //             float baseReward = 0.005f; // Smaller starting reward
    //             float growthRate = 1.08f;  // 8% growth per step (faster growth)
    //             float exponentialFactor = Mathf.Pow(growthRate, Mathf.Min(stepsHoldingPosition, 100)); // Cap at 100 steps
    //             float holdReward = baseReward * exponentialFactor;

    //             // Progressive cap that increases with holding time
    //             float maxRewardCap = Mathf.Min(0.5f + (stepsHoldingPosition * 0.01f), 3.0f);
    //             holdReward = Mathf.Min(holdReward, maxRewardCap);

    //             AddReward(holdReward);

    //             // Milestone bonuses for stability
    //             if (stepsHoldingPosition == 10)
    //             {
    //                 AddReward(0.5f);
    //                 Debug.Log("[HOLD] 10 steps milestone! Bonus: +0.5");
    //             }
    //             else if (stepsHoldingPosition == 25)
    //             {
    //                 AddReward(1.0f);
    //                 Debug.Log("[HOLD] 25 steps milestone! Bonus: +1.0");
    //             }
    //             else if (stepsHoldingPosition == 50)
    //             {
    //                 AddReward(2.0f);
    //                 Debug.Log("[HOLD] 50 steps milestone! Bonus: +2.0");
    //             }

    //             // Success after minimum hold duration
    //             if (stepsHoldingPosition >= 0 && !taskCompleted) // Reduced from 50 to 30
    //             {
    //                 taskCompleted = true;
    //                 win++;
    //                 if (floorMeshRenderer != null && winMaterial != null)
    //                     floorMeshRenderer.material = winMaterial;

    //                 // Bigger completion bonus
    //                 float completionBonus = 100f;
    //                 AddReward(completionBonus);

    //                 Debug.Log($"[PLACEMENT SUCCESS] Held stable for {stepsHoldingPosition} steps! Completion bonus: +{completionBonus}");
    //                 return;
    //             }

    //             // Periodic progress logging
    //             if (stepsHoldingPosition % 5 == 0)
    //             {
    //                 Debug.Log($"[HOLD] Stable for {stepsHoldingPosition} steps, reward: +{holdReward:F4} (capped at {maxRewardCap:F2})");
    //             }
    //         }
    //         else
    //         {
    //             // Adaptive penalty based on how long agent was holding
    //             float basePenalty = -10.0f;
    //             float holdingBonus = stepsHoldingPosition * 0.5f; // More forgiveness for longer holds
    //             float adaptivePenalty = basePenalty - holdingBonus;

    //             // More severe penalty for large movements
    //             float movementPenalty = -movement * 50f; // Increased multiplier
    //             float totalPenalty = adaptivePenalty + movementPenalty;

    //             AddReward(totalPenalty);

    //             // Partial credit: don't fully reset if agent held for a while
    //             if (stepsHoldingPosition > 10)
    //             {
    //                 stepsHoldingPosition = (int)(stepsHoldingPosition * 0.5f); // Keep 50% of progress
    //                 Debug.Log($"[HOLD] Movement detected ({movement:F6}), partial reset. Penalty: {totalPenalty:F3}");
    //             }
    //             else
    //             {
    //                 stepsHoldingPosition = 0; // Full reset for short holds
    //                 Debug.Log($"[HOLD] Movement detected ({movement:F6}), full reset. Penalty: {totalPenalty:F3}");
    //             }
    //         }

    //         lastNeedleTipPosition = needlePos;
    //         return; // Don't process shaping rewards while in contact
    //     }
    //     else
    //     {
    //         // Progressive penalty for leaving collision zone
    //         if (stepsHoldingPosition > 0)
    //         {
    //             // More severe penalty if agent was close to success
    //             float baseLeavePenalty = -10.0f;
    //             float progressPenalty = -(stepsHoldingPosition * 0.2f); // Worse if you were holding longer
    //             float totalLeavePenalty = baseLeavePenalty + progressPenalty;

    //             AddReward(totalLeavePenalty);
    //             Debug.Log($"[HOLD] Lost contact after {stepsHoldingPosition} steps! Penalty: {totalLeavePenalty:F3}");
    //         }

    //         // Reset holding trackers when not touching plane
    //         stepsHoldingPosition = 0;
    //         lastNeedleTipPosition = Vector3.zero;
    //     }

    //     // Initialize trackers
    //     if (previousDistanceToPlane == float.MaxValue)
    //     {
    //         previousDistanceToPlane = heightDist;
    //         bestDistanceToPlane = heightDist;
    //         previousCenterDist = float.PositiveInfinity;
    //         previousCenterPotential = 0f;
    //         return;
    //     }

    //     // Progress reward on height (shaping toward collision)
    //     float heightProgress = previousDistanceToPlane - heightDist;
    //     float reward = 0f;

    //     if (heightProgress > 0f)
    //     {
    //         // Scale reward based on proximity (stronger when close)
    //         float proximityMultiplier = 1f + Mathf.Max(0, (currentPlaneReachDistance * 3f - heightDist) / currentPlaneReachDistance);
    //         float heightReward = heightProgress * 3.0f * proximityMultiplier;
    //         reward += heightReward;

    //         if (heightDist < bestDistanceToPlane)
    //         {
    //             bestDistanceToPlane = heightDist;
    //             reward += 0.1f; // Increased from 0.05
    //         }

    //         if (heightDist < currentPlaneReachDistance * 2.0f)
    //             reward += 0.05f; // Increased from 0.02

    //         stepsWithoutProgress = 0;
    //     }
    //     else
    //     {
    //         stepsWithoutProgress++;
    //         // Progressive stagnation penalty
    //         reward += Mathf.Max(heightProgress * 1.5f, -0.3f); // Increased penalty cap

    //         if (stepsWithoutProgress > 50) // Earlier penalty
    //             reward -= 0.1f;
    //         if (stepsWithoutProgress > 100)
    //             reward -= 0.2f; // Escalating penalty
    //     }

    //     // Velocity penalty (encourage smooth approach)
    //     if (endEffector != null)
    //     {
    //         Rigidbody eeRb = endEffector.GetComponent<Rigidbody>();
    //         if (eeRb != null)
    //         {
    //             // Stronger penalty when close to target
    //             float velocityScale = heightDist < currentPlaneReachDistance * 2f ? 2f : 1f;
    //             reward -= eeRb.velocity.magnitude * rlVelocityPenaltyScale * velocityScale;
    //         }
    //     }

    //     // Centering shaping (potential-based) - only when approaching
    //     if (heightDist < currentPlaneReachDistance * 3f)
    //     {
    //         float planeRadiusApprox = Mathf.Max(0.01f, currentTargetPlaneScale * 0.5f);
    //         float centerDistNormalized = Mathf.Clamp01(centerDist / planeRadiusApprox);
    //         float centerPotential = 1f - centerDistNormalized;
    //         float heightFactor = Mathf.Clamp01(1f - (heightDist / (currentPlaneReachDistance * centerHeightScale)));

    //         float deltaPotential = centerPotential - previousCenterPotential;
    //         float centerReward = centerShapingScale * deltaPotential * heightFactor;
    //         centerReward = Mathf.Clamp(centerReward, -0.15f, 0.2f); // Slightly increased range
    //         reward += centerReward;

    //         previousCenterPotential = centerPotential;
    //     }

    //     previousDistanceToPlane = heightDist;
    //     AddReward(reward);

    //     if ((stepCount % 50 == 0) || reward > 0.5f || reward < -0.2f)
    //     {
    //         Debug.Log($"[PLACE] height={heightDist:F3} collision={needleTipTouchingPlane} stagnant={stepsWithoutProgress} reward={reward:F3}");
    //     }
    // }



    // IL: Reaching task
    private void CalculateReachingILReward()
    {
        if (taskCompleted) return;
        float distToNeedle = Vector3.Distance(endEffector.transform.position, needle.transform.position);

        if (distToNeedle <= currentGraspingSphereRadius)
        {
            AddReward(ilCompletionReward);
            taskCompleted = true;
            win++; ep++;
            if (floorMeshRenderer != null && winMaterial != null)
                floorMeshRenderer.material = winMaterial;
            Debug.Log("[IL REACH] Needle reached! Reward: " + ilCompletionReward);
            EndEpisode();
        }
    }

    // IL: Placement task
    private void CalculatePlacementILReward()
    {
        if (taskCompleted) return;
        if (needleGrasped)
        {
            Vector3 needlePos = useNeedleTipOnly && needleTip != null ? needleTip.transform.position : needle.transform.position;
            float distToPlane = Vector3.Distance(needlePos, targetPlane.transform.position);

            if (distToPlane <= currentPlaneReachDistance)
            {
                AddReward(ilCompletionReward);
                taskCompleted = true;
                win++; ep++;
                if (floorMeshRenderer != null && winMaterial != null)
                    floorMeshRenderer.material = winMaterial;
                Debug.Log("[IL PLACE] Needle placed! Reward: " + ilCompletionReward);
                EndEpisode();
            }
        }
    }


    private void UpdateTaskState()
    {
        wasNeedleGraspedPreviously = needleGrasped;

    }


    private void CalculateOptimizedILReward()
    {
        // Safety check - don't process if episode is ending
        if (taskCompleted) return;

        float reward = 0f;

        // SPARSE REWARD 1: One-time bonus for grasping the needle
        if (needleGrasped && !needleGraspRewardGiven)
        {
            AddReward(ilGraspReward);
            needleGraspRewardGiven = true;

            // END EPISODE IMMEDIATELY AFTER GRASPING IN IL MODE
            win++; // Count this as a successful episode
            ep++;

            if (floorMeshRenderer != null && winMaterial != null)
                floorMeshRenderer.material = winMaterial;

            Debug.Log($"[IL SUCCESS] Needle grasped! Episode ending. Reward: {ilGraspReward}");

            EndEpisode();
            return;

        }

        // PENALTY: Dropping the needle after grasping (important for IL)
        if (wasNeedleGraspedPreviously && !needleGrasped && needleGraspRewardGiven)
        {
            reward += -0.3f; // Penalty for dropping
            Debug.Log("[IL] Needle dropped penalty applied");
        }

        // Only add reward if there's actually a reward to give
        if (reward != 0f)
        {
            AddReward(reward);
        }
    }



    private void CheckGraspingConditions()
    {
        if (!hasGripper) return;

        float gripperOpenAmount = GetCurrentGripperOpenAmount();

        // Check if needle is in graspable position (within range and gripper ready)
        bool needleInRange = false;
        if (currentGraspableObject == needle)
        {
            float distanceToNeedle = Vector3.Distance(endEffector.transform.position, needle.transform.position);
            needleInRange = distanceToNeedle <= currentGraspingSphereRadius;
        }

        // Update needle color based on graspable state
        bool newGraspableState = needleInRange && !isGrasping && !needleGrasped;
        if (newGraspableState != isNeedleGraspable)
        {
            isNeedleGraspable = newGraspableState;
            UpdateNeedleVisualFeedback();
        }

        // FIXED: Try to grasp if needle is in range, not already grasping, and gripper is CLOSED
        // Changed from checking if gripper is CLOSING to checking if it IS CLOSED
        if (!isGrasping && currentGraspableObject == needle && needleInRange)
        {
            // Grasp if gripper is closed (below threshold)
            if (gripperOpenAmount < graspThreshold)
            {
                GraspObject(needle);
                needleGrasped = true;
                UpdateNeedleVisualFeedback();
            }
        }
        // Release if gripper is opened wide enough while grasping
        else if (isGrasping && gripperOpenAmount > (graspThreshold + 0.2f))
        {
            ReleaseObject();
            UpdateNeedleVisualFeedback();
        }
    }


    private void UpdateNeedleVisualFeedback()
    {
        if (needleMeshRenderer == null) return;

        if (needleGrasped)
        {
            // When grasped, use a different color (e.g., blue)
            needleMeshRenderer.material.color = Color.blue;
        }
        else if (isNeedleGraspable)
        {
            // When graspable, use bright green or the graspable material
            if (needleGraspableMaterial != null)
            {
                needleMeshRenderer.material = needleGraspableMaterial;
            }
            else
            {
                needleMeshRenderer.material.color = Color.green;
            }
        }
        else
        {
            // Default state - restore original material/color
            if (needleDefaultMaterial != null)
            {
                needleMeshRenderer.material = needleDefaultMaterial;
            }
            else
            {
                needleMeshRenderer.material.color = Color.white;
            }
        }
    }




    public void OnGraspTriggerEnter(GameObject obj)
    {
        if (obj == needle && !isGrasping) // ADD check to prevent conflicts
        {
            currentGraspableObject = obj;
        }
    }

    public void OnGraspTriggerExit(GameObject obj)
    {
        if (obj == currentGraspableObject && !isGrasping) // Only clear if not currently grasping
        {
            currentGraspableObject = null;
        }
    }

    private void ResetJointPositions()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].robotPart == null) continue;

            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();
            ArticulationBody articulationBody = joints[i].robotPart.GetComponent<ArticulationBody>();

            if (joints[i].jointType == JointType.Revolute && controller != null)
            {
                controller.RotateTo(joints[i].initialRotation);
            }
            else if (joints[i].jointType == JointType.Prismatic && articulationBody != null)
            {
                ArticulationDrive drive = GetPrismaticDrive(articulationBody);
                drive.target = joints[i].initialPosition;
                SetPrismaticDrive(articulationBody, drive);
            }
        }

        if (hasGripper)
        {
            SetGripperOpenAmount(gripper.initialOpenAmount);
        }
    }

    private void ResetNeedlePosition()
    {
        if (needle != null)
        {
            Vector3 basePosition;
            Vector3 baseRotation;

            // Set different positions based on task type
            switch (mainTaskType)
            {
                case MainTaskType.Reaching:
                    // Standard reaching position - needle on surface
                    basePosition = new Vector3(-0.135f, -0.247f, -0.104f);
                    baseRotation = new Vector3(-238f, 456f, 305f);
                    break;

                case MainTaskType.Placement:
                    // Placement task - needle should be near the robot arm (already grasped position)
                    basePosition = new Vector3(0.0014f, 0.0019f, -0.0016f); // Closer to end effector
                    baseRotation = new Vector3(-90f, -90f, 0f); // Upright orientation

                    // Auto-grasp the needle for placement tasks
                    StartCoroutine(AutoGraspNeedleForPlacement());
                    break;

                case MainTaskType.CompleteMovement:
                default:
                    // Default position for complete movement
                    basePosition = new Vector3(-0.138f, -0.244f, -0.102f);
                    baseRotation = new Vector3(-194.5f, 526f, 355f);
                    break;
            }

            Vector3 finalPosition = basePosition;
            Vector3 finalRotation = baseRotation;

            // Only apply randomization if enabled for this task type AND domain randomization is enabled
            if (useDomainRandomization && useDRanNEEDLE)
            {
                if (!Academy.Instance.IsCommunicatorOn)
                {
                    randomizationCoeff = 0.005f;
                }



                // Add randomization
                Vector3 randomOffset = new Vector3(
                    Random.Range(-randomizationCoeff, randomizationCoeff),
                    Random.Range(-randomizationCoeff * 0.5f, randomizationCoeff * 0.5f),
                    Random.Range(-randomizationCoeff, randomizationCoeff)
                );

                finalPosition = basePosition + randomOffset;

                Vector3 randomRotation = new Vector3(
                    Random.Range(-randomizationCoeff * 100f, randomizationCoeff * 100f),
                    Random.Range(-randomizationCoeff * 100f, randomizationCoeff * 100f),
                    Random.Range(-randomizationCoeff * 100f, randomizationCoeff * 100f)
                );

                finalRotation = baseRotation + randomRotation;
            }

            // Set the needle position and rotation
            needle.transform.localPosition = finalPosition;
            needle.transform.localRotation = Quaternion.Euler(finalRotation);

            // Reset needle physics
            Rigidbody needleRb = needle.GetComponent<Rigidbody>();
            if (needleRb != null)
            {
                needleRb.velocity = Vector3.zero;
                needleRb.angularVelocity = Vector3.zero;
            }

            Debug.Log($"[NEEDLE RESET] Task: {mainTaskType}, Position: {finalPosition}, Randomized: {useDomainRandomization}");
        }
    }

    private void ResetPlanePosition()
    {
        if (targetPlane == null) return;

        Vector3 basePosition = new Vector3(-0.055f, 0.2086f, -0.049f); // Set your default plane position here


        if (useDRanPLANE && useDomainRandomization)
        {
            if (!Academy.Instance.IsCommunicatorOn)
                planeRandomizationCoeff = 0.005f;
            else
                planeRandomizationCoeff = randomizationCoeff * 3f;
        }


        Vector3 randomOffset = new Vector3(
            Random.Range(-planeRandomizationCoeff * 0.1f, planeRandomizationCoeff * 0.1f), //profondità
            Random.Range(-planeRandomizationCoeff * 0.5f, planeRandomizationCoeff * 0.5f), //altezza
            Random.Range(-planeRandomizationCoeff, planeRandomizationCoeff) //laterale
        );

        targetPlane.transform.localPosition = basePosition + randomOffset;
    }





    // Add this coroutine to auto-grasp the needle for placement tasks
    private System.Collections.IEnumerator AutoGraspNeedleForPlacement()
    {
        // Wait a frame for physics to settle
        yield return null;

        if (mainTaskType == MainTaskType.Placement && needle != null && hasGripper)
        {
            // Set the needle as graspable object
            currentGraspableObject = needle;

            // Close the gripper
            SetGripperOpenAmount(0.1f); // Mostly closed

            // Wait a moment
            yield return new WaitForSeconds(0.1f);

            // Force grasp the needle
            GraspObject(needle);
            needleGrasped = true;
            isGrasping = true;

            // Update visual feedback
            UpdateNeedleVisualFeedback();

            Debug.Log("[PLACEMENT TASK] Needle auto-grasped for placement task");
        }
    }



    private void ResetVelocities()
    {
        foreach (Joint joint in joints)
        {
            if (joint.robotPart == null) continue;

            ArticulationBody artBody = joint.robotPart.GetComponent<ArticulationBody>();
            if (artBody != null)
            {
                artBody.velocity = Vector3.zero;
                artBody.angularVelocity = Vector3.zero;
            }
        }

        if (hasGripper)
        {
            if (gripper.leftFinger != null)
            {
                ArticulationBody leftBody = gripper.leftFinger.GetComponent<ArticulationBody>();
                if (leftBody != null)
                {
                    leftBody.velocity = Vector3.zero;
                    leftBody.angularVelocity = Vector3.zero;
                }
            }
            if (gripper.rightFinger != null)
            {
                ArticulationBody rightBody = gripper.rightFinger.GetComponent<ArticulationBody>();
                if (rightBody != null)
                {
                    rightBody.velocity = Vector3.zero;
                    rightBody.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    private void ProcessJointActions(ActionBuffers actionBuffers)
    {
        int jointCount = joints.Length;

        for (int i = 0; i < jointCount && i < actionBuffers.ContinuousActions.Length - (hasGripper ? 1 : 0); i++)
        {
            float action = actionBuffers.ContinuousActions[i];
            if (Mathf.Abs(action) <= 0.1f) continue;

            if (joints[i].robotPart == null) continue;

            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();
            if (controller != null)
            {
                if (joints[i].jointType == JointType.Revolute)
                {
                    RotationDirection direction = action > 0.1f ? RotationDirection.Positive :
                                              (action < -0.1f ? RotationDirection.Negative : RotationDirection.None);
                    controller.rotationState = direction;
                }
            }
        }

        // Process gripper action
        if (hasGripper && actionBuffers.ContinuousActions.Length > jointCount)
        {
            float gripperAction = actionBuffers.ContinuousActions[jointCount];
            float currentAmount = GetCurrentGripperOpenAmount();
            float newAmount = Mathf.Clamp01(currentAmount + gripperAction * 0.1f);
            SetGripperOpenAmount(newAmount);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Process input for each joint
        int jointCount = joints.Length;
        for (int i = 0; i < jointCount && i < continuousActionsOut.Length - (hasGripper ? 1 : 0); i++)
        {
            // if (i == 4 && joints[i].jointType == JointType.Prismatic)
            // {
            //     if (Input.GetKey(KeyCode.PageUp))
            //         continuousActionsOut[i] = 1.0f;
            //     else if (Input.GetKey(KeyCode.PageDown))
            //         continuousActionsOut[i] = -1.0f;
            //     else
            //         continuousActionsOut[i] = 0f;
            //     continue;
            // }

            float inputVal = Input.GetAxisRaw(joints[i].inputAxis);
            continuousActionsOut[i] = Mathf.Abs(inputVal) > 0.1f ? inputVal : 0f;
        }

        // Process gripper input
        if (hasGripper && continuousActionsOut.Length > jointCount)
        {
            if (Input.GetKey(gripperOpenKey))
                continuousActionsOut[jointCount] = 1.0f;
            else if (Input.GetKey(gripperCloseKey))
                continuousActionsOut[jointCount] = -1.0f;
            else
                continuousActionsOut[jointCount] = 0f;
        }
    }

    // Gripper control methods
    public void SetGripperOpenAmount(float amount)
    {
        float clampedAmount = Mathf.Clamp01(amount);

        if (hasGripper && gripper.leftFinger != null && gripper.rightFinger != null)
        {
            ArticulationBody leftFingerBody = gripper.leftFinger.GetComponent<ArticulationBody>();
            ArticulationBody rightFingerBody = gripper.rightFinger.GetComponent<ArticulationBody>();

            if (leftFingerBody != null && rightFingerBody != null)
            {
                float leftTarget = clampedAmount * gripper.maxOpenDistance;
                float rightTarget = clampedAmount * gripper.maxOpenDistance;

                ArticulationDrive leftDrive = leftFingerBody.xDrive;
                leftDrive.target = leftTarget;
                leftFingerBody.xDrive = leftDrive;

                ArticulationDrive rightDrive = rightFingerBody.xDrive;
                rightDrive.target = rightTarget;
                rightFingerBody.xDrive = rightDrive;
            }
        }
    }

    public float GetCurrentGripperOpenAmount()
    {
        if (!hasGripper || gripper.leftFinger == null)
            return 0f;

        ArticulationBody leftFingerBody = gripper.leftFinger.GetComponent<ArticulationBody>();
        if (leftFingerBody != null)
        {
            float currentPos = leftFingerBody.xDrive.target;
            return currentPos / gripper.maxOpenDistance;
        }

        return 0f;
    }

    private void GraspObject(GameObject obj)
    {
        if (obj == null) return;

        currentlyGraspedObject = obj;
        originalParent = obj.transform.parent;
        graspedObjectOriginalPos = obj.transform.position;
        graspedObjectOriginalRot = obj.transform.rotation;

        graspedObjectRigidbody = obj.GetComponent<Rigidbody>();

        if (graspedObjectRigidbody != null)
        {
            graspedObjectHadGravity = graspedObjectRigidbody.useGravity;
            graspedObjectRigidbody.useGravity = false;
            graspedObjectRigidbody.isKinematic = true;
        }

        // IMPORTANT: Calculate the offset BEFORE parenting to preserve grasp position
        Vector3 graspOffset = obj.transform.position - endEffector.transform.position;
        Quaternion graspRotationOffset = Quaternion.Inverse(endEffector.transform.rotation) * obj.transform.rotation;

        // Parent the object
        obj.transform.parent = endEffector.transform;

        // CRITICAL: Maintain the grasp position by setting the local position to the calculated offset
        obj.transform.localPosition = endEffector.transform.InverseTransformDirection(graspOffset);
        obj.transform.localRotation = graspRotationOffset;

        isGrasping = true;

    }

    public void ReleaseObject()
    {
        if (!isGrasping || currentlyGraspedObject == null) return;

        bool wasNeedle = (currentlyGraspedObject == needle);

        // Reset object physics
        currentlyGraspedObject.transform.parent = originalParent;
        if (graspedObjectRigidbody != null)
        {
            graspedObjectRigidbody.useGravity = graspedObjectHadGravity;
            graspedObjectRigidbody.isKinematic = false;
        }

        currentlyGraspedObject = null;
        isGrasping = false;

        if (wasNeedle)
        {
            needleGrasped = false;
            // FIXED: Always set this flag when needle is released after being grasped
            if (needleGraspRewardGiven)
            {
                wasNeedleGraspedPreviously = true;
            }
        }
    }

    // Helper methods for prismatic joints
    private ArticulationDrive GetPrismaticDrive(ArticulationBody artBody)
    {
        if (artBody.linearLockX == ArticulationDofLock.FreeMotion)
            return artBody.xDrive;
        else if (artBody.linearLockY == ArticulationDofLock.FreeMotion)
            return artBody.yDrive;
        else
            return artBody.zDrive;
    }

    private void SetPrismaticDrive(ArticulationBody artBody, ArticulationDrive drive)
    {
        if (artBody.linearLockX == ArticulationDofLock.FreeMotion)
            artBody.xDrive = drive;
        else if (artBody.linearLockY == ArticulationDofLock.FreeMotion)
            artBody.yDrive = drive;
        else
            artBody.zDrive = drive;
    }

    // Joint control methods
    public void StopAllJointRotations()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i].robotPart != null)
                UpdateRotationState(RotationDirection.None, joints[i].robotPart);
        }
    }

    public void RotateJoint(int jointIndex, RotationDirection direction)
    {
        StopAllJointRotations();
        if (jointIndex >= 0 && jointIndex < joints.Length && joints[jointIndex].robotPart != null)
        {
            UpdateRotationState(direction, joints[jointIndex].robotPart);
        }
    }

    static void UpdateRotationState(RotationDirection direction, GameObject robotPart)
    {
        ArticulationJointController jointController = robotPart.GetComponent<ArticulationJointController>();
        if (jointController != null)
        {
            jointController.rotationState = direction;
        }
    }

    // Missing methods that other scripts are calling
    public void OnGraspTriggerTargetCollision(GameObject obj)
    {
        // Handle target collision if needed
    }

    public void OnEndEffectorLose()
    {
        // Handle end effector lose event if needed
    }

    private void UpdateDistanceInfo()
    {
        if (endEffector == null || needle == null || targetPlane == null) return;

        // Calculate EE to Needle distance
        eeToNeedleDistance = Vector3.Distance(endEffector.transform.position, needle.transform.position);

        // Calculate Needle to Plane distance (use needle tip if available)
        Vector3 needlePosition = useNeedleTipOnly && needleTip != null ?
                               needleTip.transform.position :
                               needle.transform.position;

        needleToPlaneDistance = Vector3.Distance(needlePosition, targetPlane.transform.position);

        // Simple debug output every 30 frames (about 2 times per second)
        if (showDistanceInfo && Time.frameCount % 30 == 0)
        {
            //Debug.Log($"EE->Needle: {eeToNeedleDistance:F3}m | Needle->Plane: {needleToPlaneDistance:F3}m | Grasped: {needleGrasped}");
        }
    }

    private void InitializeCurriculumParameters()
    {
        // Only use curriculum learning with Reinforcement Learning mode
        if (useCurriculumLearning)
        {
            // Get curriculum parameters from ML-Agents Academy

            currentGraspingSphereRadius = Academy.Instance.EnvironmentParameters.GetWithDefault("grasping_sphere_radius", defaultGraspingSphereRadius);
            currentPlaneReachDistance = Academy.Instance.EnvironmentParameters.GetWithDefault("plane_reach_distance", defaultPlaneReachDistance);
            currentTargetPlaneScale = Academy.Instance.EnvironmentParameters.GetWithDefault("target_plane_scale", defaultTargetPlaneScale);
            currentNeedleScale = Academy.Instance.EnvironmentParameters.GetWithDefault("needle_scale", defaultNeedleScale);

            Debug.Log($"Curriculum Learning Initialized (RL Mode) - " +
                      $"Grasping Radius: {currentGraspingSphereRadius:F3}m, " +
                      $"Plane Distance: {currentPlaneReachDistance:F3}m, " +
                      $"Plane Scale: {currentTargetPlaneScale:F1}x, " +
                      $"Needle Scale: {currentNeedleScale:F1}x");
        }
        else
        {
            // Use default values for Imitation Learning or when curriculum is disabled
            currentGraspingSphereRadius = defaultGraspingSphereRadius;
            currentPlaneReachDistance = defaultPlaneReachDistance;
            currentTargetPlaneScale = defaultTargetPlaneScale;
            currentNeedleScale = defaultNeedleScale;

            string reason = currentLearningMode == LearningMode.ImitationLearning ?
                //                "Imitation Learning mode" : "Curriculum Learning disabled";
                "Imitation Learning mode" : "Curriculum Learning disabled";
            Debug.Log($"Using default parameters - {reason}");
        }

        // Apply all parameters
        UpdateAllCurriculumParameters();
    }

    private void UpdateAllCurriculumParameters()
    {
        UpdateGraspingSphereRadius();
        UpdateTargetPlaneScale();
        UpdateNeedleScale();
    }

    private void UpdateGraspingSphereRadius()
    {
        if (graspingSphereCollider != null)
        {
            graspingSphereCollider.radius = currentGraspingSphereRadius;
        }
    }

    private void UpdateTargetPlaneScale()
    {
        if (targetPlane != null)
        {
            targetPlane.transform.localScale = originalTargetPlaneScale * currentTargetPlaneScale;
        }
    }

    private void UpdateNeedleScale()
    {
        if (needle != null)
        {
            needle.transform.localScale = originalNeedleScale * currentNeedleScale;
        }
    }

    public void OnNeedleTipTouchPlane(bool isTouching)
    {
        needleTipTouchingPlane = isTouching;

        if (isTouching && needleGrasped && !taskCompleted)
        {
            taskCompletedByCollision = true;
            taskCompleted = true;
            win++;

            if (floorMeshRenderer != null && winMaterial != null)
                floorMeshRenderer.material = winMaterial;

            if (planeMeshRenderer != null && planeWinMaterial != null)
                planeMeshRenderer.material = planeWinMaterial;
            StartCoroutine(ResetPlaneColorAfterDelay(2f));

            if (currentLearningMode == LearningMode.ReinforcementLearning)
            {
                // Enhanced completion reward with efficiency bonus
                float completionReward = rlCompletionReward;

                // Add efficiency bonus based on how quickly task was completed
                float efficiencyMultiplier = 1.0f + (maxStepsPerEpisode - stepCount) / (float)maxStepsPerEpisode * 0.5f;
                completionReward *= efficiencyMultiplier;

                AddReward(completionReward);
                Debug.Log($"[SUCCESS] Task completed! RL Reward: {completionReward:F2} (Efficiency: {efficiencyMultiplier:F2}x)");
            }
            else
            {
                AddReward(ilCompletionReward);
                Debug.Log($"[SUCCESS] Task completed! IL Reward: {ilCompletionReward}");
            }

        }
        else if (!isTouching && taskCompletedByCollision)
        {
            taskCompletedByCollision = false;
        }
    }

    private IEnumerator ResetPlaneColorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (planeMeshRenderer != null && PlanedefaultMaterial != null)
            planeMeshRenderer.material = PlanedefaultMaterial;
    }


    private void ValidateReferences()
    {
        List<string> missingRefs = new List<string>();

        if (endEffector == null) missingRefs.Add("endEffector");
        if (needle == null) missingRefs.Add("needle");
        if (targetPlane == null) missingRefs.Add("targetPlane");
        if (joints == null) missingRefs.Add("joints array");

        if (joints != null)
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].robotPart == null)
                    missingRefs.Add($"joints[{i}].robotPart");
            }
        }

        if (missingRefs.Count > 0)
        {
            Debug.LogError($"Missing references: {string.Join(", ", missingRefs)}");
        }
    }

    private bool CanExecuteSuturingArch()
    {
        // Check if needle is close to target plane (entry point)
        if (useNeedleTipOnly && needleTip != null)
        {
            float distToEntry = Vector3.Distance(needleTip.transform.position, targetPlane.transform.position);
            return distToEntry <= currentPlaneReachDistance * 2.0f; // Within reasonable range
        }
        return false;
    }



    private void ExecuteSuturingArch()
    {
        if (archCoroutine != null)
        {
            StopCoroutine(archCoroutine);
        }

        archCoroutine = StartCoroutine(PerformSuturingArchMotionCoroutine());
        Debug.Log("[SUTURING] Executing arch motion...");
    }


    private void CancelSuturingArch()
    {
        if (archCoroutine != null)
        {
            StopCoroutine(archCoroutine);
            archCoroutine = null;
        }
        isExecutingArch = false;
        Debug.Log("[SUTURING] Arch motion cancelled");
    }


    private IEnumerator PerformSuturingArchMotionCoroutine()
    {
        isExecutingArch = true;

        // Get references to pitch and roll joints
        ArticulationBody pitchBody = joints[pitchJointIndex].robotPart?.GetComponent<ArticulationBody>();
        ArticulationBody rollBody = joints[rollJointIndex].robotPart?.GetComponent<ArticulationBody>();

        if (pitchBody == null || rollBody == null)
        {
            Debug.LogError("[SUTURING] Pitch or Roll joint not found!");
            isExecutingArch = false;
            yield break;
        }

        // Store initial positions
        float initialPitch = pitchBody.xDrive.target;
        float initialRoll = rollBody.xDrive.target;

        // Calculate target positions (convert degrees to radians for ArticulationBody)
        float targetPitch = initialPitch + (pitchRotationDegrees * Mathf.Deg2Rad);
        float targetRoll = initialRoll + (rollRotationDegrees * Mathf.Deg2Rad);

        float elapsed = 0f;

        // Perform smooth arch motion
        while (elapsed < archDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / archDuration);
            float curveValue = archCurve.Evaluate(t);

            // Interpolate pitch and roll
            float currentPitch = Mathf.Lerp(initialPitch, targetPitch, curveValue);
            float currentRoll = Mathf.Lerp(initialRoll, targetRoll, curveValue);

            // Apply to articulation bodies
            ArticulationDrive pitchDrive = pitchBody.xDrive;
            pitchDrive.target = currentPitch;
            pitchBody.xDrive = pitchDrive;

            ArticulationDrive rollDrive = rollBody.xDrive;
            rollDrive.target = currentRoll;
            rollBody.xDrive = rollDrive;

            yield return null;
        }

        // Ensure final position is exact
        ArticulationDrive finalPitchDrive = pitchBody.xDrive;
        finalPitchDrive.target = targetPitch;
        pitchBody.xDrive = finalPitchDrive;

        ArticulationDrive finalRollDrive = rollBody.xDrive;
        finalRollDrive.target = targetRoll;
        rollBody.xDrive = finalRollDrive;

        isExecutingArch = false;
        Debug.Log("[SUTURING] Arch motion completed!");

        // Optional: Check if needle tip is now at exit point
        CheckSuturingCompletion();
    }


    private void CheckSuturingCompletion()
    {
        if (useNeedleTipOnly && needleTip != null)
        {
            float distToExit = Vector3.Distance(needleTip.transform.position, targetPlane.transform.position);

            if (distToExit <= currentPlaneReachDistance)
            {
                Debug.Log("[SUTURING] Successfully completed suturing motion!");
                // You can add rewards or other logic here
                if (currentLearningMode == LearningMode.ReinforcementLearning)
                {
                    AddReward(rlCompletionReward);
                }
            }
        }
    }

    private void CalculateMultiAgentReward()
    {
        if (!isMultiAgentTask || taskManager == null) return;

        float reward = rlStepPenalty;

        switch (taskManager.GetCurrentPhase())
        {
            case SuturingTaskManager.SuturingPhase.Agent1_PlaceEntry:
                if (agentID == 1)
                {
                    reward += CalculateEntryPlacementReward();
                }
                break;

            case SuturingTaskManager.SuturingPhase.Agent2_Approach:
                if (agentID == 2)
                {
                    reward += CalculateExitApproachReward();
                }
                break;
        }

        AddReward(reward);
    }


    private float CalculateEntryPlacementReward()
    {
        if (needleTip == null || taskManager.entryPoint == null) return 0f;

        float dist = Vector3.Distance(needleTip.transform.position, taskManager.entryPoint.transform.position);

        // Dense reward for proximity
        float proximityReward = Mathf.Max(0, 1f - dist / 0.1f) * 0.01f;

        // Progress reward
        if (previousDistanceToPlane != float.MaxValue)
        {
            float progress = previousDistanceToPlane - dist;
            if (progress > 0)
            {
                proximityReward += progress * 0.1f;
            }
        }
        previousDistanceToPlane = dist;

        return proximityReward;
    }


    private float CalculateExitApproachReward()
    {
        if (endEffector == null || taskManager.exitPoint == null) return 0f;

        float dist = Vector3.Distance(endEffector.transform.position, taskManager.exitPoint.transform.position);

        // Dense reward for proximity
        float proximityReward = Mathf.Max(0, 1f - dist / 0.1f) * 0.01f;

        return proximityReward;
    }





}


