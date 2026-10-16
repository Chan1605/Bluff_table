using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class IntroSequence : MonoBehaviour
{
    [System.Serializable]
    public class IntroSlide
    {
        public CanvasGroup canvasGroup;
        public float holdDuration = 2f; 
        public bool playVideo;
    }

    [SerializeField] private IntroSlide[] slides;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float skipFadeDuration = 0.25f;
    [SerializeField] private string titleSceneName;
    [SerializeField] private VideoPlayer video;
    private bool skipRequested;

    private void Start()
    {
        foreach (var slide in slides)
        {
            slide.canvasGroup.alpha = 0f;
            slide.canvasGroup.gameObject.SetActive(false);
        }

        if (video != null)
        {
            video.playOnAwake = false;
            video.isLooping = false;

            if (video.gameObject.activeInHierarchy) video.Prepare();
        }

        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            skipRequested = true;
        }
    }

    private void OnDisable()
    {
        if (video != null)
            video.Stop();
    }

    private IEnumerator PlaySequence()
    {
        foreach (var slide in slides)
        {
            skipRequested = false;

            slide.canvasGroup.gameObject.SetActive(true);

        
            if (slide.playVideo) yield return StartVideo();

            // 페이드 인
            yield return StartCoroutine(FadeSkippable(slide.canvasGroup, 0f, 1f, fadeDuration));

            // 대기 구간 스킵 요청이 없을 때만 실행
            if (!skipRequested)
            {
                if (slide.playVideo && video != null)
                {
                    // [변경] 영상 슬라이드라면 영상이 끝날 때까지 대기
                    yield return StartCoroutine(WaitVideoSkippable());
                }
                else
                {
                    // 일반 이미지 슬라이드라면 지정된 holdDuration만큼 대기
                    yield return WaitSkippable(slide.holdDuration);
                }
            }

            // 페이드 아웃
            float outDuration = skipRequested ? skipFadeDuration : fadeDuration;
            skipRequested = false;
            yield return StartCoroutine(FadeSkippable(slide.canvasGroup, slide.canvasGroup.alpha, 0f, outDuration));

            slide.canvasGroup.gameObject.SetActive(false);
            if (slide.playVideo && video != null) video.Stop();
        }

        SceneTransitionMgr.Instance.LoadScene(titleSceneName);
    }

    private IEnumerator StartVideo()
    {
        if (video == null) yield break;

        if (!video.isActiveAndEnabled)
        {
            Debug.LogWarning("[Intro] VideoPlayer가 비활성 상태라 영상을 시작할 수 없습니다.");
            yield break;
        }

        if (!video.isPrepared)
        {
            video.Prepare();

            float timeout = 5f;
            while (!video.isPrepared && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime; 
                yield return null;
            }
        }

        video.time = 0;
        video.Play();
    }


    private IEnumerator WaitVideoSkippable()
    {
        if (video == null) yield break;

        // 영상이 실제로 재생을 시작할 때까지 아주 잠시 대기
        yield return new WaitForEndOfFrame();

        // 영상이 끝났거나 사용자가 클릭할 때까지 대기
        while (video.isPlaying)
        {
            if (skipRequested) yield break;
            yield return null;
        }
    }

    private IEnumerator FadeSkippable(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            if (skipRequested) break;
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        cg.alpha = to;
    }

    private IEnumerator WaitSkippable(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (skipRequested) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
