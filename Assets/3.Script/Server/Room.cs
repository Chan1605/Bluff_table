using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LiarsBar.Server
{
    // 서버 전용: 방 하나의 상태
    public class Room
    {
        public int Id { get; }
        public string Title { get; }

        // NetworkMatch용 (같은 값끼리만 서로 보임)
        public Guid MatchId { get; }

        public PlayerProfile Host { get; private set; }
        public RoomPhase Phase { get; set; } = RoomPhase.Lobby;

        // 방마다 하나 (프리팹에서 생성)
        public IGameFlow GameFlow { get; private set; }
        public GameObject GameFlowObject { get; private set; }
        public Action<int> GameOverHandler { get; private set; }

        // 결과 연출 대기 (서버 코루틴)
        public Coroutine ResultRoutine { get; set; }

        // 게임 시작 시점 기록 (중간 이탈자 포함)
        public DateTime StartedAt { get; private set; }
        public List<MatchParticipant> Participants { get; } = new();

        // 들어온 순서 (방장 이전 기준)
        private readonly List<PlayerProfile> members = new();
        private readonly Dictionary<int, PlayerProfile> seats = new();

        public IReadOnlyList<PlayerProfile> Members => members;
        public int Count => members.Count;

        public Room(int id, string title)
        {
            Id = id;
            Title = title;
            MatchId = Guid.NewGuid();
        }

        public void SetGameFlow(IGameFlow flow, GameObject flowObject, Action<int> onGameOver)
        {
            GameFlow = flow;
            GameFlowObject = flowObject;
            GameOverHandler = onGameOver;
        }

        public void ClearGameFlow()
        {
            GameFlow = null;
            GameFlowObject = null;
            GameOverHandler = null;
        }

        // 빈 좌석 배정, 실패 시 -1
        public int Add(PlayerProfile player, int maxSeats)
        {
            for (int i = 0; i < maxSeats; i++)
            {
                if (seats.ContainsKey(i)) continue;

                seats[i] = player;
                members.Add(player);
                if (Host == null) Host = player;
                return i;
            }
            return -1;
        }

        // 방장이 바뀌면 새 방장 반환
        public PlayerProfile Remove(PlayerProfile player, int seat)
        {
            if (!members.Remove(player)) return null;
            seats.Remove(seat);

            if (Host != player) return null;
            Host = members.Count > 0 ? members[0] : null;
            return Host;
        }

        public bool TryGetPlayer(int seat, out PlayerProfile player) => seats.TryGetValue(seat, out player);

        public int[] GetSortedSeats() => seats.Keys.OrderBy(s => s).ToArray();

        // 시작 시점 참가자 · 시각 기록
        public void SnapshotParticipants()
        {
            StartedAt = DateTime.Now;
            Participants.Clear();
            foreach (int seat in GetSortedSeats())
                Participants.Add(new MatchParticipant(seat, seats[seat].UserId));
        }

        public RoomInfo ToInfo(int maxSeats) => new RoomInfo
        {
            Id = Id,
            Title = Title,
            PlayerCount = members.Count,
            MaxPlayers = maxSeats,
            HostNickname = Host != null ? Host.Nickname : "",
        };
    }
}
