using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > LiarsBar > Layout Your Bid (원작)
// 지금 열린 씬의 InGameHUD → MyTurn 묶음을 원작 "Your Bid" 배치로 옮긴다 (크기 · 위치 · 글자 크기만).
// 이미 넣어 둔 키 이미지 · 색은 그대로 두고, 없는 것(D 키, 탄창 그림)만 채운다. Ctrl+Z로 되돌릴 수 있다.
// 기준: 1920x1080 캔버스, MyTurn의 기준점 = 화면 오른쪽 위
public static class YourBidLayoutTool
{
    private const string CylinderPath = "Assets/4.Sprite/UI/icon/cylinder_icon.png";
    private const string KeyDPath = "Assets/4.Sprite/UI/Key/key_D_4k.png";

    // ---- 배치 값 (여기만 고치면 다시 실행해서 맞출 수 있음)
    private const float Right = 30f;          // 화면 오른쪽 여백
    private const float TitleSize = 54f;      // "Your Bid"
    private const float LineY = -82f;         // 제목 밑줄
    private const float LineWidth = 380f;
    private const float FirstRowY = -114f;    // 첫 줄(Switch) 가운데
    private const float RowGap = 38f;
    private const float LabelSize = 28f;
    private const float KeySize = 32f;        // 정사각 키 (A, D, E, X)
    private const float SpaceWidth = 66f;     // SPACE 키
    private const float KeyGap = 6f;          // A와 D 사이
    private const float LabelGap = 12f;       // 글자와 키 사이
    private const float CylinderSize = 140f;

    [MenuItem("Tools/LiarsBar/Layout Your Bid (원작)")]
    private static void Layout()
    {
        InGameHUD hud = Object.FindAnyObjectByType<InGameHUD>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Layout Your Bid", "열린 씬에서 InGameHUD를 찾지 못했습니다. Zino_Table을 열고 실행하세요.", "확인");
            return;
        }

        var hudSo = new SerializedObject(hud);
        GameObject myTurnGo = hudSo.FindProperty("myTurnGroup").objectReferenceValue as GameObject;
        if (myTurnGo == null)
        {
            EditorUtility.DisplayDialog("Layout Your Bid", "InGameHUD의 My Turn Group이 비어 있습니다.", "확인");
            return;
        }

        Transform root = myTurnGo.transform;
        Undo.RegisterFullObjectHierarchyUndo(myTurnGo, "Layout Your Bid");

        float keysRight = -Right;
        float labelRight = keysRight - (KeySize * 2f + KeyGap) - LabelGap;   // 키 칸 너비 = A + D

        // 제목 · 밑줄
        TextAt(root, "YourBid", new Vector2(1f, 1f), new Vector2(-Right, -8f), new Vector2(360f, 66f), TitleSize, TextAlignmentOptions.BottomRight);
        Rect(root, "Line", new Vector2(1f, 0.5f), new Vector2(0f, LineY), new Vector2(LineWidth, 2f));

        // 조작 안내 4줄
        string[] labels = { "Hint_Switch", "Hint_Select", "Hint_Throw", "Hint_Call LIAR!" };
        for (int i = 0; i < labels.Length; i++)
            TextAt(root, labels[i], new Vector2(1f, 0.5f), new Vector2(labelRight, Row(i)), new Vector2(260f, 36f), LabelSize, TextAlignmentOptions.MidlineRight);

        // 키: A D / SPACE / E / X (오른쪽 정렬)
        RectTransform keyA = Rect(root, "Key_A  D", new Vector2(1f, 0.5f), new Vector2(keysRight - KeySize - KeyGap, Row(0)), new Vector2(KeySize, KeySize));
        if (keyA != null) PlaceKeyD(keyA);
        Rect(root, "Key_SPACE", new Vector2(1f, 0.5f), new Vector2(keysRight, Row(1)), new Vector2(SpaceWidth, KeySize));
        Rect(root, "Key_E", new Vector2(1f, 0.5f), new Vector2(keysRight, Row(2)), new Vector2(KeySize, KeySize));
        Rect(root, "Key_X", new Vector2(1f, 0.5f), new Vector2(keysRight, Row(3)), new Vector2(KeySize, KeySize));

        // 남은 탄창 그림: 안내 줄 왼쪽, 4줄의 가운데 높이
        float cylinderX = labelRight - 200f - CylinderSize * 0.5f;
        float cylinderY = (Row(0) + Row(3)) * 0.5f;
        RectTransform cylinder = FindOrCreateImage(root, "Cylinder", "Gun");
        SetRect(cylinder, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(cylinderX, cylinderY), new Vector2(CylinderSize, CylinderSize));
        Image cylImage = cylinder.GetComponent<Image>();
        Sprite cylSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CylinderPath);
        if (cylSprite != null) { cylImage.sprite = cylSprite; cylImage.color = Color.white; cylImage.type = Image.Type.Simple; cylImage.preserveAspect = true; }
        cylImage.raycastTarget = false;

        // (n/6): 탄창 그림 바로 밑에 작게
        TextAt(root, "Chamber", new Vector2(0.5f, 1f), new Vector2(cylinderX, cylinderY - CylinderSize * 0.5f - 2f), new Vector2(120f, 30f), 24f, TextAlignmentOptions.Top);

        EditorSceneManager.MarkSceneDirty(myTurnGo.scene);
        Selection.activeGameObject = myTurnGo;
        Debug.Log("[Layout Your Bid] 배치 완료 - 씬을 저장하고, 프리팹에도 넣으려면 InGameUI에서 Overrides > Apply All");
    }

    private static float Row(int i) => FirstRowY - i * RowGap;

    // A 키 이미지 오른쪽에 D 키: 자식 Image가 있으면 그걸 쓰고, 없으면 만든다
    private static void PlaceKeyD(RectTransform keyA)
    {
        Image d = null;
        foreach (Transform child in keyA)
            if (child.TryGetComponent(out Image img) && !child.GetComponent<TMP_Text>()) { d = img; break; }

        if (d == null)
        {
            var go = new GameObject("Key_D", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Layout Your Bid");
            go.layer = keyA.gameObject.layer;
            go.transform.SetParent(keyA, false);
            d = go.GetComponent<Image>();
            d.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KeyDPath);
            d.raycastTarget = false;
        }

        RectTransform rt = d.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(KeyGap, 0f);
        rt.sizeDelta = new Vector2(KeySize, KeySize);
        rt.localScale = Vector3.one;
    }

    private static RectTransform FindOrCreateImage(Transform root, string name, string altName)
    {
        Transform t = root.Find(name);
        if (t == null && altName != null) t = root.Find(altName);
        if (t != null)
        {
            if (!t.TryGetComponent(out Image _)) Undo.AddComponent<Image>(t.gameObject);
            return (RectTransform)t;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "Layout Your Bid");
        go.layer = root.gameObject.layer;
        go.transform.SetParent(root, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform Rect(Transform root, string name, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            Debug.LogWarning($"[Layout Your Bid] MyTurn 밑에 '{name}'이 없어 건너뜀");
            return null;
        }
        RectTransform rt = (RectTransform)t;
        SetRect(rt, new Vector2(1f, 1f), pivot, pos, size);

        // 키 이미지 안의 글자(Label)는 이미지에 글자가 있으므로 크기만 맞춰 둔다
        foreach (TMP_Text label in rt.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.transform == rt) continue;   // 자기 자신의 글자(안내 문구)는 건드리지 않음
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = size;
        }
        return rt;
    }

    private static void TextAt(Transform root, string name, Vector2 pivot, Vector2 pos, Vector2 size, float fontSize, TextAlignmentOptions align)
    {
        RectTransform rt = Rect(root, name, pivot, pos, size);
        if (rt == null || !rt.TryGetComponent(out TMP_Text text)) return;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.alignment = align;
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
}
