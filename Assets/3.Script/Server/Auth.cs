using UnityEngine;
using System;

namespace LiarsBar.Server
{
    public static class Auth
    {
        // 팀 계약: Auth.Login(nick, password, onResult)
        public static void Login(string nick, string password, Action<bool, string> onResult)
        {
            PlayerProfile me = PlayerProfile.Local;
            if (me == null)
            {
                onResult?.Invoke(false, "서버에 접속되어 있지 않습니다.");
                return;
            }
            me.RequestLogin(nick, password, onResult);
        }
    }
}
