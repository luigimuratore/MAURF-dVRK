using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class RobotControllerGrasping : Agent
{
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

    private struct AlignmentData
    {
        public bool isValid;
        public int lastUpdateFrame;

        public float leftDistanceSquared;
        public float rightDistanceSquared;

        public float leftDistance;
        public float rightDistance;

        public float leftAngle;
        public float rightAngle;

        public float leftDistanceScore;
        public float rightDistanceScore;
        public float leftAngleScore;
        public float rightAngleScore;

        public bool leftAligned;
        public bool rightAligned;
    }

    public Joint[] joints;

    // Gripper-specific fields
    [System.Serializable]
    public struct GripperJoint
    {
        public GameObject leftFinger;
        public GameObject rightFinger;
        public float initialOpenAmount;
        public float maxOpenDistance;
    }
    public GripperJoint gripper;
    public bool hasGripper = false;

    public GameObject endEffector;
    public GameObject target;
    public bool visualizeTrajectory = true;
    [SerializeField] private bool showDistanceLogs = false;

    private LineRenderer trajectoryLine;
    private float previousDistance;

    private int targetTouchCount = 0;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material loseMaterial;
    [SerializeField] private Material timeMaterial;
    [SerializeField] private MeshRenderer floorMeshRenderer;

    private int stepCount = 0;
    private int maxSteps = 0;

    // Reference to RobotPerformanceTracker
    private RobotPerformanceTracker robotPerformanceTracker;


    // Add these new fields for alignment detection
    [Header("Alignment Settings")]
    public GameObject graspingTrigger; // Empty GameObject with collider as child of gripper
    private GameObject currentGraspableObject = null; // Object currently in graspable area

    // Add this with your other serialized fields, near the robotPerformanceTracker variable
    public bool enablePerformanceTracking = true;
    private Material originalFloorMaterial;

    [Header("Plane Alignment Detection")]
    public Transform leftGripperPlane;  // Left plane in the end effector
    public Transform rightGripperPlane;  // Right plane in the end effector
    public Transform leftObjectPlane;   // Left plane in the graspable object
    public Transform rightObjectPlane;   // Right plane in the graspable object
    [SerializeField] private float planeAlignmentThreshold = 0.05f; // How close planes need to be to consider aligned
    [SerializeField] private float planeAngleThreshold = 10f; // Maximum angle difference in degrees

    // Add these fields to track the plane alignment trajectories
    private LineRenderer leftPlaneTrajectory;
    private LineRenderer rightPlaneTrajectory;
    private Color alignedColor = Color.green;
    private Color unalignedColor = Color.yellow;

    // Add these fields to your class
    private AlignmentData cachedAlignment;
    private float planeAlignmentThresholdSquared;
    private const int GRASPABLE_CACHE_FRAMES = 10;
    private int lastGraspableCacheFrame;
    private Graspable[] cachedGraspables;
    private float currentPositionVariance = 0f;
    private bool positiveRewardAchieved = false;
    private bool debugMode = false;
    
    // Add this new field at class level
    private float previousBestScore = 0f;
    

    // Start is called before the first frame update
    void Start()
    {
        if (visualizeTrajectory)
        {
            SetupTrajectoryVisualization();
        }

        if (endEffector == null)
        {
            Debug.LogError("End effector not assigned in RobotController!");
        }

        if (target == null)
        {
            Debug.LogError("Target not assigned in RobotController!");
        }

        // Get the RobotPerformanceTracker component
        robotPerformanceTracker = GetComponent<RobotPerformanceTracker>();
        if (robotPerformanceTracker == null && enablePerformanceTracking)
        {
            Debug.LogWarning("RobotPerformanceTracker not found but tracking is enabled.");
        }

        // Store initial joint positions
        for (int i = 0; i < joints.Length; i++)
        {
            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();
            if (controller != null)
            {
                Joint joint = joints[i];
                joint.initialRotation = controller.CurrentPrimaryAxisRotation();
                joints[i] = joint; // Need to reassign since Joint is a struct
                if (debugMode) Debug.Log($"Stored initial rotation for joint {i}: {joint.initialRotation} degrees");
            }
        }

        // Initialize gripper if it exists
        if (hasGripper)
        {
            // Ensure the gripper components are properly assigned
            if (gripper.leftFinger == null || gripper.rightFinger == null)
            {
                Debug.LogError("Gripper fingers not properly assigned!");
            }
            else
            {
                // Initialize gripper position
                SetGripperOpenAmount(gripper.initialOpenAmount);
            }
        }


        // Validate grasping trigger exists
        if (graspingTrigger == null)
        {
            Debug.LogWarning("No grasping trigger assigned!");
        }

        // Store original floor material
        if (floorMeshRenderer != null)
        {
            originalFloorMaterial = floorMeshRenderer.material;
        }

        // Check for missing plane references
        if (leftGripperPlane == null || rightGripperPlane == null)
        {
            Debug.LogWarning("Gripper plane references not set in inspector!");

            // Try to find them automatically if they exist with standard naming
            if (endEffector != null)
            {
                Transform[] endEffectorChildren = endEffector.GetComponentsInChildren<Transform>();
                foreach (Transform t in endEffectorChildren)
                {
                    if (t.name.Contains("LeftPlane"))
                        leftGripperPlane = t;
                    else if (t.name.Contains("RightPlane"))
                        rightGripperPlane = t;
                }
            }
        }

        // Initialize curriculum parameters
        UpdateParametersFromEnvironment();

        // Initialize squared threshold for faster distance calculations
        planeAlignmentThresholdSquared = planeAlignmentThreshold * planeAlignmentThreshold;

        if (debugMode)
        {
            Debug.Log($"Initial curriculum parameters - Position Variance: {currentPositionVariance}, " +
                      $"Alignment Threshold: {planeAlignmentThreshold}, " +
                      $"Angle Threshold: {planeAngleThreshold}°");
        }
    }

    void Update()
    {
        // Add new code for plane alignment trajectories
        UpdatePlaneTrajectories();

        // Log the real-time distance between the end effector and the target
        if (endEffector != null && target != null && showDistanceLogs)
        {
            // Use sqrMagnitude to avoid square root operation
            float distanceSquared = (endEffector.transform.position - target.transform.position).sqrMagnitude;
            Debug.Log($"Real-time Distance to Target: {Mathf.Sqrt(distanceSquared)}");
        }

    }

    private void SetupTrajectoryVisualization()
    {
        // Check if there's already a LineRenderer component for main trajectory
        trajectoryLine = GetComponent<LineRenderer>();

        // If not, add one
        if (trajectoryLine == null)
            trajectoryLine = gameObject.AddComponent<LineRenderer>();


        // Ensure the renderer is enabled
        trajectoryLine.enabled = true;

        // Debug check
        if (debugMode)
            Debug.Log("Trajectory visualization setup complete");

        // Create left plane trajectory linerenderer with more visible settings
        GameObject leftTrajectoryObj = new GameObject("LeftPlaneTrajectory");
        leftTrajectoryObj.transform.parent = transform;
        leftPlaneTrajectory = leftTrajectoryObj.AddComponent<LineRenderer>();
        leftPlaneTrajectory.startWidth = 0.001f; // Increased width
        leftPlaneTrajectory.endWidth = 0.001f;
        leftPlaneTrajectory.material = new Material(Shader.Find("Sprites/Default"));
        leftPlaneTrajectory.startColor = unalignedColor;
        leftPlaneTrajectory.endColor = unalignedColor;
        leftPlaneTrajectory.positionCount = 2;
        leftPlaneTrajectory.enabled = false; // Start disabled

        // Create right plane trajectory linerenderer with more visible settings
        GameObject rightTrajectoryObj = new GameObject("RightPlaneTrajectory");
        rightTrajectoryObj.transform.parent = transform;
        rightPlaneTrajectory = rightTrajectoryObj.AddComponent<LineRenderer>();
        rightPlaneTrajectory.startWidth = 0.001f; // Increased width
        rightPlaneTrajectory.endWidth = 0.001f;
        rightPlaneTrajectory.material = new Material(Shader.Find("Sprites/Default"));
        rightPlaneTrajectory.startColor = unalignedColor;
        rightPlaneTrajectory.endColor = unalignedColor;
        rightPlaneTrajectory.positionCount = 2;
        rightPlaneTrajectory.enabled = false; // Start disabled

        // Ensure visibility in the scene
        if (leftPlaneTrajectory != null && rightPlaneTrajectory != null)
        {
            leftPlaneTrajectory.receiveShadows = false;
            leftPlaneTrajectory.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rightPlaneTrajectory.receiveShadows = false;
            rightPlaneTrajectory.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Debug check
        if (debugMode)
            Debug.Log("Trajectory visualization setup complete");
    }

    // Reset the environment at the start of each episode
    public override void OnEpisodeBegin()
    {
        if (debugMode) Debug.Log($"Episode starting. Distance to target: {previousDistance}");

        // Reset step counter
        stepCount = 0;

        // Reset joint positions
        ResetJointPositions();

        // Reset velocities
        ResetVelocities();

        // Reset target position
        ResetTargetPosition();

        // Diagnostic info - uncomment to enable
        if (CompletedEpisodes % 100 == 0 || debugMode)
        {
            Debug.Log($"=== DIAGNOSTIC INFO (Episode {CompletedEpisodes}) ===");
            Debug.Log($"Current position variance: {currentPositionVariance}");
            Debug.Log($"Plane alignment threshold: {planeAlignmentThreshold}");
            Debug.Log($"Angle threshold: {planeAngleThreshold}°");
            
            // Check if planes exist
            Debug.Log($"Plane references: leftGripper={leftGripperPlane != null}, " +
                     $"rightGripper={rightGripperPlane != null}, " +
                     $"leftObject={leftObjectPlane != null}, " +
                     $"rightObject={rightObjectPlane != null}");
                     
            // Try to find a graspable object
            Graspable[] graspables = FindObjectsOfType<Graspable>();
            Debug.Log($"Found {graspables.Length} graspable objects in scene");
            
            // Check target reference and position
            if (target != null)
                Debug.Log($"Target position: {target.transform.position}");
        }
    }

    private void ResetJointPositions()
    {
        // Reset revolute and prismatic joints
        for (int i = 0; i < joints.Length; i++)
        {
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

        // Reset gripper
        if (hasGripper)
        {
            SetGripperOpenAmount(gripper.initialOpenAmount);
        }
    }

    public void ResetTargetPosition()
    {
        // Base position
        Vector3 basePosition = new Vector3(-0.08f, -0.2f, 0.128f);
        
        // Apply position variance if greater than 0
        if (currentPositionVariance > 0)
        {
            // Add random offset based on current variance
            Vector3 randomOffset = new Vector3(
                Random.Range(-currentPositionVariance, currentPositionVariance),
                Random.Range(-currentPositionVariance, currentPositionVariance),
                Random.Range(-currentPositionVariance, currentPositionVariance)
            );
            
            basePosition += randomOffset;
            Debug.Log($"[Target] Base position: {basePosition-randomOffset}, Random offset: {randomOffset}, Final position: {basePosition}");
        }

        // Set the target position
        target.transform.localPosition = basePosition;
    }

    // Collect observations about the environment
    public override void CollectObservations(VectorSensor sensor)
    {
        if (endEffector == null || target == null) return;

        // Observe end effector position (3 values)
        sensor.AddObservation(endEffector.transform.localPosition);

        // Observe target position (3 values)
        sensor.AddObservation(target.transform.localPosition);

        // Observe joint positions
        foreach (Joint joint in joints)
        {
            ArticulationJointController controller = joint.robotPart.GetComponent<ArticulationJointController>();
            if (controller != null)
            {
                // Use the ArticulationJointController for both joint types
                if (joint.jointType == JointType.Revolute)
                {
                    // Normalize the rotation value to be between -1 and 1
                    float normalizedRotation = controller.CurrentPrimaryAxisRotation() / 180.0f;
                    sensor.AddObservation(normalizedRotation);
                }
                else if (joint.jointType == JointType.Prismatic)
                {
                    // Get current position from the controller
                    float currentPos = controller.CurrentPosition();

                    // Get the ArticulationBody to access drive limits
                    ArticulationBody artBody = joint.robotPart.GetComponent<ArticulationBody>();
                    if (artBody != null)
                    {
                        // Determine which drive to use
                        ArticulationDrive drive;
                        if (artBody.linearLockX == ArticulationDofLock.FreeMotion)
                            drive = artBody.xDrive;
                        else if (artBody.linearLockY == ArticulationDofLock.FreeMotion)
                            drive = artBody.yDrive;
                        else
                            drive = artBody.zDrive;

                        // Normalize the position
                        float range = drive.upperLimit - drive.lowerLimit;
                        float normalizedPos = range != 0 ? (currentPos - drive.lowerLimit) / range : 0;
                        sensor.AddObservation(normalizedPos);
                    }
                    else
                    {
                        // Fallback if no ArticulationBody
                        sensor.AddObservation(0f);
                    }
                }
            }
            else
            {
                // Add a default observation if the controller is missing
                sensor.AddObservation(0f);
            }
        }

        // Add observations for plane alignment
        if (leftGripperPlane != null && rightGripperPlane != null &&
            leftObjectPlane != null && rightObjectPlane != null)
        {
            // Distance between planes (normalized)
            float leftDistance = Vector3.Distance(leftGripperPlane.position, leftObjectPlane.position);
            float rightDistance = Vector3.Distance(rightGripperPlane.position, rightObjectPlane.position);

            // Add normalized distances (0 to 1)
            sensor.AddObservation(Mathf.Clamp01(leftDistance / planeAlignmentThreshold));
            sensor.AddObservation(Mathf.Clamp01(rightDistance / planeAlignmentThreshold));

            // Add angle differences (normalized 0 to 1)
            float leftAngle = Vector3.Angle(leftGripperPlane.up, leftObjectPlane.up);
            float rightAngle = Vector3.Angle(rightGripperPlane.up, rightObjectPlane.up);

            sensor.AddObservation(Mathf.Clamp01(leftAngle / planeAngleThreshold));
            sensor.AddObservation(Mathf.Clamp01(rightAngle / planeAngleThreshold));
        }
        else
        {
            // Add placeholder values if plane references are missing
            sensor.AddObservation(1.0f); // Left distance (max)
            sensor.AddObservation(1.0f); // Right distance (max)
            sensor.AddObservation(1.0f); // Left angle (max)
            sensor.AddObservation(1.0f); // Right angle (max)
        }

        // Add gripper state observation if gripper exists
        if (hasGripper)
        {
            // Get the current gripper open amount (normalized between 0 and 1)
            float gripperOpenAmount = GetCurrentGripperOpenAmount();
            sensor.AddObservation(gripperOpenAmount);
        }
    }

    // Process actions from the neural network (or heuristic)
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Update parameters from environment
        UpdateParametersFromEnvironment();

        // Process joint actions
        ProcessJointActions(actionBuffers);

        // Calculate simple reward based on alignment
        CalculateReward();

        // Log curriculum status every 1000 steps
        /*if (stepCount % 1000 == 0)
        {
            LogCurriculumStatus();
        }*/

        // Check for episode timeout
        stepCount++;
        if (stepCount >= MaxStep)
        {
            if (floorMeshRenderer != null)
                floorMeshRenderer.material = timeMaterial;
            AddReward(-0.1f);  // Small penalty for timeout
            EndEpisode();
        }
    }

    // New method to log curriculum status
    /*private void LogCurriculumStatus()
    {
        Debug.Log($"[Curriculum Status] Step: {stepCount}, Episode: {CompletedEpisodes}\n" +
                  $"  Position Variance: {currentPositionVariance:F3}\n" +
                  $"  Alignment Threshold: {planeAlignmentThreshold:F3}\n" +
                  $"  Angle Threshold: {planeAngleThreshold:F1}°\n" +
                  $"  Successful Episodes: {(robotPerformanceTracker != null ? robotPerformanceTracker.GetSuccessCount() : 0)}");
    }*/

    public void OnEndEffectorLose()
    {
        if (floorMeshRenderer != null)
            floorMeshRenderer.material = loseMaterial;

        // Record failed episode if tracking is enabled
        /*if (enablePerformanceTracking && robotPerformanceTracker != null)
        {
            robotPerformanceTracker.RecordFailedEpisode(0);
        }*/

        if (debugMode) Debug.Log($"Failure! Episodes completed: {CompletedEpisodes}");

        // Keep small penalty for failure
        AddReward(-0.3f);
        EndEpisode();
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

    // Manual control for testing (maps keyboard inputs to actions)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;

        bool inputDetected = false;

        // Process input for each joint
        int jointCount = joints.Length;
        for (int i = 0; i < jointCount && i < continuousActionsOut.Length - (hasGripper ? 1 : 0); i++)
        {
            // Special handling for prismatic joint (joint 5, index 4)
            if (i == 4 && joints[i].jointType == JointType.Prismatic)
            {
                // Use PageUp/PageDown for prismatic joint
                if (Input.GetKey(KeyCode.PageUp))
                {
                    continuousActionsOut[i] = 1.0f;
                    inputDetected = true;
                    if (debugMode) Debug.Log("Prismatic joint - PageUp pressed: +1.0");
                }
                else if (Input.GetKey(KeyCode.PageDown))
                {
                    continuousActionsOut[i] = -1.0f;
                    inputDetected = true;
                    if (debugMode) Debug.Log("Prismatic joint - PageDown pressed: -1.0");
                }
                else
                {
                    continuousActionsOut[i] = 0f;
                }

                // Skip the standard axis input for this joint
                continue;
            }

            // Regular input handling for other joints
            float inputVal = Input.GetAxisRaw(joints[i].inputAxis);

            if (Mathf.Abs(inputVal) > 0.1f)
            {
                continuousActionsOut[i] = inputVal;
                inputDetected = true;
            }
            else
            {
                continuousActionsOut[i] = 0f;
            }
        }

        // Process gripper input
        if (hasGripper && continuousActionsOut.Length > jointCount)
        {
            if (Input.GetKey(KeyCode.M))
            {
                continuousActionsOut[jointCount] = 1.0f;
                inputDetected = true;
            }
            else if (Input.GetKey(KeyCode.N))
            {
                continuousActionsOut[jointCount] = -1.0f;
                inputDetected = true;
            }
            else
            {
                continuousActionsOut[jointCount] = 0f;
            }
        }

        // If no input detected, ensure all actions are zero
        if (!inputDetected)
        {
            for (int i = 0; i < continuousActionsOut.Length; i++)
            {
                continuousActionsOut[i] = 0f;
            }
        }
    }

    // CONTROL METHODS FROM ORIGINAL IMPLEMENTATION

    public void StopAllJointRotations()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            GameObject robotPart = joints[i].robotPart;
            UpdateRotationState(RotationDirection.None, robotPart);
        }
    }

    public void RotateJoint(int jointIndex, RotationDirection direction)
    {
        StopAllJointRotations();
        if (jointIndex >= 0 && jointIndex < joints.Length)
        {
            Joint joint = joints[jointIndex];
            UpdateRotationState(direction, joint.robotPart);
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

    private void ResetVelocities()
    {
        foreach (Joint joint in joints)
        {
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

    // Set the gripper open amount (0 = closed, 1 = fully open)
    public void SetGripperOpenAmount(float amount)
    {
        // Clamp the amount between 0 and 1
        float clampedAmount = Mathf.Clamp01(amount);

        if (hasGripper && gripper.leftFinger != null && gripper.rightFinger != null)
        {
            // Get the ArticulationBody components
            ArticulationBody leftFingerBody = gripper.leftFinger.GetComponent<ArticulationBody>();
            ArticulationBody rightFingerBody = gripper.rightFinger.GetComponent<ArticulationBody>();

            if (leftFingerBody != null && rightFingerBody != null)
            {
                // Calculate target positions for both fingers
                float leftTarget = clampedAmount * gripper.maxOpenDistance;
                float rightTarget = clampedAmount * gripper.maxOpenDistance;

                // Apply to articulation drives
                ArticulationDrive leftDrive = leftFingerBody.xDrive;
                leftDrive.target = leftTarget;
                leftFingerBody.xDrive = leftDrive;

                ArticulationDrive rightDrive = rightFingerBody.xDrive;
                rightDrive.target = rightTarget;
                rightFingerBody.xDrive = rightDrive;
            }
        }
    }

    // Get the current gripper open amount (0 = closed, 1 = fully open)
    public float GetCurrentGripperOpenAmount()
    {
        if (!hasGripper || gripper.leftFinger == null)
            return 0f;

        // Use the left finger position to determine gripper state
        ArticulationBody leftFingerBody = gripper.leftFinger.GetComponent<ArticulationBody>();
        if (leftFingerBody != null)
        {
            // Get the current position and normalize it
            float currentPos = leftFingerBody.xDrive.target;
            return currentPos / gripper.maxOpenDistance;
        }

        return 0f;
    }


    // Called by the trigger collider when an object enters the grasping area
    public void OnGraspTriggerEnter(GameObject obj)
    {
        // Only track graspable objects (objects with Graspable component or tag)
        if (obj.CompareTag("Graspable") || obj.GetComponent<Graspable>() != null)
        {
            if (debugMode) Debug.Log($"Graspable object {obj.name} entered grasping area");
            currentGraspableObject = obj;
        }
    }

    // Called when an object exits the grasping area
    public void OnGraspTriggerExit(GameObject obj)
    {
        // If this was our tracked object, forget it
        if (obj == currentGraspableObject)
        {
            if (debugMode) Debug.Log($"Graspable object {obj.name} exited grasping area");
            currentGraspableObject = null;
        }
    }


    // Helper method to determine if two planes are aligned
    private bool ArePlanesAligned(Transform plane1, Transform plane2)
    {
        // Check distance between planes
        float distance = Vector3.Distance(plane1.position, plane2.position);

        // Check angle between plane normals
        float angle = Vector3.Angle(plane1.up, plane2.up);

        bool aligned = distance <= planeAlignmentThreshold && angle <= planeAngleThreshold;

        // Debug when values are close to threshold
        if (debugMode && (distance < planeAlignmentThreshold * 2 || angle < planeAngleThreshold * 2))
        {
            Debug.Log($"Plane alignment check: Distance={distance:F3}, Threshold={planeAlignmentThreshold:F3}, " +
                     $"Angle={angle:F1}°, AngleThreshold={planeAngleThreshold:F1}° => Aligned={aligned}");
        }

        return aligned;
    }

    private void UpdatePlaneTrajectories()
    {
        // Only update if visualization is enabled
        if (!visualizeTrajectory)
            return;

        // Debug check for missing components
        if (leftPlaneTrajectory == null || rightPlaneTrajectory == null)
        {
            Debug.LogWarning("Plane trajectory line renderers not initialized!");
            return;
        }

        // Find nearest graspable object if none is in the grasping area
        GameObject targetObject = currentGraspableObject;

        // If we don't have a current graspable object
        if (targetObject == null)
        {
            // Find all graspable objects in the scene
            Graspable[] graspables = FindObjectsOfType<Graspable>();
            float closestDistance = float.MaxValue;

            // Find the closest one to the end effector
            foreach (Graspable graspable in graspables)
            {
                if (graspable.gameObject != null && endEffector != null)
                {
                    float distance = Vector3.Distance(graspable.transform.position, endEffector.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        targetObject = graspable.gameObject;
                    }
                }
            }
        }

        // Show trajectories if we have any valid object
        bool shouldShowTrajectories = targetObject != null;

        // Debug display status
        if (debugMode && Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log($"Plane trajectories: shouldShow={shouldShowTrajectories}, " +
                    $"targetObject={targetObject?.name ?? "none"}, " +
                    $"leftGripper={leftGripperPlane != null}, leftObject={leftObjectPlane != null}, " +
                    $"rightGripper={rightGripperPlane != null}, rightObject={rightObjectPlane != null}");
        }

        // Try to find plane transforms in target object if needed
        if (shouldShowTrajectories && (leftObjectPlane == null || rightObjectPlane == null) && targetObject != null)
        {
            // Try to find planes in the object
            Transform[] childTransforms = targetObject.GetComponentsInChildren<Transform>();
            foreach (Transform t in childTransforms)
            {
                if (t.name.Contains("LeftPlane"))
                    leftObjectPlane = t;
                else if (t.name.Contains("RightPlane"))
                    rightObjectPlane = t;

                // Break early if both are found
                if (leftObjectPlane != null && rightObjectPlane != null)
                    break;
            }

            if (debugMode && (leftObjectPlane != null || rightObjectPlane != null))
            {
                Debug.Log($"Found object planes: Left={leftObjectPlane != null}, Right={rightObjectPlane != null}");
            }
        }

        // Manage left plane trajectory
        bool canShowLeftTrajectory = shouldShowTrajectories && leftGripperPlane != null && leftObjectPlane != null;
        leftPlaneTrajectory.enabled = canShowLeftTrajectory;

        // Manage right plane trajectory
        bool canShowRightTrajectory = shouldShowTrajectories && rightGripperPlane != null && rightObjectPlane != null;
        rightPlaneTrajectory.enabled = canShowRightTrajectory;

        // Update trajectories
        if (canShowLeftTrajectory)
        {
            leftPlaneTrajectory.SetPosition(0, leftGripperPlane.position);
            leftPlaneTrajectory.SetPosition(1, leftObjectPlane.position);

            bool leftAligned = ArePlanesAligned(leftGripperPlane, leftObjectPlane);
            leftPlaneTrajectory.startColor = leftAligned ? alignedColor : unalignedColor;
            leftPlaneTrajectory.endColor = leftAligned ? alignedColor : unalignedColor;
        }

        if (canShowRightTrajectory)
        {
            rightPlaneTrajectory.SetPosition(0, rightGripperPlane.position);
            rightPlaneTrajectory.SetPosition(1, rightObjectPlane.position);

            bool rightAligned = ArePlanesAligned(rightGripperPlane, rightObjectPlane);
            rightPlaneTrajectory.startColor = rightAligned ? alignedColor : unalignedColor;
            rightPlaneTrajectory.endColor = rightAligned ? alignedColor : unalignedColor;
        }

        // Force redraw if needed
        if ((canShowLeftTrajectory || canShowRightTrajectory) && Time.frameCount % 5 == 0)
        {
            if (leftPlaneTrajectory != null) leftPlaneTrajectory.Simplify(0.0f);
            if (rightPlaneTrajectory != null) rightPlaneTrajectory.Simplify(0.0f);
        }
    }

    public void OnGraspTriggerTargetCollision(GameObject obj)
    {
        // Only track graspable objects (objects with Graspable component or tag)
        if (obj.CompareTag("Graspable") || obj.GetComponent<Graspable>() != null)
        {
            if (debugMode) Debug.Log($"Graspable target object {obj.name} collided with grasping area");
            currentGraspableObject = obj;

            // Update object plane references
            if (leftObjectPlane == null || rightObjectPlane == null)
            {
                Transform[] childTransforms = obj.GetComponentsInChildren<Transform>();
                foreach (Transform t in childTransforms)
                {
                    if (t.name.Contains("LeftPlane"))
                        leftObjectPlane = t;
                    else if (t.name.Contains("RightPlane"))
                        rightObjectPlane = t;
                }
            }
        }
    }



    // New method to calculate and cache alignment data
    private AlignmentData CalculateAlignmentData()
    {
        // If data is fresh enough, return cached data
        if (cachedAlignment.isValid && Time.frameCount - cachedAlignment.lastUpdateFrame < 5)
            return cachedAlignment;

        // Create new data
        AlignmentData data = new AlignmentData();
        data.isValid = false;
        data.lastUpdateFrame = Time.frameCount;

        // Ensure we have all required references
        if (leftGripperPlane == null || rightGripperPlane == null ||
            leftObjectPlane == null || rightObjectPlane == null)
            return data;

        // Calculate positions (once)
        Vector3 leftGripperPos = leftGripperPlane.position;
        Vector3 rightGripperPos = rightGripperPlane.position;
        Vector3 leftObjectPos = leftObjectPlane.position;
        Vector3 rightObjectPos = rightObjectPlane.position;

        // Calculate vectors once (reused for distance and directions)
        Vector3 leftDiffVec = leftGripperPos - leftObjectPos;
        Vector3 rightDiffVec = rightGripperPos - rightObjectPos;

        // Store the squared distances (avoid sqrt)
        data.leftDistanceSquared = leftDiffVec.sqrMagnitude;
        data.rightDistanceSquared = rightDiffVec.sqrMagnitude;

        // Store actual distances (only calculate when needed)
        data.leftDistance = Mathf.Sqrt(data.leftDistanceSquared);
        data.rightDistance = Mathf.Sqrt(data.rightDistanceSquared);

        // Calculate angles
        data.leftAngle = Vector3.Angle(leftGripperPlane.up, leftObjectPlane.up);
        data.rightAngle = Vector3.Angle(rightGripperPlane.up, rightObjectPlane.up);

        // Calculate normalized scores
        data.leftDistanceScore = Mathf.Clamp01(1.0f - (data.leftDistance / planeAlignmentThreshold));
        data.rightDistanceScore = Mathf.Clamp01(1.0f - (data.rightDistance / planeAlignmentThreshold));
        data.leftAngleScore = Mathf.Clamp01(1.0f - (data.leftAngle / planeAngleThreshold));
        data.rightAngleScore = Mathf.Clamp01(1.0f - (data.rightAngle / planeAngleThreshold));

        // Check if planes are aligned
        data.leftAligned = (data.leftDistanceSquared <= planeAlignmentThresholdSquared) &&
                          (data.leftAngle <= planeAngleThreshold);
        data.rightAligned = (data.rightDistanceSquared <= planeAlignmentThresholdSquared) &&
                           (data.rightAngle <= planeAngleThreshold);

        // Mark data as valid
        data.isValid = true;

        // Cache the data
        cachedAlignment = data;

        return data;
    }

    // Get an array of graspable objects with caching
    private Graspable[] GetGraspables()
    {
        // Return cached array if it's fresh enough
        if (cachedGraspables != null && Time.frameCount - lastGraspableCacheFrame < GRASPABLE_CACHE_FRAMES)
            return cachedGraspables;

        // Update cache
        cachedGraspables = FindObjectsOfType<Graspable>();
        lastGraspableCacheFrame = Time.frameCount;

        return cachedGraspables;
    }

    // Helper method to update object plane references
    private void UpdateObjectPlaneReferences(GameObject targetObject)
    {
        if ((leftObjectPlane == null || rightObjectPlane == null) && targetObject != null)
        {
            // Rather than getting all transforms, look specifically for planes
            Transform[] childTransforms = targetObject.GetComponentsInChildren<Transform>();
            foreach (Transform t in childTransforms)
            {
                string name = t.name;
                if (leftObjectPlane == null && name.Contains("LeftPlane"))
                    leftObjectPlane = t;
                else if (rightObjectPlane == null && name.Contains("RightPlane"))
                    rightObjectPlane = t;

                // Break early if both are found
                if (leftObjectPlane != null && rightObjectPlane != null)
                    break;
            }
        }
    }



    void OnDisable()
    {
        // Clear caches when disabled
        cachedGraspables = null;

        // Force garbage collection between episodes
        if (stepCount > 0 && stepCount % 50000 == 0)
        {
            System.GC.Collect();
        }
    }

    // Primary reward function that focuses only on successful grasping
    private void CalculateReward()
    {
        // No reward if no object in graspable area
        if (currentGraspableObject == null)
            return;

        // Calculate alignment
        AlignmentData alignData = CalculateAlignmentData();
        
        // Only proceed if alignment data is valid
        if (!alignData.isValid)
            return;
        
        // Calculate combined alignment score (0-1)
        float distanceScore = (alignData.leftDistanceScore + alignData.rightDistanceScore) * 0.5f;
        float angleScore = (alignData.leftAngleScore + alignData.rightAngleScore) * 0.5f;
        float combinedScore = (distanceScore + angleScore) * 0.5f;
        
        // Add small shaping reward based on improvement
        // Remove the static keyword since we're using the class field
        if (combinedScore > previousBestScore)
        {
            float improvement = combinedScore - previousBestScore;
            float shapingReward = improvement * 0.05f; // Small incremental reward
            AddReward(shapingReward);
            previousBestScore = combinedScore;
            
            // Log significant improvements
            if (improvement > 0.1f)
                Debug.Log($"Alignment improved: {previousBestScore-improvement:F2} → {previousBestScore:F2}, reward: +{shapingReward:F3}");
        }
        
        // Reset previous best score at episode start
        if (stepCount <= 1)
            previousBestScore = 0f;

        // Still give the big reward for full alignment
        if (alignData.leftAligned && alignData.rightAligned)
        {
            // Main success reward
            float reward = 1.0f;
            AddReward(reward);
            
            // Track successful grasp for curriculum advancement
            positiveRewardAchieved = true;
            
            // Visual feedback
            if (floorMeshRenderer != null)
                floorMeshRenderer.material = winMaterial;

            // Record successful episode
            /*if (enablePerformanceTracking && robotPerformanceTracker != null)
            {
                robotPerformanceTracker.RecordSuccessfulEpisode(Time.time, stepCount, GetCumulativeReward());
            }*/

            Debug.Log($"Success! Alignment achieved. Reward: {GetCumulativeReward():F2}");

            // End episode
            EndEpisode();
        }
    }

    // Enhanced curriculum learning parameter updates
    private void UpdateParametersFromEnvironment()
    {
        // Previous values for logging changes
        float prevPositionVariance = currentPositionVariance;
        float prevAlignThreshold = planeAlignmentThreshold;
        float prevAngleThreshold = planeAngleThreshold;

        // Read current curriculum parameters
        currentPositionVariance = Academy.Instance.EnvironmentParameters.GetWithDefault("position_variance", currentPositionVariance);
        planeAlignmentThreshold = Academy.Instance.EnvironmentParameters.GetWithDefault("plane_alignment_threshold", planeAlignmentThreshold);
        planeAngleThreshold = Academy.Instance.EnvironmentParameters.GetWithDefault("plane_angle_threshold", planeAngleThreshold);

        // Update squared threshold for efficient distance checks
        planeAlignmentThresholdSquared = planeAlignmentThreshold * planeAlignmentThreshold;

        // Log the current curriculum parameters (always, not just when they change)
        Debug.Log($"[Curriculum] Step: {stepCount}, Episode: {CompletedEpisodes}, " + 
                  $"Position Variance: {currentPositionVariance:F3}, " +
                  $"Alignment Threshold: {planeAlignmentThreshold:F3}, " +
                  $"Angle Threshold: {planeAngleThreshold:F1}°");

        // Invalidate cached alignment when parameters change
        if (prevAlignThreshold != planeAlignmentThreshold || prevAngleThreshold != planeAngleThreshold)
        {
            cachedAlignment.isValid = false;
            
            Debug.Log($"[Curriculum] Parameters changed! " +
                     $"Position Variance: {prevPositionVariance:F3} → {currentPositionVariance:F3}, " +
                     $"Alignment Threshold: {prevAlignThreshold:F3} → {planeAlignmentThreshold:F3}, " +
                     $"Angle Threshold: {prevAngleThreshold:F1}° → {planeAngleThreshold:F1}°");
        }
    }

    private void ProcessJointActions(ActionBuffers actionBuffers)
    {
        int jointCount = joints.Length;

        // Check if we're in inference mode (not in training)
        bool inferenceMode = !Academy.Instance.IsCommunicatorOn;

        // Process revolute and prismatic joint actions
        for (int i = 0; i < jointCount && i < actionBuffers.ContinuousActions.Length - (hasGripper ? 1 : 0); i++)
        {
            float action = actionBuffers.ContinuousActions[i];

            if (Mathf.Abs(action) <= 0.1f) continue; // Skip small actions for efficiency

            ArticulationJointController controller = joints[i].robotPart.GetComponent<ArticulationJointController>();

            if (controller != null)
            {
                if (joints[i].jointType == JointType.Revolute)
                {
                    // Convert continuous action to discrete rotation direction
                    RotationDirection direction = action > 0.1f ? RotationDirection.Positive :
                                              (action < -0.1f ? RotationDirection.Negative : RotationDirection.None);
                    controller.rotationState = direction;
                }
                else if (joints[i].jointType == JointType.Prismatic)
                {
                    float currentPos = controller.CurrentPosition();

                }
            }
        }
    }
}
