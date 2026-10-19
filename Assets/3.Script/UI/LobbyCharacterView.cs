using System.Collections.Generic;
using LiarsBar.Server;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerProfile))]
public class LobbyCharacterView : MonoBehaviour, IPointerClickHandler
{
    [System.Serializable]
    private class Entry
    {
        public string displayName;      
        public GameObject standModel;   
        public float yOffset;
        public float labelOffsetY;
    }

    private class ModelInstance
    {
        public GameObject go;
        public Transform[] parts;
        public int[] originalLayers;
    }


    [SerializeField] private Entry[] characters;
    [SerializeField] private Transform modelRoot;      

    [SerializeField] private Collider clickArea;
    [SerializeField] private string outlineLayerName = "Outline";

    [Header("Ground")]
    [SerializeField] private bool autoGround = true;
    [SerializeField] private float groundOffset;

    [SerializeField] private bool debugLog;        

    private const float MaxAutoLift = 3f;   // 자동 보정이 이 값을 넘으면 비정상으로 보고 건너뜀

    private PlayerProfile profile;
    private ModelInstance[] instances;
    private int current = -1;
    private int outlineLayer = -1;
    private bool hovered;
    private bool warnedCount;
    private bool interactive;       

    private Camera eventCamera;
    private float nextCameraSearch;
    private PointerEventData pointerData;
    private EventSystem pointerSystem;
    private readonly List<RaycastResult> raycastResults = new();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private string lastWhy;

    public event System.Action<bool> HighlightChanged;
    public bool IsHighlighted { get; private set; }
    public string GetDisplayName(int index)
    {
        bool valid = characters != null && index >= 0 && index < characters.Length;
        return valid ? characters[index].displayName : string.Empty;
    }

    public float GetLabelOffset(int index)
    {
        bool valid = characters != null && index >= 0 && index < characters.Length;
        return valid ? characters[index].labelOffsetY : 0f;
    }

    // 대기실 카메라 (호버 판정과 같은 카메라, 라벨이 카메라를 바라보게 할 때 사용)
    public Camera LobbyCamera => FindEventCamera();

    private void Awake()
    {
        profile = GetComponent<PlayerProfile>();
        if (modelRoot == null) modelRoot = transform;

        instances = new ModelInstance[characters != null ? characters.Length : 0];

        outlineLayer = LayerMask.NameToLayer(outlineLayerName);
        if (outlineLayer < 0)
            Debug.LogWarning($"[LobbyCharacterView] '{outlineLayerName}' 레이어가 없어 아웃라인이 표시되지 않습니다.", this);

        if (clickArea != null) clickArea.enabled = false;
    }

    private void OnEnable()
    {
        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;

        hovered = false;
        ApplyHighlight();
    }

    private void OnProfileEvent(PlayerProfile changed)
    {
        if (changed == profile) Refresh();
    }

    private void Update()
    {
        if (!interactive) return;

        bool over = IsPointerOverMe(out string why);
        if (debugLog && why != lastWhy)
        {
            lastWhy = why;
            Debug.Log($"[LobbyCharacterView] 호버 {over}: {why}", this);
        }

        if (over == hovered) return;

        hovered = over;
        ApplyHighlight();
    }

    private void Refresh()
    {
        bool show = characters != null && characters.Length > 0
                    && profile.IsLoggedIn && profile.IsInRoom && profile.Seat >= 0
                    && profile.Phase == RoomPhase.Lobby;

        interactive = show && profile.isLocalPlayer;
        if (clickArea != null) clickArea.enabled = interactive;
        if (!interactive) hovered = false;   // 콜라이더가 꺼지면 호버도 해제

        ShowModel(show ? ClampIndex(profile.CharacterId) : -1);
    }

    private int ClampIndex(int id)
    {
        if (id >= 0 && id < characters.Length) return id;

        if (!warnedCount)
        {
            warnedCount = true;
            Debug.LogWarning($"[LobbyCharacterView] 캐릭터 번호 {id}가 목록({characters.Length}개) 밖입니다. 서버 CharacterCount와 개수를 맞추세요.", this);
        }
        return 0;
    }

    private void ShowModel(int index)
    {
        if (index == current)
        {
            ApplyHighlight();
            return;
        }

        if (current >= 0 && instances[current] != null)
        {
            SetLayers(instances[current], false);
            instances[current].go.SetActive(false);
        }

        current = index;
        if (index < 0) return;

        if (instances[index] == null) instances[index] = CreateInstance(index);
        if (instances[index] != null) instances[index].go.SetActive(true);

        ApplyHighlight();
    }

    private ModelInstance CreateInstance(int index)
    {
        GameObject prefab = characters[index].standModel;
        if (prefab == null)
        {
            Debug.LogWarning($"[LobbyCharacterView] {index}번({characters[index].displayName}) StandModel이 비어 있습니다.", this);
            return null;
        }

        GameObject go = Instantiate(prefab, modelRoot, false);
        Transform[] parts = go.GetComponentsInChildren<Transform>(true);
        int[] layers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++) layers[i] = parts[i].gameObject.layer;

        float lift = characters[index].yOffset + groundOffset;

        // 가장 낮은 본(발)을 modelRoot 높이에 맞춘다
        if (autoGround)
        {
            if (TryGetLowestPoint(go, out float lowestY, out string lowestName))
            {
                float autoLift = modelRoot.position.y - lowestY;
                if (Mathf.Abs(autoLift) > MaxAutoLift)
                {
                    Debug.LogWarning($"[LobbyCharacterView] {index}번 보정값 {autoLift:0.00}m가 비정상이라 건너뜁니다 (가장 낮은 본 '{lowestName}').", this);
                }
                else
                {
                    lift += autoLift;
                    if (debugLog)
                        Debug.Log($"[LobbyCharacterView] {index} {characters[index].displayName}: 가장 낮은 본 '{lowestName}' y={lowestY:0.000}, 총 보정 {lift:0.000}m", this);
                }
            }
            else
            {
                Debug.LogWarning($"[LobbyCharacterView] {index}번 모델에서 본/메쉬를 찾지 못해 자동 높이 보정을 건너뜁니다.", this);
            }
        }

        go.transform.position += Vector3.up * lift;

        return new ModelInstance { go = go, parts = parts, originalLayers = layers };
    }


    private static bool TryGetLowestPoint(GameObject go, out float lowestY, out string lowestName)
    {
        lowestY = float.MaxValue;
        lowestName = string.Empty;

        foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            foreach (Transform bone in smr.bones)
            {
                if (bone == null || bone.position.y >= lowestY) continue;

                lowestY = bone.position.y;
                lowestName = bone.name;
            }
        }
        if (lowestName.Length > 0) return true;

        foreach (MeshRenderer mr in go.GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.bounds.min.y >= lowestY) continue;

            lowestY = mr.bounds.min.y;
            lowestName = mr.name;
        }
        return lowestName.Length > 0;
    }

    private void ApplyHighlight()
    {
        bool hasModel = current >= 0 && instances[current] != null;
        bool on = hasModel && hovered && profile.isLocalPlayer && Lobby.CanChangeCharacter;
        if (debugLog)
            Debug.Log($"[LobbyCharacterView] 아웃라인 {on} (hovered {hovered}, local {profile.isLocalPlayer}, canChange {Lobby.CanChangeCharacter}, layer {outlineLayer})", this);

        if (hasModel) SetLayers(instances[current], on);
        SetHighlighted(on);
    }

    private void SetHighlighted(bool on)
    {
        if (IsHighlighted == on) return;

        IsHighlighted = on;
        HighlightChanged?.Invoke(on);
    }
    private void SetLayers(ModelInstance model, bool outlined)
    {
        if (outlined && outlineLayer < 0) return;

        for (int i = 0; i < model.parts.Length; i++)
        {
            if (model.parts[i] == null) continue;
            model.parts[i].gameObject.layer = outlined ? outlineLayer : model.originalLayers[i];
        }
    }

    // 클릭과 같은 카메라(PhysicsRaycaster)에서 클릭 콜라이더로 직접 레이를 쏜다
    private bool IsPointerOverMe(out string why)
    {
        if (clickArea == null || !clickArea.enabled) { why = "클릭 영역 없음/꺼짐"; return false; }
        if (LoadingUI.IsShowing) { why = "로딩 중"; return false; }
        if (!TryGetPointerPosition(out Vector2 position)) { why = "마우스 없음"; return false; }

        Camera cam = FindEventCamera();
        if (cam == null) { why = "카메라 못 찾음 (PhysicsRaycaster 확인)"; return false; }

        Ray ray = cam.ScreenPointToRay(position);
        if (!clickArea.Raycast(ray, out RaycastHit hit, 1000f)) { why = "클릭 영역 밖"; return false; }

        if (IsUiOnTop(position)) { why = "UI가 위에 있음"; return false; }

        // 더 가까운 다른 콜라이더(벽 등)가 가리면 클릭도 안 되므로 호버도 제외
        int count = Physics.RaycastNonAlloc(ray, hitBuffer, 1000f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit other = hitBuffer[i];
            if (other.distance >= hit.distance - 0.01f) continue;
            if (other.collider.transform.IsChildOf(transform)) continue;

            why = debugLog ? $"'{other.collider.name}'이(가) 앞을 가림" : "다른 콜라이더가 가림";
            return false;
        }

        why = "클릭 영역 위";
        return true;
    }

    private Camera FindEventCamera()
    {
        if (eventCamera != null && eventCamera.isActiveAndEnabled) return eventCamera;
        if (Time.unscaledTime < nextCameraSearch) return null;

        nextCameraSearch = Time.unscaledTime + 1f;   // 못 찾으면 1초마다만 재시도
        eventCamera = null;

        foreach (PhysicsRaycaster raycaster in FindObjectsByType<PhysicsRaycaster>(FindObjectsSortMode.None))
        {
            if (!raycaster.isActiveAndEnabled || raycaster.eventCamera == null) continue;

            eventCamera = raycaster.eventCamera;
            break;
        }

        if (eventCamera == null) eventCamera = Camera.main;
        return eventCamera;
    }

    private bool IsUiOnTop(Vector2 position)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        if (pointerData == null || pointerSystem != es)
        {
            pointerSystem = es;
            pointerData = new PointerEventData(es);
        }
        pointerData.position = position;

        raycastResults.Clear();
        es.RaycastAll(pointerData, raycastResults);
        foreach (RaycastResult result in raycastResults)
        {
            if (result.gameObject == null) continue;
            return result.module is GraphicRaycaster;   // 가장 위 히트가 UI면 true
        }
        return false;
    }

    private static bool TryGetPointerPosition(out Vector2 position)
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        position = mouse != null ? mouse.position.ReadValue() : default;
        return mouse != null;
#else
        position = Input.mousePosition;
        return true;
#endif
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (debugLog) Debug.Log("[LobbyCharacterView] Click", this);
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (LoadingUI.IsShowing) return;                              // 로딩 화면 뒤에서 눌리지 않게
        if (!profile.isLocalPlayer || !Lobby.CanChangeCharacter) return;

        Lobby.NextCharacter();   // 번호 계산은 서버, 화면은 SyncVar가 돌아오면 갱신됨
    }
}