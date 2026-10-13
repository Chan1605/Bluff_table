using Mirror;

namespace LiarsBar.Server
{
    // 방 진행 단계 (대기 → 게임 → 결과 → 대기)
    public enum RoomPhase : byte
    {
        Lobby = 0,
        Playing = 1,
        Result = 2,
    }

    // 방 목록 한 줄 (클라 표시용)
    public struct RoomInfo
    {
        public int Id;
        public string Title;
        public int PlayerCount;
        public int MaxPlayers;
        public string HostNickname;
    }

    // 서버 → 방 밖(로비)에 있는 클라
    public struct RoomListMessage : NetworkMessage
    {
        public RoomInfo[] Rooms;
    }
}
