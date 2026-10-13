using System.Linq;
using UnityEngine;
using Mirror;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiarsBar.Server
{
    // 테스트용 접속·로비 패널 (Mirror HUD 대체)
    public class DevLobbyPanel : MonoBehaviour
    {
        private string nickInput = "";
        private string passwordInput = "";
        private string message = "";
        private string roomTitleInput = "";
        private Vector2 scroll;
        private bool imeEnabled;

        // 에디터 · Development Build에서만 Host/Server 버튼 표시
        private static bool CanHost => Debug.isDebugBuild;

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // 새 Input System은 IME(한글 조합)가 기본으로 꺼져 있음
            if (!imeEnabled && Keyboard.current != null)
            {
                Keyboard.current.SetIMEEnabled(true);
                imeEnabled = true;
            }
#endif
        }

        private void OnGUI()
        {
            LiarsNetworkManager manager = LiarsNetworkManager.Instance;
            if (manager == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 260, 140), GUI.skin.box);
            DrawConnectionPanel(manager);
            GUILayout.EndArea();

            if (!NetworkClient.isConnected) return;

            GUILayout.BeginArea(new Rect(Screen.width - 290, 10, 280, 420), GUI.skin.box);
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
            if (me == null) return;

            if (!me.IsLoggedIn) DrawLogin();
            else if (!me.IsInRoom) DrawRoomList();
            else DrawRoom(me);

            if (!string.IsNullOrEmpty(message))
                GUILayout.Label(message);
        }

        private void DrawLogin()
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

        private void DrawRoomList()
        {
            GUILayout.Label("방 제목");
            GUILayout.BeginHorizontal();
            roomTitleInput = GUILayout.TextField(roomTitleInput, 30);
            if (GUILayout.Button("방 만들기", GUILayout.Width(80)))
            {
                message = "방 만드는 중...";
                Lobby.CreateRoom(roomTitleInput, (ok, msg) => message = msg);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label($"방 목록 ({RoomList.Rooms.Count})");
            // 높이 고정 (안 하면 아래 메시지가 영역 밖으로 밀림)
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(200));
            foreach (RoomInfo room in RoomList.Rooms)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"#{room.Id} {room.Title} ({room.PlayerCount}/{room.MaxPlayers}) ★{room.HostNickname}");
                if (GUILayout.Button("입장", GUILayout.Width(50)))
                {
                    message = "입장 중...";
                    Lobby.JoinRoom(room.Id, (ok, msg) => message = msg);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        private void DrawRoom(PlayerProfile me)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"방 #{me.RoomId}  좌석 {me.Seat}{(me.IsHost ? "  ★방장" : "")}");
            if (GUILayout.Button("나가기", GUILayout.Width(60)))
            {
                message = "";
                Lobby.LeaveRoom();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(me.Phase switch
            {
                RoomPhase.Playing => "게임 중",
                RoomPhase.Result => "결과 화면",
                _ => "대기 중",
            });

            if (me.Phase == RoomPhase.Lobby)
            {
                if (me.IsHost)
                {
                    GUI.enabled = Lobby.CanStartGame;
                    if (GUILayout.Button("게임 시작"))
                    {
                        message = "시작 요청...";
                        Lobby.StartGame((ok, msg) => message = msg);
                    }
                    GUI.enabled = true;
                }
                else
                {
                    // 서버 확정값 표시
                    bool want = GUILayout.Toggle(me.IsReady, " 준비");
                    if (want != me.IsReady)
                        Lobby.SetReady(want);
                }
            }

            GUILayout.Space(8);
            // 같은 방 사람만 보임
            foreach (PlayerProfile p in PlayerProfile.All.Where(p => p.IsInRoom).OrderBy(p => p.Seat))
            {
                string name = p.IsLoggedIn ? p.Nickname : "(로그인 전)";
                string stats = p.HasStats ? $" ({p.GamesPlayed}전 {p.Wins}승)" : "";
                // 결과 화면: 이전 → 현재
                if (p.Phase == RoomPhase.Result && p.HasStats && p.PrevGamesPlayed >= 0)
                    stats = $" ({p.PrevGamesPlayed}전 {p.PrevWins}승 → {p.GamesPlayed}전 {p.Wins}승)";
                string host = p.IsHost ? " ★" : "";
                string ready = p.IsReady ? " [준비]" : "";
                string mine = p == me ? "  ← 나" : "";
                GUILayout.Label($"좌석 {p.Seat}: {name}{stats}{host}{ready}{mine}");
            }
        }
    }
}
