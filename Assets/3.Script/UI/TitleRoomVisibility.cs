using LiarsBar.Server;
using UnityEngine;


public class TitleRoomVisibility : MonoBehaviour
{
    [SerializeField] private GameObject[] hideWhileInRoom;   // Canvas, VP 등
    [SerializeField] private UIPanelManager panelManager;    // 방을 나온 뒤 패널 복귀용 (선택)
    [SerializeField] private PanelId returnPanel = PanelId.Play;

    private bool wasInRoom;

    private void OnEnable() { RoomScenes.SceneChanged += OnSceneChanged; }
    private void OnDisable() { RoomScenes.SceneChanged -= OnSceneChanged; }

    // loadedScene이 "" 이면 방 밖
    private void OnSceneChanged(string loadedScene)
    {
        bool inRoom = !string.IsNullOrEmpty(loadedScene);

        if (hideWhileInRoom != null)
        {
            foreach (GameObject go in hideWhileInRoom)
                if (go != null) go.SetActive(!inRoom);
        }

        // 접속·로그인 때도 "" 로 한 번 호출되므로, 방에 있다가 나온 경우에만 패널을 연다
        if (!inRoom && wasInRoom && panelManager != null)
            panelManager.Open(returnPanel);

        wasInRoom = inRoom;
    }
}