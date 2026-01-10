using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraspingTriggerCollision : MonoBehaviour
{
    // Reference to the robot controller
    private RobotController robotController;

    private void Start()
    {
        // Find the robot controller in the parent hierarchy
        robotController = GetComponentInParent<RobotController>();
        if (robotController == null)
        {
            Debug.LogError("GraspingTriggerCollision: Could not find RobotController in parent hierarchy");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (robotController != null)
        {
            // Notify about graspable objects
            robotController.OnGraspTriggerEnter(other.gameObject);
            
            // Check for target collision when carrying an object
            robotController.OnGraspTriggerTargetCollision(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (robotController != null)
        {
            robotController.OnGraspTriggerExit(other.gameObject);
        }
    }
}