using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;

namespace LiarsBar.Server
{
    public class LiarsNetworkManager : NetworkManager
    {
        public static LiarsNetworkManager Instance => singleton as LiarsNetworkManager;

        [Header("Liar's Bar")]
        [Tooltip("서버 설정 에셋 (주소 · 포트 · 인원 · 닉네임 규칙)")]
        [SerializeField] private ServerConfig config;

        [Tooltip("좌석 0~3 위치. 비어 있으면 원점에 스폰")]
        [SerializeField] private Transform[] seatSpawnPoints;

        [Tooltip("IGameFlow 구현 컴포넌트. 비우면 같은 오브젝트에서 자동으로 찾음")]
        [SerializeField] private MonoBehaviour gameFlowBehaviour;

        private readonly Dictionary<int, PlayerProfile> seats = new();

        private IGameFlow gameFlow;
        public bool GameInProgress { get; private set; }

        // 게임 시작 시점의 참가자 기록 (중간 이탈자 포함)
        private DateTime matchStartedAt;
        private readonly List<MatchParticipant> matchParticipants = new();
        public ServerConfig Config => config;

        public override void Awake()
        {
            base.Awake();

            // 중복 매니저로 파괴되는 경우 제외
            if (singleton != this) return;

            if (config == null)
            {
                Debug.LogWarning("[Server] ServerConfig가 연결되지 않아 기본값으로 실행합니다.");
                config = ScriptableObject.CreateInstance<ServerConfig>();
            }
            ApplyConfig();
        }

        // 설정 에셋 → Mirror 설정
        private void ApplyConfig()
        {
            networkAddress = config.NetworkAddress;
            maxConnections = config.MaxSeats; // Host 포함

            if (transport is PortTransport portTransport)
                portTransport.Port = config.Port;

            string security = transport.IsEncrypted ? $"암호화 {transport.EncryptionCipher}" : "암호화 없음";
            Debug.Log($"[Config] 주소 {config.NetworkAddress}:{config.Port}, 좌석 {config.MaxSeats}, 시작 인원 {config.MinPlayersToStart}, {security}");

            if (config.UseDatabase && !transport.IsEncrypted)
                Debug.LogWarning("[Config] Use Database가 켜져 있지만 통신이 암호화되지 않아 비밀번호가 평문으로 전송됩니다.");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            seats.Clear();
            GameInProgress = false;

            gameFlow = gameFlowBehaviour as IGameFlow ?? GetComponent<IGameFlow>();
            if (gameFlow == null)
            {
                Debug.LogError("[Server] IGameFlow 구현 컴포넌트가 없습니다. DummyGameFlow를 붙이세요.");
                return;
            }
            gameFlow.GameOver += OnGameOver;
        }
        // 서버 시작 입구: DB 확인 후 Listen

        // UI 로딩 표시용
        public bool IsPreparingServer { get; private set; }

        // 전용 서버 빌드(UNITY_SERVER) 또는 그래픽 없이 실행된 빌드
        public static bool IsDedicatedServer => Utils.IsHeadless() && !Application.isEditor;

        public void StartHostSafely() => _ = StartAfterDatabaseAsync(true);
        public void StartServerSafely() => _ = StartAfterDatabaseAsync(false);

        public override void Start()
        {
            base.Start();

            // 전용 서버 자동 시작 (Headless Start Mode는 Do Nothing)
            if (IsDedicatedServer)
                StartServerSafely();
        }

        private async Task StartAfterDatabaseAsync(bool asHost)
        {
            if (IsPreparingServer || NetworkServer.active) return;
            IsPreparingServer = true;
            try
            {
                if (config.UseDatabase)
                {
                    bool ready = await PrepareDatabaseAsync();

                    // await 중 파괴됐으면 중단
                    if (this == null) return;

                    if (!ready)
                    {
                        Debug.LogError("[Server] DB에 접속할 수 없어 서버를 열지 않습니다. (DB 없이 테스트하려면 ServerConfig의 Use Database를 끄세요)");
                        if (IsDedicatedServer)
                            Application.Quit(1);
                        return;
                    }
                }

                if (asHost) StartHost();
                else StartServer();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Server] 서버 시작 중 오류: {e.Message}");
                if (IsDedicatedServer)
                    Application.Quit(1);
            }
            finally
            {
                IsPreparingServer = false;
            }
        }

        private async Task<bool> PrepareDatabaseAsync()
        {
            if (!Db.Initialize())
                return false;

            bool ok = await Db.PingAsync();
            if (ok) Debug.Log("[DB] 준비 완료");
            return ok;
        }

        public override void OnStopServer()
        {
            if (gameFlow != null) gameFlow.GameOver -= OnGameOver;
            base.OnStopServer();
        }
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            if (GameInProgress)
            {
                Debug.Log($"[Server] 게임 진행 중 -> conn {conn.connectionId} 입장 거부");
                conn.Disconnect();
                return;
            }
            base.OnServerConnect(conn);
        }
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            int seat = FindFreeSeat();
            if (seat < 0)
            {
                Debug.Log($"[Server] 빈 좌석 없음 -> conn {conn.connectionId} 연결 종료");
                conn.Disconnect();
                return;
            }

            Transform spawn = GetSpawnPoint(seat);
            Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

            GameObject player = Instantiate(playerPrefab, pos, rot);
            player.name = $"Player [seat {seat}]";

            PlayerProfile profile = player.GetComponent<PlayerProfile>();
            profile.ServerSetSeat(seat);
            seats[seat] = profile;

            NetworkServer.AddPlayerForConnection(conn, player);

            Debug.Log($"[Server] conn {conn.connectionId} → 좌석 {seat} ({seats.Count}/{config.MaxSeats})");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null && conn.identity.TryGetComponent(out PlayerProfile profile))
            {
                int seat = profile.Seat;
                seats.Remove(seat);
                Debug.Log($"[Server] 좌석 {seat} 비움 ({seats.Count}/{config.MaxSeats})");

                if (GameInProgress)
                {
                    // 처리 규칙은 플레이어 담당
                    gameFlow.HandleDisconnect(seat);
                }
                else
                {
                    // 남은 인원으로 시작 조건 재확인
                    TryStartGame();
                }
            }
            base.OnServerDisconnect(conn);
        }
        private int FindFreeSeat()
        {
            for(int i = 0; i < config.MaxSeats; i++)
            {
                if (!seats.ContainsKey(i)) return i;
            }
            return -1;
        }
        private Transform GetSpawnPoint(int seat)
        {
            if (seatSpawnPoints == null || seat >= seatSpawnPoints.Length) return null;
            return seatSpawnPoints[seat];
        }
        public bool IsNicknameTaken(string nick)
        {
            foreach (PlayerProfile p in seats.Values)
                if (p.Nickname == nick) return true;
            return false;
        }

        // 서버 전용: 좌석 → 플레이어 (GameManager용)
        public bool TryGetPlayer(int seat, out PlayerProfile profile) => seats.TryGetValue(seat, out profile);

        // 서버 전용
        public void TryStartGame()
        {
            if (GameInProgress || gameFlow == null) return;
            if (seats.Count < config.MinPlayersToStart) return;

            foreach (PlayerProfile p in seats.Values)
                if (!p.IsLoggedIn || !p.IsReady) return;

            int[] playingSeats = seats.Keys.OrderBy(s => s).ToArray();

            matchStartedAt = DateTime.Now;
            matchParticipants.Clear();
            foreach (int seat in playingSeats)
                matchParticipants.Add(new MatchParticipant(seat, seats[seat].UserId));

            GameInProgress = true;
            Debug.Log($"[Server] 전원 준비 -> StartGame({string.Join(", ", playingSeats)})");
            gameFlow.StartGame(playingSeats);
        }

        // winnerSeat -1 = 승자 없음
        private void OnGameOver(int winnerSeat)
        {
            // 늦게 도착한 종료 신호 무시
            if (!GameInProgress) return;

            GameInProgress = false;
            foreach (PlayerProfile p in seats.Values)
                p.ServerResetReady();

            Debug.Log($"[Server] 게임 종료 - 승자 좌석 {winnerSeat}, 로비 복귀");

            // 저장은 기다리지 않음
            if (config.UseDatabase && Db.IsAvailable)
                _ = SaveMatchAsync(winnerSeat);
        }

        private async Task SaveMatchAsync(int winnerSeat)
        {
            DateTime startedAt = matchStartedAt;
            DateTime endedAt = DateTime.Now;

            // DB 계정만 기록
            List<MatchParticipant> registered = matchParticipants.Where(p => p.UserId > 0).ToList();
            if (registered.Count == 0)
            {
                Debug.Log("[DB] 기록할 DB 계정 참가자가 없어 전적 저장을 건너뜁니다.");
                return;
            }

            int winnerUserId = -1;
            foreach (MatchParticipant p in registered)
                if (p.Seat == winnerSeat) winnerUserId = p.UserId;

            try
            {
                int matchId = await MatchRepository.SaveMatchAsync(startedAt, endedAt, registered, winnerUserId);
                if (matchId > 0)
                {
                    Debug.Log($"[DB] 전적 저장 완료 - match_id {matchId}, 참가 {registered.Count}명, 승자 user_id {winnerUserId}");
                    RefreshStats(registered);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[DB] 전적 저장 중 오류: {e.Message}");
            }
        }

        // 저장 후 아직 접속 중인 참가자만 갱신 (좌석이 아니라 user_id로 찾음)
        private void RefreshStats(List<MatchParticipant> participants)
        {
            if (this == null || !NetworkServer.active) return;

            foreach (PlayerProfile p in seats.Values)
            {
                if (participants.Any(m => m.UserId == p.UserId))
                    _ = p.ServerLoadStatsAsync();
            }
        }
    }
}
