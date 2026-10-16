using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 1인칭 카메라 리그. 기획서 43쪽: 자유 둘러보기 / 고정 · 연출 컷 / 관전.
    ///
    /// Animator(Camera.controller)가 정하는 것 — 아래 세 값의 곡선과 상태 사이 블렌드 시간
    ///   focusWeight  0 = 마우스 둘러보기, 1 = 대상 바라보기 (전이 시간이 곧 시선 이동 이징)
    ///   fovOffset    줌 (음수 = 확대)
    ///   orbitDegrees 승리 연출에서 대상 주위를 도는 각도
    /// 코드가 정하는 것 — 리그를 어디에 둘지(앵커), 무엇을 바라볼지(대상), 마우스 입력.
    ///
    /// 계층
    ///   CameraRig  ← Animator + CameraDirector
    ///   └ Main Camera
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Camera cam;

        [Header("둘러보기")]
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] float yawLimit = 70f;
        [SerializeField] float pitchMin = -35f;
        [SerializeField] float pitchMax = 30f;

        [Header("승리 회전")]
        [SerializeField] float orbitRadius = 1.6f;
        [SerializeField] float orbitHeight = 0.4f;

        // ▼ Animator 클립이 직접 쓰는 값
        [SerializeField] float focusWeight;
        [SerializeField] float fovOffset;
        [SerializeField] float orbitDegrees;

        Transform anchor;       // 1인칭 머리 또는 관전 앵커
        Transform focusTarget;  // null이면 시선은 그대로 두고 줌만
        bool inputLocked;
        bool orbiting;
        float yaw, pitch, baseFov;
        Quaternion focusRot;

        public bool IsSpectating { get; private set; }

        void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            baseFov = cam.fieldOfView;
        }

        public void AttachTo(Transform newAnchor, bool spectating)
        {
            anchor = newAnchor;
            IsSpectating = spectating;
            yaw = pitch = 0f;
        }

        /// <summary>로직의 OnCameraLockRequested — 입력만 막는다 (시선 이동 없음)</summary>
        public void SetInputLocked(bool locked) => inputLocked = locked;

        /// <summary>대상을 바라보며 고정 · 줌. target이 null이면 현재 시선 유지 + 줌만 (내 룰렛)</summary>
        public void Focus(Transform target)
        {
            focusTarget = target;
            orbiting = false;
            if (target == null) focusRot = CurrentLookRotation();
            animator.SetBool(AnimIds.Camera.FocusedId, true);
        }

        public void Release() => animator.SetBool(AnimIds.Camera.FocusedId, false);

        public void PlayVictory(Transform winnerFocus)
        {
            focusTarget = winnerFocus;
            orbiting = winnerFocus != null;
            animator.SetTrigger(AnimIds.Camera.VictoryId);
        }

        public void ResetAll()
        {
            focusTarget = null;
            orbiting = false;
            inputLocked = false;
            IsSpectating = false;
            yaw = pitch = 0f;
            animator.SetBool(AnimIds.Camera.FocusedId, false);
            animator.SetTrigger(AnimIds.Camera.ResetId);
        }

        void Update()
        {
            // 연출로 시선이 넘어간 동안(focusWeight > 0)이나 잠금 중엔 둘러보기 입력을 받지 않는다
            if (inputLocked || focusWeight > 0.01f || anchor == null) return;
            Vector2 d = ReadMouseDelta() * sensitivity;
            yaw = Mathf.Clamp(yaw + d.x, -yawLimit, yawLimit);
            pitch = Mathf.Clamp(pitch - d.y, pitchMin, pitchMax);
        }

        void LateUpdate()
        {
            if (anchor == null) return;

            Vector3 pos = anchor.position;
            Quaternion free = CurrentLookRotation();
            Quaternion look = free;

            if (orbiting && focusTarget != null)
            {
                // 승리: 대상 주위를 orbitDegrees만큼 돈다 (각도 곡선은 Victory 클립)
                Vector3 start = anchor.position - focusTarget.position;
                start.y = 0f;
                Vector3 dir = Quaternion.Euler(0f, orbitDegrees, 0f) * start.normalized;
                Vector3 orbitPos = focusTarget.position + dir * orbitRadius + Vector3.up * orbitHeight;
                pos = Vector3.Lerp(pos, orbitPos, focusWeight);
                look = Quaternion.Slerp(free, Quaternion.LookRotation(focusTarget.position - pos), focusWeight);
            }
            else
            {
                Quaternion target = focusTarget != null
                    ? Quaternion.LookRotation(focusTarget.position - pos)
                    : focusRot;
                look = Quaternion.Slerp(free, target, focusWeight);
            }

            transform.SetPositionAndRotation(pos, look);
            cam.fieldOfView = baseFov + fovOffset;
        }

        Quaternion CurrentLookRotation() => anchor.rotation * Quaternion.Euler(pitch, yaw, 0f);

        static Vector2 ReadMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#endif
        }
    }
}
