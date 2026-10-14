using System.Collections.Generic;
using LiarsBar.Server;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyListUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Transform content;           // ScrollView의 Content
    [SerializeField] private LobbyRowUI rowPrefab;
    [SerializeField] private TMP_Text emptyText;          // 방이 없을 때 안내
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button[] backBtns;           // 상단 원형 Back
    [SerializeField] private Button refreshBtn;
    [SerializeField] private Button quickJoinBtn;
    [SerializeField] private Button joinWithIdBtn;
    [SerializeField] private TMP_InputField idInput;
    [SerializeField] private float refreshInterval = 1f;  // 목록 자동 갱신 주기(초)

    private readonly List<LobbyRowUI> rows = new();
    private float timer;
    private bool isBusy;
    private bool warnedMissingList;

    private void OnEnable()
    {
        if (backBtns != null)
        {
            foreach (Button b in backBtns)
                if (b != null) b.onClick.AddListener(OnClickBack);
        }
        if (refreshBtn != null) refreshBtn.onClick.AddListener(OnClickRefresh);
        if (quickJoinBtn != null) quickJoinBtn.onClick.AddListener(OnClickQuickJoin);
        if (joinWithIdBtn != null) joinWithIdBtn.onClick.AddListener(OnClickJoinWithId);

        isBusy = false;
        SetMessage(string.Empty);
        if (idInput != null) idInput.text = string.Empty;

        timer = 0f;
        Rebuild();
    }

    private void OnDisable()
    {
        if (backBtns != null)
        {
            foreach (Button b in backBtns)
                if (b != null) b.onClick.RemoveListener(OnClickBack);
        }
        if (refreshBtn != null) refreshBtn.onClick.RemoveListener(OnClickRefresh);
        if (quickJoinBtn != null) quickJoinBtn.onClick.RemoveListener(OnClickQuickJoin);
        if (joinWithIdBtn != null) joinWithIdBtn.onClick.RemoveListener(OnClickJoinWithId);
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;

        timer = 0f;
        Rebuild();
    }

    private void OnClickBack()
    {
        if (panelManager != null) panelManager.Back();
    }

    private void OnClickRefresh() { Rebuild(); }

    private void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    // RoomList.Rooms를 줄(row)로 그림. 줄은 재사용하고 남는 줄만 끈다.
    private void Rebuild()
    {
        bool connected = NetworkClient.isConnected;
        bool canDraw = content != null && rowPrefab != null;
        int shown = 0;

        if (!canDraw && !warnedMissingList)
        {
            warnedMissingList = true;
            Debug.LogWarning("[LobbyListUI] content 또는 rowPrefab이 연결되지 않아 목록을 그릴 수 없습니다.", this);
        }

        if (connected && canDraw)
        {
            foreach (RoomInfo room in RoomList.Rooms)
            {
                if (shown >= rows.Count)
                    rows.Add(Instantiate(rowPrefab, content));

                LobbyRowUI row = rows[shown];
                if (row == null) continue;   // 외부에서 파괴된 줄은 건너뜀

                row.gameObject.SetActive(true);
                row.Bind(room, JoinRoom);
                shown++;
            }
        }

        for (int i = shown; i < rows.Count; i++)
        {
            if (rows[i] != null) rows[i].gameObject.SetActive(false);
        }

        if (emptyText != null)
        {
            string empty = connected ? "No lobbies found." : "Not connected to server.";
            emptyText.gameObject.SetActive(shown == 0);
            if (emptyText.text != empty) emptyText.text = empty;
        }
    }

    private void OnClickQuickJoin()
    {
        if (!NetworkClient.isConnected)
        {
            SetMessage("Not connected to server.");
            return;
        }

        foreach (RoomInfo room in RoomList.Rooms)
        {
            if (room.PlayerCount >= room.MaxPlayers) continue;

            JoinRoom(room);
            return;
        }

        SetMessage("No available lobby.");
    }

    private void OnClickJoinWithId()
    {
        if (idInput == null)
        {
            SetMessage("ID input is not available.");
            return;
        }

        string id = idInput.text.Trim().TrimStart('#');
        if (id.Length == 0)
        {
            SetMessage("Please enter a lobby ID.");
            return;
        }
        if (!NetworkClient.isConnected)
        {
            SetMessage("Not connected to server.");
            return;
        }

        // Id 타입을 몰라도 되도록 목록에서 문자열로 비교해 찾음
        foreach (RoomInfo room in RoomList.Rooms)
        {
            if (room.Id.ToString() != id) continue;

            JoinRoom(room);
            return;
        }

        SetMessage("Lobby not found.");
    }

    private void JoinRoom(RoomInfo room)
    {
        if (isBusy) return;

        isBusy = true;
        SetMessage("Joining lobby...");
        Lobby.JoinRoom(room.Id, OnJoinResult);
    }

    private void OnJoinResult(bool ok, string msg)
    {
        isBusy = false;
        SetMessage(msg);
        Debug.Log($"[LobbyListUI] Join result: {ok}, {msg}");

        // TODO: ok일 때 방 대기 화면으로 전환
    }
}