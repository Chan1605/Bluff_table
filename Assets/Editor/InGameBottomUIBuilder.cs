using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 인게임 하단 UI (원작 배치) - InGameUI 프리팹에 넣는다.
// - 왼쪽 아래: 채팅 (로비 RoomLobbyCanvas의 입력창 · 로그를 복사 + RoomChatUI)
// - 가운데 아래: 퀵챗 말풍선 + [V] (QuickChatHudBuilder)
// - 오른쪽 아래(내 차례가 아닐 때, IdleHints): 감정 표현 마름모 3개 + [1][2][3] [4] [5], Look [SPACE]
// 처음 한 번은 에디터가 켜질 때 자동으로 만들고, 다시 만들려면 Tools > LiarsBar > Build InGame Bottom UI
// "[Gen] "으로 시작하는 오브젝트만 지우고 다시 만들므로 다른 수정은 그대로 남는다.
[InitializeOnLoad]
public static class InGameBottomUIBuilder
{
    private const string PrefabPath = "Assets/2.Model/Prefabs/UI/InGameUI.prefab";
    private const string LobbyPath = "Assets/2.Model/Prefabs/UI/RoomLobbyCanvas.prefab";
    private const string Icon = "Assets/4.Sprite/UI/icon/";
    private const string Key = "Assets/4.Sprite/UI/Key/";
    private const string ChatName = "[Gen] Chat";
    private const string AutoKey = "LiarsBar.InGameBottomUI.Auto";

    static InGameBottomUIBuilder()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    private static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(AutoKey, false)) return;
        SessionState.SetBool(AutoKey, true);

        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || asset.GetComponentInChildren<InGameHUD>(true) == null) return;
        if (FindDeep(asset.transform, ChatName) != null) return;   // 이미 만들어 둠

        Build();
    }

    [MenuItem("Tools/LiarsBar/Build InGame Bottom UI")]
    private static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            InGameHUD hud = root.GetComponentInChildren<InGameHUD>(true);
            var so = new SerializedObject(hud);
            var playing = (so.FindProperty("playingGroup").objectReferenceValue as GameObject)?.transform as RectTransform;
            var idle = (so.FindProperty("idleHints").objectReferenceValue as GameObject)?.transform as RectTransform;
            if (playing == null || idle == null)
            {
                Debug.LogWarning("[InGame Bottom UI] InGameHUD의 Playing Group / Idle Hints가 비어 있어 중단");
                return;
            }

            RemoveGenerated(playing);
            RemoveGenerated(idle);

            BuildChat(playing);
            QuickChatHudBuilder.BuildHud(playing);
            BuildEmotes(idle);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[InGame Bottom UI] InGameUI 프리팹에 하단 UI(채팅 · 퀵챗 · 감정 표현 · Look)를 만들었습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 채팅 (왼쪽 아래)

    private static void BuildChat(RectTransform playing)
    {
        RectTransform group = Group(playing, ChatName, new Vector2(0f, 0f));

        GameObject lobby = PrefabUtility.LoadPrefabContents(LobbyPath);
        try
        {
            Transform input = FindDeep(lobby.transform, "InputField (TMP)");
            Transform log = FindDeep(lobby.transform, "LogText");
            if (input == null || log == null)
            {
                Debug.LogWarning("[InGame Bottom UI] 로비 채팅(InputField (TMP) / LogText)을 찾지 못해 채팅 생략");
                return;
            }

            // 입력창: "PRESS ENTER TO CHAT" 상자
            var inputRt = (RectTransform)Object.Instantiate(input.gameObject, group, false).transform;
            inputRt.name = "Input";
            SetRect(inputRt, Vector2.zero, Vector2.zero, new Vector2(23f, 23f), new Vector2(240f, 46f));
            TMP_InputField field = inputRt.GetComponent<TMP_InputField>();
            if (field.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "PRESS ENTER TO CHAT";
                placeholder.fontSize = 17f;
                placeholder.alignment = TextAlignmentOptions.Center;
                if (!placeholder.TryGetComponent(out LocalizedText _)) placeholder.gameObject.AddComponent<LocalizedText>();
            }
            if (field.textComponent != null) field.textComponent.fontSize = 18f;

            // 받은 메시지: 입력창 바로 위로 쌓임
            var logRt = (RectTransform)Object.Instantiate(log.gameObject, group, false).transform;
            logRt.name = "Log";
            SetRect(logRt, Vector2.zero, Vector2.zero, new Vector2(23f, 78f), new Vector2(520f, 220f));
            TMP_Text logText = logRt.GetComponent<TMP_Text>();
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.fontSize = 20f;
            logText.raycastTarget = false;

            RoomChatUI chat = group.gameObject.AddComponent<RoomChatUI>();
            var chatSo = new SerializedObject(chat);
            chatSo.FindProperty("chatInput").objectReferenceValue = field;
            chatSo.FindProperty("logText").objectReferenceValue = logText;
            chatSo.ApplyModifiedPropertiesWithoutUndo();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(lobby);
        }
    }

    // ---------------------------------------------------------------- 감정 표현 · Look (오른쪽 아래)

    private static void BuildEmotes(RectTransform idle)
    {
        // 예전 안내(Emote [1 - 4])는 숨기고 Look [SPACE]는 원작 위치로
        SetActive(idle, "Emote", false);
        SetActive(idle, "Key_1 - 4", false);
        SetActive(idle, "Key_1 - 5", false);

        Transform look = idle.Find("Look");
        if (look != null)
        {
            SetRect((RectTransform)look, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-136f, 64f), new Vector2(200f, 44f));
            if (look.TryGetComponent(out TMP_Text text)) { text.fontSize = 26f; text.alignment = TextAlignmentOptions.MidlineRight; }
        }

        Transform space = idle.Find("Key_SPACE");
        if (space != null)
        {
            SetRect((RectTransform)space, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-84f, 64f), new Vector2(74f, 36f));
            UseKeySprite(space, Key + "key_space.png");
        }

        RectTransform group = Group(idle, "[Gen] Emotes", new Vector2(1f, 0f));
        Diamond(group, "Emote_1", Icon + "fuckyou_icon.png", new Vector2(-276f, 90f));
        Diamond(group, "Emote_2", Icon + "youredead_icon.png", new Vector2(-227f, 189f));
        Diamond(group, "Emote_3", Icon + "grr_icon.png", new Vector2(-128f, 246f));

        Img(group, "Key_1", Key + "key_1.png", new Vector2(-378f, 48f), new Vector2(26f, 26f));
        Img(group, "Key_2", Key + "key_2.png", new Vector2(-348f, 48f), new Vector2(26f, 26f));
        Img(group, "Key_3", Key + "key_3.png", new Vector2(-318f, 48f), new Vector2(26f, 26f));
        Img(group, "Key_4", Key + "key_4.png", new Vector2(-177f, 148f), new Vector2(26f, 26f));
        Img(group, "Key_5", Key + "key_5.png", new Vector2(-80f, 200f), new Vector2(26f, 26f));
    }

    private static void Diamond(RectTransform parent, string name, string iconPath, Vector2 pos)
    {
        Image bg = Img(parent, name, Icon + "emote_diamond.png", pos, new Vector2(95f, 95f));
        Image icon = Img((RectTransform)bg.transform, "Icon", iconPath, Vector2.zero, new Vector2(48f, 48f));
        RectTransform rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    // 키 상자를 키 이미지로 (글자 · 테두리는 이미지에 있으므로 끔)
    private static void UseKeySprite(Transform key, string spritePath)
    {
        if (key.TryGetComponent(out Image image))
        {
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        if (key.TryGetComponent(out Outline outline)) outline.enabled = false;
        foreach (TMP_Text label in key.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- 공통

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

    // 묶음 기준점에서 pos만큼 떨어진 곳에 가운데를 맞춘 이미지
    private static Image Img(RectTransform parent, string name, string spritePath, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        SetRect(rt, parent.pivot, new Vector2(0.5f, 0.5f), pos, size);

        Image image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (image.sprite == null) Debug.LogWarning($"[InGame Bottom UI] 스프라이트 없음: {spritePath}");
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

    private static void SetActive(Transform parent, string name, bool active)
    {
        Transform t = parent.Find(name);
        if (t != null) t.gameObject.SetActive(active);
    }

    private static void RemoveGenerated(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith("[Gen] ")) Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
