using System;
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
