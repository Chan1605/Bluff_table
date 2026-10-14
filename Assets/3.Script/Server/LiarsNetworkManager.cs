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
        [Tooltip("서버 설정 에셋 (주소 · 포트 · 인원 · 방 · 닉네임 규칙)")]
        [SerializeField] private ServerConfig config;

        [Tooltip("좌석 0~3 기본 위치. 씬에 SeatAnchors가 있으면 그쪽을 먼저 씀")]
        [SerializeField] private Transform[] seatSpawnPoints;

        [Tooltip("방마다 하나씩 만들 IGameFlow 프리팹. NetworkIdentity가 있으면 NetworkMatch도 필요 (같은 방에만 보임)")]
        [SerializeField] private GameObject gameFlowPrefab;

        // 서버 전용: 접속 중인 전원 / 방
        private readonly HashSet<PlayerProfile> players = new();
        private readonly Dictionary<int, Room> rooms = new();
        private int nextRoomId = 1;

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

            // 네트워크 GameFlow는 클라도 알아야 스폰됨
            if (gameFlowPrefab != null && gameFlowPrefab.TryGetComponent(out NetworkIdentity _) && !spawnPrefabs.Contains(gameFlowPrefab))
                spawnPrefabs.Add(gameFlowPrefab);
        }

        public override void OnValidate()
        {
            base.OnValidate();

            if (gameFlowPrefab == null) return;
            if (gameFlowPrefab.GetComponent<IGameFlow>() == null)
            {
                Debug.LogWarning($"[Server] {gameFlowPrefab.name}에 IGameFlow 구현 컴포넌트가 없습니다.");
                gameFlowPrefab = null;
            }
            else if (gameFlowPrefab.TryGetComponent(out NetworkIdentity _) && !gameFlowPrefab.TryGetComponent(out NetworkMatch _))
            {
                Debug.LogWarning($"[Server] {gameFlowPrefab.name}에 NetworkMatch가 없어 방 사람에게 보이지 않습니다. 추가하세요.");
            }
        }

        // 설정 에셋 → Mirror 설정
        private void ApplyConfig()
        {
            networkAddress = config.NetworkAddress;
            maxConnections = config.MaxPlayers; // Host 포함

            if (transport is PortTransport portTransport)
                portTransport.Port = config.Port;

            string security = transport.IsEncrypted ? $"암호화 {transport.EncryptionCipher}" : "암호화 없음";
            Debug.Log($"[Config] 주소 {config.NetworkAddress}:{config.Port}, 최대 접속 {config.MaxPlayers}, 방 {config.MaxRooms}개 x {config.MaxSeats}석, {security}");

            if (config.UseDatabase && !transport.IsEncrypted)
                Debug.LogWarning("[Config] Use Database가 켜져 있지만 통신이 암호화되지 않아 비밀번호가 평문으로 전송됩니다.");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            players.Clear();
            rooms.Clear();
            nextRoomId = 1;

            if (!(NetworkServer.aoi is MatchInterestManagement))
                Debug.LogWarning("[Server] Match Interest Management가 없어 다른 방 사람끼리도 서로 보입니다. NetworkManager에 추가하세요.");
            if (gameFlowPrefab == null)
                Debug.LogError("[Server] GameFlow 프리팹이 없어 게임을 시작할 수 없습니다. LiarsNetworkManager의 Game Flow Prefab을 연결하세요.");
            else
                Debug.Log($"[Server] GameFlow 프리팹 = {gameFlowPrefab.name}");
        }

        public override void OnStopServer()
        {
            foreach (Room room in rooms.Values)
                CleanupRoom(room);
            rooms.Clear();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RoomList.Register();
        }

        public override void OnStopClient()
        {
            RoomList.Clear();
            base.OnStopClient();
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

        // 접속하면 방 밖(로비)에 스폰 → 방에 들어가야 좌석이 생김
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            GameObject player = Instantiate(playerPrefab);
            player.name = $"Player [conn {conn.connectionId}]";

            if (!player.TryGetComponent(out NetworkMatch _))
                Debug.LogError("[Server] Player 프리팹에 NetworkMatch가 없습니다. 방끼리 분리되지 않습니다.");

            PlayerProfile profile = player.GetComponent<PlayerProfile>();
            players.Add(profile);

            NetworkServer.AddPlayerForConnection(conn, player);

            Debug.Log($"[Server] conn {conn.connectionId} 접속 ({players.Count}/{config.MaxPlayers})");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null && conn.identity.TryGetComponent(out PlayerProfile profile))
            {
                // 먼저 빼야 나가는 사람에게 방 목록을 보내지 않음
                players.Remove(profile);
                LeaveRoom(profile, "접속 종료");
                Debug.Log($"[Server] conn {conn.connectionId} 접속 종료 ({players.Count}/{config.MaxPlayers})");
            }
            base.OnServerDisconnect(conn);
        }

        public Transform GetSeatSpawnPoint(int seat)
        {
            if (seatSpawnPoints == null || seat < 0 || seat >= seatSpawnPoints.Length) return null;
            return seatSpawnPoints[seat];
        }

        public bool IsNicknameTaken(string nick)
        {
            foreach (PlayerProfile p in players)
                if (p.Nickname == nick) return true;
            return false;
        }

        // ───── 방 (서버 전용) ─────
        // 반환: 실패 사유 (성공이면 null)

        public string CreateRoom(PlayerProfile player, string rawTitle)
        {
            if (!player.IsLoggedIn) return "로그인 후 방을 만들 수 있습니다.";
            if (player.IsInRoom) return "이미 방에 들어가 있습니다.";

            string title = (rawTitle ?? string.Empty).Trim();
            if (title.Length < 1 || title.Length > config.RoomTitleMaxLength)
                return $"방 제목은 1~{config.RoomTitleMaxLength}자로 입력하세요.";
            if (rooms.Count >= config.MaxRooms)
                return "방을 더 만들 수 없습니다. 잠시 후 다시 시도해 주세요.";

            Room room = new Room(nextRoomId++, title);
            rooms.Add(room.Id, room);
            CreateGameFlow(room);
            Debug.Log($"[Room] #{room.Id} '{title}' 생성 - 방장 '{player.Nickname}' (방 {rooms.Count}/{config.MaxRooms})");

            EnterRoom(room, player);
            BroadcastRoomList();
            return null;
        }

        public string JoinRoom(PlayerProfile player, int roomId)
        {
            if (!player.IsLoggedIn) return "로그인 후 입장할 수 있습니다.";
            if (player.IsInRoom) return "이미 방에 들어가 있습니다.";
            if (!rooms.TryGetValue(roomId, out Room room)) return "없어진 방입니다.";
            if (room.Phase != RoomPhase.Lobby) return "이미 게임이 시작된 방입니다.";
            if (room.Count >= config.MaxSeats) return "방이 가득 찼습니다.";

            EnterRoom(room, player);
            BroadcastRoomList();
            return null;
        }

        public void LeaveRoom(PlayerProfile player, string reason)
        {
            if (!player.IsInRoom || !rooms.TryGetValue(player.RoomId, out Room room)) return;

            int seat = player.Seat;
            PlayerProfile newHost = room.Remove(player, seat);
            player.ServerLeaveRoom();
            SetMatch(player, Guid.Empty);
            Debug.Log($"[Room] #{room.Id} 좌석 {seat} '{player.Nickname}' 나감 ({reason}) - {room.Count}/{config.MaxSeats}");

            // 게임 중 이탈: 처리 규칙은 게임 로직 (결과 연출 중이면 좌석만 비움)
            if (room.Phase == RoomPhase.Playing)
            {
                room.GameFlow?.HandleDisconnect(seat);

                // 방이 비면 서버가 판을 정리 (GameFlow가 종료를 못 보낸 경우 대비)
                if (room.Phase == RoomPhase.Playing && room.Count == 0)
                    EndGame(room, -1, "모두 나감", false);
            }

            if (room.Count == 0)
            {
                rooms.Remove(room.Id);
                CleanupRoom(room);
                Debug.Log($"[Room] #{room.Id} 삭제 (빈 방)");
            }
            else if (newHost != null)
            {
                newHost.ServerSetHost(true);
                Debug.Log($"[Room] #{room.Id} 방장 → '{newHost.Nickname}' (좌석 {newHost.Seat})");
            }

            BroadcastRoomList();
        }

        private void EnterRoom(Room room, PlayerProfile player)
        {
            int seat = room.Add(player, config.MaxSeats);
            player.ServerEnterRoom(room.Id, room.Title, seat, room.Host == player);
            SetMatch(player, room.MatchId);

            Transform spawn = GetSeatSpawnPoint(seat);
            if (spawn != null) player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            Debug.Log($"[Room] #{room.Id} 좌석 {seat} '{player.Nickname}' 입장{(room.Host == player ? " (방장)" : "")} - {room.Count}/{config.MaxSeats}");
        }

        // 같은 matchId끼리만 서로 보임 (Guid.Empty = 아무도 못 봄, 자기 자신은 보임)
        private static void SetMatch(PlayerProfile player, Guid matchId)
        {
            if (!player.TryGetComponent(out NetworkMatch match)) return;
            match.matchId = matchId;

            // 나간 사람을 보던 목록은 Mirror가 다시 계산하지 않으므로 직접 비움
            if (matchId == Guid.Empty)
                NetworkServer.RebuildObservers(player.netIdentity, false);
        }

        // ───── 방별 게임 (서버 전용) ─────

        // GameFlow가 좌석 → 플레이어를 찾을 때 (자기 방 안에서만)
        public bool TryGetPlayer(IGameFlow flow, int seat, out PlayerProfile player)
        {
            foreach (Room room in rooms.Values)
                if (room.GameFlow == flow) return room.TryGetPlayer(seat, out player);

            player = null;
            return false;
        }

        // 방장 시작 버튼
        public string StartGame(PlayerProfile host)
        {
            if (!host.IsInRoom || !rooms.TryGetValue(host.RoomId, out Room room)) return "방에 들어가 있지 않습니다.";
            if (!host.IsHost) return "방장만 시작할 수 있습니다.";
            if (room.Phase != RoomPhase.Lobby) return "이미 게임 중입니다.";
            if (room.GameFlow == null) return "게임을 시작할 수 없습니다. (서버 설정 오류)";
            if (room.Count < config.MinPlayersToStart) return $"{config.MinPlayersToStart}명 이상이어야 시작할 수 있습니다.";

            foreach (PlayerProfile p in room.Members)
                if (p != host && !p.IsReady) return "준비하지 않은 사람이 있습니다.";

            room.SnapshotParticipants();
            foreach (PlayerProfile p in room.Members)
                p.ServerSnapshotStats();

            SetPhase(room, RoomPhase.Playing);
            BroadcastRoomList(); // 게임 중인 방은 목록에서 숨김

            int[] playingSeats = room.GetSortedSeats();
            Debug.Log($"[Room] #{room.Id} 방장 '{host.Nickname}' 시작 -> StartGame({string.Join(", ", playingSeats)})");
            room.GameFlow.StartGame(playingSeats);
            return null;
        }

        private void CreateGameFlow(Room room)
        {
            if (gameFlowPrefab == null) return;

            GameObject flowObject = Instantiate(gameFlowPrefab);
            flowObject.name = $"GameFlow [room {room.Id}]";

            IGameFlow flow = flowObject.GetComponent<IGameFlow>();
            if (flow == null)
            {
                Debug.LogError($"[Room] #{room.Id} {gameFlowPrefab.name}에 IGameFlow가 없습니다.");
                Destroy(flowObject);
                return;
            }

            // 네트워크 오브젝트면 이 방 사람에게만 스폰
            if (flowObject.TryGetComponent(out NetworkIdentity _))
            {
                if (flowObject.TryGetComponent(out NetworkMatch match)) match.matchId = room.MatchId;
                NetworkServer.Spawn(flowObject);
            }

            Action<int> onGameOver = winnerSeat => EndGame(room, winnerSeat, "GameFlow 종료", true);
            flow.GameOver += onGameOver;
            room.SetGameFlow(flow, flowObject, onGameOver);
        }

        // 방 삭제 · 서버 종료 시
        private void CleanupRoom(Room room)
        {
            if (room.ResultRoutine != null)
            {
                StopCoroutine(room.ResultRoutine);
                room.ResultRoutine = null;
            }

            if (room.GameFlow != null) room.GameFlow.GameOver -= room.GameOverHandler;

            GameObject flowObject = room.GameFlowObject;
            if (flowObject != null)
            {
                if (NetworkServer.active && flowObject.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0)
                    NetworkServer.Destroy(flowObject);
                else
                    Destroy(flowObject);
            }
            room.ClearGameFlow();
        }

        private static void SetPhase(Room room, RoomPhase phase)
        {
            room.Phase = phase;
            foreach (PlayerProfile p in room.Members)
                p.ServerSetPhase(phase);
        }

        // normalEnd: 저장 + 결과 연출 대기 / false: 기록 없이 바로 대기실
        private void EndGame(Room room, int winnerSeat, string reason, bool normalEnd)
        {
            // 늦게 도착한 종료 신호 · 삭제된 방 무시
            if (room.Phase != RoomPhase.Playing || !rooms.ContainsKey(room.Id)) return;

            SetPhase(room, RoomPhase.Result);
            float wait = normalEnd ? config.ResultSeconds : 0f;
            Debug.Log($"[Room] #{room.Id} 게임 종료 - 승자 좌석 {winnerSeat} ({reason}), {wait}초 뒤 대기실 복귀");

            // 저장은 기다리지 않음 (결과 연출 중 전적 갱신)
            if (normalEnd && config.UseDatabase && Db.IsAvailable)
                _ = SaveMatchAsync(room.Id, room.StartedAt, new List<MatchParticipant>(room.Participants), winnerSeat);

            if (wait > 0f && room.Count > 0) room.ResultRoutine = StartCoroutine(ReturnToLobbyAfter(room, wait));
            else ReturnToLobby(room);
        }

        private IEnumerator ReturnToLobbyAfter(Room room, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            room.ResultRoutine = null;
            ReturnToLobby(room);
        }

        private void ReturnToLobby(Room room)
        {
            if (room.Phase != RoomPhase.Result) return;

            SetPhase(room, RoomPhase.Lobby);
            foreach (PlayerProfile p in room.Members)
                p.ServerResetReady();

            Debug.Log($"[Room] #{room.Id} 대기실 복귀 ({room.Count}명)");
            BroadcastRoomList(); // 다시 목록에 보임
        }

        private async Task SaveMatchAsync(int roomId, DateTime startedAt, List<MatchParticipant> participants, int winnerSeat)
        {
            DateTime endedAt = DateTime.Now;

            // DB 계정만 기록
            List<MatchParticipant> registered = participants.Where(p => p.UserId > 0).ToList();
            if (registered.Count == 0)
            {
                Debug.Log($"[DB] #{roomId} 기록할 DB 계정 참가자가 없어 전적 저장을 건너뜁니다.");
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
                    Debug.Log($"[DB] #{roomId} 전적 저장 완료 - match_id {matchId}, 참가 {registered.Count}명, 승자 user_id {winnerUserId}");
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

            foreach (PlayerProfile p in players)
            {
                if (participants.Any(m => m.UserId == p.UserId))
                    _ = p.ServerLoadStatsAsync();
            }
        }

        // ───── 방 목록 ─────

        public void SendRoomList(PlayerProfile player)
        {
            if (player.connectionToClient != null)
                player.connectionToClient.Send(BuildRoomList());
        }

        // 방 밖(로비)에 있는 로그인한 사람에게만
        private void BroadcastRoomList()
        {
            RoomListMessage message = BuildRoomList();
            foreach (PlayerProfile p in players)
                if (p.IsLoggedIn && !p.IsInRoom && p.connectionToClient != null)
                    p.connectionToClient.Send(message);
        }

        private RoomListMessage BuildRoomList()
        {
            return new RoomListMessage
            {
                // 대기 중인 방만
                Rooms = rooms.Values.Where(r => r.Phase == RoomPhase.Lobby).OrderBy(r => r.Id).Select(r => r.ToInfo(config.MaxSeats)).ToArray(),
            };
        }
    }
}
