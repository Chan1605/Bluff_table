using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > LiarsBar > Build InGame UI
// 원작 인게임 레이아웃(HUD + ESC 패널)을 InGameUI 프리팹으로 만든다. 다시 실행하면 프리팹을 새로 만든다.
// 열린 씬 중 Zino_Table이 있으면 프리팹을 하나 배치해 준다 (씬 저장 필요).
public static class InGameUIBuilder
{
    private const string PrefabPath = "Assets/2.Model/Prefabs/UI/InGameUI.prefab";
    private const string SettingPanelPath = "Assets/2.Model/Prefabs/UI/SettingPanel.prefab";
    private const string TableSceneName = "Zino_Table";
    private const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee";

    private static readonly Color Gold = new Color(0.93f, 0.82f, 0.45f, 1f);
    private static readonly Color MenuText = new Color(0.82f, 0.8f, 0.76f, 1f);
    private static readonly Color KeyBg = new Color(0.1f, 0.14f, 0.16f, 0.9f);

    private static TMP_FontAsset font;
    private static Sprite uiSprite, knobSprite;

    [MenuItem("Tools/LiarsBar/Build InGame UI")]
    private static void Build()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        GameObject settingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingPanelPath);
        if (font == null || settingPrefab == null)
        {
            Debug.LogError("[InGameUIBuilder] 폰트 또는 SettingPanel 프리팹을 찾지 못했습니다.");
            return;
        }

        // 열린 씬을 건드리지 않도록 미리보기 씬에서 만든 뒤 프리팹으로 저장
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = BuildRoot(preview, settingPrefab);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        PlaceInTableScene();
        Debug.Log($"[InGameUIBuilder] {PrefabPath} 를 만들었습니다.");
    }

    private static GameObject BuildRoot(Scene scene, GameObject settingPrefab)
    {
        var root = new GameObject("InGameUI", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 5;

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<UIInputController>();   // ESC로 메뉴 닫기 (팀원 스크립트)

        RectTransform rt = (RectTransform)root.transform;

        // ---------------- 게임 중 HUD
        RectTransform playing = Stretch(rt, "Playing");

        // 왼쪽 위: 테이블 + 선언
        RectTransform table = Group(playing, "Table", new Vector2(0f, 1f));
        TMP_Text title = Text(table, "Title", "", 54f, Color.white, TextAlignmentOptions.MidlineLeft, new Vector2(57f, -6f), new Vector2(520f, 62f), new Vector2(0f, 1f));
        Bar(table, "Line", new Vector2(0f, -66f), new Vector2(328f, 2f), new Vector2(0f, 0.5f), Gold);
        Image portrait = Box(table, "Portrait", new Vector2(57f, -77f), new Vector2(84f, 100f), new Vector2(0f, 1f), new Color(0.3f, 0.3f, 0.3f, 1f));
        TMP_Text claimName = Text(table, "ClaimName", "", 32f, Color.white, TextAlignmentOptions.Center, new Vector2(160f, -78f), new Vector2(210f, 40f), new Vector2(0f, 1f));
        TMP_Text claimVerb = Text(table, "ClaimVerb", "", 16f, Color.white, TextAlignmentOptions.Center, new Vector2(160f, -117f), new Vector2(210f, 22f), new Vector2(0f, 1f));
        TMP_Text claimValue = Text(table, "ClaimValue", "", 34f, Color.white, TextAlignmentOptions.Center, new Vector2(160f, -136f), new Vector2(210f, 44f), new Vector2(0f, 1f));

        // 가운데: 다른 사람 선언 팝업
        RectTransform popupRt = Group(playing, "ClaimPopup", new Vector2(0.5f, 0.5f));
        CanvasGroup popup = popupRt.gameObject.AddComponent<CanvasGroup>();
        popup.alpha = 0f;
        popup.blocksRaycasts = false;
        TMP_Text popupName = Text(popupRt, "Name", "", 48f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 78f), new Vector2(700f, 64f), new Vector2(0.5f, 0.5f));
        Bar(popupRt, "LineTop", new Vector2(0f, 43f), new Vector2(420f, 2f), new Vector2(0.5f, 0.5f), Gold);
        TMP_Text popupValue = Text(popupRt, "Value", "", 60f, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(700f, 80f), new Vector2(0.5f, 0.5f));
        Bar(popupRt, "LineBottom", new Vector2(0f, -42f), new Vector2(420f, 2f), new Vector2(0.5f, 0.5f), Gold);

        // 위 가운데: 내 차례 타이머
        RectTransform timerRt = Group(playing, "TurnTimer", new Vector2(0.5f, 1f));
        TMP_Text timerText = Ring(timerRt, new Vector2(0f, -62f), 76f, 40f);

        // 오른쪽 위: Your Bid + 조작 안내
        RectTransform myTurn = Group(playing, "MyTurn", new Vector2(1f, 1f));
        Localized(Text(myTurn, "YourBid", "Your Bid", 40f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(-128f, -12f), new Vector2(320f, 56f), new Vector2(1f, 1f)));
        TMP_Text chamber = Text(myTurn, "Chamber", "(0/6)", 36f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(-20f, -12f), new Vector2(110f, 56f), new Vector2(1f, 1f));
        chamber.fontStyle = FontStyles.Italic;
        Bar(myTurn, "Line", new Vector2(0f, -66f), new Vector2(350f, 2f), new Vector2(1f, 0.5f), Gold);
        (string label, string key, float width)[] hints =
        {
            ("Switch", "A  D", 54f), ("Select", "SPACE", 70f), ("Throw", "E", 30f), ("Call LIAR!", "X", 30f),
        };
        for (int i = 0; i < hints.Length; i++)
        {
            float y = -78f - i * 31f;
            Localized(Text(myTurn, "Hint_" + hints[i].label, hints[i].label, 24f, Color.white, TextAlignmentOptions.MidlineRight,
                           new Vector2(-30f - hints[i].width, y), new Vector2(220f, 30f), new Vector2(1f, 1f)));
            Key(myTurn, hints[i].key, new Vector2(-20f, y - 2f), hints[i].width);
        }

        // 오른쪽 아래: 내 차례가 아닐 때 안내
        RectTransform idle = Group(playing, "IdleHints", new Vector2(1f, 0f));
        Localized(Text(idle, "Look", "Look", 32f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(-124f, 40f), new Vector2(200f, 44f), new Vector2(1f, 0f)));
        Key(idle, "SPACE", new Vector2(-40f, 84f), 74f);
        Localized(Text(idle, "Emote", "Emote", 26f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(-124f, 92f), new Vector2(200f, 40f), new Vector2(1f, 0f)));
        Key(idle, "1 - 5", new Vector2(-40f, 130f), 74f);

        // ---------------- 결과
        RectTransform result = Stretch(rt, "Result");
        RectTransform resultTop = Group(result, "Returning", new Vector2(1f, 1f));
        Localized(Text(resultTop, "Label", "RETURNING TO LOBBY", 40f, Color.white, TextAlignmentOptions.MidlineRight, new Vector2(-118f, -24f), new Vector2(700f, 56f), new Vector2(1f, 1f)));
        TMP_Text resultTimer = Ring(resultTop, new Vector2(-58f, -52f), 62f, 34f);
        RectTransform winner = Group(result, "Winner", new Vector2(0.5f, 0.5f));
        Localized(Text(winner, "Label", "WINNER", 28f, Gold, TextAlignmentOptions.Center, new Vector2(560f, 330f), new Vector2(500f, 36f), new Vector2(0.5f, 0.5f)));
        TMP_Text winnerName = Text(winner, "Name", "", 48f, Color.white, TextAlignmentOptions.Center, new Vector2(560f, 280f), new Vector2(600f, 64f), new Vector2(0.5f, 0.5f));
        result.gameObject.SetActive(false);

        // ---------------- ESC 메뉴
        RectTransform menu = Stretch(rt, "Menu");
        Image dim = menu.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);   // 뒤 화면 어둡게 + 클릭 차단

        RectTransform menuGroup = Group(menu, "MenuGroup", new Vector2(0.5f, 0.5f));
        Button continueBtn = MenuButton(menuGroup, "CONTINUE", 72f);
        Button settingsBtn = MenuButton(menuGroup, "SETTINGS", 0f);
        Button leaveBtn = MenuButton(menuGroup, "LEAVE", -72f);
        Bar(menuGroup, "LineLeft", new Vector2(-182f, 0f), new Vector2(3f, 300f), new Vector2(0.5f, 0.5f), Gold);
        Bar(menuGroup, "LineRight", new Vector2(183f, 0f), new Vector2(3f, 300f), new Vector2(0.5f, 0.5f), Gold);

        // 타이틀과 같은 세팅 패널 (패널 매니저 없이 BackRequested로 복귀)
        var settingsGo = (GameObject)PrefabUtility.InstantiatePrefab(settingPrefab, scene);
        RectTransform settingsRt = (RectTransform)settingsGo.transform;
        settingsRt.SetParent(menu, false);
        settingsRt.anchorMin = Vector2.zero;
        settingsRt.anchorMax = Vector2.one;
        settingsRt.offsetMin = settingsRt.offsetMax = Vector2.zero;
        settingsGo.SetActive(false);

        // ---------------- 연결
        InGameHUD hud = root.AddComponent<InGameHUD>();
        Assign(hud, ("playingGroup", playing.gameObject), ("resultGroup", result.gameObject),
               ("tableTitle", title), ("portrait", portrait), ("claimName", claimName), ("claimVerb", claimVerb), ("claimValue", claimValue),
               ("popup", popup), ("popupName", popupName), ("popupValue", popupValue),
               ("myTurnGroup", myTurn.gameObject), ("timerGroup", timerRt.gameObject), ("timerText", timerText), ("chamberText", chamber),
               ("idleHints", idle.gameObject), ("resultTimerText", resultTimer), ("winnerText", winnerName));

        InGameMenuUI menuUi = root.AddComponent<InGameMenuUI>();
        Assign(menuUi, ("root", menu.gameObject), ("menuGroup", menuGroup.gameObject), ("continueBtn", continueBtn),
               ("settingsBtn", settingsBtn), ("leaveBtn", leaveBtn), ("settings", settingsGo.GetComponent<SettingsMenuUI>()));

        menu.gameObject.SetActive(false);
        myTurn.gameObject.SetActive(false);
        timerRt.gameObject.SetActive(false);

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (!PrefabUtility.IsPartOfPrefabInstance(t)) t.gameObject.layer = 5;
        return root;
    }

    private static void PlaceInTableScene()
    {
        Scene table = SceneManager.GetSceneByName(TableSceneName);
        if (!table.IsValid() || !table.isLoaded)
        {
            Debug.Log($"[InGameUIBuilder] {TableSceneName} 씬이 열려 있지 않아 배치는 건너뜁니다. 프리팹을 씬에 직접 넣어 주세요.");
            return;
        }

        foreach (GameObject go in table.GetRootGameObjects())
            if (go.GetComponent<InGameHUD>() != null) return;   // 이미 있음 (프리팹이 갱신되면 같이 바뀜)

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, table);
        Undo.RegisterCreatedObjectUndo(instance, "Place InGame UI");
        EditorSceneManager.MarkSceneDirty(table);
        Debug.Log($"[InGameUIBuilder] {TableSceneName}에 InGameUI를 배치했습니다. 씬을 저장하세요.");
    }

    // ---------------------------------------------------------------- Parts

    private static RectTransform Stretch(RectTransform parent, string name)
    {
        RectTransform rt = Child(parent, name);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    // 화면 모서리 기준 묶음 (크기 0, 자식은 이 점 기준 좌표)
    private static RectTransform Group(RectTransform parent, string name, Vector2 anchor)
    {
        RectTransform rt = Child(parent, name);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private static TMP_Text Text(RectTransform parent, string name, string text, float size, Color color,
                                 TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta, Vector2 pivot)
    {
        RectTransform rt = Child(parent, name);
        Place(rt, pivot, pos, sizeDelta);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    private static TMP_Text Localized(TMP_Text text)
    {
        text.gameObject.AddComponent<LocalizedText>();
        return text;
    }

    private static Image Bar(RectTransform parent, string name, Vector2 pos, Vector2 size, Vector2 pivot, Color color)
    {
        Image image = Box(parent, name, pos, size, pivot, color);
        return image;
    }

    private static Image Box(RectTransform parent, string name, Vector2 pos, Vector2 size, Vector2 pivot, Color color)
    {
        RectTransform rt = Child(parent, name);
        Place(rt, pivot, pos, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // 원형 테두리 + 숫자 (타이머)
    private static TMP_Text Ring(RectTransform parent, Vector2 pos, float size, float fontSize)
    {
        Image ring = Box(parent, "Ring", pos, new Vector2(size, size), new Vector2(0.5f, 0.5f), new Color(0f, 0f, 0f, 0.35f));
        ring.sprite = knobSprite;
        Outline outline = ring.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_Text text = Text((RectTransform)ring.transform, "Value", "", fontSize, Color.white, TextAlignmentOptions.Center,
                             Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
        RectTransform trt = text.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        return text;
    }

    // 키 안내 상자 (예: SPACE)
    private static void Key(RectTransform parent, string label, Vector2 pos, float width)
    {
        Image box = Box(parent, "Key_" + label, pos, new Vector2(width, 26f), new Vector2(1f, 1f), KeyBg);
        box.sprite = uiSprite;
        box.type = Image.Type.Sliced;
        Outline outline = box.gameObject.AddComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        TMP_Text text = Text((RectTransform)box.transform, "Label", label, 16f, Gold, TextAlignmentOptions.Center,
                             Vector2.zero, new Vector2(width, 26f), new Vector2(0.5f, 0.5f));
        RectTransform trt = text.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    private static Button MenuButton(RectTransform parent, string label, float y)
    {
        TMP_Text text = Text(parent, "Btn_" + label, label, 46f, Color.white, TextAlignmentOptions.Center,
                             new Vector2(-345f, y), new Vector2(320f, 64f), new Vector2(0.5f, 0.5f));
        text.raycastTarget = true;
        Localized(text);

        Button button = text.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        ColorBlock colors = button.colors;
        colors.normalColor = MenuText;
        colors.highlightedColor = Color.white;
        colors.selectedColor = MenuText;
        colors.pressedColor = Gold;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        return button;
    }

    private static RectTransform Child(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 부모 묶음의 점(anchor) 기준으로 배치. pivot = 이 오브젝트의 기준점
    private static void Place(RectTransform rt, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        RectTransform parent = (RectTransform)rt.parent;
        Vector2 anchor = parent != null ? parent.pivot : new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Assign(Object target, params (string field, Object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, Object value) in values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError($"[InGameUIBuilder] {target.GetType().Name}.{field} 필드를 찾지 못했습니다.");
            else p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
