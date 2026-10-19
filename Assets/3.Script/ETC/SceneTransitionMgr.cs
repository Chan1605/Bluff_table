using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionMgr : MonoBehaviour
{
    public static SceneTransitionMgr Instance;

    [SerializeField] private CanvasGroup fadeCanvasGroup; 
    [SerializeField] private float fadeDuration = 0.5f;

    private AsyncOperation preloadOp;   // 미리 읽어 둔 씬 (활성화 대기 중)
    private string preloadScene;
    private bool isLoading;             // 전환이 겹치지 않게

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }


    private void Start()
    {
        StartCoroutine(Fade(1f, 0f)); 
    }

    // 화면은 그대로 두고 씬만 미리 읽어 둔다. 활성화는 LoadScene에서 한다.
    public void Preload(string sceneName)
    {
        if (preloadOp != null || isLoading) return;

        preloadOp = SceneManager.LoadSceneAsync(sceneName);
        if (preloadOp == null)
        {
            Debug.LogWarning($"[SceneTransition] '{sceneName}' 씬을 미리 불러올 수 없습니다. Build Settings를 확인하세요.");
            return;
        }

        preloadScene = sceneName;
        preloadOp.allowSceneActivation = false;   // 읽기만 하고 progress 0.9에서 대기
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading) return;

        isLoading = true;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        AsyncOperation op = null;

        if (preloadOp != null && preloadScene == sceneName)
        {
            op = preloadOp;
            while (op.progress < 0.9f) yield return null;
         
        }
        else if (preloadOp != null)
        {
            preloadOp.allowSceneActivation = true;
            yield return preloadOp;
        }

        yield return StartCoroutine(Fade(0f, 1f)); // 페이드아웃

        if (op != null)
        {
            op.allowSceneActivation = true;
            yield return op;
        }
        else
        {
            yield return SceneManager.LoadSceneAsync(sceneName);
        }

        preloadOp = null;
        preloadScene = null;
        isLoading = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        fadeCanvasGroup.alpha = from;
        fadeCanvasGroup.blocksRaycasts = true; // 페이드 중엔 클릭 막기

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; 
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = (to > 0.5f); 
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(Fade(1f, 0f));
    }
}