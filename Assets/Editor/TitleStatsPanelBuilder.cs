using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 타이틀 LIAR'S NEWS 아래에 내 전적 패널을 만든다 (검은 반투명 패널 + 금색 다이아몬드 구분선, 로그인 패널과 같은 톤)
// 만든 오브젝트는 이름 앞에 "[Gen] " → 여러 번 실행해도 같은 결과. 위치 · 크기는 만든 뒤 씬에서 자유롭게 옮겨도 됨 (다시 실행하면 기본 위치로 돌아감)
// 메뉴: Tools / UI / 타이틀 전적 패널 만들기 (열린 씬)   ※ TitleScene을 연 상태에서 실행 후 씬 저장
public static class TitleStatsPanelBuilder
{
    private const string NewsName = "LiarNewsimg";
    private const string PanelName = "[Gen] StatsPanel";
    private const string DividerPath = "Assets/4.Sprite/UI/Login/Redesign/login_divider.png";

    private static readonly Color Gold = new Color32(238, 205, 122, 255);
    private static readonly Color SoftText = new Color32(217, 205, 184, 230);
    private static readonly Color Dim = new Color32(217, 205, 184, 150);
    private static readonly Color Red = new Color32(220, 70, 60, 255);

    private static readonly Vector2 PanelSize = new Vector2(560f, 230f);
    private const float GapBelowNews = 18f;

    [MenuItem("Tools/UI/타이틀 전적 패널 만들기 (열린 씬)")]
    private static void Run()
    {
        RectTransform news = FindInScene(NewsName);
        if (news == null) { Debug.LogError($"[전적 패널] 열린 씬에 '{NewsName}'이 없음 (TitleScene을 열고 실행)"); return; }

        RectTransform parent = (RectTransform)news.parent;
        TMP_FontAsset font = FindMenuFont(parent);
        Sprite divider = AssetDatabase.LoadAssetAtPath<Sprite>(DividerPath);

        Undo.RegisterFullObjectHierarchyUndo(parent.gameObject, "Title Stats Panel");

        // 패널: 뉴스와 같은 기준점, 뉴스 바로 아래 가운데 정렬
        RectTransform panel = GetOrCreate(parent, PanelName, typeof(Image));
        panel.anchorMin = news.anchorMin;
        panel.anchorMax = news.anchorMax;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = PanelSize;
        float newsBottom = news.anchoredPosition.y - news.rect.height * news.pivot.y;
        float newsCenterX = news.anchoredPosition.x + news.rect.width * (0.5f - news.pivot.x);
        panel.anchoredPosition = new Vector2(newsCenterX, newsBottom - GapBelowNews - PanelSize.y * 0.5f);
        panel.SetSiblingIndex(news.GetSiblingIndex() + 1);

        Image back = panel.GetComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.62f);
        back.raycastTarget = false;
        if (!panel.TryGetComponent(out CanvasGroup _)) Undo.AddComponent<CanvasGroup>(panel.gameObject);

        GenImage(panel, "[Gen] DividerTop", divider, new Vector2(0f, 70f), new Vector2(460f, 14f), Gold);
        GenImage(panel, "[Gen] DividerBottom", divider, new Vector2(0f, -100f), new Vector2(460f, 14f), Gold);

        TMP_Text header = GenText(panel, "[Gen] Header", font, "YOUR RECORD", 20f, Gold, new Vector2(0f, 92f), new Vector2(460f, 30f), TextAlignmentOptions.Center);
        header.fontStyle = FontStyles.Bold;
        header.characterSpacing = 8f;

        TMP_Text nickname = GenText(panel, "[Gen] Nickname", font, "Nickname", 34f, Color.white, new Vector2(-95f, 34f), new Vector2(310f, 44f), TextAlignmentOptions.Left);
        nickname.fontStyle = FontStyles.Bold;
        nickname.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text tier = GenText(panel, "[Gen] Tier", font, "REGULAR", 18f, Gold, new Vector2(125f, 34f), new Vector2(250f, 44f), TextAlignmentOptions.Right);
        tier.fontStyle = FontStyles.Italic;

        TMP_Text record = GenText(panel, "[Gen] Record", font, "0 W   0 L", 30f, SoftText, new Vector2(-95f, -14f), new Vector2(310f, 48f), TextAlignmentOptions.Left);
        TMP_Text winRate = GenText(panel, "[Gen] WinRate", font, "--", 42f, Gold, new Vector2(125f, -12f), new Vector2(250f, 56f), TextAlignmentOptions.Right);
        winRate.fontStyle = FontStyles.Bold;

        // 승률 막대
        RectTransform bar = GetOrCreate(panel, "[Gen] Bar", typeof(Image));
        Center(bar, new Vector2(0f, -50f), new Vector2(500f, 6f));
        Image barBack = bar.GetComponent<Image>();
        barBack.color = new Color(1f, 1f, 1f, 0.12f);
        barBack.raycastTarget = false;
        RectTransform fill = GetOrCreate(bar, "[Gen] Fill", typeof(Image));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = Red;
        fillImage.raycastTarget = false;

        TMP_Text games = GenText(panel, "[Gen] Games", font, "0 GAMES", 16f, Dim, new Vector2(-95f, -76f), new Vector2(310f, 28f), TextAlignmentOptions.Left);
        TMP_Text rateLabel = GenText(panel, "[Gen] WinRateLabel", font, "WIN RATE", 16f, Dim, new Vector2(125f, -76f), new Vector2(250f, 28f), TextAlignmentOptions.Right);
        games.characterSpacing = rateLabel.characterSpacing = 4f;

        // 런타임 스크립트 연결
        TitleStatsUI ui = panel.GetComponent<TitleStatsUI>();
        if (ui == null) ui = Undo.AddComponent<TitleStatsUI>(panel.gameObject);
        var so = new SerializedObject(ui);
        so.FindProperty("header").objectReferenceValue = header;
        so.FindProperty("nickname").objectReferenceValue = nickname;
        so.FindProperty("tier").objectReferenceValue = tier;
        so.FindProperty("record").objectReferenceValue = record;
        so.FindProperty("winRate").objectReferenceValue = winRate;
        so.FindProperty("winRateLabel").objectReferenceValue = rateLabel;
        so.FindProperty("games").objectReferenceValue = games;
        so.FindProperty("barFill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;
        Debug.Log($"[전적 패널] '{PanelName}' 생성 · 갱신 완료 (부모 {parent.name}, 폰트 {(font != null ? font.name : "기본")}) - 씬을 저장하세요", panel);
    }

    private static RectTransform FindInScene(string name)
    {
        foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt.name == name && rt.gameObject.scene.IsValid()) return rt;
        return null;
    }

    // 왼쪽 메뉴(ENTER THE BAR 등) 글꼴
    private static TMP_FontAsset FindMenuFont(RectTransform titleGroup)
    {
        Transform panel = titleGroup.parent != null ? titleGroup.parent : titleGroup;
        foreach (TMP_Text t in panel.GetComponentsInChildren<TMP_Text>(true))
            if (t.font != null && t.transform.parent != null && t.transform.parent.name.EndsWith("Btn")) return t.font;
        return null;
    }

    private static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void GenImage(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = GetOrCreate(parent, name, typeof(Image));
        Center(rt, pos, size);
        Image img = rt.GetComponent<Image>();
        img.sprite = sprite;
        img.color = sprite != null ? Color.white : color * new Color(1f, 1f, 1f, 0.5f);   // 스프라이트 없으면 금색 얇은 선
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        if (sprite == null) rt.sizeDelta = new Vector2(size.x, 1.5f);
    }

    private static TMP_Text GenText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, Color color,
                                    Vector2 pos, Vector2 rectSize, TextAlignmentOptions align)
    {
        RectTransform rt = GetOrCreate(parent, name, typeof(TextMeshProUGUI));
        Center(rt, pos, rectSize);
        var t = rt.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform GetOrCreate(RectTransform parent, string name, System.Type component)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var go = new GameObject(name, typeof(RectTransform), component);
        Undo.RegisterCreatedObjectUndo(go, "Title Stats Panel");
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }
}
