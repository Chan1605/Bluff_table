using System.Text;
using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class RoomLobbyUI : MonoBehaviour
{
    [SerializeField] private Button leaveBtn;
    [SerializeField] private Button readyBtn;        // 게스트
    [SerializeField] private GameObject readyMark;   // 준비 완료 
    [SerializeField] private Button startBtn;        // 호스트
    [SerializeField] private TMP_Text startLabel;    // START / WAITING 
    [SerializeField] private TMP_Text titleText;     // 방 제목
    [SerializeField] private TMP_Text roomIdText;    // 로비 코드
    [SerializeField] private TMP_Text membersText;   // 좌석별 목록 
    [SerializeField] private TMP_Text messageText;   // 결과 메시지 

    private bool isBusy;

    private void OnEnable()
    {
        if (leaveBtn != null) leaveBtn.onClick.AddListener(OnClickLeave);
        if (readyBtn != null) readyBtn.onClick.AddListener(OnClickReady);
        if (startBtn != null) startBtn.onClick.AddListener(OnClickStart);

        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        PlayerProfile.Removed += OnProfileEvent;

        isBusy = false;
        SetMessage(string.Empty);
        Refresh();
    }

    private void OnDisable()
    {
        if (leaveBtn != null) leaveBtn.onClick.RemoveListener(OnClickLeave);
        if (readyBtn != null) readyBtn.onClick.RemoveListener(OnClickReady);
        if (startBtn != null) startBtn.onClick.RemoveListener(OnClickStart);

        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;
        PlayerProfile.Removed -= OnProfileEvent;
    }

    private void OnProfileEvent(PlayerProfile _) { Refresh(); }


    private static void ClearSelection()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void OnClickLeave()
    {
        ClearSelection();

        PlayerProfile me = PlayerProfile.Local;
        if (me == null || !me.IsInRoom) return;   // 방 밖이면 로딩만 돌지 않게

        LoadingUI.Show("Returning to Lobby", false);
        Lobby.LeaveRoom();
    }

    private void OnClickReady()
    {
        ClearSelection();

        PlayerProfile me = PlayerProfile.Local;
        if (me == null || me.IsHost) return;

        Lobby.SetReady(!me.IsReady);   // 화면은 서버가 확정한 값으로 갱신됨
    }

    private void OnClickStart()
    {
        ClearSelection();
        if (isBusy || !Lobby.CanStartGame) return;

        isBusy = true;
        SetMessage("Starting...");
        Lobby.StartGame(OnStartResult);
    }

    private void OnStartResult(bool ok, string msg)
    {
        isBusy = false;
        SetMessage(msg);
        Debug.Log($"[RoomLobbyUI] Start result: {ok}, {msg}");


    }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    private void Refresh()
    {
        PlayerProfile me = PlayerProfile.Local;
        if (me == null || !me.IsInRoom) return;

        bool host = me.IsHost;
        if (readyBtn != null) readyBtn.gameObject.SetActive(!host);
        if (readyMark != null) readyMark.SetActive(!host && me.IsReady);

        if (startBtn != null)
        {
            bool canStart = Lobby.CanStartGame;
            startBtn.gameObject.SetActive(host);
            startBtn.interactable = canStart;
            if (startLabel != null) startLabel.text = canStart ? "START" : "WAITING";
        }

        if (titleText != null) titleText.text = Lobby.CurrentRoomTitle;
        if (roomIdText != null) roomIdText.text = $"#{me.RoomId}";  
        if (membersText != null) membersText.text = BuildMembers();
    }

    private static string BuildMembers()
    {
        StringBuilder sb = new();
        foreach (PlayerProfile p in Lobby.GetRoomMembers())
        {
            string name = p.IsLoggedIn ? p.Nickname : "(...)";
            string tag = p.IsHost ? " [HOST]" : (p.IsReady ? " [READY]" : "");
            string mine = p == PlayerProfile.Local ? "  <- me" : "";
            sb.Append(p.Seat).Append(". ").Append(name).Append(tag).Append(mine).Append('\n');
        }
        return sb.ToString();
    }
}