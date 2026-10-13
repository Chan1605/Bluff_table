using System;
using System.Collections.Generic;
using Mirror;

namespace LiarsBar.Server
{
    // UI용 방 목록 (클라 전용, 서버가 보내 줌)
    public static class RoomList
    {
        private static RoomInfo[] rooms = Array.Empty<RoomInfo>();

        public static IReadOnlyList<RoomInfo> Rooms => rooms;

        // 목록이 바뀔 때마다
        public static event Action OnChanged;

        internal static void Register()
        {
            NetworkClient.ReplaceHandler<RoomListMessage>(OnRoomList);
        }

        internal static void Clear()
        {
            rooms = Array.Empty<RoomInfo>();
            OnChanged?.Invoke();
        }

        private static void OnRoomList(RoomListMessage message)
        {
            rooms = message.Rooms ?? Array.Empty<RoomInfo>();
            OnChanged?.Invoke();
        }
    }
}
