using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] PlayerLook look;
    [SerializeField] Transform body;  
    [Tooltip("머리 본 기준 눈 위치 (위, 앞)")]
    [SerializeField] Vector3 eyeOffset = new(0f, 0.08f, 0.1f);

    void LateUpdate()
    {
        if (look.Head == null)
        {
            return;
        }

        Quaternion lookRotation = body.rotation
                                * Quaternion.Euler(look.Pitch, look.Yaw, 0f);

        Vector3 position = look.Head.position + lookRotation * eyeOffset;

        transform.SetPositionAndRotation(position, lookRotation);
    }
}