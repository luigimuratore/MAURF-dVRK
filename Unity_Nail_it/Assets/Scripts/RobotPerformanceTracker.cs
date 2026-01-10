using UnityEngine;
using System.IO;

public class RobotPerformanceTracker : MonoBehaviour
{
    [SerializeField] private PerformanceMetrics metrics = new PerformanceMetrics();
    [SerializeField] private RobotController robotController;

    /*void Awake() 
    {
        // Get reference to robot controller if not assigned
        if (robotController == null)
            robotController = GetComponent<RobotController>();
            
        // Only initialize files if tracking is enabled
        if (robotController != null && !robotController.enablePerformanceTracking)
        {
            Debug.Log("Performance tracking disabled - no metrics will be recorded");
            return;
        }
        
        // Your existing file initialization code
        InitializeFiles();
    }
    
    void Start()
    {
        robotController = GetComponent<RobotController>();
        if (robotController == null)
        {
            Debug.LogError("RobotPerformanceTracker needs to be attached to the same GameObject as RobotController!");
            enabled = false;
        }
    }

    public void RecordSuccessfulEpisode(float episodeTime, int steps, float reward)
    {
        Debug.Log($"Recording successful episode - Time: {episodeTime:F2}s, Steps: {steps}, Reward: {reward:F2}");
        
        metrics.totalEpisodes++;
        metrics.successfulEpisodes++;
        metrics.totalStepsToSuccess += steps;
        metrics.averageStepsToSuccess = (float)metrics.totalStepsToSuccess / metrics.successfulEpisodes;

        metrics.totalTime += episodeTime;
        metrics.averageTime = metrics.totalTime / metrics.successfulEpisodes;
        metrics.bestTime = Mathf.Min(metrics.bestTime, episodeTime);
        metrics.worstTime = Mathf.Max(metrics.worstTime, episodeTime);

        metrics.totalReward += reward;
        metrics.averageRewardPerEpisode = metrics.totalReward / metrics.totalEpisodes;

        metrics.AddEpisodeResult(true);
    }

    public void RecordFailedEpisode(float reward)
    {
        Debug.Log($"Recording failed episode - Reward: {reward:F2}");
        
        metrics.totalEpisodes++;
        metrics.failedEpisodes++;
        metrics.totalReward += reward;
        metrics.averageRewardPerEpisode = metrics.totalReward / metrics.totalEpisodes;
        metrics.AddEpisodeResult(false);
    }

    public void RecordTimeoutEpisode(float reward)
    {
        Debug.Log($"Recording timeout episode - Reward: {reward:F2}");
        
        metrics.totalEpisodes++;
        metrics.timeoutEpisodes++;
        metrics.totalReward += reward;
        metrics.averageRewardPerEpisode = metrics.totalReward / metrics.totalEpisodes;
        metrics.AddEpisodeResult(false);
    }

    // Methods for tracking grasping events
    public void LogGraspAttempt(bool success)
    {
        Debug.Log($"[Tracker] Grasp attempt: {(success ? "Success" : "Failure")}");
        // You could update metrics.totalGraspAttempts here if you want
    }

    public void LogGraspRelease()
    {
        Debug.Log("[Tracker] Object released");
        // You could update release metrics here
    }

    public void LogEndEffectorLose()
    {
        Debug.Log("[Tracker] End effector lost target");
        // Track when end effector loses sight of target
    }

    public void LogGraspTriggerCollision()
    {
        Debug.Log("[Tracker] Grasp trigger collision with target");
        // Track when gripper comes in contact with target
    }

    private void OnApplicationQuit()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string finalReport = metrics.GetFullReport();
        
        Debug.Log("=== FINAL PERFORMANCE REPORT ===");
        Debug.Log(finalReport);

        try
        {
            string directory = "/home/omen_w01/Documents/Luigi/Unity/dVRK_luigi_GRASPING_needle/Assets/Performance/";
            
            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            string path = Path.Combine(directory, $"Performance_Report_{timestamp}.txt");
            File.WriteAllText(path, finalReport);
            Debug.Log($"Performance report saved to: {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save report: {e.Message}");
        }
    }

    private void InitializeFiles()
    {
        try
        {
            string directory = "/home/omen_w01/Documents/Luigi/Unity/dVRK_luigi_GRASPING_needle/Assets/Performance/";
            
            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Debug.Log($"Created performance metrics directory at: {directory}");
            }
            
            // Reset performance metrics at start
            metrics = new PerformanceMetrics();
            
            // Optionally log that tracking is enabled
            Debug.Log("Performance tracking initialized - metrics will be recorded");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize performance tracking: {e.Message}");
        }
    }

    // Add this new method
    public int GetSuccessCount()
    {
        return metrics.successfulEpisodes;
    }*/
}