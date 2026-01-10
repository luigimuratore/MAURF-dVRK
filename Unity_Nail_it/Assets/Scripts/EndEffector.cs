using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndEffector : MonoBehaviour
{
    public RobotController robotController;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Goal"))
        {
            //robotController.OnEndEffectorWin();
        }
        if (other.CompareTag("table"))
        {
            robotController.OnEndEffectorLose();
        }
    }
}
