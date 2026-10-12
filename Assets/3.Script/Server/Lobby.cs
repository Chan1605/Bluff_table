using UnityEngine;

namespace LiarsBar.Server
{
    // UI용 로비 입구
    public static class Lobby
    {
        public static void SetReady(bool ready)
        {
            PlayerProfile me = PlayerProfile.Local;
            if (me == null)
            {
                Debug.LogWarning("[Lobby] 서버에 접속되어 있지 않습니다.");
                return;
            }
            me.RequestSetReady(ready);
        }
    }
}
