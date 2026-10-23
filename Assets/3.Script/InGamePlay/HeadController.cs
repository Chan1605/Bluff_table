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

    private float currentPitch = 0f;
    private float currentYaw = 0f;

    [SerializeField] private float remoteSmoothing = 12f;   // 남의 고개가 받은 각도를 따라가는 속도 (클수록 빠름)

    private float targetPitch;
    private float targetYaw;
    private bool remote;   // 서버에서 받은 각도로 움직이는 남의 캐릭터

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
        remote = false;   // 내 입력은 보간 없이 바로

        // 옵션 > 마우스 감도 (50 = 1배, 0 ~ 100). 슬라이더를 움직이면 바로 반영
        float sensitivity = lookSensitivity * GameSettings.MouseSensitivityMultiplier;
        currentYaw += mouseDelta.x * sensitivity;
        currentPitch -= mouseDelta.y * sensitivity;

        currentYaw = Mathf.Clamp(currentYaw, minYaw, maxYaw);
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
    }

    public void SetAbsoluteRotation(float pitch, float yaw) // 타 플레이어 고개 움직임. 서버에서 호출
    {
        targetPitch = pitch;
        targetYaw = yaw;
        remote = true;
    }

    // 애니메이터 등이 본을 덮어써도 프레임 마지막에 다시 적용
    private void LateUpdate()
    {
        if (remote)
        {
            float t = 1f - Mathf.Exp(-remoteSmoothing * Time.deltaTime);   // 프레임레이트와 무관한 보간
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, t);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, t);
        }
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
        Quaternion look = Quaternion.AngleAxis(currentYaw, axis.up)
                        * Quaternion.AngleAxis(currentPitch, axis.right);

        headPivot.rotation = look * baseWorld;
    }

    public Vector2 GetcurrentAngles() => new Vector2(currentPitch, currentYaw);
}
