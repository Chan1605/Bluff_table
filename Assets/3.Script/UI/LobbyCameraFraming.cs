using LiarsBar.Server;
using Unity.Cinemachine;
using UnityEngine;

// Player 프리팹 루트에 붙인다. 내 캐릭터가 대기실에 있는 동안 CinemachineCamera를 띄워
// 대기실 좌석 4개(SeatAnchors)가 한 화면에 모두 들어오도록 잡는다. 모든 클라이언트가 같은 구도를 본다.
// 대기실을 벗어나면 끄고, 기존 카메라(DefaultCam 등)로 CinemachineBrain이 블렌드해서 돌아간다.
// 씬 파일은 건드리지 않는다 (카메라는 실행 중에만 생성).
[RequireComponent(typeof(PlayerProfile))]
public class LobbyCameraFraming : MonoBehaviour
{
    private const int MaxSeats = 4;

    [Tooltip("바라볼 지점 (좌석 줄 중심 기준, 미터). X를 +로 주면 오른쪽 끝 캐릭터의 라벨 자리가 생김")]
    [SerializeField] private Vector3 lookOffset = new Vector3(0.3f, 0.15f, 0f);
    [Tooltip("카메라 높이 (좌석 줄 중심 기준, 미터). lookOffset.y보다 크면 살짝 내려다봄")]
    [SerializeField] private float cameraHeight = 0.55f;
    [Tooltip("양 끝 좌석 바깥 여유 (미터). 키우면 카메라가 뒤로 물러남")]
    [SerializeField] private float sideMargin = 1.2f;
    [SerializeField, Range(20f, 70f)] private float fieldOfView = 40f;
    [Tooltip("다른 CinemachineCamera보다 높아야 함")]
    [SerializeField] private int priority = 50;

    private PlayerProfile profile;
    private CinemachineCamera lobbyCam;

    private void Awake()
    {
        profile = GetComponent<PlayerProfile>();
    }

    private void OnEnable()
    {
        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;
        SetActive(false);
    }

    private void OnDestroy()
    {
        if (lobbyCam != null) Destroy(lobbyCam.gameObject);
    }

    private void OnProfileEvent(PlayerProfile changed)
    {
        if (changed == profile) Refresh();
    }

    private void Refresh()
    {
        bool lobby = profile.isLocalPlayer && profile.IsLoggedIn && profile.IsInRoom
                     && profile.Seat >= 0 && profile.Phase == RoomPhase.Lobby;
        SetActive(lobby);
    }

    // 대기실 씬이 늦게 올라와도(SeatAnchors 등록) 바로 맞도록 매 프레임 갱신
    private void LateUpdate()
    {
        if (lobbyCam != null && lobbyCam.gameObject.activeSelf) Place();
    }

    private void SetActive(bool active)
    {
        if (!active)
        {
            if (lobbyCam != null) lobbyCam.gameObject.SetActive(false);
            return;
        }

        if (lobbyCam == null) lobbyCam = CreateCamera();
        Place();
        lobbyCam.gameObject.SetActive(true);
    }

    private CinemachineCamera CreateCamera()
    {
        GameObject go = new GameObject("LobbyFramingCam (runtime)");
        go.SetActive(false);

        CinemachineCamera cam = go.AddComponent<CinemachineCamera>();
        cam.Priority.Enabled = true;
        cam.Priority.Value = priority;

        LensSettings lens = cam.Lens;
        lens.FieldOfView = fieldOfView;
        cam.Lens = lens;
        return cam;
    }

    private void Place()
    {
        // 대기실 씬에 원작 카메라 자리(LobbyCameraAnchor)가 있으면 그대로 사용
        LobbyCameraAnchor anchor = LobbyCameraAnchor.Current;
        if (anchor != null)
        {
            lobbyCam.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
            ApplyFieldOfView(anchor.FieldOfView);
            return;
        }

        GetSeatRow(out Vector3 center, out Quaternion rot, out float halfSpan);

        // 좌석 줄 전체 + 여유가 화면 가로에 들어오는 거리
        Camera output = Camera.main;
        float aspect = output != null ? output.aspect : 16f / 9f;
        float halfWidth = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect;
        float distance = (halfSpan + sideMargin) / Mathf.Max(halfWidth, 0.01f);

        Vector3 look = center + rot * lookOffset;
        Vector3 pos = center + rot * new Vector3(lookOffset.x, cameraHeight, -distance);   // 캐릭터 정면(-Z) 쪽
        lobbyCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));

        ApplyFieldOfView(fieldOfView);   // 플레이 중 Inspector 조정 반영
    }

    private void ApplyFieldOfView(float fov)
    {
        if (Mathf.Approximately(lobbyCam.Lens.FieldOfView, fov)) return;

        LensSettings lens = lobbyCam.Lens;
        lens.FieldOfView = fov;
        lobbyCam.Lens = lens;
    }

    // 대기실 좌석들의 중심과 방향. 좌석이 없으면 내 위치 기준
    private void GetSeatRow(out Vector3 center, out Quaternion rot, out float halfSpan)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        Transform first = null;

        for (int i = 0; i < MaxSeats; i++)
        {
            Transform seat = SeatAnchors.Get(SeatArea.WaitingRoom, i);
            if (seat == null) continue;

            if (first == null) first = seat;
            sum += seat.position;
            count++;
        }

        if (count == 0)
        {
            center = transform.position;
            rot = transform.rotation;
            halfSpan = 0f;
            return;
        }

        center = sum / count;
        rot = first.rotation;

        halfSpan = 0f;
        Vector3 right = rot * Vector3.right;
        for (int i = 0; i < MaxSeats; i++)
        {
            Transform seat = SeatAnchors.Get(SeatArea.WaitingRoom, i);
            if (seat != null) halfSpan = Mathf.Max(halfSpan, Mathf.Abs(Vector3.Dot(seat.position - center, right)));
        }
    }
}
