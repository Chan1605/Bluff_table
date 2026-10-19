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
        public bool showLoadingNearEnd;
    }

    [SerializeField] private IntroSlide[] slides;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float skipFadeDuration = 0.25f;
    [SerializeField] private string titleSceneName;
    [SerializeField] private VideoPlayer video;

    [Header("Loading Icon")]
    [SerializeField] private GameObject loadingIcon;
    [SerializeField] private float loadingLeadSeconds = 3f;

    private bool skipRequested;

    private void Start()
    {
        foreach (var slide in slides)
        {
            slide.canvasGroup.alpha = 0f;
            slide.canvasGroup.gameObject.SetActive(false);
        }

        if (loadingIcon != null) loadingIcon.SetActive(false);

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

            bool isVideo = slide.playVideo && video != null;

            // 로딩 슬라이드의 영상은 건너뛰어도 경고 구간까지는 재생
            if (isVideo && (!skipRequested || slide.showLoadingNearEnd))
            {
                yield return StartCoroutine(WaitVideoSkippable(slide));
            }
            else if (!skipRequested)
            {
             
                yield return WaitSkippable(slide.holdDuration);
            }

            // 로딩 슬라이드: 경고 화면(영상 마지막 프레임)과 로딩 아이콘을 그대로 둔 채 타이틀로 전환
            if (slide.showLoadingNearEnd)
            {
                ShowLoading();
                break;
            }

            // 페이드 아웃
            float outDuration = skipRequested ? skipFadeDuration : fadeDuration;
            skipRequested = false;
            yield return StartCoroutine(FadeSkippable(slide.canvasGroup, slide.canvasGroup.alpha, 0f, outDuration));

            slide.canvasGroup.gameObject.SetActive(false);
            if (isVideo) video.Stop();
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


    private IEnumerator WaitVideoSkippable(IntroSlide slide)
    {
        if (video == null) yield break;

        // 영상이 실제로 재생을 시작할 때까지 아주 잠시 대기
        yield return new WaitForEndOfFrame();

        // 영상이 끝나거나, (일반 영상 슬라이드에서) 사용자가 클릭할 때까지 대기
        while (video.isPlaying)
        {
            if (skipRequested)
            {
                if (!slide.showLoadingNearEnd) yield break;

                // 로딩 슬라이드: 끝내지 않고 경고 구간으로 점프해서 끝까지 재생
                skipRequested = false;
                JumpToWarning();
            }

            // 끝나기 loadingLeadSeconds초 전부터 로딩 아이콘 (경고 문구 구간)
            if (slide.showLoadingNearEnd && video.length > 0 &&
                video.length - video.time <= loadingLeadSeconds)
            {
                ShowLoading();
            }

            yield return null;
        }
    }

    private void JumpToWarning()
    {
        double warningStart = video.length - loadingLeadSeconds;
        if (warningStart > 0 && video.time < warningStart) video.time = warningStart;

        ShowLoading();
    }

    private void ShowLoading()
    {
        if (loadingIcon == null || loadingIcon.activeSelf) return;

        loadingIcon.SetActive(true);   // 아이콘 애니메이션은 켜질 때 Animator가 자동 재생

        // 아이콘이 켜지는 순간부터 실제로 타이틀을 읽기 시작
        if (SceneTransitionMgr.Instance != null)
            SceneTransitionMgr.Instance.Preload(titleSceneName);
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