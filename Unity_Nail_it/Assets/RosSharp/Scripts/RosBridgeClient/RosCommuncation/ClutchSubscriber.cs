using UnityEngine;
using RosSharp.RosBridgeClient;

namespace RosSharp.RosBridgeClient
{
    public class ClutchSubscriber : UnitySubscriber<MessageTypes.Sensor.Joy>
    {
        public static bool ClutchEnabled { get; private set; } = false;
        
        protected override void ReceiveMessage(MessageTypes.Sensor.Joy message)
        {
            if (message.buttons != null && message.buttons.Length > 0)
            {
                // Update clutch state: 0 = disabled (move robot), 1 = enabled (don't move robot)
                ClutchEnabled = message.buttons[0] == 1;
                
                // Optional debug log
                Debug.Log($"Clutch state: {(ClutchEnabled ? "ENABLED" : "DISABLED")}");
            }
        }
    }
}