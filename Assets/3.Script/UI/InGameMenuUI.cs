using LiarsBar.Server;
using UnityEngine;
using UnityEngine.UI;

// 인게임 ESC 패널: CONTINUE / SETTINGS / LEAVE
// 열기: PlayerController가 ESC → InGameEvent.OnMenuOpened (입력은 UI 모드로 바뀜)
// 닫기: ESC 다시(UIInputController) 또는 CONTINUE → OnMenuClosed (입력은 게임 모드로 돌아감)
// SETTINGS는 타이틀과 같은 세팅 패널(SettingsMenuUI)을 그대로 쓴다. 목록에서 Back → ESC 메뉴로 복귀
public class InGameMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject root;         // 어두운 배경 + 메뉴 전체
    [SerializeField] private GameObject menuGroup;    // CONTINUE / SETTINGS / LEAVE
    [SerializeField] private Button continueBtn;
    [SerializeField] private Button settingsBtn;
    [SerializeField] private Button leaveBtn;
    [SerializeField] private SettingsMenuUI settings;

    private void Awake()
    {
        HideAll();
    }

    private void OnEnable()
    {
        InGameEvent.OnMenuOpened += Open;
        InGameEvent.OnMenuClosed += HideAll;
        InGameEvent.OnReEntryInLobby += HideAll;

        if (continueBtn != null) continueBtn.onClick.AddListener(OnClickContinue);
        if (settingsBtn != null) settingsBtn.onClick.AddListener(OnClickSettings);
        if (leaveBtn != null) leaveBtn.onClick.AddListener(OnClickLeave);
        if (settings != null) settings.BackRequested += OnSettingsBack;
    }

    private void OnDisable()
    {
        InGameEvent.OnMenuOpened -= Open;
        InGameEvent.OnMenuClosed -= HideAll;
        InGameEvent.OnReEntryInLobby -= HideAll;

        if (continueBtn != null) continueBtn.onClick.RemoveListener(OnClickContinue);
        if (settingsBtn != null) settingsBtn.onClick.RemoveListener(OnClickSettings);
        if (leaveBtn != null) leaveBtn.onClick.RemoveListener(OnClickLeave);
        if (settings != null) settings.BackRequested -= OnSettingsBack;
    }

    private void Open()
    {
        if (root != null) root.SetActive(true);
        if (menuGroup != null) menuGroup.SetActive(true);
        if (settings != null) settings.gameObject.SetActive(false);
    }

    private void HideAll()
    {
        if (settings != null) settings.gameObject.SetActive(false);
        if (root != null) root.SetActive(false);
    }

    private void OnClickContinue()
    {
        UIInputController input = UIInputController.Instance;
        if (input != null && input.IsInGameMenuOpen)
        {
            input.CloseInGameMenu();   // 입력 전환 + OnMenuClosed
            return;
        }

        // UIInputController가 씬에 없을 때를 대비해 직접 게임 입력으로 되돌림
        PlayerState me = PlayerState.Local;
        if (me != null && me.TryGetComponent(out PlayerController controller) && controller.inputActions != null)
        {
            controller.inputActions.UI.Disable();
            controller.inputActions.Player.Enable();
        }
        InGameEvent.TriggerOnMenuClosed();
    }

    private void OnClickSettings()
    {
        if (menuGroup != null) menuGroup.SetActive(false);
        if (settings != null) settings.gameObject.SetActive(true);
    }

    private void OnSettingsBack()
    {
        if (settings != null) settings.gameObject.SetActive(false);
        if (menuGroup != null) menuGroup.SetActive(true);
    }

    // 게임 중 나가기: 서버가 HandleDisconnect로 남은 사람끼리 진행시키고, 나는 타이틀(로비 목록)로
    private void OnClickLeave()
    {
        HideAll();
        LoadingUI.Show("Returning to Lobby", false);
        Lobby.LeaveRoom();
    }
}
