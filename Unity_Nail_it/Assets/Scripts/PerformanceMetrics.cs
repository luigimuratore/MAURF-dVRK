using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.IO;

[System.Serializable]
public class PerformanceMetrics : MonoBehaviour
{
    public int totalEpisodes = 0;
    public int successfulEpisodes = 0;
    public int failedEpisodes = 0;
    public int timeoutEpisodes = 0;
    public int totalStepsToSuccess = 0;
    public float averageStepsToSuccess = 0f;
    
    public float totalTime = 0f;
    public float averageTime = 0f;
    public float bestTime = float.MaxValue;
    public float worstTime = 0f;
    
    public float totalReward = 0f;
    public float averageRewardPerEpisode = 0f;
    
    private Queue<bool> recentEpisodeResults = new Queue<bool>();
    private const int RECENT_EPISODES_WINDOW = 100;

    public void AddEpisodeResult(bool success)
    {
        recentEpisodeResults.Enqueue(success);
        if (recentEpisodeResults.Count > RECENT_EPISODES_WINDOW)
        {
            recentEpisodeResults.Dequeue();
        }
    }

    public float GetRecentSuccessRate()
    {
        if (recentEpisodeResults.Count == 0) return 0f;
        
        int successCount = 0;
        foreach (bool success in recentEpisodeResults)
        {
            if (success) successCount++;
        }
        return (float)successCount / recentEpisodeResults.Count * 100f;
    }

    public string GetFullReport()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== PERFORMANCE REPORT ===");
        report.AppendLine($"Total Episodes: {totalEpisodes}");
        report.AppendLine($"Successful Episodes: {successfulEpisodes}");
        report.AppendLine($"Failed Episodes: {failedEpisodes}");
        report.AppendLine($"Timeout Episodes: {timeoutEpisodes}");
        report.AppendLine($"Success Rate: {(totalEpisodes > 0 ? (float)successfulEpisodes / totalEpisodes * 100 : 0):F2}%");
        report.AppendLine($"Recent Success Rate (last {recentEpisodeResults.Count} episodes): {GetRecentSuccessRate():F2}%");
        
        if (successfulEpisodes > 0)
        {
            report.AppendLine($"Average Steps to Success: {averageStepsToSuccess:F2}");
            report.AppendLine($"Average Time to Success: {averageTime:F2}s");
            report.AppendLine($"Best Time: {bestTime:F2}s");
            report.AppendLine($"Worst Time: {worstTime:F2}s");
        }
        
        report.AppendLine($"Average Reward per Episode: {averageRewardPerEpisode:F2}");
        report.AppendLine("=========================");
        
        return report.ToString();
    }
}