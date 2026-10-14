using LiarsBar.Server;
using UnityEngine;


public class TitleRoomVisibility : MonoBehaviour
{
    [SerializeField] private GameObject[] hideWhileInRoom;   // Canvas, VP 등

    private void OnEnable() { RoomScenes.SceneChanged += OnSceneChanged; }
    private void OnDisable() { RoomScenes.SceneChanged -= OnSceneChanged; }

    // loadedScene이 "" 이면 방 밖
    private void OnSceneChanged(string loadedScene)
    {
        bool inRoom = !string.IsNullOrEmpty(loadedScene);

        if (hideWhileInRoom == null) return;
        foreach (GameObject go in hideWhileInRoom)
            if (go != null) go.SetActive(!inRoom);
    }
}