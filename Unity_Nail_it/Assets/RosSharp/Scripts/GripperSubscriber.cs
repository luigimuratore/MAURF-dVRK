using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;

public class GripperSubscriber : UnitySubscriber<JointState>
{
    [Header("Gripper Finger GameObjects")]
    public GameObject leftFinger;
    public GameObject rightFinger;

    [Header("Gripper Settings")]
    public float rotationAngle = 30f;       // Angolo di rotazione in gradi quando si chiude/apre
    public float movementSpeed = 2f;        // Velocità di animazione

    private HingeJoint leftHinge, rightHinge;
    private Quaternion leftClosedRot, rightClosedRot, leftOpenRot, rightOpenRot;
    private float target; // -1 = chiuso, 1 = aperto

    protected override void Start()
    {
        base.Start();
        
        // Ottieni i componenti HingeJoint
        leftHinge = leftFinger.GetComponent<HingeJoint>();
        rightHinge = rightFinger.GetComponent<HingeJoint>();
        
        if (leftHinge == null || rightHinge == null)
        {
            Debug.LogError("HingeJoint non trovato su uno o entrambi i finger. Verifica di aver aggiunto questo componente.");
            return;
        }
        
        // Memorizza le rotazioni iniziali come 'chiuse'
        leftClosedRot = leftFinger.transform.localRotation;
        rightClosedRot = rightFinger.transform.localRotation;
        
        // Calcola le rotazioni aperte utilizzando gli assi dei HingeJoint
        Vector3 leftAxis = leftFinger.transform.TransformDirection(leftHinge.axis);
        Vector3 rightAxis = rightFinger.transform.TransformDirection(rightHinge.axis);
        
        leftOpenRot = Quaternion.AngleAxis(-rotationAngle, leftAxis) * leftClosedRot;
        rightOpenRot = Quaternion.AngleAxis(rotationAngle, rightAxis) * rightClosedRot;
        
        target = -1; // Default a chiuso
    }

    protected override void ReceiveMessage(JointState message)
    {
        if (message.position.Length > 0)
        {
            float pos = (float)message.position[0];
            target = Mathf.Clamp(pos, -1f, 1f); // -1 (chiuso) o 1 (aperto)
        }
    }

    private void Update()
    {
        if (leftHinge == null || rightHinge == null) return;
        
        // Anima le dita in base al target
        Quaternion leftTarget = (target > 0) ? leftOpenRot : leftClosedRot;
        Quaternion rightTarget = (target > 0) ? rightOpenRot : rightClosedRot;

        leftFinger.transform.localRotation = Quaternion.Slerp(
            leftFinger.transform.localRotation, leftTarget, Time.deltaTime * movementSpeed);

        rightFinger.transform.localRotation = Quaternion.Slerp(
            rightFinger.transform.localRotation, rightTarget, Time.deltaTime * movementSpeed);
    }
}
