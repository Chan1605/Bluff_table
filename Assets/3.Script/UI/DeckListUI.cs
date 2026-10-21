using LiarsBar.Server;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 원작 "Deck Contains" 연출: 라운드 시작 때 20장을 펼쳐 공개하는 동안 덱 구성을 계단식으로 띄운다
//   Deck Contains        ← 먼저 나타남
//   ┌ 6 x King           ← 한 줄씩 아래로 차례대로 (왼쪽에서 밀려 들어오며 페이드 인)
//     6 x Queen
//     6 x Ace
//     2 x Joker
// CardPlacer가 InGameEvent.OnDeckListShown(true / false)를 보내면 재생 / 숨김.
// 원작처럼 펼쳐진 카드 위 공중에 뜨도록 월드 캔버스로 만든다 (프리팹 · Inspector 연결 없음, 폰트는 HUD 글자에서 가져옴).
// 위치: CardPlacer.DeckPoint(테이블 중앙) 위 · 내 자리에서 보아 약간 건너편, 내 쪽을 향해 똑바로 선다.
// 플레이 중 Hierarchy > DontDestroyOnLoad > [DeckListUI]를 선택해 아래 값을 바꾸면 바로 반영된다.
public class DeckListUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[DeckListUI]");
        DontDestroyOnLoad(go);
        go.AddComponent<DeckListUI>();
    }

    [Header("월드 배치 (m)")]
    [Tooltip("테이블 중앙(덱 자리)에서 글자 블록 중심까지 높이")]
    [SerializeField] private float height = 1f;
    [Tooltip("내 자리에서 보아 건너편(+) / 내 쪽(-)으로 이동")]
    [SerializeField] private float farOffset = -1.5f;
    [Tooltip("좌우 이동 (+ = 오른쪽)")]
    [SerializeField] private float sideOffset = 0.18f;
    [Tooltip("캔버스 1px의 월드 크기. 0.001 = 1px당 1mm (제목 76px ≈ 7.6cm)")]
    [SerializeField] private float pixelSize = 0.0012f;
    [Tooltip("위를 향해 살짝 젖히는 각도 (내려다보는 시점 보정)")]
    [SerializeField] private float tiltBack = 0f;

    // 덱 구성 (CardPlacer: King · Queen · Ace 각 6장 + Joker 2장 = 20장)
    private static readonly (string key, int count)[] Entries =
    {
        ("King", 6), ("Queen", 6), ("Ace", 6), ("Joker", 2),
    };

    // 레이아웃 (캔버스 px, 블록 왼쪽 위 기준)
    private const float HeaderSize = 76f;
    private const float LineSize = 62f;
    private const float LineHeight = 72f;
    private const float LineIndent = 34f;     // 목록 글자 들여쓰기
    private const float BarX = 14f;           // 목록 왼쪽 세로선 위치
    private const float BarThickness = 3f;

    // 타이밍 (초)
    private const float HeaderIn = 0.35f;
    private const float FirstLineDelay = 0.3f;
    private const float LineStagger = 0.2f;   // 계단식 간격
    private const float LineIn = 0.3f;
    private const float SlideFrom = -40f;     // 왼쪽에서 밀려 들어오는 거리 (px)
    private const float FadeOut = 0.3f;

    private Canvas worldCanvas;
    private TMP_Text style;   // 글자 모양을 복사해 올 HUD 글자
    private RectTransform root;
    private CanvasGroup group;
    private TMP_Text header;
    private TMP_Text[] lines;
    private RectTransform bar;
    private RectTransform tick;
    private Sequence seq;

    private void OnEnable()
    {
        InGameEvent.OnDeckListShown += HandleShown;
        InGameEvent.OnReEntryInLobby += HideNow;
        Localization.LanguageChanged += Redraw;
    }

    private void OnDisable()
    {
        InGameEvent.OnDeckListShown -= HandleShown;
        InGameEvent.OnReEntryInLobby -= HideNow;
        Localization.LanguageChanged -= Redraw;
        seq?.Kill();
    }

    private void HandleShown(bool show)
    {
        if (show) Show();
        else Hide();
    }

    // ───── 재생 ─────

    private void Show()
    {
        if (!Build()) return;
        Redraw();
        seq?.Kill();

        Place();
        worldCanvas.gameObject.SetActive(true);
        group.alpha = 1f;

        seq = DOTween.Sequence().SetTarget(root);

        // ① 제목
        Prepare(header, 0f);
        seq.Insert(0f, FadeIn(header, HeaderIn));
        seq.Insert(0f, header.rectTransform.DOAnchorPosX(0f, HeaderIn).SetEase(Ease.OutCubic));

        // ② 왼쪽 괄호 선 (위 가로 → 세로로 자라남)
        tick.localScale = new Vector3(0f, 1f, 1f);
        bar.localScale = new Vector3(1f, 0f, 1f);
        seq.Insert(FirstLineDelay - 0.1f, tick.DOScaleX(1f, 0.2f).SetEase(Ease.OutCubic));
        seq.Insert(FirstLineDelay, bar.DOScaleY(1f, 0.25f).SetEase(Ease.OutCubic));

        // ③ 목록 한 줄씩 (계단식)
        for (int i = 0; i < lines.Length; i++)
        {
            TMP_Text line = lines[i];
            float at = FirstLineDelay + i * LineStagger;
            Prepare(line, LineIndent);
            seq.Insert(at, FadeIn(line, LineIn));
            seq.Insert(at, line.rectTransform.DOAnchorPosX(LineIndent, LineIn).SetEase(Ease.OutCubic));
        }
    }

    private void Hide()
    {
        if (worldCanvas == null || !worldCanvas.gameObject.activeSelf) return;
        seq?.Kill();
        seq = DOTween.Sequence().SetTarget(root)
            .Append(DOTween.To(() => group.alpha, a => group.alpha = a, 0f, FadeOut))
            .OnComplete(() => worldCanvas.gameObject.SetActive(false));
    }

    private void HideNow()
    {
        seq?.Kill();
        if (worldCanvas != null) worldCanvas.gameObject.SetActive(false);
    }

    // 테이블 중앙 위, 내 자리 → 테이블 중앙 방향을 바라보게 (글자가 나를 향함)
    private void Place()
    {
        if (worldCanvas == null) return;
        CardPlacer placer = FindFirstObjectByType<CardPlacer>();
        Vector3 center = placer != null && placer.DeckPoint != null ? placer.DeckPoint.position : transform.position;

        Vector3 forward = Vector3.zero;
        Transform chair = PlayerProfile.Local != null ? SeatAnchors.Get(SeatArea.Table, PlayerProfile.Local.Seat) : null;
        if (chair != null) forward = Vector3.ProjectOnPlane(center - chair.position, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f && Camera.main != null) forward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Transform t = worldCanvas.transform;
        t.position = center + Vector3.up * height + forward * farOffset + right * sideOffset;
        t.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(-tiltBack, 0f, 0f);
        t.localScale = Vector3.one * pixelSize;
        if (Camera.main != null) worldCanvas.worldCamera = Camera.main;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && worldCanvas != null && worldCanvas.gameObject.activeSelf) Place();
    }
#endif

    private static void Prepare(TMP_Text text, float x)
    {
        text.alpha = 0f;
        Vector2 p = text.rectTransform.anchoredPosition;
        text.rectTransform.anchoredPosition = new Vector2(x + SlideFrom, p.y);
    }

    private static Tween FadeIn(TMP_Text text, float duration)
    {
        return DOTween.To(() => text.alpha, a => text.alpha = a, 1f, duration).SetEase(Ease.OutQuad);
    }

    private void Redraw()
    {
        if (worldCanvas == null) return;
        header.text = Localization.Get("Deck Contains");
        for (int i = 0; i < lines.Length; i++)
            lines[i].text = $"{Entries[i].count} x {Localization.Get(Entries[i].key)}";
    }

    // ───── UI 만들기 (처음 한 번, 캔버스가 바뀌면 다시) ─────

    private bool Build()
    {
        if (worldCanvas != null) return true;

        // 글자 모양은 HUD 제목("KING'S TABLE")과 똑같이: 폰트 · 머티리얼 · 스타일을 그대로 복사.
        // 제목이 없으면 HUD의 다른 글자, 그것도 없으면 TMP 기본 폰트
        InGameHUD hud = FindFirstObjectByType<InGameHUD>(FindObjectsInactive.Include);
        style = hud != null ? hud.TitleText : null;
        if ((style == null || style.font == null) && hud != null)
            foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true))
                if (t.font != null) { style = t; break; }
        TMP_FontAsset font = style != null ? style.font : TMP_Settings.defaultFontAsset;

        float blockW = 760f;
        float blockH = HeaderSize + 10f + LineHeight * Entries.Length;

        // 월드 캔버스 (블록 중심 = 캔버스 중심)
        var canvasGo = new GameObject("[Gen] DeckList (World)", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        worldCanvas = canvasGo.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        var canvasRt = (RectTransform)canvasGo.transform;
        canvasRt.sizeDelta = new Vector2(blockW, blockH);
        canvasRt.pivot = new Vector2(0.5f, 0.5f);

        var go = new GameObject("Block", typeof(RectTransform), typeof(CanvasGroup));
        root = (RectTransform)go.transform;
        root.SetParent(canvasRt, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        header = MakeText("Header", font, HeaderSize, 0f);

        float listTop = -(HeaderSize + 10f);
        lines = new TMP_Text[Entries.Length];
        for (int i = 0; i < Entries.Length; i++)
            lines[i] = MakeText($"Line_{i}", font, LineSize, listTop - i * LineHeight);

        // 첫 줄 왼쪽의 ┌ 모양 선
        tick = MakeLine("Tick", new Vector2(BarX, listTop + 4f), new Vector2(0f, 1f), new Vector2(54f, BarThickness));
        bar = MakeLine("Bar", new Vector2(BarX, listTop + 4f), new Vector2(0f, 1f), new Vector2(BarThickness, LineHeight));

        canvasGo.SetActive(false);
        return true;
    }

    private TMP_Text MakeText(string name, TMP_FontAsset font, float size, float y)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(720f, size * 1.2f);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        if (style != null && style.font == font)
        {
            text.fontSharedMaterial = style.fontSharedMaterial;   // 외곽선 · 두께 등 HUD와 같은 머티리얼 (공유, 인스턴스 안 만듦)
            text.fontStyle = style.fontStyle;
        }
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }

    private RectTransform MakeLine(string name, Vector2 pos, Vector2 pivot, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;
        return rt;
    }
}
