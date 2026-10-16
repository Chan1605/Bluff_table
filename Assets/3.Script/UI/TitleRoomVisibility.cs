using LiarsBar.Server;
using UnityEngine;

// 방에 들어가 있는 동안 타이틀 UI를 숨기고, 방을 나오면 다시 보여준 뒤 지정한 패널을 연다.
// 입장: 로딩이 끝난 뒤(최소 표시 시간 포함)에 숨긴다. 퇴장: 타이틀 UI가 복구되면 나가기 로딩을 끈다.
// 항상 활성 상태인 오브젝트(PanelManager)에 붙일 것. 숨길 대상에 붙이면 안 된다.
public class TitleRoomVisibility : MonoBehaviour
{
    [SerializeField] private GameObject[] hideWhileInRoom;   // Canvas, VP 등 (LoadingPrefab은 넣지 말 것)
    [SerializeField] private UIPanelManager panelManager;    // 방을 나온 뒤 패널 복귀용 (선택)
    [SerializeField] private PanelId returnPanel = PanelId.Play;

    private bool inRoom;

    private void OnEnable() { RoomScenes.SceneChanged += OnSceneChanged; }
    private void OnDisable() { RoomScenes.SceneChanged -= OnSceneChanged; }

    // loadedScene이 "" 이면 방 밖
    private void OnSceneChanged(string loadedScene)
    {
        bool wasInRoom = inRoom;
        inRoom = !string.IsNullOrEmpty(loadedScene);

        if (inRoom)
        {
            // 로딩이 끝나는 시점에 타이틀 UI를 숨긴다 (로딩이 없으면 즉시)
            LoadingUI.Hide(Apply);
            return;
        }

        Apply();

        // 접속·로그인 때도 "" 로 한 번 호출되므로, 방에 있다가 나온 경우에만 처리한다
        if (!wasInRoom) return;

        if (panelManager != null) panelManager.Open(returnPanel);
        LoadingUI.Hide();   // 나가기 로딩 종료 (최소 표시 시간 후)
    }

    // 현재 상태 기준으로 반영 (로딩이 끝나기 전에 방을 나갔다면 숨기지 않음)
    private void Apply()
    {
        if (hideWhileInRoom == null) return;

        foreach (GameObject go in hideWhileInRoom)
            if (go != null) go.SetActive(!inRoom);
    }
}