using UnityEngine;
using UnityEngine.UI;

public class PlayMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button matchmakingBtn;
    [SerializeField] private Button hostGameBtn;
    [SerializeField] private Button lobbyListBtn;
    [SerializeField] private Button backBtn;

    private void OnEnable()
    {
        if(matchmakingBtn != null)
        matchmakingBtn.onClick.AddListener(OnClickMatchmaking);
        hostGameBtn.onClick.AddListener(OnClickHostGame);
        lobbyListBtn.onClick.AddListener(OnClickLobbyList);
        backBtn.onClick.AddListener(OnClickBack);
    }

    private void OnDisable()
    {
        if (matchmakingBtn != null)
            matchmakingBtn.onClick.RemoveListener(OnClickMatchmaking);
        hostGameBtn.onClick.RemoveListener(OnClickHostGame);
        lobbyListBtn.onClick.RemoveListener(OnClickLobbyList);
        backBtn.onClick.RemoveListener(OnClickBack);
    }

    private void OnClickMatchmaking() { Debug.Log("매칭 시작"); }
    private void OnClickHostGame() { Debug.Log("호스트 게임"); }
    private void OnClickLobbyList() { Debug.Log("로비 목록"); }
    private void OnClickBack() { panelManager.Back(); }
}