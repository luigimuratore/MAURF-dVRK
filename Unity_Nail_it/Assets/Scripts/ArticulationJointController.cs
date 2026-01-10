using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum RotationDirection { None = 0, Positive = 1, Negative = -1 };
public enum JointType { Revolute, Prismatic };

public class ArticulationJointController : MonoBehaviour
{
    public RotationDirection rotationState = RotationDirection.None;
    public float rotationSpeed = 100.0f;
    public float linearSpeed = 0.05f;
    [SerializeField] private JointType jointType = JointType.Revolute;
    
    private ArticulationBody articulation;

    // LIFE CYCLE

    void Start()
    {
        articulation = GetComponent<ArticulationBody>();
        
        // Auto-detect joint type if possible
        if (articulation != null)
        {
            if (articulation.jointType == ArticulationJointType.RevoluteJoint)
                jointType = JointType.Revolute;
            else if (articulation.jointType == ArticulationJointType.PrismaticJoint)
                jointType = JointType.Prismatic;
        }
    }

    void FixedUpdate() 
    {
        if (rotationState != RotationDirection.None && articulation != null) 
        {
            if (jointType == JointType.Revolute)
            {
                // Handle revolute joint rotation
                float rotationChange = (float)rotationState * rotationSpeed * Time.fixedDeltaTime;
                float rotationGoal = CurrentPrimaryAxisRotation() + rotationChange;
                RotateTo(rotationGoal);
            }
            else if (jointType == JointType.Prismatic)
            {
                // Handle prismatic joint movement
                float movement = (float)rotationState * linearSpeed;
                MoveTo(CurrentPosition() + movement);
            }
        }
    }

    // MOVEMENT HELPERS - REVOLUTE JOINTS

    public float CurrentPrimaryAxisRotation()
    {
        if (articulation == null) {
            articulation = GetComponent<ArticulationBody>();
        }
        
        if (articulation == null || jointType != JointType.Revolute) {
            return 0f; // Return default if not found or not a revolute joint
        }
        
        float currentRotationRads = articulation.jointPosition[0];
        float currentRotation = Mathf.Rad2Deg * currentRotationRads;
        return currentRotation;
    }

    public void RotateTo(float primaryAxisRotation)
    {
        if (articulation == null) {
            articulation = GetComponent<ArticulationBody>();
            if (articulation == null) return;
        }
        
        if (jointType != JointType.Revolute) return;
        
        var drive = articulation.xDrive;
        drive.target = primaryAxisRotation;
        articulation.xDrive = drive;
    }
    
    // MOVEMENT HELPERS - PRISMATIC JOINTS
    
    public float CurrentPosition()
    {
        if (articulation == null) {
            articulation = GetComponent<ArticulationBody>();
        }
        
        if (articulation == null || jointType != JointType.Prismatic) {
            return 0f; // Return default if not found or not a prismatic joint
        }
        
        ArticulationDrive drive;
        if (articulation.linearLockX == ArticulationDofLock.FreeMotion)
            drive = articulation.xDrive;
        else if (articulation.linearLockY == ArticulationDofLock.FreeMotion)
            drive = articulation.yDrive;
        else
            drive = articulation.zDrive;
            
        return drive.target;
    }
    
    public void MoveTo(float position)
    {
        if (articulation == null) {
            articulation = GetComponent<ArticulationBody>();
            if (articulation == null) return;
        }
        
        if (jointType != JointType.Prismatic) return;
        
        // Determine which drive to use based on which axis is unlocked
        if (articulation.linearLockX == ArticulationDofLock.FreeMotion)
        {
            var drive = articulation.xDrive;
            drive.target = position;
            articulation.xDrive = drive;
        }
        else if (articulation.linearLockY == ArticulationDofLock.FreeMotion)
        {
            var drive = articulation.yDrive;
            drive.target = position;
            articulation.yDrive = drive;
        }
        else
        {
            var drive = articulation.zDrive;
            drive.target = position;
            articulation.zDrive = drive;
        }
    }
    
    // PUBLIC INTERFACE FOR BOTH JOINT TYPES
    
    public bool IsPrismatic()
    {
        return jointType == JointType.Prismatic;
    }
    
    public bool IsRevolute()
    {
        return jointType == JointType.Revolute;
    }
    
    // Unified method to set target for any joint type
    public void SetTarget(float targetValue)
    {
        if (jointType == JointType.Revolute)
            RotateTo(targetValue);
        else
            MoveTo(targetValue);
    }
}