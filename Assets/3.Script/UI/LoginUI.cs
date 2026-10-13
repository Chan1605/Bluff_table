using System.Collections;
using LiarsBar.Server;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginUI : MonoBehaviour
{
    [SerializeField] GameObject LoginPanel;
    [SerializeField] Button LoginBtn;
    [SerializeField] private TMP_InputField nickInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text messageText;

    private const int NickMaxLength = 20;        // DevLobbyPanel과 동일
    private const int PasswordMaxLength = 40;
    private const float ConnectTimeout = 10f;    // 접속 대기 한도(초)

    private bool isBusy;

    [SerializeField] private bool devHostFallback = true;   // 에디터/개발 빌드 전용: 서버가 없으면 Host로 대체

    private const float StartGrace = 1f;   // 접속/Host 시작 직후 상태가 잡히길 기다리는 시간(초)

    // 템플릿의 CanHost와 같은 조건 (에디터 · Development Build)
    private bool CanDevHost => devHostFallback && Debug.isDebugBuild;

    private void Awake()
    {
        nickInput.characterLimit = NickMaxLength;
        passwordInput.characterLimit = PasswordMaxLength;
        passwordInput.contentType = TMP_InputField.ContentType.Password;
    }

    private void Start()
    {
        // 이미 로그인된 상태(타이틀 씬 복귀 등)면 로그인 패널을 보여주지 않음
        PlayerProfile me = PlayerProfile.Local;
        if (me != null && me.IsLoggedIn)
            LoginPanel.SetActive(false);
    }

    private void OnEnable()
    {
        LoginBtn.onClick.AddListener(OnClickLogin);
        nickInput.onSubmit.AddListener(OnSubmitNick);
        passwordInput.onSubmit.AddListener(OnSubmitPassword);

        SetBusy(false);
        messageText.text = string.Empty;
        passwordInput.text = string.Empty;
    }

    private void OnDisable()
    {
        LoginBtn.onClick.RemoveListener(OnClickLogin);
        nickInput.onSubmit.RemoveListener(OnSubmitNick);
        passwordInput.onSubmit.RemoveListener(OnSubmitPassword);
    }

    private void OnSubmitNick(string _) { passwordInput.ActivateInputField(); }
    private void OnSubmitPassword(string _) { OnClickLogin(); }

    private void OnClickLogin()
    {
        if (isBusy) return;

        string nick = nickInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrWhiteSpace(nick))
        {
            messageText.text = "Please enter your nickname.";
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            messageText.text = "Please enter your password.";
            return;
        }

        SetBusy(true);
        StartCoroutine(LoginRoutine(nick, password));
    }

    private IEnumerator LoginRoutine(string nick, string password)
    {
        LiarsNetworkManager manager = LiarsNetworkManager.Instance;
        if (manager == null)
        {
            Fail("Network manager not found.");
            yield break;
        }
        if (manager.IsPreparingServer)
        {
            Fail("Server is preparing. Please try again shortly.");
            yield break;
        }

        messageText.text = "Connecting to server...";

        // 아직 접속 전이면 접속 시작 (Host로 이미 접속돼 있으면 건너뜀)
        bool startedByUs = false;
        bool triedDevHost = false;
        if (!NetworkClient.active)
        {
            manager.StartClient();
            startedByUs = true;
        }

        // 접속 + 로컬 플레이어(PlayerProfile) 준비까지 대기
        float elapsed = 0f;
        while (!(NetworkClient.isConnected && PlayerProfile.Local != null))
        {
            // Host 준비(DB 확인) 중에는 클라이언트가 꺼져 있어도 실패로 보지 않음
            bool stopped = !NetworkClient.active && !manager.IsPreparingServer && elapsed >= StartGrace;
            bool timedOut = elapsed >= ConnectTimeout;

            if (stopped || timedOut)
            {
                // 에디터/개발 빌드: 접속할 서버가 없으면 Host로 대체 (한 번만)
                if (CanDevHost && startedByUs && !triedDevHost)
                {
                    triedDevHost = true;
                    if (NetworkClient.active) manager.StopClient();

                    messageText.text = "No server found. Starting dev host...";
                    manager.StartHostSafely();
                    elapsed = 0f;
                    continue;
                }

                string reason;
                string uiMessage;

                if (!NetworkClient.active)
                {
                    reason = triedDevHost
                        ? "dev host failed (DB check / port)"
                        : "client stopped (connection refused / transport error)";
                    uiMessage = triedDevHost
                        ? "Dev host failed. Check DB settings."
                        : "Connection failed. Check the server.";
                }
                else if (NetworkClient.isConnected)
                {
                    reason = "connected, but PlayerProfile.Local is null";
                    uiMessage = "Connected, but player is not ready.";
                }
                else
                {
                    reason = "connect timeout";
                    uiMessage = "Connection timed out.";
                }

                // 정리 전에 상태를 기록해야 실제 실패 시점의 값이 남음
                Debug.LogWarning($"[LoginUI] {reason} | address={manager.networkAddress} " +
                                 $"active={NetworkClient.active} connected={NetworkClient.isConnected} " +
                                 $"ready={NetworkClient.ready} hasLocal={PlayerProfile.Local != null}");

                if (startedByUs && NetworkClient.active)
                {
                    if (NetworkServer.active) manager.StopHost();
                    else manager.StopClient();   // 다음 시도를 위해 정리
                }

                Fail(uiMessage);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        messageText.text = "Logging in...";
        Auth.Login(nick, password, OnLoginResult);
    }

    private void OnLoginResult(bool ok, string msg)
    {
        SetBusy(false);
        messageText.text = msg;   // 실패 사유는 서버가 준 메시지 그대로 표시

        if (!ok) return;

        passwordInput.text = string.Empty;
        LoginPanel.SetActive(false);
    }

    private void Fail(string msg)
    {
        messageText.text = msg;
        SetBusy(false);
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        LoginBtn.interactable = !busy;
        nickInput.interactable = !busy;
        passwordInput.interactable = !busy;
    }
}