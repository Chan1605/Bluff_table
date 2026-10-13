using System.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Mirror;

namespace LiarsBar.Server
{
    public class PlayerProfile : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnSeatChanged))]
        private int seat = -1;

        [SyncVar(hook = nameof(OnNicknameChanged))]
        private string nickname = "";

        [SyncVar(hook = nameof(OnReadyChanged))]
        private bool isReady;

        // 0 = 방 밖 (로비)
        [SyncVar(hook = nameof(OnRoomChanged))]
        private int roomId;

        [SyncVar(hook = nameof(OnHostChanged))]
        private bool isHost;

        [SyncVar(hook = nameof(OnPhaseChanged))]
        private RoomPhase phase;

        // 게임 시작 직전 전적 (결과 화면 변화 표시용, -1 = 없음)
        [SyncVar]
        private int prevWins;

        [SyncVar]
        private int prevGamesPlayed = -1;

        [SyncVar]
        private int wins;

        // -1 = 전적 없음 (DB 미사용 · 로딩 전)
        [SyncVar(hook = nameof(OnStatsChanged))]
        private int gamesPlayed = -1;

        public int Seat => seat;
        public string Nickname => nickname;
        public bool IsLoggedIn => !string.IsNullOrEmpty(nickname);
        public bool IsReady => isReady;
        public int RoomId => roomId;
        public bool IsInRoom => roomId > 0;
        public bool IsHost => isHost;
        public RoomPhase Phase => phase;
        public int PrevGamesPlayed => prevGamesPlayed;
        public int PrevWins => prevWins;

        // 내 방 단계가 바뀔 때 (UI 화면 전환용)
        public static event Action<RoomPhase> LocalPhaseChanged;
        public int GamesPlayed => gamesPlayed;
        public int Wins => wins;
        public bool HasStats => gamesPlayed >= 0;

        public static PlayerProfile Local { get; private set; }

        public static readonly List<PlayerProfile> All = new();

        private Action<bool, string> pendingLoginCallback;
        private Action<bool, string> pendingRoomCallback;

        // 서버 전용 (DB 없이 로그인 시 -1)
        private int userId = -1;
        public int UserId => userId;

        // 서버 전용: 중복 요청 방지
        private bool loginInProgress;

        public override void OnStartClient()
        {
            All.Add(this);
            ApplySeatPosition();
            Debug.Log($"[Client] 플레이어 등장 - netId {netId},좌석 {seat}, 닉네임 '{nickname}'");
        }
        public override void OnStartLocalPlayer()
        {
            Local = this;
            Debug.Log($"[Client] 내 좌석은 {seat}번");
        }
        public override void OnStopClient()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }
        private void OnSeatChanged(int oldSeat,int newSeat)
        {
            Debug.Log($"[Client] 좌석 변경 {oldSeat} -> {newSeat}");
            ApplySeatPosition();
        }

        // 좌석 위치로 이동 (NetworkTransform 없이 클라에서 맞춤)
        private void ApplySeatPosition()
        {
            LiarsNetworkManager manager = LiarsNetworkManager.Instance;
            if (seat < 0 || manager == null) return;

            Transform spawn = manager.GetSeatSpawnPoint(seat);
            if (spawn != null) transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }

        private void OnRoomChanged(int oldRoom, int newRoom)
        {
            Debug.Log($"[Client] '{nickname}' 방 {oldRoom} → {newRoom}");
        }

        private void OnHostChanged(bool oldValue, bool newValue)
        {
            if (newValue) Debug.Log($"[Client] 방 {roomId} 방장 = '{nickname}'");
        }

        private void OnPhaseChanged(RoomPhase oldPhase, RoomPhase newPhase)
        {
            if (!isLocalPlayer) return;
            Debug.Log($"[Client] 방 {roomId} {oldPhase} → {newPhase}");
            LocalPhaseChanged?.Invoke(newPhase);
        }

        [Server]
        public void ServerSetPhase(RoomPhase newPhase)
        {
            phase = newPhase;
        }

        [Server]
        public void ServerSnapshotStats()
        {
            prevWins = wins;
            prevGamesPlayed = gamesPlayed;
        }

        [Server]
        public void ServerEnterRoom(int newRoomId, int seatIndex, bool host)
        {
            phase = RoomPhase.Lobby;
            prevGamesPlayed = -1;
            isReady = false;
            isHost = host;
            seat = seatIndex;
            roomId = newRoomId;
        }

        [Server]
        public void ServerLeaveRoom()
        {
            phase = RoomPhase.Lobby;
            prevGamesPlayed = -1;
            isReady = false;
            isHost = false;
            seat = -1;
            roomId = 0;
        }

        [Server]
        public void ServerSetHost(bool host)
        {
            isHost = host;
            if (host) isReady = false;
        }

        [Server]
        public void ServerResetReady()
        {
            isReady = false;
        }

        [Client]
        public void RequestLogin(string nick, string password, Action<bool, string> onResult)
        {
            pendingLoginCallback = onResult;
            CmdLogin(nick, password);
        }

        [Command]
        private void CmdLogin(string nick, string password)
        {
            if (loginInProgress)
            {
                TargetLoginResult(false, "로그인 처리 중입니다.");
                return;
            }

            string error = ValidateNickname(nick, out string cleaned);
            if (error == null) error = ValidatePassword(password);
            if (error != null)
            {
                TargetLoginResult(false, error);
                return;
            }

            _ = LoginAsync(cleaned, password);
        }

        // 서버 전용 (async라 [Server] 미사용)
        private async Task LoginAsync(string cleanedNick, string password)
        {
            // 개발 모드: 닉네임만으로 통과
            if (!LiarsNetworkManager.Instance.Config.UseDatabase)
            {
                CompleteLogin(cleanedNick, -1, "로그인 성공 (DB 없이)");
                return;
            }

            // DB 사용 중인데 접속 불가 → 거부
            if (!Db.IsAvailable)
            {
                TargetLoginResult(false, "서버 점검 중입니다. 잠시 후 다시 시도해 주세요.");
                return;
            }

            loginInProgress = true;
            LoginOutcome outcome = await UserRepository.LoginOrRegisterAsync(cleanedNick, password);
            loginInProgress = false;

            // await 중 나갔으면 중단
            if (this == null || connectionToClient == null) return;

            if (!outcome.Success)
            {
                TargetLoginResult(false, outcome.Message);
                return;
            }

            // await 중 같은 닉네임이 먼저 로그인했는지
            if (LiarsNetworkManager.Instance.IsNicknameTaken(cleanedNick))
            {
                TargetLoginResult(false, "이미 접속 중인 닉네임입니다.");
                return;
            }

            CompleteLogin(cleanedNick, outcome.UserId, outcome.Message);
            _ = ServerLoadStatsAsync();
        }

        [Server]
        private void CompleteLogin(string cleanedNick, int dbUserId, string message)
        {
            userId = dbUserId;
            nickname = cleanedNick;
            TargetLoginResult(true, message);
            Debug.Log($"[Server] 로그인 - '{cleanedNick}' (user_id {dbUserId})");

            // 로비 화면용 방 목록
            LiarsNetworkManager.Instance.SendRoomList(this);
        }

        // 서버 전용 (async라 [Server] 미사용)
        public async Task ServerLoadStatsAsync()
        {
            if (userId <= 0 || !Db.IsAvailable) return;

            UserStats? stats = await UserRepository.GetStatsAsync(userId);

            // await 중 나갔으면 중단
            if (this == null || stats == null) return;

            wins = stats.Value.Wins;
            gamesPlayed = stats.Value.GamesPlayed;
            Debug.Log($"[Server] '{nickname}' 전적 로드 - {gamesPlayed}전 {wins}승 (user_id {userId})");
        }

        private void OnStatsChanged(int oldValue, int newValue)
        {
            Debug.Log($"[Client] 좌석 {seat} 전적 {newValue}전 {wins}승");
        }

        [Server]
        private string ValidatePassword(string password)
        {
            ServerConfig config = LiarsNetworkManager.Instance.Config;
            if (!config.UseDatabase) return null;

            int length = password?.Length ?? 0;
            if (length < config.PasswordMinLength || length > config.PasswordMaxLength)
                return $"비밀번호는 {config.PasswordMinLength}~{config.PasswordMaxLength}자로 입력하세요.";
            return null;
        }
        [Server]
        private string ValidateNickname(string raw, out string cleaned)
        {
            cleaned = (raw ?? string.Empty).Trim();

            if (IsLoggedIn)
                return "이미 로그인되어 있습니다.";
            ServerConfig config = LiarsNetworkManager.Instance.Config;
            if (cleaned.Length < config.NicknameMinLength || cleaned.Length > config.NicknameMaxLength)
                return $"닉네임은 {config.NicknameMinLength}~{config.NicknameMaxLength}자로 입력하세요.";
            if (LiarsNetworkManager.Instance.IsNicknameTaken(cleaned))
                return "이미 접속 중인 닉네임입니다.";

            return null;
        }
        [TargetRpc]
        private void TargetLoginResult(bool ok, string message)
        {
            Action<bool, string> callback = pendingLoginCallback;
            pendingLoginCallback = null;
            callback?.Invoke(ok, message);
        }
        private void OnNicknameChanged(string oldName, string newName)
        {
            Debug.Log($"[Client] 좌석 {seat} 닉네임 '{oldName}' → '{newName}'");
        }

        [Client]
        public void RequestSetReady(bool ready)
        {
            CmdSetReady(ready);
        }

        [Command]
        private void CmdSetReady(bool ready)
        {
            // 대기 중인 방 안의 방장 아닌 사람만
            if (!IsLoggedIn || !IsInRoom || isHost || phase != RoomPhase.Lobby) return;

            isReady = ready;
        }

        [Client]
        public void RequestCreateRoom(string title, Action<bool, string> onResult)
        {
            pendingRoomCallback = onResult;
            CmdCreateRoom(title);
        }

        [Client]
        public void RequestJoinRoom(int targetRoomId, Action<bool, string> onResult)
        {
            pendingRoomCallback = onResult;
            CmdJoinRoom(targetRoomId);
        }

        [Client]
        public void RequestLeaveRoom()
        {
            CmdLeaveRoom();
        }

        [Client]
        public void RequestStartGame(Action<bool, string> onResult)
        {
            pendingRoomCallback = onResult;
            CmdStartGame();
        }

        [Command]
        private void CmdStartGame()
        {
            string error = LiarsNetworkManager.Instance.StartGame(this);
            if (error != null) Debug.Log($"[Room] '{nickname}' 시작 실패 - {error}");
            TargetRoomResult(error == null, error ?? "게임을 시작합니다.");
        }

        [Command]
        private void CmdCreateRoom(string title)
        {
            string error = LiarsNetworkManager.Instance.CreateRoom(this, title);
            if (error != null) Debug.Log($"[Room] '{nickname}' 방 만들기 실패 - {error}");
            TargetRoomResult(error == null, error ?? "방을 만들었습니다.");
        }

        [Command]
        private void CmdJoinRoom(int targetRoomId)
        {
            string error = LiarsNetworkManager.Instance.JoinRoom(this, targetRoomId);
            if (error != null) Debug.Log($"[Room] '{nickname}' 방 #{targetRoomId} 입장 실패 - {error}");
            TargetRoomResult(error == null, error ?? "방에 들어왔습니다.");
        }

        [Command]
        private void CmdLeaveRoom()
        {
            LiarsNetworkManager.Instance.LeaveRoom(this, "나가기");
        }

        [TargetRpc]
        private void TargetRoomResult(bool ok, string message)
        {
            Action<bool, string> callback = pendingRoomCallback;
            pendingRoomCallback = null;
            callback?.Invoke(ok, message);
        }

        private void OnReadyChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"[Client] 좌석 {seat} 준비 {oldValue} → {newValue}");
        }
    }
}
