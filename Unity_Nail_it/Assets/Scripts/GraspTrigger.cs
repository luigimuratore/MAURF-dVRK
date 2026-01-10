using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraspTrigger : MonoBehaviour
{
    private RobotController robotController;
    
    void Start()
    {
        // Find the robot controller (can be direct reference if you prefer)
        robotController = GetComponentInParent<RobotController>();
        
        if (robotController == null)
        {
            Debug.LogError("GraspTrigger couldn't find RobotController in parent hierarchy");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (robotController != null)
        {
            robotController.OnGraspTriggerEnter(other.gameObject);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (robotController != null)
        {
            robotController.OnGraspTriggerExit(other.gameObject);
        }
    }
}