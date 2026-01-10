using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NeedleTipCollision : MonoBehaviour
{
    private RobotController robotController;
    
    void Start()
    {
        // Find the RobotController in the parent hierarchy instead of the entire scene
        robotController = GetComponentInParent<RobotController>();
        
        // If not found in parent, try to find it in the same environment
        if (robotController == null)
        {
            // Look for RobotController in the same root object
            Transform rootTransform = transform;
            while (rootTransform.parent != null)
            {
                rootTransform = rootTransform.parent;
            }
            robotController = rootTransform.GetComponentInChildren<RobotController>();
        }
        
        // Last resort: find by proximity (closest RobotController)
        if (robotController == null)
        {
            RobotController[] allControllers = FindObjectsOfType<RobotController>();
            if (allControllers.Length > 0)
            {
                float closestDistance = float.MaxValue;
                foreach (RobotController controller in allControllers)
                {
                    float distance = Vector3.Distance(transform.position, controller.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        robotController = controller;
                    }
                }
            }
        }
        
        if (robotController == null)
        {
            Debug.LogError($"RobotController not found for {gameObject.name}!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to the target plane
        if (other.gameObject.name.Contains("TargetPlane") || other.CompareTag("TargetPlane"))
        {
            if (robotController != null)
            {
                robotController.OnNeedleTipTouchPlane(true);
                Debug.Log($"Needle tip touched plane in environment: {robotController.gameObject.name}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the collider belongs to the target plane
        if (other.gameObject.name.Contains("TargetPlane") || other.CompareTag("TargetPlane"))
        {
            if (robotController != null)
            {
                robotController.OnNeedleTipTouchPlane(false);
                Debug.Log($"Needle tip left plane in environment: {robotController.gameObject.name}");
            }
        }
    }
}