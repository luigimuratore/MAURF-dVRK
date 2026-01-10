using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RosSharp.RosBridgeClient
{
    public class ArticulationJointStateSubscriber : UnitySubscriber<MessageTypes.Sensor.JointState>
    {
        public List<string> JointNames;
        public List<ArticulationJointStateWriter> JointStateWriters;
        
        // Fattori di scala per i movimenti dei giunti
        [Tooltip("Fattore di scala per ciascun giunto (1.0 = movimento identico)")]
        public List<float> JointScaleFactors;
        
        // Visualizzatore di stato del clutch nell'Inspector
        [SerializeField] private bool clutchIsEnabled = false;
        
        // Ultime posizioni del master ricevute
        private Dictionary<string, float> lastMasterPositions = new Dictionary<string, float>();
        
        // Posizioni attuali del robot simulato
        private Dictionary<string, float> currentRobotPositions = new Dictionary<string, float>();
        
        // Flag per il primo messaggio
        private bool isFirstMessage = true;

        protected override void Start()
        {
            base.Start();
            
            // Inizializza i dizionari
            foreach (string jointName in JointNames)
            {
                lastMasterPositions[jointName] = 0f;
                currentRobotPositions[jointName] = 0f;
            }
            
            // Inizializza i fattori di scala se necessario
            if (JointScaleFactors == null || JointScaleFactors.Count != JointNames.Count)
            {
                JointScaleFactors = new List<float>();
                for (int i = 0; i < JointNames.Count; i++)
                {
                    JointScaleFactors.Add(1.0f); // Valore predefinito: scala 1:1
                }
            }
        }

        protected override void ReceiveMessage(MessageTypes.Sensor.JointState message)
        {  
            // Aggiorna lo stato del clutch nell'inspector
            clutchIsEnabled = ClutchSubscriber.ClutchEnabled;
            
            // Se è il primo messaggio, inizializza i riferimenti senza muovere il robot
            if (isFirstMessage)
            {
                InitializeReferences(message);
                return;
            }
            
            // Se il clutch è attivo, aggiorna solo i riferimenti senza muovere il robot
            if (clutchIsEnabled)
            {
                UpdateReferences(message);
                return;
            }
            
            // Altrimenti processa i movimenti relativi
            ProcessRelativeMovements(message);
        }
        
        // Inizializza i riferimenti per il primo messaggio
        private void InitializeReferences(MessageTypes.Sensor.JointState message)
        {
            for (int i = 0; i < message.name.Length; i++)
            {
                string jointName = message.name[i];
                if (JointNames.Contains(jointName))
                {
                    lastMasterPositions[jointName] = (float)message.position[i];
                }
            }
            isFirstMessage = false;
            Debug.Log("Riferimenti iniziali impostati");
        }
        
        // Aggiorna i riferimenti quando il clutch è attivo
        private void UpdateReferences(MessageTypes.Sensor.JointState message)
        {
            for (int i = 0; i < message.name.Length; i++)
            {
                string jointName = message.name[i];
                if (JointNames.Contains(jointName))
                {
                    lastMasterPositions[jointName] = (float)message.position[i];
                }
            }
        }
        
        // Processa i movimenti relativi
        private void ProcessRelativeMovements(MessageTypes.Sensor.JointState message)
        {
            for (int i = 0; i < message.name.Length; i++)
            {
                string jointName = message.name[i];
                float currentMasterPosition = (float)message.position[i];
                
                int jointIndex = JointNames.IndexOf(jointName);
                if (jointIndex >= 0 && lastMasterPositions.ContainsKey(jointName))
                {
                    // Calcola lo spostamento relativo del master
                    float masterDelta = currentMasterPosition - lastMasterPositions[jointName];
                    
                    // Applica il fattore di scala al movimento
                    float scaledDelta = masterDelta * JointScaleFactors[jointIndex];
                    
                    // Aggiorna la posizione del robot applicando lo spostamento relativo scalato
                    float newRobotPosition = currentRobotPositions[jointName] + scaledDelta;
                    JointStateWriters[jointIndex].Write(newRobotPosition);
                    
                    // Aggiorna le posizioni memorizzate
                    currentRobotPositions[jointName] = newRobotPosition;
                    lastMasterPositions[jointName] = currentMasterPosition;
                }
            }
        }
    }
}

