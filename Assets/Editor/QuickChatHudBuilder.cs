using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 음성 채팅 대신 퀵챗을 쓰면서 바뀐 InGameUI 프리팹 부분을 만든다.
// - 가운데 아래: 마이크 → 말풍선 + [V]
// - ESC 메뉴: 음성 설정 자리였던 가운데 칸(두 금색 선 사이)에 조작법 안내
// "[Gen] "으로 시작하는 오브젝트만 지우고 다시 만들므로 여러 번 실행해도 된다.
public static class QuickChatHudBuilder
{
    private const string PrefabPath = "Assets/2.Model/Prefabs/UI/InGameUI.prefab";
    private const string IconPath = "Assets/4.Sprite/UI/icon/icon_quickchat.png";
    private const string KeyPath = "Assets/4.Sprite/UI/Key/";
    private const string HudName = "[Gen] QuickChat";
    private const string OldHudName = "[Gen] Voice";
    private const string ControlsName = "[Gen] Controls";

    // 가운데 칸: x -180 ~ 180, y -150 ~ 150 (LineLeft · LineRight 사이)
    private const float KeyRight = -28f;
    private const float LabelLeft = -8f;
    private const float FirstRowY = 84f;
    private const float RowStep = 36f;
    private const float KeySize = 30f;

    private static readonly Color Gold = new Color(0.93f, 0.82f, 0.45f, 1f);
    private static readonly Color MenuText = new Color(0.82f, 0.8f, 0.76f, 1f);

    [MenuItem("Tools/LiarsBar/퀵챗 HUD · ESC 조작법 만들기")]
    private static void Build()
    {
        Sprite icon = LoadIcon();
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            InGameHUD hud = root.GetComponentInChildren<InGameHUD>(true);
            InGameMenuUI menuUi = root.GetComponentInChildren<InGameMenuUI>(true);
            var playing = (new SerializedObject(hud).FindProperty("playingGroup").objectReferenceValue as GameObject)?.transform as RectTransform;
            var menuGroup = (new SerializedObject(menuUi).FindProperty("menuGroup").objectReferenceValue as GameObject)?.transform as RectTransform;
            if (playing == null || menuGroup == null)
            {
                Debug.LogWarning("[퀵챗 UI] InGameHUD Playing Group 또는 InGameMenuUI Menu Group이 비어 있어 중단");
                return;
            }

            BuildHud(playing, icon);
            BuildControls(menuGroup);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[퀵챗 UI] 하단 마이크 → 퀵챗 아이콘, ESC 메뉴 가운데에 조작법을 넣었습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // InGameBottomUIBuilder도 이걸 부른다 (전체를 다시 만들어도 마이크로 돌아가지 않게)
    internal static void BuildHud(RectTransform playing, Sprite icon = null)
    {
        if (icon == null) icon = LoadIcon();
        Remove(playing, OldHudName);
        Remove(playing, HudName);

        RectTransform group = Group(playing, HudName, new Vector2(0.5f, 0f));
        Img(group, "Icon", icon, new Vector2(-20f, 36f), new Vector2(34f, 30f));
        Img(group, "Key_V", Key("key_V"), new Vector2(18f, 36f), new Vector2(30f, 30f));
    }

    private static void BuildControls(RectTransform menuGroup)
    {
        Remove(menuGroup, ControlsName);

        TMP_Text style = null;
        Transform continueBtn = menuGroup.Find("Btn_CONTINUE");
        if (continueBtn != null) style = continueBtn.GetComponent<TMP_Text>();

        RectTransform group = Group(menuGroup, ControlsName, new Vector2(0.5f, 0.5f));

        TMP_Text title = Label(group, "Title", "CONTROLS", style, 24f, Gold, TextAlignmentOptions.Center,
                               new Vector2(0.5f, 0.5f), new Vector2(0f, 128f), new Vector2(330f, 40f));
        title.characterSpacing = 8f;

        Row(group, 0, "Switch", "key_A", "key_D");
        Row(group, 1, "Select", "key_W", "key_S");
        Row(group, 2, "Look", "key_space");
        Row(group, 3, "Throw", "key_E");
        Row(group, 4, "Call LIAR!", "key_X");
        Row(group, 5, "Emote", "key_1", "-", "key_5");
        Row(group, 6, "Quick Chat", "key_V");
    }

    // 키 이미지는 KeyRight에 오른쪽을 맞추고, 설명은 LabelLeft부터 왼쪽 정렬
    private static void Row(RectTransform group, int index, string label, params string[] keys)
    {
        float y = FirstRowY - index * RowStep;

        float width = 0f;
        foreach (string k in keys) width += KeyWidth(k) + 4f;
        float x = KeyRight - width + 4f;

        foreach (string k in keys)
        {
            float w = KeyWidth(k);
            if (k == "-")
                Label(group, "Dash", "-", null, 22f, MenuText, TextAlignmentOptions.Center,
                      new Vector2(0.5f, 0.5f), new Vector2(x + w * 0.5f, y), new Vector2(w, KeySize), false);
            else
                Img(group, k, Key(k), new Vector2(x + w * 0.5f, y), new Vector2(w, KeySize));
            x += w + 4f;
        }

        Label(group, "Label_" + label, label, null, 24f, MenuText, TextAlignmentOptions.MidlineLeft,
              new Vector2(0f, 0.5f), new Vector2(LabelLeft, y), new Vector2(190f, RowStep));
    }

    private static float KeyWidth(string key) => key == "key_space" ? 72f : key == "-" ? 14f : KeySize;

    private static TMP_Text Label(RectTransform parent, string name, string text, TMP_Text style, float size, Color color,
                                  TextAlignmentOptions align, Vector2 pivot, Vector2 pos, Vector2 rectSize, bool localized = true)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        SetRect(rt, new Vector2(0.5f, 0.5f), pivot, pos, rectSize);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        TMP_Text source = style != null ? style : FindStyle(parent);
        if (source != null)
        {
            tmp.font = source.font;
            tmp.fontSharedMaterial = source.fontSharedMaterial;
        }
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;   // Ellipsis면 한글(대체 폰트) 줄 높이가 칸보다 커서 글자가 통째로 사라진다
        tmp.raycastTarget = false;
        if (localized) go.AddComponent<LocalizedText>();
        return tmp;
    }

    private static TMP_Text FindStyle(Transform from)
    {
        Transform group = from;
        while (group != null && group.name != "MenuGroup") group = group.parent;
        Transform btn = group != null ? group.Find("Btn_CONTINUE") : null;
        return btn != null ? btn.GetComponent<TMP_Text>() : null;
    }

    // 새로 넣은 PNG는 Default 텍스처로 들어오므로 스프라이트로 바꿔 둔다
    private static Sprite LoadIcon()
    {
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[퀵챗 UI] 아이콘 없음: {IconPath}");
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static Sprite Key(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KeyPath + name + ".png");
        if (sprite == null) Debug.LogWarning($"[퀵챗 UI] 키 스프라이트 없음: {name}");
        return sprite;
    }

    private static RectTransform Group(RectTransform parent, string name, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static Image Img(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        SetRect(rt, parent.pivot, new Vector2(0.5f, 0.5f), pos, size);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    private static void Remove(Transform parent, string name)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            if (parent.GetChild(i).name == name) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
