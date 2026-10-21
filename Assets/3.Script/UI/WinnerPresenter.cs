using DG.Tweening;
using LiarsBar.Server;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

// 마지막 1인 승리 연출 (원작: 승자 정면 3인칭 화면 + 승리 모션, RETURNING TO LOBBY 카운트다운 동안 유지)
// - 승자 캐릭터 Animator에 Winner 트리거 (Bristle_OG: Any State → Victory, 캐릭터별 원작 승리 클립)
// - 모든 플레이어 화면을 승자 정면 카메라로 (테이블 건너편에서 승자를 바라보며 천천히 다가감)
// - 내가 승자면 1인칭용으로 숨겨 둔 머리를 다시 보이게
// - 내가 승자면 머리가 마우스 커서 쪽을 바라봄 (원작 킥). 기존 고개 동기화(CmdSyncHeadRotation → SyncVar)로 다른 사람에게도 보임
// 결과 단계(RoomPhase.Result)가 끝나 대기실로 가면 카메라를 지운다 (캐릭터는 PlayerVisualSetup이 지움)
// 프리팹 · Inspector 연결 없이 실행 시 자동 생성
public class WinnerPresenter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[WinnerPresenter]");
        DontDestroyOnLoad(go);
        go.AddComponent<WinnerPresenter>();
    }

    private static readonly int WinnerId = Animator.StringToHash("Winner");

    private const int CameraPriority = 100;      // 1인칭(20) · 관전(20)보다 위

    // 거리 · 높이는 m가 아니라 "승자 의자 ~ 테이블 중앙 거리(R)" 배율 → 맵 · 캐릭터 크기가 달라도 같은 구도
    // 원작처럼 테이블 건너편 자리 근처에서 승자를 바라보며 결과 시간 동안 조금 다가간다
    private const float StartDistance = 1.75f;   // 승자 머리에서 카메라까지 = R × 1.75 (건너편 자리 근처)
    private const float EndDistance = 1.45f;
    private const float SideOffset = 0.18f;      // R × 0.18 만큼 옆으로 (정면에서 살짝 비켜)
    private const float CameraHeight = 0.02f;    // 머리 높이 + R × 0.02
    private const float FocusDrop = 0.12f;       // 머리보다 R × 0.12 아래(가슴 쪽)를 바라봄
    private const float FieldOfView = 42f;

    // 승자 고개 → 커서 (화면 커서 위치를 카메라 ~ 머리 사이 지점으로 바꿔 그쪽을 바라봄)
    private const float LookPointRatio = 0.6f;   // 카메라 → 머리 거리 중 머리에서 이만큼 떨어진 곳 (작을수록 크게 돌아감)
    private const float LookSharpness = 10f;     // 따라가는 빠르기
    private const float LookSyncInterval = 0.1f; // 서버 전송 간격 (PlayerController와 같게)

    private CinemachineCamera cam;
    private HeadController lookHead;
    private Transform lookBody;
    private PlayerController lookSender;
    private Vector2 lookAngles;                  // (pitch, yaw)
    private float nextLookSync;
    private Tween dolly;
    private FirstPersonHeadHider restoredHider;

    private void OnEnable()
    {
        InGameEvent.OnWinnerAnnounced += HandleWinner;
        InGameEvent.OnEntryInGame += Clear;
        PlayerProfile.LocalPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        InGameEvent.OnWinnerAnnounced -= HandleWinner;
        InGameEvent.OnEntryInGame -= Clear;
        PlayerProfile.LocalPhaseChanged -= HandlePhaseChanged;
        Clear();
    }

    private void HandlePhaseChanged(RoomPhase phase)
    {
        if (phase != RoomPhase.Result) Clear();
    }

    private void HandleWinner(int seat)
    {
        Clear();
        if (seat < 0) return;   // 승자 없음 (모두 나감 등)

        PlayerProfile winner = FindPlayer(seat);
        if (winner == null || !winner.TryGetComponent(out PlayerState state) || state.headController == null) return;

        HeadController hc = state.headController;
        Animator body = hc.GetComponentInParent<Animator>();
        Transform head = hc.headPivot != null ? hc.headPivot : hc.transform;

        // 1) 승리 모션
        if (body != null && HasParameter(body, WinnerId)) body.SetTrigger(WinnerId);

        // 2) 내가 승자면 숨겨 둔 머리를 다시 보이게 (3인칭으로 나를 보게 되므로)
        if (winner.isLocalPlayer && body != null && body.TryGetComponent(out FirstPersonHeadHider hider) && hider.enabled)
        {
            hider.enabled = false;   // OnDisable이 머리 크기를 되돌림
            restoredHider = hider;
        }

        // 2-1) 내가 승자면 고개가 커서를 따라감
        if (winner.isLocalPlayer && body != null)
        {
            lookHead = hc;
            lookBody = body.transform;
            lookSender = winner.GetComponent<PlayerController>();
            lookAngles = hc.GetcurrentAngles();
        }

        // 3) 승자 정면 카메라 (테이블 가운데 쪽에서 승자를 바라봄)
        Vector3 forward = TowardTableCenter(seat, body != null ? body.transform : head, out float radius);
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        var go = new GameObject("[Gen] WinnerCam");
        go.transform.SetParent(transform, false);
        cam = go.AddComponent<CinemachineCamera>();
        LensSettings lens = cam.Lens;
        lens.FieldOfView = FieldOfView;
        lens.NearClipPlane = 0.05f;
        cam.Lens = lens;
        cam.Priority = CameraPriority;

        float distance = StartDistance;
        void Place()
        {
            if (cam == null || head == null) return;
            Vector3 focus = head.position - Vector3.up * (FocusDrop * radius);
            Vector3 pos = head.position + (forward * distance + right * SideOffset + Vector3.up * CameraHeight) * radius;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(focus - pos, Vector3.up));
        }
        Place();

        float seconds = ResultSeconds();
        dolly = DOTween.To(() => distance, d => { distance = d; Place(); }, EndDistance, seconds)
            .SetEase(Ease.OutSine)
            .SetTarget(this);
    }

    private void Update()
    {
        if (lookHead == null || lookBody == null || lookHead.headPivot == null) return;
        Camera view = Camera.main;
        if (view == null || Mouse.current == null) return;

        Transform head = lookHead.headPivot;
        Vector3 toCam = view.transform.position - head.position;
        Ray ray = view.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 point = ray.GetPoint(toCam.magnitude * (1f - LookPointRatio));
        Vector3 toPoint = point - head.position;

        // 커서가 화면 가운데면 각도 0 (클립 자세 그대로), 벗어난 만큼 좌우 · 위아래로
        Vector3 up = lookBody.up;
        float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(toCam, up), Vector3.ProjectOnPlane(toPoint, up), up);
        float pitch = Elevation(toCam, up) - Elevation(toPoint, up);   // 위를 보면 음수 (HeadController 규칙)

        Vector2 target = new Vector2(
            Mathf.Clamp(pitch, lookHead.minPitch, lookHead.maxPitch),
            Mathf.Clamp(yaw, lookHead.minYaw, lookHead.maxYaw));
        lookAngles = Vector2.Lerp(lookAngles, target, 1f - Mathf.Exp(-LookSharpness * Time.deltaTime));
        lookHead.SetAbsoluteRotation(lookAngles.x, lookAngles.y);   // 실제 적용은 HeadController.LateUpdate

        if (lookSender != null && Time.time >= nextLookSync)
        {
            nextLookSync = Time.time + LookSyncInterval;
            lookSender.SendHeadRotation(lookAngles.x, lookAngles.y);
        }
    }

    private static float Elevation(Vector3 dir, Vector3 up) =>
        Mathf.Asin(Mathf.Clamp(Vector3.Dot(dir.normalized, up), -1f, 1f)) * Mathf.Rad2Deg;

    private void Clear()
    {
        lookHead = null;
        lookBody = null;
        lookSender = null;
        DOTween.Kill(this);
        dolly = null;
        if (cam != null) Destroy(cam.gameObject);
        cam = null;
        if (restoredHider != null) restoredHider.enabled = true;   // 캐릭터가 아직 남아 있으면 1인칭 상태로 되돌림
        restoredHider = null;
    }

    // ───── 내부 ─────

    private static PlayerProfile FindPlayer(int seat)
    {
        PlayerProfile me = PlayerProfile.Local;
        if (me == null) return null;
        foreach (PlayerProfile p in PlayerProfile.All)
            if (p != null && p.IsInRoom && p.RoomId == me.RoomId && p.Seat == seat) return p;
        return null;
    }

    // 승자 의자 → 테이블 가운데 방향 (수평) + 그 거리(R). 못 구하면 R = 1.5
    private static Vector3 TowardTableCenter(int seat, Transform fallback, out float radius)
    {
        radius = 1.5f;
        Vector3 center = Vector3.zero;
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Transform c = SeatAnchors.Get(SeatArea.Table, i);
            if (c == null) continue;
            center += c.position;
            n++;
        }

        Transform chair = SeatAnchors.Get(SeatArea.Table, seat);
        if (n > 0 && chair != null)
        {
            Vector3 dir = Vector3.ProjectOnPlane(center / n - chair.position, Vector3.up);
            if (dir.sqrMagnitude > 1e-4f)
            {
                radius = dir.magnitude;
                return dir.normalized;
            }
        }
        Vector3 f = Vector3.ProjectOnPlane(fallback.forward, Vector3.up);
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    private static bool HasParameter(Animator animator, int id)
    {
        if (animator.runtimeAnimatorController == null) return false;   // 컨트롤러가 없으면 parameters 접근 시 경고
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.nameHash == id) return true;
        return false;
    }

    private static float ResultSeconds()
    {
        LiarsNetworkManager manager = LiarsNetworkManager.Instance;
        return manager != null && manager.Config != null ? manager.Config.ResultSeconds : GameFlowTiming.WinnerSeq;
    }
}
