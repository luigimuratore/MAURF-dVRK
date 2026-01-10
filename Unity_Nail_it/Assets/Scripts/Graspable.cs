using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Graspable : MonoBehaviour
{
    // Save initial state
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool wasKinematic;
    
    public Vector3 originalPosition;
    public Quaternion originalRotation;
    
    void Start()
    {
        // Record initial state
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        
        // Store original transform
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
        }
    }
    
    public void ResetToInitialState()
    {
        // Reset position and rotation
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        // Reset physics properties
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = wasKinematic;
        }
    }
    
    // Called when object is grasped
    public void OnGrasped()
    {
        // Handle grasping event
    }
    
    // Called when object is released
    public void OnReleased()
    {
        // Handle release event
    }
}