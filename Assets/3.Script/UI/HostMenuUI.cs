using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Dropdown lobbyTypeDropdown;   // 0 = Public, 1 = Private
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button createBtn;       // 오른쪽 화살표 버튼
    [SerializeField] private Button[] backBtns;      // 상단 원형 Back + 하단 Back

    private const int TitleMaxLength = 30;   // DevLobbyPanel과 동일

    private bool isBusy;

    // 서버에 공개/비공개 기능이 생기면 CreateRoom에 같이 넘길 값
    public bool IsPrivateLobby => lobbyTypeDropdown != null && lobbyTypeDropdown.value == 1;

    private void Awake()
    {
        lobbyNameInput.characterLimit = TitleMaxLength;
    }

    private void OnEnable()
    {
        createBtn.onClick.AddListener(OnClickCreate);
        lobbyNameInput.onSubmit.AddListener(OnSubmit);
        foreach (Button b in backBtns) b.onClick.AddListener(OnClickBack);

        isBusy = false;
        messageText.text = string.Empty;
        if (lobbyTypeDropdown != null)
        {
            lobbyTypeDropdown.SetValueWithoutNotify(0);
            LocalizeLobbyTypes();
        }
        Localization.LanguageChanged += LocalizeLobbyTypes;

        PlayerProfile me = PlayerProfile.Local;
        lobbyNameInput.text = (me != null && me.IsLoggedIn) ? $"{me.Nickname}'s Lobby" : string.Empty;
    }

    private void OnDisable()
    {
        createBtn.onClick.RemoveListener(OnClickCreate);
        lobbyNameInput.onSubmit.RemoveListener(OnSubmit);
        foreach (Button b in backBtns) b.onClick.RemoveListener(OnClickBack);
        Localization.LanguageChanged -= LocalizeLobbyTypes;
    }

    // 드롭다운 항목 글자만 번역 (값 0 = Public, 1 = Private는 그대로)
    private void LocalizeLobbyTypes()
    {
        if (lobbyTypeDropdown == null || lobbyTypeDropdown.options.Count < 2) return;

        lobbyTypeDropdown.options[0].text = Localization.Get("Public");
        lobbyTypeDropdown.options[1].text = Localization.Get("Private");
        lobbyTypeDropdown.RefreshShownValue();
    }

    private void OnSubmit(string _) { OnClickCreate(); }
    private void OnClickBack() { panelManager.Back(); }

    private void OnClickCreate()
    {
        if (isBusy) return;

        string title = lobbyNameInput.text.Trim();
        if (title.Length == 0)
        {
            messageText.text = Localization.Get("Please enter a lobby name.");
            return;
        }

        isBusy = true;
        messageText.text = Localization.Get("Creating lobby...");
        Debug.Log($"[HostMenuUI] Lobby type: {(IsPrivateLobby ? "Private" : "Public")} (서버 미지원 - 표시만)");
        LoadingUI.Show("Setting up your table");
        Lobby.CreateRoom(title, OnCreateResult);
    }

    private void OnCreateResult(bool ok, string msg)
    {
        isBusy = false;
        messageText.text = msg;   // 서버가 준 결과 메시지 그대로 표시
        Debug.Log($"[HostMenuUI] Create result: {ok}, {msg}");

        // 실패 시에만 로딩을 끈다. 성공하면 대기실 씬이 올라오며 타이틀 Canvas가 꺼질 때 함께 정리된다.
        if (!ok) LoadingUI.Hide();
    }
}