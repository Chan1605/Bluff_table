using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 퀵챗: V로 목록 열기 → 1~5로 대사 선택 → 서버가 모두에게 알림 → 말한 사람 머리 위 말풍선
// 대사는 번호로만 주고받고, 글자는 받는 쪽 언어로 바꾼다 (Strings.csv)
public class QuickChatUI : MonoBehaviour
{
    public static readonly string[] Lines = { "Trust me.", "Liar...", "Not a chance.", "Hurry up!", "Good luck." };

    private const int SeatCount = 4;
    private const float MenuAutoClose = 5f;
    private const float BubbleTime = 2.8f;
    private const float PopTime = 0.18f;
    private const float FadeTime = 0.35f;
    private const float AbovePlate = 70f;    // 이름판 위로 띄우는 높이 (캔버스 단위)
    private const float AboveHead = 0.45f;   // 이름판이 없을 때 머리 위 높이 (m)
    private const float SendCooldown = 2f;   // 서버 PlayerState.QuickChatCooldown과 같게

    private static readonly Color PanelColor = new Color(0.07f, 0.055f, 0.045f, 0.9f);
    private static readonly Color AccentColor = new Color(0.95f, 0.64f, 0.23f);
    private static readonly Color TextColor = new Color(0.96f, 0.92f, 0.84f);

    private class Bubble
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TMP_Text title;
        public TMP_Text body;
        public Transform plate;
        public Transform head;
        public float start;
        public bool active;
    }

    private static QuickChatUI instance;

    private Canvas canvas;
    private RectTransform menu;
    private TMP_Text menuTitle;
    private TMP_Text menuBody;
    private readonly Bubble[] bubbles = new Bubble[SeatCount];
    private TMP_FontAsset font;
    private bool menuOpen;
    private float menuCloseTime;
    private float nextSendTime;
    private float localEchoTime = -10f;

    public static bool IsOpen => instance != null && instance.menuOpen;
    public static void Toggle() { if (instance != null) instance.SetMenu(!instance.menuOpen); }
    public static void Close() { if (instance != null) instance.SetMenu(false); }

    // 내 대사: 서버로 보내고 내 말풍선은 바로 띄운다 (서버 왕복을 기다리지 않음)
    public static void Say(int line)
    {
        if (instance == null) return;
        instance.SetMenu(false);

        PlayerState me = PlayerState.Local;
        if (me == null || me.isDead || line < 0 || line >= Lines.Length) return;
        if (Time.unscaledTime < instance.nextSendTime) return;

        instance.nextSendTime = Time.unscaledTime + SendCooldown;
        me.CmdQuickChat(line);
        Debug.Log($"[QuickChat] 전송 - 대사 {line}");

        if (MySeat >= 0)
        {
            instance.localEchoTime = Time.unscaledTime;
            instance.Show(MySeat, line);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[QuickChatUI]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<QuickChatUI>();
    }

    private void OnEnable()
    {
        InGameEvent.OnQuickChat += HandleQuickChat;
        InGameEvent.OnMenuOpened += HandleMenuOpened;
        InGameEvent.OnMenuClosed += HandleMenuClosed;
        InGameEvent.OnReEntryInLobby += HideAll;
        InGameEvent.OnPlayerEliminated += HandleEliminated;
    }

    private void OnDisable()
    {
        InGameEvent.OnQuickChat -= HandleQuickChat;
        InGameEvent.OnMenuOpened -= HandleMenuOpened;
        InGameEvent.OnMenuClosed -= HandleMenuClosed;
        InGameEvent.OnReEntryInLobby -= HideAll;
        InGameEvent.OnPlayerEliminated -= HandleEliminated;
    }

    private static int MySeat => PlayerProfile.Local != null ? PlayerProfile.Local.Seat : -1;

    #region 목록

    private void SetMenu(bool open)
    {
        if (open && (NamePlateBoard.Instance == null || PlayerState.Local == null || PlayerState.Local.isDead)) open = false;
        if (open == menuOpen && (!open || menu != null)) return;

        menuOpen = open;
        if (open)
        {
            Build();
            RefreshMenu();
            menuCloseTime = Time.unscaledTime + MenuAutoClose;
        }
        if (menu != null) menu.gameObject.SetActive(open);
    }

    private void RefreshMenu()
    {
        string hex = ColorUtility.ToHtmlStringRGB(AccentColor);
        var sb = new System.Text.StringBuilder();
        int count = Mathf.Min(Lines.Length, PlayerState.QuickChatCount);
        for (int i = 0; i < count; i++)
            sb.Append($"<color=#{hex}><b>{i + 1}</b></color>   {Localization.Get(Lines[i])}\n");
        sb.Append($"<size=70%><color=#93A1B0>{Localization.Get("1-5 to say  ·  V to close")}</color></size>");

        menuTitle.text = Localization.Get("QUICK CHAT");
        menuBody.text = sb.ToString();
    }

    private void Update()
    {
        if (menuOpen && (Time.unscaledTime > menuCloseTime || NamePlateBoard.Instance == null || PlayerState.Local == null))
            SetMenu(false);
    }

    #endregion

    #region 말풍선

    // 서버에서 온 퀵챗. 내 것은 Say에서 이미 띄웠으니 다시 띄우지 않는다
    private void HandleQuickChat(int seat, int line)
    {
        Debug.Log($"[QuickChat] 수신 - 좌석 {seat}, 대사 {line}");
        if (seat == MySeat && Time.unscaledTime - localEchoTime < SendCooldown) return;
        Show(seat, line);
    }

    private void Show(int seat, int line)
    {
        if (seat < 0 || seat >= SeatCount || line < 0 || line >= Lines.Length) return;
        if (NamePlateBoard.Instance == null) return;

        Build();
        Bubble b = bubbles[seat];

        PlayerProfile speaker = FindProfile(seat);
        bool mine = seat == MySeat;
        b.title.text = mine ? Localization.Get("You") : speaker != null ? speaker.Nickname : string.Empty;
        b.body.text = Localization.Get(Lines[line]);

        Transform chair = SeatAnchors.Get(SeatArea.Table, seat);
        NamePlate plate = chair != null ? chair.GetComponentInChildren<NamePlate>(true) : null;
        b.plate = plate != null ? plate.transform : null;
        b.head = null;
        if (speaker != null && speaker.TryGetComponent(out PlayerState state) && state.headController != null)
            b.head = state.headController.headPivot != null ? state.headController.headPivot : state.headController.transform;

        b.start = Time.unscaledTime;
        b.active = true;
        b.rect.gameObject.SetActive(true);
        b.rect.SetAsLastSibling();   // 최근 말풍선이 위로
    }

    private void LateUpdate()
    {
        if (canvas == null) return;

        Camera cam = FindCamera();
        float scale = canvas.scaleFactor;
        int mySeat = MySeat;

        for (int i = 0; i < SeatCount; i++)
        {
            Bubble b = bubbles[i];
            if (b == null || !b.active) continue;

            float t = Time.unscaledTime - b.start;
            if (t > BubbleTime + FadeTime)
            {
                Hide(b);
                continue;
            }

            float alpha = t < BubbleTime ? 1f : 1f - (t - BubbleTime) / FadeTime;
            b.rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(Mathf.Clamp01(t / PopTime)));

            // 내 말은 1인칭이라 머리 위가 안 보임 → 목록 자리에 띄운다
            if (i == mySeat)
            {
                b.rect.position = menu.position + new Vector3(b.rect.rect.width * 0.5f * scale, 0f, 0f);
            }
            else
            {
                Vector3 screen = Vector3.zero;
                bool visible = cam != null && TryAnchor(b, cam, scale, out screen);
                if (!visible) alpha = 0f;
                else b.rect.position = screen;
            }
            b.group.alpha = alpha;
        }
    }

    // 이름판이 보이면 그 위, 아니면(결과 화면 등) 머리 위
    private static bool TryAnchor(Bubble b, Camera cam, float scale, out Vector3 screen)
    {
        screen = Vector3.zero;
        bool usePlate = b.plate != null && b.plate.gameObject.activeInHierarchy;
        if (!usePlate && b.head == null) return false;

        Vector3 world = usePlate ? b.plate.position : b.head.position + Vector3.up * AboveHead;
        screen = cam.WorldToScreenPoint(world);
        if (screen.z <= 0f) return false;   // 카메라 뒤

        screen.z = 0f;
        if (usePlate) screen.y += AbovePlate * scale;
        return true;
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static void Hide(Bubble b)
    {
        b.active = false;
        b.rect.gameObject.SetActive(false);
    }

    private void HideAll()
    {
        SetMenu(false);
        if (canvas != null) canvas.enabled = true;
        foreach (Bubble b in bubbles)
            if (b != null) Hide(b);
    }

    #endregion

    private void HandleMenuOpened()
    {
        SetMenu(false);
        if (canvas != null) canvas.enabled = false;
    }

    private void HandleMenuClosed()
    {
        if (canvas != null) canvas.enabled = true;
    }

    private void HandleEliminated(int seat)
    {
        if (seat == MySeat) SetMenu(false);
    }

    // MainCamera 태그가 없는 씬이면 Camera.main이 null → 켜진 카메라 중 depth가 가장 큰 것
    private static Camera FindCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled) return cam;

        cam = null;
        foreach (Camera c in Camera.allCameras)
            if (cam == null || c.depth > cam.depth) cam = c;
        return cam;
    }

    private static PlayerProfile FindProfile(int seat)
    {
        PlayerProfile me = PlayerProfile.Local;
        foreach (PlayerProfile p in PlayerProfile.All)
            if (p != null && p.IsInRoom && p.Seat == seat && (me == null || p.RoomId == me.RoomId)) return p;
        return null;
    }

    #region UI 생성

    private void Build()
    {
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform root = GetComponent<RectTransform>();
            menu = MakePanel("Menu", root, new Vector2(0f, 0f), out menuTitle, out menuBody);
            menu.anchorMin = menu.anchorMax = Vector2.zero;
            menu.anchoredPosition = new Vector2(48f, 170f);
            menu.gameObject.SetActive(false);

            for (int i = 0; i < SeatCount; i++)
            {
                RectTransform rect = MakePanel($"Bubble {i}", root, new Vector2(0.5f, 0f), out TMP_Text title, out TMP_Text body);
                rect.gameObject.SetActive(false);
                bubbles[i] = new Bubble { rect = rect, group = rect.GetComponent<CanvasGroup>(), title = title, body = body };
            }
        }

        // 타이틀 씬에서 먼저 만들어졌으면 기본 폰트(한글 없음)일 수 있다 → 테이블 씬 글꼴로 교체
        if (font == null || !font.HasCharacter('가', true)) ApplyFont(FindKoreanFont());
    }

    private void ApplyFont(TMP_FontAsset newFont)
    {
        if (newFont == null) return;
        font = newFont;
        menuTitle.font = menuBody.font = font;
        foreach (Bubble b in bubbles)
            b.title.font = b.body.font = font;
    }

    private TMP_FontAsset FindKoreanFont()
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.font != null && t.transform.root != transform && t.font.HasCharacter('가', true)) return t.font;
        return TMP_Settings.defaultFontAsset;
    }

    private static RectTransform MakePanel(string name, Transform parent, Vector2 pivot, out TMP_Text title, out TMP_Text body)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CanvasGroup),
                                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.pivot = pivot;

        var image = go.GetComponent<Image>();
        image.color = PanelColor;
        image.raycastTarget = false;

        go.GetComponent<CanvasGroup>().blocksRaycasts = false;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(26, 26, 14, 16);
        layout.spacing = 2f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 왼쪽 주황 띠
        var bar = new GameObject("Accent", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        bar.transform.SetParent(go.transform, false);
        bar.GetComponent<LayoutElement>().ignoreLayout = true;
        var barRect = (RectTransform)bar.transform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(5f, 0f);
        barRect.anchoredPosition = Vector2.zero;
        var barImage = bar.GetComponent<Image>();
        barImage.color = AccentColor;
        barImage.raycastTarget = false;

        title = MakeText(go.transform, 20f, AccentColor, FontStyles.Bold);
        body = MakeText(go.transform, 30f, TextColor, FontStyles.Normal);
        return rect;
    }

    private static TMP_Text MakeText(Transform parent, float size, Color color, FontStyles style)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.richText = true;
        return text;
    }

    #endregion
}
