using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메뉴 한 번으로 세팅 패널 내용(원작 레이아웃)과 호스트 패널의 로비 타입 드롭다운을 만든다.
// 만든 오브젝트는 이름이 "[Gen] "으로 시작하고, 다시 실행하면 지우고 새로 만든다. (기존 틀은 그대로 둠)
// - 세팅 패널: SettingPanel 프리팹을 수정 (열린 씬의 인스턴스가 덮어쓴 tabs 값은 되돌림)
// - 로비 타입: TitleScene 안의 HostPanel(프리팹 연결이 풀린 씬 오브젝트)을 수정 → TitleScene을 열고 실행 후 저장
public static class SettingsPanelBuilder
{
    private const string SettingPanelPath = "Assets/2.Model/Prefabs/UI/SettingPanel.prefab";
    private const string FontGuid = "8f586378b4e144a9851e7b34d9b748ee";
    private const string Gen = "[Gen] ";

    // 원작 1920x1080 기준 좌표를 탭 버튼 기준으로 옮긴 값
    private const float LabelFromTab = 450f;      // 탭 글자 왼쪽 → 항목 이름 왼쪽
    private const float RowStep = 60f;
    private const float RowWidth = 760f;
    private const float SelectorX = 623f;         // 항목 이름 왼쪽 기준 값 중앙
    private const float ArrowOffset = 103f;
    private const float SliderX = 660f;
    private const float SliderValueRight = 585f;
    private const float SwitchX = 657f;
    private const float SwitchTextRight = 615f;

    private static readonly Color LabelColor = new Color(0.78f, 0.76f, 0.72f, 1f);
    private static readonly Color ValueColor = Color.white;
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.35f);

    private static TMP_FontAsset font;
    private static Sprite uiSprite, bgSprite, knobSprite, checkSprite, arrowSprite, maskSprite;

    [MenuItem("Tools/LiarsBar/Build Settings Panel")]
    private static void BuildSettingsPanel()
    {
        if (!LoadAssets()) return;

        GameObject root = PrefabUtility.LoadPrefabContents(SettingPanelPath);
        try
        {
            if (BuildSettings(root))
            {
                PrefabUtility.SaveAsPrefabAsset(root, SettingPanelPath);
                RevertSceneOverrides();
                Debug.Log("[SettingsPanelBuilder] 세팅 패널을 만들었습니다.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/LiarsBar/Build Host Lobby Type")]
    private static void BuildHostLobbyType()
    {
        if (!LoadAssets()) return;

        HostMenuUI host = FindInOpenScenes<HostMenuUI>();
        if (host == null)
        {
            Debug.LogError("[SettingsPanelBuilder] 열린 씬에서 HostMenuUI를 찾지 못했습니다. TitleScene을 열고 다시 실행하세요.");
            return;
        }

        if (BuildHost(host))
        {
            EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
            Debug.Log("[SettingsPanelBuilder] 로비 타입 드롭다운을 만들었습니다. 씬을 저장하세요.", host);
        }
    }

    // 씬의 SettingPanel 인스턴스가 tabs / sections를 덮어쓰고 있으면 프리팹 값으로 되돌린다 (panelManager 연결은 유지)
    private static void RevertSceneOverrides()
    {
        foreach (SettingsMenuUI menu in Resources.FindObjectsOfTypeAll<SettingsMenuUI>())
        {
            if (EditorUtility.IsPersistent(menu) || !menu.gameObject.scene.IsValid()) continue;
            if (!PrefabUtility.IsPartOfPrefabInstance(menu)) continue;

            var so = new SerializedObject(menu);
            foreach (string field in new[] { "tabs", "sections", "headerTabText", "headerLine", "applyBtn", "restoreBtn", "backBtn" })
            {
                SerializedProperty p = so.FindProperty(field);
                if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.UserAction);
            }
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            Debug.Log($"[SettingsPanelBuilder] 씬 '{menu.gameObject.scene.name}'의 세팅 패널 덮어쓰기를 되돌렸습니다. 씬을 저장하세요.", menu);
        }
    }

    private static T FindInOpenScenes<T>() where T : Component
    {
        foreach (T c in Resources.FindObjectsOfTypeAll<T>())
            if (!EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid()) return c;
        return null;
    }

    // ------------------------------------------------------------------ Settings

    private static bool BuildSettings(GameObject root)
    {
        RemoveGenerated(root.transform);

        RectTransform group = Find(root.transform, "SettingGroup");
        RectTransform settingText = Find(root.transform, "SettingText");
        RectTransform restoreBtn = Find(root.transform, "ReStoreBtn");
        RectTransform backBtn = Find(root.transform, "BackBtn");
        RectTransform[] tabButtons =
        {
            Find(root.transform, "displayBtn"), Find(root.transform, "SettingBtn"),
            Find(root.transform, "GrapicBtn"), Find(root.transform, "AudioBtn"),
        };
        if (group == null || settingText == null || restoreBtn == null || backBtn == null || System.Array.IndexOf(tabButtons, null) >= 0)
        {
            Debug.LogError("[SettingsPanelBuilder] SettingPanel 안에서 필요한 오브젝트를 찾지 못했습니다 (SettingGroup, SettingText, 탭 버튼 4개, BackBtn, ReStoreBtn).");
            return false;
        }

        // 기준 좌표: 첫 탭 버튼의 왼쪽 끝과 세로 중앙 (SettingGroup 좌표)
        GetRect(group, tabButtons[0], out float tabLeft, out float tabCenterY);
        float labelX = tabLeft + LabelFromTab;
        float rowY0 = tabCenterY;

        // 헤더: SETTINGS 옆 탭 이름 + 오른쪽 가로선
        TMP_Text headerTab = Object.Instantiate(settingText.gameObject, settingText.parent).GetComponent<TMP_Text>();
        headerTab.name = Gen + "HeaderTab";
        headerTab.text = string.Empty;
        headerTab.color = Color.white;
        RectTransform headerRt = headerTab.rectTransform;
        float oldWidth = headerRt.sizeDelta.x;
        headerRt.sizeDelta = new Vector2(500f, headerRt.sizeDelta.y);
        headerRt.anchoredPosition += new Vector2(200f + (500f - oldWidth) * 0.5f, 0f);
        headerTab.textWrappingMode = TextWrappingModes.NoWrap;

        GetRect(group, settingText, out _, out float headerY);
        RectTransform headerLine = Line(group, Gen + "HeaderLine", new Vector2(0f, headerY), 100f, 2f);

        // 탭 내용
        RectTransform contents = Child(group, Gen + "Contents");
        Place(contents, Center, Center, Vector2.zero, Vector2.zero);

        GameObject display = BuildDisplay(contents, labelX, rowY0);
        GameObject gameplay = BuildGameplay(contents, labelX, rowY0);
        GameObject graphics = BuildGraphics(contents, labelX, rowY0, tabLeft);
        GameObject audio = BuildAudio(contents, labelX, rowY0);
        GameObject[] tabContents = { display, gameplay, graphics, audio };
        foreach (GameObject content in tabContents) content.SetActive(false);

        // Apply: Restore Defaults 자리 (탭을 열면 Apply, 목록에서는 Restore Defaults)
        GameObject apply = Object.Instantiate(restoreBtn.gameObject, restoreBtn.parent);
        apply.name = Gen + "ApplyBtn";
        TMP_Text applyText = apply.GetComponentInChildren<TMP_Text>(true);
        if (applyText != null) applyText.text = "Apply";
        apply.SetActive(false);

        // SettingsMenuUI 연결
        SettingsMenuUI menu = root.GetComponent<SettingsMenuUI>();
        if (menu == null) menu = root.AddComponent<SettingsMenuUI>();

        var so = new SerializedObject(menu);
        so.FindProperty("headerTabText").objectReferenceValue = headerTab;
        so.FindProperty("settingsTitle").objectReferenceValue = settingText.GetComponent<TMP_Text>();
        so.FindProperty("headerLine").objectReferenceValue = headerLine;
        so.FindProperty("headerLineRight").floatValue = tabLeft + 1673f;   // 원작: 화면 오른쪽 끝 근처까지
        so.FindProperty("backBtn").objectReferenceValue = backBtn.GetComponent<Button>();
        so.FindProperty("applyBtn").objectReferenceValue = apply.GetComponent<Button>();
        so.FindProperty("restoreBtn").objectReferenceValue = restoreBtn.GetComponent<Button>();

        string[] titles = { "DISPLAY", "GAMEPLAY", "GRAPHICS", "AUDIO" };
        SerializedProperty tabs = so.FindProperty("tabs");
        tabs.arraySize = titles.Length;
        for (int i = 0; i < titles.Length; i++)
        {
            SerializedProperty tab = tabs.GetArrayElementAtIndex(i);
            tab.FindPropertyRelative("title").stringValue = titles[i];
            tab.FindPropertyRelative("button").objectReferenceValue = tabButtons[i].GetComponent<Button>();
            tab.FindPropertyRelative("underline").objectReferenceValue = tabButtons[i].GetComponent<HoverUnderline>();
            tab.FindPropertyRelative("content").objectReferenceValue = tabContents[i];
        }

        MonoBehaviour[] sections =
        {
            display.GetComponent<DisplaySettingsUI>(), gameplay.GetComponent<GameplaySettingsUI>(),
            graphics.GetComponent<GraphicsSettingsUI>(), audio.GetComponent<AudioSettingsUI>(),
        };
        SerializedProperty sectionList = so.FindProperty("sections");
        sectionList.arraySize = sections.Length;
        for (int i = 0; i < sections.Length; i++)
            sectionList.GetArrayElementAtIndex(i).objectReferenceValue = sections[i];

        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayer(root.transform, root.layer);
        return true;
    }

    private static GameObject BuildDisplay(RectTransform contents, float labelX, float y0)
    {
        RectTransform content = Content(contents, "Display");
        DisplaySettingsUI ui = content.gameObject.AddComponent<DisplaySettingsUI>();

        ArrowSelector resolution = Selector(Row(content, "Resolution", labelX, y0, null, null));
        ArrowSelector vSync = Selector(Row(content, "VSync", labelX, y0 - RowStep, null, null));
        ArrowSelector maxFps = Selector(Row(content, "Max Fps", labelX, y0 - RowStep * 2, null, null));
        Toggle window = Switch(Row(content, "Window Mode", labelX, y0 - RowStep * 3, null, null), out TMP_Text windowText);
        Slider brightness = SliderRow(Row(content, "Brightness", labelX, y0 - RowStep * 4, null, null), 0.5f, 1.5f, false, 1f, out TMP_Text brightnessText);

        Assign(ui, ("resolution", resolution), ("vSync", vSync), ("maxFps", maxFps), ("windowMode", window),
               ("windowModeText", windowText), ("brightness", brightness), ("brightnessText", brightnessText));
        return content.gameObject;
    }

    private static GameObject BuildGameplay(RectTransform contents, float labelX, float y0)
    {
        RectTransform content = Content(contents, "Gameplay");
        GameplaySettingsUI ui = content.gameObject.AddComponent<GameplaySettingsUI>();

        ArrowSelector language = Selector(Row(content, "Language", labelX, y0, null, null));
        Slider sensitivity = SliderRow(Row(content, "Mouse Sensitivity", labelX, y0 - RowStep, null, null), 0f, 100f, true, 50f, out TMP_Text sensitivityText);
        ArrowSelector controller = Selector(Row(content, "Controller", labelX, y0 - RowStep * 2, null, null));

        Assign(ui, ("language", language), ("mouseSensitivity", sensitivity),
               ("mouseSensitivityText", sensitivityText), ("controller", controller));
        return content.gameObject;
    }

    private static GameObject BuildGraphics(RectTransform contents, float labelX, float y0, float tabLeft)
    {
        RectTransform content = Content(contents, "Graphics");
        GraphicsSettingsUI ui = content.gameObject.AddComponent<GraphicsSettingsUI>();

        // 원작: 프리셋 줄은 위쪽(탭보다 196 위), 세부 항목은 탭보다 76 위부터 시작
        float presetY = y0 + 196f;
        float detailY = y0 + 76f;

        TMP_Text hint = Text(content, "Hint", string.Empty, 28f, ValueColor, TextAlignmentOptions.TopLeft,
                             Center, new Vector2(0f, 1f), new Vector2(tabLeft + 1258f, presetY + 22f), new Vector2(430f, 200f));
        hint.textWrappingMode = TextWrappingModes.Normal;

        ArrowSelector preset = Selector(Row(content, "Quality Presets", labelX, presetY, hint, "Sets all graphics options below at once."));
        Line(content, "PresetLine", new Vector2(labelX + 3f, presetY - 28f), 753f, 2f);

        string[] labels = { "Quality", "Anti-Aliasing", "Anti-Aliasing Quality", "Texture Quality", "Shadow Quality", "Fog" };
        string[] hints =
        {
            "Overall rendering quality level.",
            "Smooths jagged edges. TAA looks best, FXAA is fastest.",
            "Quality of the anti-aliasing filter.",
            "Changes how sharp and detailed surfaces look.",
            "Quality of shadows cast by lights. Off disables shadows.",
            "Atmospheric fog in the scene.",
        };
        var selectors = new ArrowSelector[labels.Length];
        for (int i = 0; i < labels.Length; i++)
            selectors[i] = Selector(Row(content, labels[i], labelX, detailY - RowStep * i, hint, hints[i]));

        float toggleY = detailY - RowStep * labels.Length;
        Toggle filmGrain = Switch(Row(content, "Film Grain", labelX, toggleY, hint, "Adds a subtle film grain over the image."), out _);
        Toggle panini = Switch(Row(content, "Panini Projection", labelX, toggleY - RowStep, hint, "Reduces stretching at the edges of the screen."), out _);
        Toggle vignette = Switch(Row(content, "Vignette", labelX, toggleY - RowStep * 2, hint, "Darkens the corners of the screen."), out _);

        Assign(ui, ("preset", preset), ("quality", selectors[0]), ("antiAliasing", selectors[1]),
               ("antiAliasingQuality", selectors[2]), ("texture", selectors[3]), ("shadow", selectors[4]), ("fog", selectors[5]),
               ("filmGrain", filmGrain), ("panini", panini), ("vignette", vignette));
        return content.gameObject;
    }

    private static GameObject BuildAudio(RectTransform contents, float labelX, float y0)
    {
        RectTransform content = Content(contents, "Audio");
        AudioSettingsUI ui = content.gameObject.AddComponent<AudioSettingsUI>();

        Slider main = SliderOnly(Row(content, "Main Volume", labelX, y0, null, null));
        Slider music = SliderOnly(Row(content, "Music Volume", labelX, y0 - RowStep, null, null));
        Slider dialogue = SliderOnly(Row(content, "Dialogue Volume", labelX, y0 - RowStep * 2, null, null));

        Assign(ui, ("mainVolume", main), ("musicVolume", music), ("dialogueVolume", dialogue));
        return content.gameObject;
    }

    // ------------------------------------------------------------------ Host

    private static bool BuildHost(HostMenuUI host)
    {
        GameObject root = host.gameObject;
        RemoveGenerated(root.transform, true);

        RectTransform typeBg = Find(root.transform, "TypeBG");
        if (typeBg == null)
        {
            Debug.LogError("[SettingsPanelBuilder] HostPanel에서 TypeBG를 찾지 못했습니다.", host);
            return false;
        }

        // 기존 'Public' 입력창 제거 (HostMenuUI가 참조하지 않는 오브젝트). Ctrl+Z로 되돌릴 수 있음
        for (int i = typeBg.childCount - 1; i >= 0; i--)
        {
            Transform child = typeBg.GetChild(i);
            if (child.GetComponent<TMP_InputField>() != null) Undo.DestroyObjectImmediate(child.gameObject);
        }

        var res = new TMP_DefaultControls.Resources
        {
            standard = uiSprite, background = bgSprite, inputField = bgSprite, knob = knobSprite,
            checkmark = checkSprite, dropdown = arrowSprite, mask = maskSprite,
        };
        GameObject go = TMP_DefaultControls.CreateDropdown(res);
        go.name = Gen + "LobbyTypeDropdown";
        Undo.RegisterCreatedObjectUndo(go, "Build Lobby Type");
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(typeBg, false);
        Place(rt, Center, Center, Vector2.zero, new Vector2(328.8f, 57.8f));

        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        go.GetComponent<Image>().color = Color.black;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "Public", "Private" });
        dropdown.SetValueWithoutNotify(0);

        StyleText(dropdown.captionText, 36f, TextAlignmentOptions.Left);
        dropdown.captionText.rectTransform.offsetMin = new Vector2(20f, 0f);
        StyleText(dropdown.itemText, 32f, TextAlignmentOptions.Left);

        Transform arrow = rt.Find("Arrow");
        if (arrow != null) arrow.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f, 1f);

        RectTransform template = dropdown.template;
        template.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.97f);
        template.sizeDelta = new Vector2(0f, 130f);
        Transform item = template.Find("Viewport/Content/Item");
        if (item != null)
        {
            ((RectTransform)item).sizeDelta = new Vector2(0f, 56f);
            Image itemBg = item.Find("Item Background")?.GetComponent<Image>();
            if (itemBg != null) itemBg.color = new Color(0.12f, 0.12f, 0.12f, 1f);

            Toggle toggle = item.GetComponent<Toggle>();
            ColorBlock colors = toggle.colors;
            colors.highlightedColor = new Color(0.35f, 0.3f, 0.22f, 1f);
            colors.selectedColor = new Color(0.25f, 0.22f, 0.17f, 1f);
            toggle.colors = colors;
        }
        RectTransform contentRt = template.Find("Viewport/Content") as RectTransform;
        if (contentRt != null) contentRt.sizeDelta = new Vector2(0f, 56f);

        var so = new SerializedObject(host);
        so.FindProperty("lobbyTypeDropdown").objectReferenceValue = dropdown;
        so.ApplyModifiedProperties();

        SetLayer(go.transform, root.layer);
        return true;
    }

    // ------------------------------------------------------------------ Parts

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 LeftMid = new Vector2(0f, 0.5f);

    private static RectTransform Content(RectTransform parent, string name)
    {
        RectTransform content = Child(parent, Gen + name);
        Place(content, Center, Center, Vector2.zero, Vector2.zero);
        return content;
    }

    // 한 줄: 투명 배경(호버 감지) + 항목 이름. 오른쪽 컨트롤은 줄 왼쪽 끝 기준 x로 붙인다.
    private static RectTransform Row(RectTransform content, string label, float x, float y, TMP_Text hint, string description)
    {
        RectTransform row = Child(content, "Row_" + label);
        Place(row, Center, LeftMid, new Vector2(x, y), new Vector2(RowWidth, 56f));
        row.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        TMP_Text labelText = Text(row, "Label", label, 30f, LabelColor, TextAlignmentOptions.MidlineLeft,
                                  LeftMid, LeftMid, Vector2.zero, new Vector2(440f, 56f));

        SettingsRowHint rowHint = row.gameObject.AddComponent<SettingsRowHint>();
        var so = new SerializedObject(rowHint);
        so.FindProperty("label").objectReferenceValue = labelText;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("description").stringValue = description ?? string.Empty;
        so.FindProperty("normalColor").colorValue = LabelColor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    private static ArrowSelector Selector(RectTransform row)
    {
        RectTransform root = Child(row, "Selector");
        Place(root, LeftMid, Center, new Vector2(SelectorX, 0f), new Vector2(280f, 56f));
        ArrowSelector selector = root.gameObject.AddComponent<ArrowSelector>();

        Button left = ArrowButton(root, "Left", "<", -ArrowOffset);
        Button right = ArrowButton(root, "Right", ">", ArrowOffset);
        TMP_Text value = Text(root, "Value", "-", 26f, ValueColor, TextAlignmentOptions.Center,
                              Center, Center, Vector2.zero, new Vector2(170f, 56f));

        Assign(selector, ("leftBtn", left), ("rightBtn", right), ("valueText", value));
        return selector;
    }

    private static Button ArrowButton(RectTransform parent, string name, string glyph, float x)
    {
        TMP_Text text = Text(parent, name, glyph, 34f, ValueColor, TextAlignmentOptions.Center,
                             Center, Center, new Vector2(x, 2f), new Vector2(44f, 56f));
        text.raycastTarget = true;

        Button button = text.gameObject.AddComponent<Button>();
        button.targetGraphic = text;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        return button;
    }

    private static Slider SliderRow(RectTransform row, float min, float max, bool whole, float value, out TMP_Text valueText)
    {
        valueText = Text(row, "Value", value.ToString(), 26f, ValueColor, TextAlignmentOptions.MidlineRight,
                         LeftMid, new Vector2(1f, 0.5f), new Vector2(SliderValueRight, 0f), new Vector2(90f, 56f));
        Slider slider = SliderOnly(row);
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.value = value;
        return slider;
    }

    private static Slider SliderOnly(RectTransform row)
    {
        var res = new DefaultControls.Resources { standard = uiSprite, background = bgSprite, knob = knobSprite };
        GameObject go = DefaultControls.CreateSlider(res);
        go.name = "Slider";
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(row, false);
        Place(rt, LeftMid, Center, new Vector2(SliderX, 0f), new Vector2(154f, 24f));

        Slider slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        Navigation nav = slider.navigation;
        nav.mode = Navigation.Mode.None;
        slider.navigation = nav;

        // 원작처럼 가는 막대 + 흰 손잡이
        Restyle(rt.Find("Background"), new Color(0.45f, 0.45f, 0.45f, 0.85f), new Vector2(0f, 0.38f), new Vector2(1f, 0.62f));
        Transform fillArea = rt.Find("Fill Area");
        if (fillArea != null)
        {
            ((RectTransform)fillArea).anchorMin = new Vector2(0f, 0.38f);
            ((RectTransform)fillArea).anchorMax = new Vector2(1f, 0.62f);
            Restyle(fillArea.Find("Fill"), new Color(0.88f, 0.88f, 0.88f, 1f), null, null);
        }
        Transform handle = rt.Find("Handle Slide Area/Handle");
        if (handle != null)
        {
            handle.GetComponent<Image>().color = Color.white;
            ((RectTransform)handle).sizeDelta = new Vector2(22f, 0f);
        }
        return slider;
    }

    private static Toggle Switch(RectTransform row, out TMP_Text stateText)
    {
        stateText = Text(row, "State", "ON", 26f, ValueColor, TextAlignmentOptions.MidlineRight,
                         LeftMid, new Vector2(1f, 0.5f), new Vector2(SwitchTextRight, 0f), new Vector2(70f, 56f));

        RectTransform root = Child(row, "Switch");
        Place(root, LeftMid, Center, new Vector2(SwitchX, 0f), new Vector2(60f, 30f));
        Image background = root.gameObject.AddComponent<Image>();
        background.sprite = uiSprite;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        RectTransform knob = Child(root, "Knob");
        Place(knob, Center, Center, new Vector2(15f, 0f), new Vector2(24f, 24f));
        Image knobImage = knob.gameObject.AddComponent<Image>();
        knobImage.sprite = knobSprite;
        knobImage.raycastTarget = false;

        Toggle toggle = root.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = null;
        toggle.isOn = true;
        Navigation nav = toggle.navigation;
        nav.mode = Navigation.Mode.None;
        toggle.navigation = nav;

        SwitchToggle sw = root.gameObject.AddComponent<SwitchToggle>();
        Assign(sw, ("knob", knob), ("stateText", stateText), ("knobGraphic", knobImage));
        return toggle;
    }

    private static RectTransform Line(RectTransform parent, string name, Vector2 pos, float width, float height)
    {
        RectTransform line = Child(parent, name);
        Place(line, Center, LeftMid, pos, new Vector2(width, height));
        Image image = line.gameObject.AddComponent<Image>();
        image.color = LineColor;
        image.raycastTarget = false;
        return line;
    }

    private static TMP_Text Text(RectTransform parent, string name, string text, float size, Color color,
                                 TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        RectTransform rt = Child(parent, name);
        Place(rt, anchor, pivot, pos, sizeDelta);
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

    private static void StyleText(TMP_Text text, float size, TextAlignmentOptions align)
    {
        if (text == null) return;
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void Restyle(Transform target, Color color, Vector2? anchorMin, Vector2? anchorMax)
    {
        if (target == null) return;
        target.GetComponent<Image>().color = color;
        var rt = (RectTransform)target;
        if (anchorMin.HasValue) rt.anchorMin = anchorMin.Value;
        if (anchorMax.HasValue) rt.anchorMax = anchorMax.Value;
    }

    // ------------------------------------------------------------------ Utils

    private static bool LoadAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        if (font == null)
        {
            Debug.LogError("[SettingsPanelBuilder] 세팅 패널 폰트(TMP Font Asset)를 찾지 못했습니다.");
            return false;
        }

        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        checkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        arrowSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd");
        maskSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
        return true;
    }

    private static void Assign(Object target, params (string field, Object value)[] values)
    {
        var so = new SerializedObject(target);
        foreach ((string field, Object value) in values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogError($"[SettingsPanelBuilder] {target.GetType().Name}.{field} 필드를 찾지 못했습니다.");
            else p.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Child(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    // 이름 앞뒤 공백 무시 (기존 오브젝트 이름에 공백이 섞여 있음)
    private static RectTransform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.Trim() == name) return t as RectTransform;
        return null;
    }

    // target의 왼쪽 끝 x와 세로 중앙 y를 space 좌표로
    private static void GetRect(RectTransform space, RectTransform target, out float left, out float centerY)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector3 bottomLeft = space.InverseTransformPoint(corners[0]);
        Vector3 topLeft = space.InverseTransformPoint(corners[1]);
        left = bottomLeft.x - space.rect.center.x;
        centerY = (bottomLeft.y + topLeft.y) * 0.5f - space.rect.center.y;
    }

    private static void RemoveGenerated(Transform root, bool undo = false)
    {
        var targets = new List<GameObject>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(Gen)) targets.Add(t.gameObject);

        foreach (GameObject go in targets)
        {
            if (go == null) continue;
            if (undo) Undo.DestroyObjectImmediate(go);
            else Object.DestroyImmediate(go);
        }
    }

    private static void SetLayer(Transform root, int layer)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
