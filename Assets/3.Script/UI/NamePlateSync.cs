using System.Collections.Generic;
using LiarsBar.Server;
using UnityEngine;

// 테이블 머리 위 이름판(NamePlateBoard / NamePlate)을 실제 방 인원으로 채운다.
// - 이름: 로그인 닉네임 (PlayerProfile.Nickname)
// - 보이는 자리: 방에 앉은 좌석만
// - 탄 수 (n/6) · 사망: PlayerState.currentChamber / isDead
// 이름판은 의자 프리팹(Chair/NamePlate)에 들어 있고 자리 번호가 -1이라, 테이블 좌석 순서(SeatAnchors)대로 번호를 붙여 준다.
// 씬에 따로 놓지 않아도 게임 시작 시 스스로 생기고, 테이블 씬의 NamePlateBoard가 있을 때만 동작한다.
public class NamePlateSync : MonoBehaviour
{
    private const float Interval = 0.25f;

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
        foreach (PlayerProfile p in PlayerProfile.All)
        {
            if (p == null || !p.IsInRoom || p.RoomId != me.RoomId || p.Seat < 0) continue;

            seats.Add(p.Seat);
            board.SetName(p.Seat, p.Nickname);   // 같은 글자면 Text가 다시 그리지 않음

            if (p.TryGetComponent(out PlayerState state))
            {
                board.SetShots(p.Seat, state.currentChamber);
                board.SetDead(p.Seat, state.isDead);
            }
        }

        // 나간 사람 자리는 숨긴다 (자리 구성이 바뀔 때만)
        seats.Sort();
        string key = string.Join(",", seats);
        if (key == shownSeatsKey) return;
        shownSeatsKey = key;
        board.ShowSeats(seats.ToArray());
    }

    // 테이블 의자 i번의 이름판 = 좌석 i (SetSeat이 보드에 등록하고 저장된 이름 · 탄 수를 바로 입힘)
    private static void AssignChairSeats()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform chair = SeatAnchors.Get(SeatArea.Table, i);
            if (chair == null) continue;

            NamePlate plate = chair.GetComponentInChildren<NamePlate>(true);
            if (plate != null && plate.Seat != i) plate.SetSeat(i);
        }
    }
}
