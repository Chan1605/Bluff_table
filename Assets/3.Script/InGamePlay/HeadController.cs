using UnityEngine;
using Unity.Cinemachine;

public class HeadController : MonoBehaviour
{
    public Transform headPivot;
    public CinemachineCamera firstPersonCamera;

    public float lookSensitivity = 0.5f;
    public float minPitch = -45f;
    public float maxPitch = 45f;
    public float minYaw = -60f;
    public float maxYaw = 60f;

    [Header("패 보기 (스페이스 · 내 차례) - 플레이 중 바로 반영")]
    [Tooltip("패를 볼 때 시야각. 작을수록 크게 당겨 보인다")]
    public float focusFov = 65f;
    [Tooltip("패를 겨눈 뒤 더할 각도 (x = 위아래, +면 더 아래 / y = 좌우, +면 오른쪽)")]
    public Vector2 focusAngleOffset = new Vector2(0f, 0f);
    [Tooltip("당기고 돌아오는 속도 (초당 비율)")]
    public float focusSpeed = 5f;

    private Transform focusTarget;
    private float focusBlend;
    private float defaultFov = -1f;

    private float currentPitch = 0f;
    private float currentYaw = 0f;

    // 원작 리그 본은 기본 회전이 0이 아니다 → 처음 회전(부모 기준)을 기억해 두고 그 위에 고개 각도를 더한다
    private Quaternion baseLocalRotation;
    private bool hasBase;

    // 고개 축 기준 = 캐릭터 몸(Animator가 있는 루트). 이 컴포넌트가 머리 카메라에 붙어 있어도 고개 회전에 같이 돌지 않게
    private Transform body;

    private void Awake()
    {
        Animator animator = GetComponentInParent<Animator>();
        body = animator != null ? animator.transform : transform.root;
        CacheBase();
    }

    private void CacheBase()
    {
        if (hasBase || headPivot == null) return;

        baseLocalRotation = headPivot.localRotation;
        hasBase = true;
    }

    public void AddLookInput(Vector2 mouseDelta) // 로컬 플레이어 고개 움직임
    {
        currentYaw += mouseDelta.x * lookSensitivity;
        currentPitch -= mouseDelta.y * lookSensitivity;

        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    public void SetAbsoluteRotation(float pitch, float yaw) // 타 플레이어 고개 움직임. 서버에서 호출
    {
        currentPitch = pitch;
        currentYaw = yaw;
    }

    // 애니메이터 등이 본을 덮어써도 프레임 마지막에 다시 적용
    private void LateUpdate()
    {
        UpdateFocus();
        ApplyRotation();
    }

    public void ApplyRotation()
    {
        if (headPivot == null) return;
        CacheBase();

        Transform parent = headPivot.parent;
        Quaternion baseWorld = parent != null ? parent.rotation * baseLocalRotation : baseLocalRotation;

        // 좌우 = 캐릭터 위쪽 축, 위아래 = 캐릭터 오른쪽 축 (본마다 다른 로컬 축과 무관)
        Transform axis = body != null ? body : transform;

        // 패를 보는 중이면 마우스 각도와 손패 겨눔 각도를 섞는다
        float pitch = currentPitch, yaw = currentYaw;
        if (focusBlend > 0f && TryGetFocusAngles(baseWorld, out float focusPitch, out float focusYaw))
        {
            float t = Smooth(focusBlend);
            pitch = Mathf.LerpAngle(currentPitch, focusPitch, t);
            yaw = Mathf.LerpAngle(currentYaw, focusYaw, t);
        }

        Quaternion look = Quaternion.AngleAxis(yaw, axis.up)
                        * Quaternion.AngleAxis(pitch, axis.right);

        headPivot.rotation = look * baseWorld;
    }

    public Vector2 GetcurrentAngles() => new Vector2(currentPitch, currentYaw);

    // 로컬 플레이어 전용: 패를 볼 때 손패를 겨누고 시야를 좁힌다 (null = 원래 시점으로 복귀). 마우스 각도는 그대로 보관
    public void SetFocusTarget(Transform target) => focusTarget = target;

    private void UpdateFocus()
    {
        float goal = focusTarget != null ? 1f : 0f;
        focusBlend = Mathf.MoveTowards(focusBlend, goal, Time.deltaTime * focusSpeed);

        if (firstPersonCamera == null) return;
        if (defaultFov < 0f) defaultFov = firstPersonCamera.Lens.FieldOfView;
        if (focusBlend <= 0f && goal <= 0f && Mathf.Approximately(firstPersonCamera.Lens.FieldOfView, defaultFov)) return;

        var lens = firstPersonCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(defaultFov, focusFov, Smooth(focusBlend));
        firstPersonCamera.Lens = lens;
    }

    // 카메라가 손패를 보려면 기본 시선에서 몇 도 돌려야 하는지 (몸 기준 좌우 · 위아래)
    private bool TryGetFocusAngles(Quaternion baseWorld, out float pitch, out float yaw)
    {
        pitch = yaw = 0f;
        if (focusTarget == null || headPivot == null) return false;

        Transform cam = firstPersonCamera != null ? firstPersonCamera.transform : headPivot;
        Transform axis = body != null ? body : transform;

        // 카메라는 머리에 붙어 있으므로, 머리 기본 회전일 때의 카메라 앞 방향을 구한다
        Vector3 camForwardInHead = Quaternion.Inverse(headPivot.rotation) * cam.forward;
        Vector3 baseForward = axis.InverseTransformDirection(baseWorld * camForwardInHead);
        Vector3 toTarget = axis.InverseTransformDirection(focusTarget.position - cam.position);
        if (toTarget.sqrMagnitude < 1e-6f) return false;

        yaw = Mathf.DeltaAngle(Heading(baseForward), Heading(toTarget)) + focusAngleOffset.y;
        pitch = Elevation(baseForward) - Elevation(toTarget) + focusAngleOffset.x;   // + = 아래
        return true;
    }

    private static float Heading(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
    private static float Elevation(Vector3 v) => Mathf.Atan2(v.y, new Vector2(v.x, v.z).magnitude) * Mathf.Rad2Deg;
    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
