using LiarsBar.Server;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 원작 커서(파란 삼각형 · 금색 테두리)를 화면 맨 위에 그리고, 상황에 맞춰 마우스를 가둔다
// - 게임 중(1인칭 조작): 잠금 + 숨김 (고개는 마우스 이동량으로 돌아감)
// - ESC 메뉴: 풀어 줌 (이때만 창 밖으로 나갈 수 있음)
// - 그 밖(인트로 · 타이틀 · 로비 · 로딩 · 결과 화면): 창 안에 가둠
public class CustomCursor : MonoBehaviour
{
    public static CustomCursor Instance;

    private const string SpritePath = "Cursor_Bluff";                   // Resources/Cursor_Bluff.png
    private static readonly Vector2 Size = new Vector2(24f, 32f);         // 1920x1080 기준
    private static readonly Vector2 Hotspot = new Vector2(0.104f, 0.93f); // 그림 속 삼각형 끝

    private Image pointer;
    private RectTransform pointerRt;
    private bool hiddenByCode;
    private bool applied;
    private CursorLockMode appliedMode;
    private PlayerState cachedState;
    private PlayerController controller;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.isBatchMode) return;   // 서버 빌드
        new GameObject("[CustomCursor]").AddComponent<CustomCursor>();
    }

    private void Awake()
    {
        // 예전 CursorCanvas 프리팹이 씬에 있어도 하나만 남긴다
        if (Instance != null && Instance != this)
        {
            Destroy(transform.root.gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
        Build();
    }

    public void SetVisible(bool visible)
    {
        hiddenByCode = !visible;
    }

    private void Update()
    {
        CursorLockMode mode = DecideMode();

        // 창이 포커스를 잃으면 OS가 잠금을 풀어 버리므로, 돌아왔을 때 다시 건다
        if (!Application.isFocused) applied = false;
        else if (!applied || mode != appliedMode)
        {
            Cursor.lockState = mode;
            appliedMode = mode;
            applied = true;
        }

        bool hasSprite = pointer != null && pointer.sprite != null;
        bool free = mode != CursorLockMode.Locked;
        Cursor.visible = free && !hasSprite;   // 커서 그림이 없을 때만 OS 커서

        bool show = free && hasSprite && !hiddenByCode && Mouse.current != null;
        if (pointer != null && pointer.enabled != show) pointer.enabled = show;
        if (show) pointerRt.position = Mouse.current.position.ReadValue();
    }

    private CursorLockMode DecideMode()
    {
        UIInputController ui = UIInputController.Instance;
        if (ui != null && ui.IsInGameMenuOpen) return CursorLockMode.None;

        PlayerProfile me = PlayerProfile.Local;
        if (me != null && me.Phase == RoomPhase.Playing && GameInputActive()) return CursorLockMode.Locked;

#if UNITY_EDITOR
        return CursorLockMode.None;       // 에디터에서는 메뉴 · 인스펙터를 눌러야 하므로 가두지 않음
#else
        return CursorLockMode.Confined;
#endif
    }

    // 인게임 조작(Player 맵)이 켜져 있을 때 = 게임 중. ESC · 채팅 · 로딩 중에는 꺼져 있다
    private bool GameInputActive()
    {
        PlayerState state = PlayerState.Local;
        if (state == null) return false;
        if (state != cachedState)
        {
            cachedState = state;
            controller = state.GetComponent<PlayerController>();
        }
        return controller != null && controller.inputActions != null && controller.inputActions.Player.enabled;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;   // 로딩 화면 · 페이드보다 위

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        var go = new GameObject("Pointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);
        pointerRt = (RectTransform)go.transform;
        pointerRt.anchorMin = pointerRt.anchorMax = Vector2.zero;
        pointerRt.pivot = Hotspot;
        pointerRt.sizeDelta = Size;

        pointer = go.GetComponent<Image>();
        pointer.sprite = Resources.Load<Sprite>(SpritePath);
        pointer.raycastTarget = false;
        pointer.enabled = false;
        if (pointer.sprite == null) Debug.LogWarning($"[CustomCursor] Resources/{SpritePath} 스프라이트를 찾지 못해 OS 커서를 씁니다.");
    }
}
