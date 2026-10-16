using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HostMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button createBtn;       // 오른쪽 화살표 버튼
    [SerializeField] private Button[] backBtns;      // 상단 원형 Back + 하단 Back

    private const int TitleMaxLength = 30;   // DevLobbyPanel과 동일

    private bool isBusy;

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

        PlayerProfile me = PlayerProfile.Local;
        lobbyNameInput.text = (me != null && me.IsLoggedIn) ? $"{me.Nickname}'s Lobby" : string.Empty;
    }

    private void OnDisable()
    {
        createBtn.onClick.RemoveListener(OnClickCreate);
        lobbyNameInput.onSubmit.RemoveListener(OnSubmit);
        foreach (Button b in backBtns) b.onClick.RemoveListener(OnClickBack);
    }

    private void OnSubmit(string _) { OnClickCreate(); }
    private void OnClickBack() { panelManager.Back(); }

    private void OnClickCreate()
    {
        if (isBusy) return;

        string title = lobbyNameInput.text.Trim();
        if (title.Length == 0)
        {
            messageText.text = "Please enter a lobby name.";
            return;
        }

        isBusy = true;
        messageText.text = "Creating lobby...";
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