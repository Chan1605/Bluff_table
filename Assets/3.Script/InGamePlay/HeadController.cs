using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeadController : MonoBehaviour
{
    public Transform headPivot;
    public Camera firstPersonCamera;

    public float lookSensitivity = 0.5f;
    public float minPitch = -45f;
    public float maxPitch = 45f;
    public float minYaw = -60f;
    public float maxYaw = 60f;

    private float currentPitch = 0f;
    private float currentYaw = 0f;

    public void AddLookInput(Vector2 mouseDelta) // 로컬 플레이어 고개 움직임
    {
        currentYaw += mouseDelta.x * lookSensitivity;
        currentPitch -= mouseDelta.y * lookSensitivity;

        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        ApplyRotation();
    }

    public void SetAbsoluteRotation(float pitch, float yaw) // 타 플레이어 고개 움직임. 서버에서 호출
    {
        currentPitch = pitch;
        currentYaw = yaw;
        ApplyRotation();
    }

    public void ApplyRotation() 
    {
        if(headPivot != null)
        {
            headPivot.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }
    }

    public Vector2 GetcurrentAngles() => new Vector2(currentPitch, currentYaw);
}
