using System.Collections.Generic;
using LiarsBar.Server;
using UnityEngine;


public class NamePlateSync : MonoBehaviour
{
    private const float Interval = 0.25f;
    private const int SeatCount = 4;
    private const float PlateAboveHead = 0.5f;   // 머리(HeadPivot) 위 높이 = R × 0.35 (얼굴에 가리면 올리기)
    private const float FollowSharpness = 12f;    // 클수록 머리를 바로 따라감

    private readonly Transform[] plates = new Transform[SeatCount];
    private readonly Transform[] heads = new Transform[SeatCount];
    private float tableRadius = 1f;

    private readonly List<int> seats = new List<int>();
    private string shownSeatsKey;
    private NamePlateBoard shownBoard;
    private float nextSync;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[NamePlateSync]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<NamePlateSync>();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSync) return;
        nextSync = Time.unscaledTime + Interval;

        NamePlateBoard board = NamePlateBoard.Instance;
        PlayerProfile me = PlayerProfile.Local;
        if (board == null || me == null || !me.IsInRoom) return;

        if (board != shownBoard)
        {
            shownBoard = board;
            shownSeatsKey = null;   // 테이블 씬이 새로 열리면 보이는 자리를 다시 정한다
        }

        AssignChairSeats();

        seats.Clear();
        System.Array.Clear(heads, 0, heads.Length);
        foreach (PlayerProfile p in PlayerProfile.All)
        {
            if (p == null || !p.IsInRoom || p.RoomId != me.RoomId || p.Seat < 0) continue;

            if (p != me) seats.Add(p.Seat);       // 내 이름판은 보이지 않게 (원작)
            board.SetName(p.Seat, p.Nickname);   // 같은 글자면 Text가 다시 그리지 않음

            if (p.TryGetComponent(out PlayerState state))
            {
                board.SetShots(p.Seat, state.currentChamber);
                board.SetDead(p.Seat, state.isDead);
                if (p.Seat < SeatCount && state.headController != null)
                    heads[p.Seat] = state.headController.headPivot != null ? state.headController.headPivot : state.headController.transform;
            }
        }

        // 나간 사람 자리는 숨긴다 (자리 구성이 바뀔 때만). 결과 화면에서는 전부 숨김
        bool result = me.Phase == RoomPhase.Result;
        seats.Sort();
        string key = result ? "result" : string.Join(",", seats);
        if (key == shownSeatsKey) return;
        shownSeatsKey = key;
        board.ShowSeats(result ? new int[0] : seats.ToArray());
    }

    // 이름판을 그 자리 캐릭터 머리 위에 둔다 (애니메이션 뒤 · 매 프레임, 부드럽게)
    private void LateUpdate()
    {
        float t = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);
        for (int i = 0; i < SeatCount; i++)
        {
            Transform plate = plates[i];
            Transform head = heads[i];
            if (plate == null || head == null || !plate.gameObject.activeInHierarchy) continue;

            Vector3 target = head.position + Vector3.up * (PlateAboveHead * tableRadius);
            plate.position = (plate.position - target).sqrMagnitude > 4f ? target : Vector3.Lerp(plate.position, target, t);
        }
    }

    // 테이블 의자 i번의 이름판 = 좌석 i (SetSeat이 보드에 등록하고 저장된 이름 · 탄 수를 바로 입힘)
    private void AssignChairSeats()
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        for (int i = 0; i < SeatCount; i++)
        {
            Transform chair = SeatAnchors.Get(SeatArea.Table, i);
            if (chair == null) { plates[i] = null; continue; }
            center += chair.position;
            count++;

            NamePlate plate = chair.GetComponentInChildren<NamePlate>(true);
            if (plate != null && plate.Seat != i) plate.SetSeat(i);
            plates[i] = plate != null ? plate.transform : null;
        }

        // 테이블 크기 R = 의자들 ~ 중앙 평균 수평 거리
        if (count == 0) return;
        center /= count;
        float sum = 0f;
        for (int i = 0; i < SeatCount; i++)
        {
            Transform chair = SeatAnchors.Get(SeatArea.Table, i);
            if (chair != null) sum += Vector3.ProjectOnPlane(chair.position - center, Vector3.up).magnitude;
        }
        if (sum > 0.01f) tableRadius = sum / count;
    }
}
