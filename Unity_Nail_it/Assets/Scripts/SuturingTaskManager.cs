using UnityEngine;
using Unity.MLAgents;

public class SuturingTaskManager : MonoBehaviour
{
    [Header("Agents")]
    public RobotController agent1_PSM1;
    public RobotController agent2_PSM2;

    [Header("Suturing Points")]
    public GameObject entryPoint;
    public GameObject exitPoint;
    public GameObject needle;

    [Header("Task Control")]
    [SerializeField] private KeyCode executeArchKey = KeyCode.PageUp;
    [SerializeField] private KeyCode handoverKey = KeyCode.L;
    [SerializeField] private bool autoProgressEnabled = false;

    public enum SuturingPhase
    {
        Agent1_Approach,      // Agent1 approaches needle
        Agent1_Grasp,         // Agent1 grasps needle
        Agent1_PlaceEntry,    // Agent1 places needle at entry point
        WaitingForArch,       // Waiting for user/agent to trigger arch
        PerformingArch,       // Arch motion in progress
        WaitingForHandover,   // Arch complete, waiting for handover
        Agent2_Approach,      // Agent2 approaches needle tip at exit
        Agent2_Grasp,         // Agent2 grasps needle
        Agent2_Complete,      // Agent2 completes task
        TaskComplete          // Full task complete
    }

    [Header("Phase Tracking")]
    [SerializeField] private SuturingPhase currentPhase = SuturingPhase.Agent1_Approach;
    private SuturingPhase previousPhase;

    [Header("Phase Completion Settings")]
    [SerializeField] private float entryPointThreshold = 0.02f;
    [SerializeField] private float exitPointThreshold = 0.02f;
    [SerializeField] private float autoProgressDelay = 0.5f;

    private float phaseTransitionTimer = 0f;
    private bool archComplete = false;

    void Start()
    {
        ValidateReferences();
        InitializeTask();
    }

    void Update()
    {
        UpdatePhaseLogic();
        HandleManualInputs();
        
        // DEBUG: Press 'I' to inspect needle state
        if (Input.GetKeyDown(KeyCode.I))
        {
            InspectNeedleState();
        }
        
        if (currentPhase != previousPhase)
        {
            OnPhaseChanged();
            previousPhase = currentPhase;
        }
    }

    private void UpdatePhaseLogic()
    {
        switch (currentPhase)
        {
            case SuturingPhase.Agent1_PlaceEntry:
                CheckEntryPointPlacement();
                break;

            case SuturingPhase.WaitingForArch:
                if (autoProgressEnabled)
                {
                    phaseTransitionTimer += Time.deltaTime;
                    if (phaseTransitionTimer >= autoProgressDelay)
                    {
                        TriggerArchMotion();
                    }
                }
                break;

            case SuturingPhase.PerformingArch:
                if (archComplete)
                {
                    TransitionToPhase(SuturingPhase.WaitingForHandover);
                }
                break;

            case SuturingPhase.WaitingForHandover:
                if (autoProgressEnabled)
                {
                    phaseTransitionTimer += Time.deltaTime;
                    if (phaseTransitionTimer >= autoProgressDelay)
                    {
                        InitiateHandover();
                    }
                }
                break;

            case SuturingPhase.Agent2_Approach:
                CheckAgent2GraspCondition();
                break;
        }
    }

    private void HandleManualInputs()
    {
        // Manual arch trigger
        if (Input.GetKeyDown(executeArchKey) && currentPhase == SuturingPhase.WaitingForArch)
        {
            TriggerArchMotion();
        }

        // Manual handover trigger
        if (Input.GetKeyDown(handoverKey) && currentPhase == SuturingPhase.WaitingForHandover)
        {
            InitiateHandover();
        }
    }

    private void CheckEntryPointPlacement()
    {
        if (agent1_PSM1.needleTip != null && entryPoint != null)
        {
            float dist = Vector3.Distance(agent1_PSM1.needleTip.transform.position, entryPoint.transform.position);
            
            if (dist <= entryPointThreshold && agent1_PSM1.needleGrasped)
            {
                Debug.Log("[SUTURING] Entry point reached!");
                TransitionToPhase(SuturingPhase.WaitingForArch);
                
                // Reward agent1
                agent1_PSM1.AddReward(10f);
            }
        }
    }

    private void TriggerArchMotion()
    {
        Debug.Log("[SUTURING] Starting arch motion...");
        TransitionToPhase(SuturingPhase.PerformingArch);
        archComplete = false;
        
        // Start arch on agent1
        agent1_PSM1.PerformSuturingArchMotion();
        
        // Monitor arch completion
        StartCoroutine(WaitForArchCompletion());
    }

    private System.Collections.IEnumerator WaitForArchCompletion()
    {
        yield return new WaitUntil(() => !agent1_PSM1.isExecutingArch);
        archComplete = true;
        Debug.Log("[SUTURING] Arch motion complete!");
    }

    private void InitiateHandover()
    {
        Debug.Log("[SUTURING] Initiating handover to Agent2...");
        
        // Agent1 releases needle - FIRST force stop any kinematic state
        if (agent1_PSM1.isGrasping)
        {
            agent1_PSM1.ReleaseObject();
        }
        
        // CRITICAL: Ensure needle is visible and physically present
        if (needle != null)
        {
            // Re-enable renderer if it exists
            MeshRenderer needleRenderer = needle.GetComponent<MeshRenderer>();
            if (needleRenderer != null)
            {
                needleRenderer.enabled = true;
            }
            
            // Ensure rigidbody is properly configured
            Rigidbody needleRb = needle.GetComponent<Rigidbody>();
            if (needleRb != null)
            {
                needleRb.isKinematic = false;
                needleRb.useGravity = true;
                needleRb.velocity = Vector3.zero;
                needleRb.angularVelocity = Vector3.zero;
                needleRb.WakeUp(); // Force physics update
            }
            
            // Reset parent (ensure it's not child of agent1)
            needle.transform.parent = null;
            
            // Log needle state for debugging
            Debug.Log($"[HANDOVER] Needle position: {needle.transform.position}");
            Debug.Log($"[HANDOVER] Needle active: {needle.activeInHierarchy}");
            Debug.Log($"[HANDOVER] Needle renderer enabled: {needleRenderer?.enabled}");
        }
        
        // Wait one frame before enabling agent2 to let physics settle
        StartCoroutine(EnableAgent2AfterDelay());
    }

    private System.Collections.IEnumerator EnableAgent2AfterDelay()
    {
        // Wait for physics to settle
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        
        // Disable agent1, enable agent2
        agent1_PSM1.enabled = false;
        agent2_PSM2.enabled = true;
        
        TransitionToPhase(SuturingPhase.Agent2_Approach);
        
        Debug.Log("[HANDOVER] Agent2 now active");
    }

    private void CheckAgent2GraspCondition()
    {
        if (exitPoint != null && agent2_PSM2.endEffector != null)
        {
            float dist = Vector3.Distance(agent2_PSM2.endEffector.transform.position, exitPoint.transform.position);
            
            if (dist <= exitPointThreshold && agent2_PSM2.needleGrasped)
            {
                Debug.Log("[SUTURING] Agent2 grasped needle!");
                TransitionToPhase(SuturingPhase.Agent2_Complete);
                
                // Reward agent2
                agent2_PSM2.AddReward(10f);
            }
        }
    }

    private void TransitionToPhase(SuturingPhase newPhase)
    {
        currentPhase = newPhase;
        phaseTransitionTimer = 0f;
    }

    private void OnPhaseChanged()
    {
        Debug.Log($"[SUTURING] Phase changed: {previousPhase} → {currentPhase}");
        
        // Update agent task types based on phase
        switch (currentPhase)
        {
            case SuturingPhase.Agent1_PlaceEntry:
                agent1_PSM1.mainTaskType = RobotController.MainTaskType.Placement;
                break;
                
            case SuturingPhase.Agent2_Approach:
                agent2_PSM2.mainTaskType = RobotController.MainTaskType.Reaching;
                break;
        }
    }

    private void InitializeTask()
    {
        // Enable only agent1 at start
        agent1_PSM1.enabled = true;
        agent2_PSM2.enabled = false;
        
        currentPhase = SuturingPhase.Agent1_Approach;
        previousPhase = currentPhase;
        
        Debug.Log("[SUTURING] Task initialized. Agent1 ready.");
    }

    private void ValidateReferences()
    {
        if (agent1_PSM1 == null) Debug.LogError("Agent1 (PSM1) not assigned!");
        if (agent2_PSM2 == null) Debug.LogError("Agent2 (PSM2) not assigned!");
        if (entryPoint == null) Debug.LogError("Entry point not assigned!");
        if (exitPoint == null) Debug.LogError("Exit point not assigned!");
        if (needle == null) Debug.LogError("Needle not assigned!");
    }

    private void InspectNeedleState()
    {
        if (needle == null)
        {
            Debug.LogError("[DEBUG] Needle is NULL!");
            return;
        }
        
        Debug.Log("=== NEEDLE STATE INSPECTION ===");
        Debug.Log($"Active: {needle.activeInHierarchy}");
        Debug.Log($"Position: {needle.transform.position}");
        Debug.Log($"Parent: {(needle.transform.parent != null ? needle.transform.parent.name : "None")}");
        
        MeshRenderer renderer = needle.GetComponent<MeshRenderer>();
        Debug.Log($"Renderer enabled: {renderer?.enabled}");
        
        Rigidbody rb = needle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"IsKinematic: {rb.isKinematic}");
            Debug.Log($"UseGravity: {rb.useGravity}");
            Debug.Log($"IsSleeping: {rb.IsSleeping()}");
        }
        
        Debug.Log($"Current Phase: {currentPhase}");
        Debug.Log($"Agent1 Grasping: {agent1_PSM1?.isGrasping}");
        Debug.Log($"Agent2 Grasping: {agent2_PSM2?.isGrasping}");
        Debug.Log("================================");
    }

    public SuturingPhase GetCurrentPhase() => currentPhase;
    public bool IsArchComplete() => archComplete;
}