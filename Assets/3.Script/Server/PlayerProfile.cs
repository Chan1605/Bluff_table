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

        public int Seat => seat;
        public string Nickname => nickname;
        public bool IsLoggedIn => !string.IsNullOrEmpty(nickname);
        public bool IsReady => isReady;

        public static PlayerProfile Local { get; private set; }

        public static readonly List<PlayerProfile> All = new();

        private Action<bool, string> pendingLoginCallback;

        // 서버 전용 (DB 없이 로그인 시 -1)
        private int userId = -1;
        public int UserId => userId;

        // 서버 전용: 중복 요청 방지
        private bool loginInProgress;

        public override void OnStartClient()
        {
            All.Add(this);
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
        }

        [Server]
        public void ServerSetSeat(int seatIndex)
        {
            seat = seatIndex;
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
        }

        [Server]
        private void CompleteLogin(string cleanedNick, int dbUserId, string message)
        {
            userId = dbUserId;
            nickname = cleanedNick;
            TargetLoginResult(true, message);
            Debug.Log($"[Server] 좌석 {seat} 로그인 - '{cleanedNick}' (user_id {dbUserId})");
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
            LiarsNetworkManager manager = LiarsNetworkManager.Instance;

            if (!IsLoggedIn) return;
            if (manager.GameInProgress) return;

            isReady = ready;
            manager.TryStartGame();
        }

        private void OnReadyChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"[Client] 좌석 {seat} 준비 {oldValue} → {newValue}");
        }
    }
}
