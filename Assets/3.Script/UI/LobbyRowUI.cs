using System;
using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private Button joinBtn;

    private RoomInfo room;
    private Action<RoomInfo> onJoin;

    private void OnEnable() { joinBtn.onClick.AddListener(OnClickJoin); }
    private void OnDisable() { joinBtn.onClick.RemoveListener(OnClickJoin); }

    public void Bind(RoomInfo info, Action<RoomInfo> joinCallback)
    {
        room = info;
        onJoin = joinCallback;

        titleText.text = info.Title;
        countText.text = $"{info.PlayerCount}/{info.MaxPlayers}";
        joinBtn.interactable = info.PlayerCount < info.MaxPlayers;   // 가득 찬 방은 JOIN 비활성
    }

    private void OnClickJoin() { onJoin?.Invoke(room); }
}