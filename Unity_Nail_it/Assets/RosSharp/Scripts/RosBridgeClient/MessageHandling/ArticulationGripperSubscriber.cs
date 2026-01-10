using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class ArticulationGripperSubscriber : UnitySubscriber<JointState>
{
    [Header("Gripper Finger GameObjects")]
    public GameObject leftFinger;
    public GameObject rightFinger;

    [Header("Gripper Settings")]
    public float maxOpeningAngle = 30f;    // Angolo massimo di apertura in gradi
    public float movementSpeed = 10f;      // Velocità di risposta del drive

    public enum RotationAxis
    {
        X, Y, Z
    }

    [Header("Drive Configuration")]
    public RotationAxis leftFingerAxis = RotationAxis.X;
    public RotationAxis rightFingerAxis = RotationAxis.X;
    public bool invertLeftFinger = false;
    public bool invertRightFinger = false;

    private ArticulationBody leftArticulation, rightArticulation;
    private float target; // -1 = chiuso, 1 = aperto
    private bool isNewCommandReceived = false;

    protected override void Start()
    {
        base.Start();
        
        // Ottieni i componenti ArticulationBody
        leftArticulation = leftFinger.GetComponent<ArticulationBody>();
        rightArticulation = rightFinger.GetComponent<ArticulationBody>();
        
        if (leftArticulation == null || rightArticulation == null)
        {
            Debug.LogError("ArticulationBody non trovato su uno o entrambi i finger. Verifica di aver aggiunto questo componente.");
            return;
        }
        
        // Configura i drive
        ConfigureDrives();
        
        target = -10f; // Default a chiuso
    }

    private void ConfigureDrives()
    {
        // Configura i drive per entrambe le dita
        if (leftArticulation != null)
        {
            ConfigureDrive(leftArticulation, leftFingerAxis);
        }

        if (rightArticulation != null)
        {
            ConfigureDrive(rightArticulation, rightFingerAxis);
        }
    }

    private void ConfigureDrive(ArticulationBody body, RotationAxis axis)
    {
        // Configura il drive appropriato in base all'asse
        ArticulationDrive drive;
        
        switch (axis)
        {
            case RotationAxis.X:
                drive = body.xDrive;
                break;
            case RotationAxis.Y:
                drive = body.yDrive;
                break;
            case RotationAxis.Z:
                drive = body.zDrive;
                break;
            default:
                drive = body.xDrive;
                break;
        }
        
        drive.stiffness = 10000;
        drive.damping = 500;
        drive.forceLimit = 1000;
        
        // Assegna il drive configurato all'articolazione
        switch (axis)
        {
            case RotationAxis.X:
                body.xDrive = drive;
                break;
            case RotationAxis.Y:
                body.yDrive = drive;
                break;
            case RotationAxis.Z:
                body.zDrive = drive;
                break;
        }
    }

    protected override void ReceiveMessage(JointState message)
    {
        if (message.position.Length > 0)
        {
            float pos = (float)message.position[0];
            target = Mathf.Clamp(pos, -1f, 1f); // Normalizza tra -1 (chiuso) e 1 (aperto)
            isNewCommandReceived = true;
        }
    }

    private void Update()
    {
        if (leftArticulation == null || rightArticulation == null || !isNewCommandReceived) return;
        
        // Converti il target normalizzato in target di posizione per i drive
        float normalizedTarget = (target + 1f) / 2f; // Converti da [-1,1] a [0,1]
        
        // Calcola gli angoli considerando le inversioni
        float leftAngle = normalizedTarget * maxOpeningAngle;
        float rightAngle = normalizedTarget * maxOpeningAngle;
        
        if (invertLeftFinger) leftAngle = -leftAngle;
        if (invertRightFinger) rightAngle = -rightAngle;
        
        // Applica ai drive appropriati
        ApplyDriveTarget(leftArticulation, leftFingerAxis, leftAngle);
        ApplyDriveTarget(rightArticulation, rightFingerAxis, rightAngle);
        
        isNewCommandReceived = false;
    }
    
    private void ApplyDriveTarget(ArticulationBody body, RotationAxis axis, float targetAngle)
    {
        ArticulationDrive drive;
        
        switch (axis)
        {
            case RotationAxis.X:
                drive = body.xDrive;
                drive.target = targetAngle;
                body.xDrive = drive;
                break;
            case RotationAxis.Y:
                drive = body.yDrive;
                drive.target = targetAngle;
                body.yDrive = drive;
                break;
            case RotationAxis.Z:
                drive = body.zDrive;
                drive.target = targetAngle;
                body.zDrive = drive;
                break;
        }
    }
}