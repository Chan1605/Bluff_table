using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace LiarsBar.Server
{
    // 테스트용 접속·로비 패널 (Mirror HUD 대체)
    public class DevLobbyPanel : MonoBehaviour
    {
        private string nickInput = "";
        private string passwordInput = "";
        private string message = "";

        // 에디터 · Development Build에서만 Host/Server 버튼 표시
        private static bool CanHost => Debug.isDebugBuild;

        private void OnGUI()
        {
            LiarsNetworkManager manager = LiarsNetworkManager.Instance;
            if (manager == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 260, 140), GUI.skin.box);
            DrawConnectionPanel(manager);
            GUILayout.EndArea();

            if (!NetworkClient.isConnected) return;

            GUILayout.BeginArea(new Rect(Screen.width - 270, 10, 260, 300), GUI.skin.box);
            DrawLobbyPanel();
            GUILayout.EndArea();
        }

        private void DrawConnectionPanel(LiarsNetworkManager manager)
        {
            // 서버 준비 중
            if (manager.IsPreparingServer)
            {
                GUILayout.Label("서버 준비 중 (DB 확인)...");
                return;
            }

            // 시작 전
            if (!NetworkServer.active && !NetworkClient.active)
            {
                if (CanHost && GUILayout.Button("Host (Server + Client)"))
                    manager.StartHostSafely();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Client", GUILayout.Width(80)))
                    manager.StartClient();
                manager.networkAddress = GUILayout.TextField(manager.networkAddress);
                GUILayout.EndHorizontal();

                if (CanHost && GUILayout.Button("Server Only"))
                    manager.StartServerSafely();
                return;
            }

            // 접속 중
            if (NetworkClient.active && !NetworkClient.isConnected)
            {
                GUILayout.Label($"{manager.networkAddress} 에 접속 중...");
                if (GUILayout.Button("취소"))
                    manager.StopClient();
                return;
            }

            // 실행 중
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                GUILayout.Label("Host 실행 중");
                if (GUILayout.Button("Stop Host")) manager.StopHost();
            }
            else if (NetworkServer.active)
            {
                GUILayout.Label("Server 실행 중");
                if (GUILayout.Button("Stop Server")) manager.StopServer();
            }
            else
            {
                GUILayout.Label($"Client 접속됨 ({manager.networkAddress})");
                if (GUILayout.Button("Stop Client")) manager.StopClient();
            }
        }

        private void DrawLobbyPanel()
        {
            PlayerProfile me = PlayerProfile.Local;
            if (me != null && !me.IsLoggedIn)
            {
                GUILayout.Label("닉네임");
                nickInput = GUILayout.TextField(nickInput, 20);
                GUILayout.Label("비밀번호");
                passwordInput = GUILayout.PasswordField(passwordInput, '*', 40);
                if (GUILayout.Button("로그인"))
                {
                    message = "로그인 중...";
                    Auth.Login(nickInput, passwordInput, (ok, msg) => message = msg);
                }
            }
            else if (me != null)
            {
                // 서버 확정값 표시
                bool want = GUILayout.Toggle(me.IsReady, " 준비");
                if (want != me.IsReady)
                    Lobby.SetReady(want);
            }

            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message);

            GUILayout.Space(8);
            foreach (PlayerProfile p in PlayerProfile.All)
            {
                string name = p.IsLoggedIn ? p.Nickname : "(로그인 전)";
                string stats = p.HasStats ? $" ({p.GamesPlayed}전 {p.Wins}승)" : "";
                string ready = p.IsReady ? " [준비]" : "";
                string mine = p == me ? "  ← 나" : "";
                GUILayout.Label($"좌석 {p.Seat}: {name}{stats}{ready}{mine}");
            }
        }
    }
}
