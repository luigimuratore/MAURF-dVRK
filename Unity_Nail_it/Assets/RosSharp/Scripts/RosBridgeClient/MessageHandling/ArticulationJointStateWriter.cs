using UnityEngine;

namespace RosSharp.RosBridgeClient
{
    [RequireComponent(typeof(ArticulationBody))]
    public class ArticulationJointStateWriter : MonoBehaviour
    {
        private ArticulationBody articulation;
        public float scale = 1;
        private float newState;
        private float prevState;
        private bool isNewStateReceived;
        
        public enum JointType
        {
            Revolute,  // Giunto rotazionale (predefinito)
            Prismatic  // Giunto prismatico
        }
        
        public JointType jointType = JointType.Revolute;
        
        public enum PrismaticAxis
        {
            X,
            Y,
            Z
        }
        
        public PrismaticAxis prismaticAxis = PrismaticAxis.Z;  // Default Z come indicato

        private void Start()
        {
            articulation = GetComponent<ArticulationBody>();
        }

        private void Update()
        {
            if (isNewStateReceived)
            {
                WriteUpdate();
                isNewStateReceived = false;
            }
        }

        private void WriteUpdate()
        {
            if (jointType == JointType.Revolute)
            {
                // Per giunti rotazionali, usiamo xDrive e convertiamo da radianti a gradi
                var drive = articulation.xDrive;
                drive.target = newState * Mathf.Rad2Deg * scale;
                articulation.xDrive = drive;
            }
            else // JointType.Prismatic
            {
                // Per giunti prismatici, usa il drive corrispondente all'asse selezionato
                float targetValue = newState * scale; // In metri, senza conversione radianti-gradi
                
                switch (prismaticAxis)
                {
                    case PrismaticAxis.X:
                        var xDrive = articulation.xDrive;
                        xDrive.target = targetValue;
                        articulation.xDrive = xDrive;
                        break;
                    
                    case PrismaticAxis.Y:
                        var yDrive = articulation.yDrive;
                        yDrive.target = targetValue;
                        articulation.yDrive = yDrive;
                        break;
                    
                    case PrismaticAxis.Z:
                        var zDrive = articulation.zDrive;
                        zDrive.target = targetValue;
                        articulation.zDrive = zDrive;
                        break;
                }
            }
            
            prevState = newState;
        }

        public void Write(float state)
        {
            newState = state;
            isNewStateReceived = true;
        }
    }
}