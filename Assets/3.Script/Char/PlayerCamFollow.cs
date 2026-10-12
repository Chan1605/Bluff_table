using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] PlayerLook look;
    [SerializeField] Transform body;  // Player 루트
    [Tooltip("머리 본 기준 눈 위치 (위, 앞)")]
    [SerializeField] Vector3 eyeOffset = new(0f, 0.08f, 0.1f);

    void LateUpdate()
    {
        if (look.Head == null) return;

        // 회전: 머리 본이 아니라 Yaw/Pitch 값으로 직접 계산
        Quaternion lookRotation = body.rotation
                                * Quaternion.Euler(look.Pitch, look.Yaw, 0f);

        // 위치: 머리 위치 + 눈 높이 오프셋
        Vector3 position = look.Head.position + lookRotation * eyeOffset;

        transform.SetPositionAndRotation(position, lookRotation);
    }
}