using System;

namespace LiarsBar.Server
{
    // 서버 ↔ 플레이어 담당(GameManager) 약속 (서버 전용)
    public interface IGameFlow
    {
        void StartGame(int[] seats);

        void HandleDisconnect(int seat);

        // 승자 좌석 (-1 = 없음)
        event Action<int> GameOver;
    }
}
