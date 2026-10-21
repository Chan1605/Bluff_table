using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 로그인 패널을 원작 타이틀(LIAR'S NEWS) 톤으로 다시 꾸민다: 검은 패널 + 금색 아르데코 테두리 · 다이아몬드 구분선 · 밑줄 입력칸
// LoginUI가 참조하는 오브젝트(입력칸 · 버튼 · 메시지)는 그대로 두고 모양 · 위치만 바꾼다 → 로그인 로직 · 씬 참조 영향 없음
// 새로 만드는 장식은 이름 앞에 "[Gen] " → 여러 번 실행해도 같은 결과
// 메뉴: Tools / UI / 로그인 패널 리디자인 (열린 씬)   ※ TitleScene을 연 상태에서 실행 후 씬 저장
//       Tools / UI / 로그인 옛 리소스 정리 (열린 씬)  ※ 리디자인 확정 후: 숨겨 둔 옛 그림 오브젝트 삭제 → 씬 저장 → 안 쓰는 에셋을 휴지통으로
public static class LoginPanelRestyler
{
    private const string SpriteFolder = "Assets/4.Sprite/UI/Login/Redesign/";
    private static readonly Color Gold = new Color32(238, 205, 122, 255);
    private static readonly Color SoftText = new Color32(217, 205, 184, 230);

    // 레이아웃 (LoginGroup 중심 기준 px, 캔버스 1920x1080)
    private static readonly Vector2 FrameSize = new Vector2(700f, 720f);
    private const float TitleY = 290f, DividerTopY = 238f, SubtitleY = 200f;
    private const float IdLabelY = 140f, IdFieldY = 85f, PwLabelY = 10f, PwFieldY = -45f;
    private const float MessageY = -112f, ButtonY = -192f, DividerBottomY = -285f;
    private const float FieldWidth = 560f, FieldHeight = 72f;

    [MenuItem("Tools/UI/로그인 패널 리디자인 (열린 씬)")]
    private static void Run()
    {
        var ui = Object.FindFirstObjectByType<LoginUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogError("[로그인 리디자인] 열린 씬에 LoginUI가 없음 (TitleScene을 열고 실행)"); return; }

        var so = new SerializedObject(ui);
        var panel = so.FindProperty("LoginPanel").objectReferenceValue as GameObject;
        var button = so.FindProperty("LoginBtn").objectReferenceValue as Button;
        var idInput = so.FindProperty("nickInput").objectReferenceValue as TMP_InputField;
        var pwInput = so.FindProperty("passwordInput").objectReferenceValue as TMP_InputField;
        var message = so.FindProperty("messageText").objectReferenceValue as TMP_Text;
        if (panel == null || button == null || idInput == null || pwInput == null || message == null)
        {
            Debug.LogError("[로그인 리디자인] LoginUI의 참조(LoginPanel · LoginBtn · nickInput · passwordInput · messageText)가 비어 있음");
            return;
        }

        PrepareSprites();
        Sprite panelSprite = LoadSprite("login_panel");
        Sprite dividerSprite = LoadSprite("login_divider");
        Sprite fieldSprite = LoadSprite("login_field");
        Sprite buttonSprite = LoadSprite("login_button");
        Sprite glowSprite = LoadSprite("login_glow");
        if (panelSprite == null || dividerSprite == null || fieldSprite == null || buttonSprite == null || glowSprite == null) return;

        RectTransform back = (RectTransform)idInput.transform.parent;   // BackGround (입력칸의 부모)
        RectTransform group = (RectTransform)back.parent;               // LoginGroup
        TMP_FontAsset font = message.font;

        Undo.RegisterFullObjectHierarchyUndo(panel, "Login Panel Redesign");

        // 1) 배경: 화면 어둡게 + 옛 가죽 카드 그림 · 아이콘 숨김 (오브젝트는 남김: 입력칸이 BackGround 자식)
        if (panel.TryGetComponent(out Image dim)) dim.color = new Color(0f, 0f, 0f, 0.6f);
        if (back.TryGetComponent(out Image backImage)) backImage.enabled = false;
        foreach (string name in new[] { "SpadeImg", "IdImg", "PWimg" })
        {
            Transform t = back.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        // 2) 패널 (뒤쪽 은은한 빛 + 아르데코 테두리)
        Image glow = GenImage(group, "[Gen] Glow", glowSprite, Vector2.zero, new Vector2(1300f, 1300f), false);
        glow.transform.SetSiblingIndex(0);
        Image frame = GenImage(group, "[Gen] Frame", panelSprite, Vector2.zero, FrameSize, true);
        frame.transform.SetSiblingIndex(1);

        // 3) 제목 · 구분선 · 부제
        TMP_Text title = GenText(group, "[Gen] Title", font, "LOGIN", 76f, Gold, new Vector2(0f, TitleY), new Vector2(600f, 100f));
        title.characterSpacing = 28f;
        title.fontStyle = FontStyles.Bold;
        SetLocalizedKey(title, "LOGIN");

        GenImage(group, "[Gen] Divider Top", dividerSprite, new Vector2(0f, DividerTopY), new Vector2(600f, 24f), false);

        TMP_Text subtitle = GenText(group, "[Gen] Subtitle", font, "Only the brave take a seat.", 24f, SoftText, new Vector2(0f, SubtitleY), new Vector2(600f, 40f));
        subtitle.fontStyle = FontStyles.Italic;
        SetLocalizedKey(subtitle, "Only the brave take a seat.");

        GenImage(group, "[Gen] Divider Bottom", dividerSprite, new Vector2(0f, DividerBottomY), new Vector2(600f, 24f), false);

        // 4) 라벨 · 입력칸
        StyleLabel(group.Find("IDinfo"), IdLabelY);
        StyleLabel(group.Find("PWinfo"), PwLabelY);
        StyleField(idInput, fieldSprite, IdFieldY);
        StyleField(pwInput, fieldSprite, PwFieldY);

        // 5) 안내 메시지 (로그인 실패 등)
        RectTransform mrt = message.rectTransform;
        mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = new Vector2(0f, MessageY);
        mrt.sizeDelta = new Vector2(FieldWidth, 40f);
        message.fontSize = 24f;
        message.color = new Color32(232, 128, 110, 255);
        message.alignment = TextAlignmentOptions.Center;
        message.textWrappingMode = TextWrappingModes.Normal;

        // 6) 버튼
        RectTransform brt = (RectTransform)button.transform;
        brt.anchoredPosition = new Vector2(0f, ButtonY);
        brt.sizeDelta = new Vector2(420f, 90f);
        Image buttonImage = button.GetComponent<Image>();
        buttonImage.sprite = buttonSprite;
        buttonImage.type = Image.Type.Sliced;
        buttonImage.pixelsPerUnitMultiplier = 1.33f;   // 테두리를 버튼 높이에 맞게 가늘게
        buttonImage.color = Color.white;
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = buttonImage;
        ColorBlock cb = button.colors;
        cb.normalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        cb.highlightedColor = Color.white;
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        cb.colorMultiplier = 1.25f;   // 마우스를 올리면 금색이 살짝 밝아짐
        cb.fadeDuration = 0.12f;
        button.colors = cb;

        TMP_Text label = GenText(brt, "[Gen] Label", font, "ENTER THE BAR", 28f, Gold, Vector2.zero, Vector2.zero);
        RectTransform lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.characterSpacing = 14f;
        label.fontStyle = FontStyles.Bold;
        SetLocalizedKey(label, "ENTER THE BAR");

        // 7) 열릴 때 살짝 떠오르며 나타나기
        if (!group.TryGetComponent(out PanelIntroFx _)) Undo.AddComponent<PanelIntroFx>(group.gameObject);

        EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = panel;
        Debug.Log("[로그인 리디자인] 완료 — 씬을 저장하세요 (Ctrl+S)", panel);
    }

    // ───── 옛 리소스 정리 ─────

    // 리디자인 전에 쓰던 에셋 (가죽 카드 · 아이콘 · 그림 글씨가 든 스프라이트 시트, 씬에 풀려서 안 쓰던 옛 프리팹)
    private static readonly string[] OldAssets =
    {
        "Assets/4.Sprite/UI/Login/LoginUI.png",
        "Assets/2.Model/Prefabs/UI/LoginPanel.prefab",
    };

    [MenuItem("Tools/UI/로그인 옛 리소스 정리 (열린 씬)")]
    private static void CleanupOldResources()
    {
        var ui = Object.FindFirstObjectByType<LoginUI>(FindObjectsInactive.Include);
        if (ui == null) { Debug.LogError("[로그인 정리] 열린 씬에 LoginUI가 없음 (TitleScene을 열고 실행)"); return; }
        var so = new SerializedObject(ui);
        var idInput = so.FindProperty("nickInput").objectReferenceValue as TMP_InputField;
        var button = so.FindProperty("LoginBtn").objectReferenceValue as Button;
        if (idInput == null || button == null) { Debug.LogError("[로그인 정리] LoginUI 참조가 비어 있음"); return; }
        if (idInput.transform.parent.parent.Find("[Gen] Frame") == null)
        {
            Debug.LogError("[로그인 정리] 리디자인이 적용되지 않은 씬입니다. 먼저 '로그인 패널 리디자인'을 실행하세요.");
            return;
        }

        if (!EditorUtility.DisplayDialog("로그인 옛 리소스 정리",
                "1) 숨겨 둔 옛 오브젝트(SpadeImg · IdImg · PWimg)와 BackGround의 옛 그림을 지우고 씬을 저장합니다.\n" +
                "2) 다른 씬 · 프리팹에서 안 쓰는 옛 에셋을 휴지통으로 옮깁니다:\n   " + string.Join("\n   ", OldAssets),
                "정리", "취소"))
            return;

        // 1) 씬: 옛 그림 오브젝트 · 컴포넌트 제거
        Transform back = idInput.transform.parent;
        foreach (string name in new[] { "SpadeImg", "IdImg", "PWimg" })
        {
            Transform t = back.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
        if (back.TryGetComponent(out Image backImage)) Undo.DestroyObjectImmediate(backImage);   // 입력칸 부모라 오브젝트는 남김
        Undo.RecordObject(button, "Clear old sprite state");
        button.spriteState = default;   // 옛 시트를 가리키던 Highlighted / Pressed 스프라이트

        var scene = ui.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) { Debug.LogError("[로그인 정리] 씬 저장 실패 → 에셋은 지우지 않음"); return; }

        // 2) 에셋: 저장된 모든 씬 · 프리팹에서 참조를 다시 확인한 뒤 휴지통으로
        var candidates = new HashSet<string>(OldAssets.Where(p => AssetDatabase.LoadMainAssetAtPath(p) != null));
        var users = new Dictionary<string, List<string>>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (candidates.Contains(path) || !path.StartsWith("Assets/")) continue;
            foreach (string dep in AssetDatabase.GetDependencies(path, false))
            {
                if (!candidates.Contains(dep)) continue;
                if (!users.TryGetValue(dep, out var list)) users[dep] = list = new List<string>();
                list.Add(path);
            }
        }

        foreach (string path in candidates)
        {
            if (users.TryGetValue(path, out var list))
            {
                Debug.LogWarning($"[로그인 정리] 아직 쓰는 곳이 있어 남김: {path}\n  ← {string.Join("\n  ← ", list)}");
                continue;
            }
            Debug.Log(AssetDatabase.MoveAssetToTrash(path) ? $"[로그인 정리] 휴지통으로 이동: {path}" : $"[로그인 정리] 이동 실패: {path}");
        }

        // 비어 버린 Login 폴더의 옛 스프라이트 폴더는 Redesign이 있어서 그대로 둔다
        AssetDatabase.Refresh();
        Debug.Log("[로그인 정리] 완료");
    }

    // ───── 요소 ─────

    private static void StyleLabel(Transform t, float y)
    {
        if (t == null || !t.TryGetComponent(out TMP_Text text)) return;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(FieldWidth, 40f);
        text.fontSize = 24f;
        text.color = Gold;
        text.characterSpacing = 6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void StyleField(TMP_InputField input, Sprite sprite, float y)
    {
        RectTransform rt = (RectTransform)input.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(FieldWidth, FieldHeight);

        if (input.TryGetComponent(out Image img))
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = Color.white;
        }

        if (input.textViewport != null)
        {
            input.textViewport.offsetMin = new Vector2(20f, 6f);
            input.textViewport.offsetMax = new Vector2(-20f, -6f);
        }
        if (input.textComponent != null)
        {
            input.textComponent.fontSize = 32f;
            input.textComponent.color = new Color32(242, 236, 224, 255);
            input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (input.placeholder is TMP_Text ph)
        {
            ph.fontSize = 28f;
            ph.color = new Color32(190, 176, 150, 140);
            ph.fontStyle = FontStyles.Italic;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
        }

        input.customCaretColor = true;
        input.caretColor = Gold;
        input.caretWidth = 2;
        input.selectionColor = new Color(0.93f, 0.80f, 0.48f, 0.35f);
    }

    private static Image GenImage(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size, bool sliced)
    {
        RectTransform rt = GetOrCreate(parent, name, typeof(Image));
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = rt.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text GenText(RectTransform parent, string name, TMP_FontAsset font, string text, float size, Color color, Vector2 pos, Vector2 rectSize)
    {
        RectTransform rt = GetOrCreate(parent, name, typeof(TextMeshProUGUI));
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = rectSize;
        var t = rt.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform GetOrCreate(RectTransform parent, string name, System.Type component)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;
        var go = new GameObject(name, typeof(RectTransform), component);
        Undo.RegisterCreatedObjectUndo(go, "Login Panel Redesign");
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void SetLocalizedKey(TMP_Text text, string key)
    {
        if (!text.TryGetComponent(out LocalizedText lt)) lt = Undo.AddComponent<LocalizedText>(text.gameObject);
        var s = new SerializedObject(lt);
        s.FindProperty("key").stringValue = key;
        s.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───── 스프라이트 가져오기 설정 (9-slice 테두리 포함) ─────

    private static void PrepareSprites()
    {
        Prepare("login_panel", 100);
        Prepare("login_field", 28);
        Prepare("login_button", 44);
        Prepare("login_divider", 0);
        Prepare("login_glow", 0);
    }

    private static void Prepare(string name, int border)
    {
        string path = SpriteFolder + name + ".png";
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) { Debug.LogError($"[로그인 리디자인] 스프라이트 없음: {path}"); return; }

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        bool dirty = ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || settings.spriteMeshType != SpriteMeshType.FullRect
                     || ti.spriteBorder != new Vector4(border, border, border, border);
        if (!dirty) return;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // Sliced 이미지는 FullRect여야 테두리가 정확
        ti.SetTextureSettings(settings);
        ti.spriteBorder = new Vector4(border, border, border, border);
        ti.SaveAndReimport();
    }

    private static Sprite LoadSprite(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name + ".png");
        if (s == null) Debug.LogError($"[로그인 리디자인] 스프라이트를 못 읽음: {SpriteFolder}{name}.png");
        return s;
    }
}
