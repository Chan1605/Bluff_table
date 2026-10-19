using System.Collections.Generic;
using UnityEngine;
using LiarsBar.Server;

// 테이블 씬: 같은 방에 앉은 사람이 없는 좌석(의자)은 숨긴다. SeatAnchors(Table)와 같은 오브젝트에 붙인다
[RequireComponent(typeof(SeatAnchors))]
public class EmptySeatHider : MonoBehaviour
{
    private SeatAnchors anchors;
    private readonly HashSet<int> occupied = new();

    private void Awake() => anchors = GetComponent<SeatAnchors>();

    private void OnEnable()
    {
        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        PlayerProfile.Removed += OnProfileEvent;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;
        PlayerProfile.Removed -= OnProfileEvent;
    }

    private void OnProfileEvent(PlayerProfile _) => Refresh();

    private void Refresh()
    {
        PlayerProfile me = PlayerProfile.Local;
        if (me == null || !me.IsInRoom) return;   // 내 정보가 아직 없으면 건드리지 않음 (전부 보이는 상태 유지)

        occupied.Clear();
        foreach (PlayerProfile p in PlayerProfile.All)
            if (p != null && p.RoomId == me.RoomId && p.Seat >= 0)
                occupied.Add(p.Seat);

        for (int i = 0; i < anchors.SeatCount; i++)
        {
            Transform seat = anchors.GetSeat(i);
            if (seat != null) seat.gameObject.SetActive(occupied.Contains(i));
        }
    }
}
