using System.Collections;
using LiarsBar.Server;
using UnityEngine;
using UnityEngine.UI;

// 내가 러시안 룰렛에서 총에 맞았을 때 1인칭 화면 연출 (원작 Die 프레임)
//   0s      : 화면 테두리에 흰 → 분홍 붓질 섬광 (frame_00000~00049, 30fps ≈ 1.7초)
//   1.0~2.2s: 검은 화면으로 서서히 덮임
//   2.5s    : PlayerState가 관전 카메라로 바꾸는 시점(GameFlowTiming.TransitionSpectator) → 검은 화면이 걷히며 관전 시작
// 프레임은 InGameHUD의 Death Frames에서 가져온다 (컴포넌트 ⋮ 메뉴 "Die 프레임 채우기").
// 프리팹 · Inspector 연결 없이 실행 시 자동 생성 (화면 맨 위 오버레이 캔버스).
public class DeathScreenUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[DeathScreenUI]");
        DontDestroyOnLoad(go);
        go.AddComponent<DeathScreenUI>();
    }

    private const float BlackFadeInStart = 1.0f;
    private const float BlackFadeInDuration = 1.2f;
    private const float BlackFadeOutDuration = 0.6f;

    private Canvas canvas;
    private Image frameImage;
    private Image black;
    private Coroutine routine;

    private void OnEnable()
    {
        InGameEvent.OnPlayerEliminated += HandleEliminated;
        InGameEvent.OnReEntryInLobby += HideNow;
        InGameEvent.OnEntryInGame += HideNow;
    }

    private void OnDisable()
    {
        InGameEvent.OnPlayerEliminated -= HandleEliminated;
        InGameEvent.OnReEntryInLobby -= HideNow;
        InGameEvent.OnEntryInGame -= HideNow;
    }

    private void HandleEliminated(int seat)
    {
        if (PlayerProfile.Local == null || seat != PlayerProfile.Local.Seat) return;   // 내가 맞았을 때만
        Build();
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        InGameHUD hud = FindFirstObjectByType<InGameHUD>(FindObjectsInactive.Include);
        Sprite[] frames = hud != null ? hud.DeathFrames : null;
        float fps = hud != null && hud.DeathFps > 0f ? hud.DeathFps : 30f;
#if UNITY_EDITOR
        if (frames == null || frames.Length == 0) frames = LoadEditorFrames();
#endif

        canvas.gameObject.SetActive(true);
        SetAlpha(black, 0f);
        frameImage.enabled = frames != null && frames.Length > 0;

        float t = 0f;
        float frameEnd = frames != null ? frames.Length / fps : 0f;
        float spectateAt = GameFlowTiming.TransitionSpectator;

        while (t < spectateAt)
        {
            t += Time.deltaTime;

            if (frameImage.enabled)
            {
                int i = Mathf.FloorToInt(t * fps);
                if (i < frames.Length) frameImage.sprite = frames[i];
                else frameImage.enabled = false;   // 마지막 프레임(완전 투명) 뒤로는 끈다
            }

            SetAlpha(black, Mathf.Clamp01((t - BlackFadeInStart) / BlackFadeInDuration));
            yield return null;
        }

        // 관전 카메라로 넘어간 뒤 검은 화면을 걷는다
        frameImage.enabled = false;
        float f = 0f;
        while (f < BlackFadeOutDuration)
        {
            f += Time.deltaTime;
            SetAlpha(black, 1f - f / BlackFadeOutDuration);
            yield return null;
        }

        canvas.gameObject.SetActive(false);
        routine = null;
    }

#if UNITY_EDITOR
    // InGameHUD에 프레임이 연결되지 않았을 때 에디터에서만 폴더에서 직접 읽는다 (빌드에서는 동작하지 않음 → 'Die 프레임 채우기' 후 프리팹 저장 필요)
    private const string EditorFramesFolder = "Assets/4.Sprite/UI/Die";
    private static Sprite[] editorFrames;

    private static Sprite[] LoadEditorFrames()
    {
        if (editorFrames != null) return editorFrames;
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { EditorFramesFolder }))
            paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal);
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (string p in paths)
        {
            Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s != null) list.Add(s);
        }
        editorFrames = list.ToArray();
        Debug.LogWarning($"[DeathScreen] InGameHUD에 Die 프레임이 연결되지 않아 에디터에서만 {EditorFramesFolder}에서 {editorFrames.Length}장을 읽었습니다. " +
                         "빌드 전에 InGameUI 프리팹의 InGameHUD ⋮ 메뉴 'Die 프레임 채우기' 실행 후 프리팹을 저장하세요.");
        return editorFrames;
    }
#endif

    private void HideNow()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    private void Build()
    {
        if (canvas != null) return;

        var canvasGo = new GameObject("[Gen] DeathScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;   // HUD · 로딩 화면보다 위
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        black = MakeFullScreen("Black", canvasGo.transform);
        black.color = new Color(0f, 0f, 0f, 0f);
        frameImage = MakeFullScreen("Frame", canvasGo.transform);   // 검은 화면 위에 섬광
        frameImage.color = Color.white;

        canvasGo.SetActive(false);
    }

    private static Image MakeFullScreen(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
