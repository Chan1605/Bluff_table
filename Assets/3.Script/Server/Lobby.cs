using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Server
{
    // UI용 로비 입구
    public static class Lobby
    {
        // 결과는 onResult(성공, 메시지). 방 상태는 PlayerProfile.Local.IsInRoom으로 판단
        public static void CreateRoom(string title, Action<bool, string> onResult)
        {
            PlayerProfile me = GetLocal(onResult);
            if (me != null) me.RequestCreateRoom(title, onResult);
        }

        public static void JoinRoom(int roomId, Action<bool, string> onResult)
        {
            PlayerProfile me = GetLocal(onResult);
            if (me != null) me.RequestJoinRoom(roomId, onResult);
        }

        public static void LeaveRoom()
        {
            PlayerProfile me = GetLocal(null);
            if (me != null) me.RequestLeaveRoom();
        }

        // 방장 전용
        public static void StartGame(Action<bool, string> onResult)
        {
            PlayerProfile me = GetLocal(onResult);
            if (me != null) me.RequestStartGame(onResult);
        }

        // 시작 버튼 활성 조건: 방장 + 대기 중 + 방장 제외 전원 준비 + 최소 인원 (최종 판단은 서버)
        public static bool CanStartGame
        {
            get
            {
                PlayerProfile me = PlayerProfile.Local;
                if (me == null || !me.IsHost || me.Phase != RoomPhase.Lobby) return false;

                int count = 0;
                foreach (PlayerProfile p in PlayerProfile.All)
                {
                    if (p.RoomId != me.RoomId) continue;
                    count++;
                    if (p != me && !p.IsReady) return false;
                }

                LiarsNetworkManager manager = LiarsNetworkManager.Instance;
                int minPlayers = manager != null ? manager.Config.MinPlayersToStart : 2;
                return count >= minPlayers;
            }
        }

        // 내 방 제목 (방 밖이면 "")
        public static string CurrentRoomTitle
        {
            get
            {
                PlayerProfile me = PlayerProfile.Local;
                return me != null ? me.RoomTitle : "";
            }
        }

        // 내 방 사람들 (좌석 순). 방 밖이면 빈 목록
        public static List<PlayerProfile> GetRoomMembers()
        {
            List<PlayerProfile> members = new();
            PlayerProfile me = PlayerProfile.Local;
            if (me == null || !me.IsInRoom) return members;

            foreach (PlayerProfile p in PlayerProfile.All)
                if (p.RoomId == me.RoomId && p.Seat >= 0) members.Add(p);
            members.Sort((a, b) => a.Seat.CompareTo(b.Seat));
            return members;
        }

        // 내 캐릭터 → 다음 번호 (대기실에서만, 번호 계산은 서버)
        public static void NextCharacter()
        {
            PlayerProfile me = GetLocal(null);
            if (me != null) me.RequestNextCharacter();
        }

        // 지금 캐릭터를 바꿀 수 있는지 (클릭 반응 · 커서 표시용, 최종 판단은 서버)
        public static bool CanChangeCharacter
        {
            get
            {
                PlayerProfile me = PlayerProfile.Local;
                return me != null && me.IsInRoom && me.Phase == RoomPhase.Lobby;
            }
        }

        // 방장은 준비 없음
        public static void SetReady(bool ready)
        {
            PlayerProfile me = GetLocal(null);
            if (me != null) me.RequestSetReady(ready);
        }

        private static PlayerProfile GetLocal(Action<bool, string> onResult)
        {
            PlayerProfile me = PlayerProfile.Local;
            if (me == null)
            {
                Debug.LogWarning("[Lobby] 서버에 접속되어 있지 않습니다.");
                onResult?.Invoke(false, "서버에 접속되어 있지 않습니다.");
            }
            return me;
        }
    }
}
