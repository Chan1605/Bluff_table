using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookTester : MonoBehaviour
{
    enum Mode { Manual, Sway, LookAtTarget }

    [SerializeField] PlayerLook look;
    [SerializeField] Mode mode = Mode.Manual;

    [Header("Manual: 플레이 중 슬라이더로 조작")]
    [SerializeField, Range(-90f, 90f)] float yaw;
    [SerializeField, Range(-60f, 60f)] float pitch;

    [Header("Sway: 자동으로 두리번거림")]
    [SerializeField] float swaySpeed = 0.5f;

    [Header("LookAtTarget: 특정 대상 바라보기")]
    [SerializeField] Transform target;

    [Header("네트워크 수신처럼 부드럽게")]
    [SerializeField] float smooth = 10f;

    void Update()
    {
        float targetYaw = look.Yaw, targetPitch = look.Pitch;

        switch (mode)
        {
            case Mode.Manual:
                targetYaw = yaw;
                targetPitch = pitch;
                break;

            case Mode.Sway:
                float t = Time.time * swaySpeed;
                targetYaw = Mathf.Sin(t) * 60f;
                targetPitch = Mathf.Sin(t * 0.7f) * 15f;
                break;

            case Mode.LookAtTarget:
                if (target == null || look.Head == null) break;
                Vector3 dir = look.transform.InverseTransformDirection(
                    target.position - look.Head.position);
                targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                targetPitch = -Mathf.Atan2(dir.y,
                    new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
                break;
        }

        float k = smooth * Time.deltaTime;
        look.SetLookAngles(Mathf.LerpAngle(look.Yaw, targetYaw, k),
                           Mathf.Lerp(look.Pitch, targetPitch, k));
    }
}
