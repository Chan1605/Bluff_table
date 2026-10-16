using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using LiarsBar.Server;

// 대기실: 내 캐릭터를 왼쪽 클릭하면 다음 캐릭터로 변경 (대기실 씬에 하나만 둠)
public class CharacterClickSelector : MonoBehaviour
{
    [Tooltip("비우면 Camera.main 사용")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private LayerMask clickMask = ~0;

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // UI 위 클릭은 무시 (버튼 · 채팅창)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!Lobby.CanChangeCharacter) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, clickMask, QueryTriggerInteraction.Ignore)) return;

        // 모델이 자식으로 바뀌어도 루트의 PlayerProfile을 찾음. 남의 캐릭터는 무시
        PlayerProfile clicked = hit.collider.GetComponentInParent<PlayerProfile>();
        if (clicked == null || clicked != PlayerProfile.Local) return;

        Lobby.NextCharacter();
    }
}
