using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Server
{
    public enum SeatArea : byte
    {
        WaitingRoom = 0,
        Table = 1
    }

    // 씬에 두는 좌석 위치 묶음. 씬이 로드되면 등록, 언로드되면 해제
    public class SeatAnchors : MonoBehaviour
    {
        [Tooltip("대기실 = 방에서 대기 중일 때 / 테이블 = 게임 중 · 결과")]
        [SerializeField] private SeatArea area = SeatArea.Table;

        [Tooltip("좌석 0~3 위치 (좌석 번호 순서)")]
        [SerializeField] private Transform[] seats;

        private static readonly Dictionary<SeatArea, SeatAnchors> active = new();

        // 등록 · 해제될 때 (플레이어가 위치를 다시 맞춤)
        public static event Action Changed;

        public SeatArea Area => area;
        public int SeatCount => seats != null ? seats.Length : 0;
        public Transform GetSeat(int index) => seats != null && index >= 0 && index < seats.Length ? seats[index] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
            Changed = null;
        }

        private void OnEnable()
        {
            if (active.TryGetValue(area, out SeatAnchors other) && other != null && other != this)
                Debug.LogWarning($"[Seat] {area} 좌석 묶음이 두 개입니다 ({other.name}, {name}). 나중 것을 씁니다.");

            active[area] = this;
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (!active.TryGetValue(area, out SeatAnchors current) || current != this) return;

            active.Remove(area);
            Changed?.Invoke();
        }

        public static Transform Get(SeatArea area, int seat)
        {
            if (seat < 0 || !active.TryGetValue(area, out SeatAnchors anchors) || anchors == null) return null;

            Transform[] list = anchors.seats;
            return list != null && seat < list.Length ? list[seat] : null;
        }

        public static SeatArea AreaFor(RoomPhase phase)
        {
            return phase == RoomPhase.Lobby ? SeatArea.WaitingRoom : SeatArea.Table;
        }
    }
}
