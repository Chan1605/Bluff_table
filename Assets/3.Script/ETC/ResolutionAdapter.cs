using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 해상도 · 화면비 대응 (16:10 · 4:3 · 21:9 · 창 크기 조절). 16:9에서는 지금과 똑같이 보인다
// 1) 화면 기준 CanvasScaler를 모두 Expand로 → 1920x1080 기준 UI가 어떤 비율에서도 잘리거나 겹치지 않음
// 2) 1920x1080 고정 크기 전체 화면 배경(타이틀 RenderTexture · 스토리 · 페이드 · 로딩 커버 등)을 화면을 덮도록 키움 → 검은 띠 · 덜 덮인 가장자리 없음
// 씬 · 프리팹은 건드리지 않고 실행 중에만 맞춘다 (나중에 추가되는 씬 · 프리팹도 자동 적용)
public class ResolutionAdapter : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[ResolutionAdapter]");
        DontDestroyOnLoad(go);
        go.AddComponent<ResolutionAdapter>();
    }

    private static readonly Vector2 Reference = new Vector2(1920f, 1080f);
    private const float ScanInterval = 1f;   // 늦게 생기는 UI(프리팹 · 코드 생성)도 잡도록 주기적으로 훑음

    private struct Cover
    {
        public Vector2 BaseSize;
        public RectTransform Root;
    }

    private readonly Dictionary<RectTransform, Cover> covers = new();
    private readonly List<RectTransform> removed = new();
    private float nextScan;

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => nextScan = 0f;

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + ScanInterval;
            Scan();
        }
        ApplyCovers();
    }

    private void Scan()
    {
        foreach (CanvasScaler scaler in FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        foreach (Graphic graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(graphic is Image || graphic is RawImage)) continue;

            RectTransform rt = graphic.rectTransform;
            if (covers.ContainsKey(rt) || rt.childCount > 0 || rt.anchorMin != rt.anchorMax) continue;
            if (Mathf.Abs(rt.sizeDelta.x - Reference.x) > 2f || Mathf.Abs(rt.sizeDelta.y - Reference.y) > 2f) continue;

            RectTransform root = RootCanvasRect(rt);
            if (root == null) continue;
            covers[rt] = new Cover { BaseSize = rt.sizeDelta, Root = root };
        }
    }

    // 화면 비율이 16:9와 다르면 남는 쪽까지 덮을 만큼 비율 유지한 채 키움 (넘치는 부분은 화면 밖)
    private void ApplyCovers()
    {
        foreach (KeyValuePair<RectTransform, Cover> pair in covers)
        {
            RectTransform rt = pair.Key;
            Cover cover = pair.Value;
            if (rt == null || cover.Root == null) { removed.Add(rt); continue; }

            Vector2 screen = cover.Root.rect.size;
            float scale = Mathf.Max(screen.x / cover.BaseSize.x, screen.y / cover.BaseSize.y);
            if (scale <= 0f) continue;

            Vector2 size = cover.BaseSize * scale;
            if ((rt.sizeDelta - size).sqrMagnitude > 0.01f) rt.sizeDelta = size;
        }

        foreach (RectTransform rt in removed) covers.Remove(rt);
        removed.Clear();
    }

    // 맨 바깥 화면 기준 Canvas (World Space는 제외)
    private static RectTransform RootCanvasRect(RectTransform rt)
    {
        Canvas[] canvases = rt.GetComponentsInParent<Canvas>(true);
        if (canvases.Length == 0) return null;
        Canvas root = canvases[canvases.Length - 1];
        return root.renderMode == RenderMode.WorldSpace ? null : (RectTransform)root.transform;
    }
}
