using UnityEngine;
using UnityEngine.UI;

public class PlayMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button hostGameBtn;
    [SerializeField] private Button lobbyListBtn;
    [SerializeField] private Button backBtn;

    private void OnEnable()
    {
        hostGameBtn.onClick.AddListener(OnClickHostGame);
        lobbyListBtn.onClick.AddListener(OnClickLobbyList);
        backBtn.onClick.AddListener(OnClickBack);
    }

    private void OnDisable()
    {
        hostGameBtn.onClick.RemoveListener(OnClickHostGame);
        lobbyListBtn.onClick.RemoveListener(OnClickLobbyList);
        backBtn.onClick.RemoveListener(OnClickBack);
    }

    private void OnClickHostGame() { panelManager.Open(PanelId.Host); }
    private void OnClickLobbyList() { panelManager.Open(PanelId.LobbyList); }
    private void OnClickBack() { panelManager.Back(); }
}