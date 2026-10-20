using System;
using DG.Tweening;
using LiarsBar.Server;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

// 공용 로딩 화면
[RequireComponent(typeof(CanvasGroup))]
public class LoadingUI : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform spinner;         
    [SerializeField] private float dotInterval = 0.4f;
    [SerializeField] private float minVisibleSeconds = 0.5f; // 최소 로딩이 보여지는 시간
    [SerializeField] private float timeoutSeconds = 15f;     // 안전장치: 끝 신호가 없으면 자동 종료
    [SerializeField] private int sortingOrder = 100;         // 다른 씬의 Canvas보다도 위에 그리기

    [Header("Cover")]
    [SerializeField] private GameObject coverBackground;     // 타이틀 내에서 로딩 백그라운드
    [SerializeField] private GameObject coverSolid;          // 룸 > 타이틀

    [Header("Video (로비 → 인게임, 원작 로딩 화면)")]
    [Tooltip("로비에서 게임이 시작될 때 이 중 하나를 무작위로 틀어 준다 (원작 로딩 영상)")]
    [SerializeField] private VideoClip[] gameLoadingClips;
    [Tooltip("영상 로딩은 최소 이 시간만큼 보여 준다 (일반 로딩은 Min Visible Seconds)")]
    [SerializeField] private float videoMinSeconds = 5f;
    [Tooltip("아래 상자에 무작위로 보여 줄 문구 (영어 원문 = 번역 키)")]
    [SerializeField] private string[] gameLoadingTips =
    {
        "Jokers count as any card. Save them for when it matters.",
        "Call LIAR! only when you are sure... or when you feel lucky.",
        "Every pull of the trigger brings the bullet one chamber closer.",
        "Empty your hand and you are safe until the next round.",
        "A calm face is the best bluff at the table.",
    };
    [SerializeField] private Color tipBoxColor = new Color(0.27f, 0.09f, 0.12f, 1f);
    [SerializeField] private Color tipBorderColor = new Color(0.93f, 0.82f, 0.52f, 1f);
    [SerializeField] private Color tipTextColor = new Color(0.93f, 0.84f, 0.62f, 1f);
    [Tooltip("인트로와 같은 로딩 아이콘 애니메이션 (intro_loading 시퀀스 Animator). 비우면 아이콘 없음")]
    [SerializeField] private RuntimeAnimatorController loadingIconController;

    private enum Cover { Background, Solid, Video }

    private GameObject videoCover;       // 원작 로딩 화면 전체 (검은 바탕 + 영상 + 문구 상자 + Loading...)
    private RawImage videoImage;
    private AspectRatioFitter videoFitter;
    private VideoPlayer videoPlayer;
    private RenderTexture videoTexture;
    private TMP_Text tipText;
    private TMP_Text videoLoadingText;
    private RoomPhase lastPhase = RoomPhase.Lobby;
    private float currentMinSeconds;
    private const string VideoLoadingWord = "Loading";

    private const int DotCount = 3; // . . .

    private static LoadingUI instance;

    private CanvasGroup group;
    private Tween dotTween, spinTween, timerTween;
    private string message = string.Empty;
    private int currentDots = 1;
    private float shownAt;
    private bool isShowing;
    private bool navigationBlocked;
    private Action pendingHidden;   // 로딩이 완전히 끝난 뒤 실행할 콜백

    public static bool IsShowing => instance != null && instance.isShowing;

    // 끝날 때까지 표시 (끝 신호가 없으면 timeoutSeconds 후 자동 종료)
    public static void Show(string message, bool useBackground = true)
    {
        if (Ready()) instance.Begin(message, instance.timeoutSeconds, true, useBackground ? Cover.Background : Cover.Solid);
    }

    // 정해진 시간만 표시
    public static void ShowFor(string message, float seconds, bool useBackground = true)
    {
        if (Ready()) instance.Begin(message, seconds, false, useBackground ? Cover.Background : Cover.Solid);
    }

    // 로딩 영상(3개 중 무작위) 위에 문구 · 아이콘 표시. 영상이 없으면 단색 커버
    public static void ShowVideo(string message)
    {
        // 다른 플레이어 로딩까지 기다리므로 안전장치 시간은 서버 대기(SceneLoadTimeout)보다 길게
        if (Ready()) instance.Begin(message, Mathf.Max(instance.timeoutSeconds, GameFlowTiming.SceneLoadTimeout + 5f), true, Cover.Video);
    }

    // 게임 시작 로딩 화면(영상)은 모든 플레이어가 준비돼 게임에 들어갈 때(OnEntryInGame)만 닫는다.
    // (내 씬 로딩이 끝났다고 TitleRoomVisibility 등이 부르는 Hide는 무시)
    public static void Hide()
    {
        if (instance != null && !instance.IsVideoShowing) instance.End(false);
    }

    // 로딩이 실제로 끝난 뒤(최소 표시 시간 포함)에 onHidden 호출. 로딩 중이 아니면 즉시 호출
    public static void Hide(Action onHidden)
    {
        if (instance == null || !instance.isShowing || instance.IsVideoShowing)
        {
            onHidden?.Invoke();
            return;
        }

        instance.pendingHidden += onHidden;
        instance.End(false);
    }

    // 표시 중에 문구만 바꿈
    public static void SetMessage(string message)
    {
        if (instance == null || !instance.isShowing) return;

        instance.message = Localization.Get(message ?? string.Empty);
        instance.SetDots(instance.currentDots);
    }

    private static bool Ready()
    {
        if (instance != null) return true;

        Debug.LogWarning("[LoadingUI] 씬에 LoadingUI가 없습니다.");
        return false;
    }

    private void Awake()
    {
        instance = this;

        SetupOverlayCanvas();

        group = GetComponent<CanvasGroup>();
        group.ignoreParentGroups = true;
        SetGroup(0f, false);
    }

    // 로비 → 게임 시작: 모든 플레이어 화면에 로딩 영상. 테이블에 들어가면(OnEntryInGame) 닫힌다
    private void OnEnable()
    {
        PlayerProfile.LocalPhaseChanged += HandlePhaseChanged;
        InGameEvent.OnEntryInGame += HandleEntryInGame;
    }

    private void HandlePhaseChanged(RoomPhase phase)
    {
        bool starting = lastPhase == RoomPhase.Lobby && phase == RoomPhase.Playing;
        lastPhase = phase;
        if (starting) ShowVideo(VideoLoadingWord);
    }

    private bool IsVideoShowing => isShowing && videoCover != null && videoCover.activeSelf;

    // 모든 플레이어 준비 완료 → 서버가 게임 진입을 알림 → 모두 같이 닫힘 (최소 시간은 준비 신호 전에 이미 채움)
    private void HandleEntryInGame()
    {
        if (IsVideoShowing) End(true);
    }

    // 로딩 화면이 최소 시간만큼 보인 뒤 action 실행 (로딩 중이 아니면 바로)
    public static void AfterMinimum(Action action)
    {
        if (instance == null || !instance.isShowing)
        {
            action?.Invoke();
            return;
        }

        float remaining = instance.currentMinSeconds - (Time.unscaledTime - instance.shownAt);
        if (remaining <= 0f) action?.Invoke();
        else DOVirtual.DelayedCall(remaining, () => action?.Invoke(), true);
    }

    private void OnDisable()
    {
        PlayerProfile.LocalPhaseChanged -= HandlePhaseChanged;
        InGameEvent.OnEntryInGame -= HandleEntryInGame;
        StopVideo();
        KillAll();
        isShowing = false;
        pendingHidden = null;   // 다른 이유로 꺼진 경우 남은 콜백 폐기
        if (group != null) SetGroup(0f, false);
        BlockNavigation(false);
    }

    private void OnDestroy()
    {
        KillAll();
        if (videoTexture != null)
        {
            videoTexture.Release();
            Destroy(videoTexture);
        }
        if (instance == this) instance = null;
    }

    // 다른 Canvas보다 위에 그려지도록 Canvas를 보장
    private void SetupOverlayCanvas()
    {
        Canvas overlay = GetComponent<Canvas>();
        if (overlay == null) overlay = gameObject.AddComponent<Canvas>();

        if (overlay.isRootCanvas)
        {
            if (!TryGetComponent(out CanvasScaler _))
            {
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }
        }
        else
        {
            overlay.overrideSorting = true;   // 다른 Canvas의 자식일 때만 필요
        }
        overlay.sortingOrder = sortingOrder;

        if (!TryGetComponent(out GraphicRaycaster _)) gameObject.AddComponent<GraphicRaycaster>();
    }

    private void Begin(string text, float autoHideSeconds, bool warnOnTimeout, Cover cover)
    {
        message = Localization.Get(text ?? string.Empty);
        shownAt = Time.unscaledTime;
        currentMinSeconds = cover == Cover.Video ? Mathf.Max(minVisibleSeconds, videoMinSeconds) : minVisibleSeconds;
        SetCover(cover);

        // 새 요청이 이전 종료 예약을 덮어쓴다
        timerTween?.Kill();
        timerTween = DOVirtual.DelayedCall(autoHideSeconds, () =>
        {
            if (warnOnTimeout) Debug.LogWarning($"[LoadingUI] {autoHideSeconds}초 동안 Hide가 없어 자동 종료합니다: {message}");
            End(true);
        }, true);

        SetDots(currentDots);
        if (isShowing) return;   // 이미 표시 중이면 문구, 커버, 타이머만 갱신

        isShowing = true;
        SetGroup(1f, true);      // 로딩 즉시 표시 (마우스 입력 차단)
        BlockNavigation(true);   // 키보드 Submit 차단
        StartDots();
        StartSpin();
    }

    // 단색 커버가 없으면 배경 커버를 그대로 쓴다. 영상은 단색 커버 위에 (첫 프레임 전까지는 단색만 보임)
    private void SetCover(Cover cover)
    {
        bool video = cover == Cover.Video && EnsureVideoCover();
        bool solid = (cover == Cover.Solid || cover == Cover.Video) && coverSolid != null;
        if (coverBackground != null) coverBackground.SetActive(!solid);
        if (coverSolid != null) coverSolid.SetActive(solid);

        if (video) PlayRandomVideo();
        else StopVideo();
    }

    // 원작 로딩 화면을 처음 쓸 때 코드로 만든다 (1920x1080 기준):
    // 검은 바탕 / 위쪽 가로 꽉 찬 영상 / 아래 금테 상자 안 팁 문구 / 오른쪽 아래 "Loading..." + 로딩 아이콘
    // 맨 위에 덮이므로 방 만들기 · 나가기 때 쓰는 문구(LoadBack)는 이 화면에서는 보이지 않는다
    private bool EnsureVideoCover()
    {
        if (videoCover != null) return true;
        if (gameLoadingClips == null || gameLoadingClips.Length == 0) return false;

        TMP_FontAsset font = label != null ? label.font : null;

        // 검은 바탕 (가장 위)
        videoCover = NewUI("[Video] LoadScreen", transform);
        Stretch((RectTransform)videoCover.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        videoCover.transform.SetAsLastSibling();
        var black = videoCover.AddComponent<Image>();
        black.color = Color.black;
        black.raycastTarget = false;

        // 영상: 위쪽에 가로로 꽉 차게, 높이는 영상 비율대로
        GameObject video = NewUI("Video", videoCover.transform);
        var vrt = (RectTransform)video.transform;
        vrt.anchorMin = new Vector2(0f, 1f);
        vrt.anchorMax = new Vector2(1f, 1f);
        vrt.pivot = new Vector2(0.5f, 1f);
        vrt.anchoredPosition = Vector2.zero;
        vrt.sizeDelta = Vector2.zero;
        videoImage = video.AddComponent<RawImage>();
        videoImage.raycastTarget = false;
        videoImage.enabled = false;
        videoFitter = video.AddComponent<AspectRatioFitter>();
        videoFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;

        videoPlayer = video.AddComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
        videoPlayer.started += _ => { if (videoImage != null) videoImage.enabled = true; };

        // 팁 상자: 아래쪽, 좌우 45 · 아래 102 · 높이 195
        GameObject box = NewUI("TipBox", videoCover.transform);
        Stretch((RectTransform)box.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(45f, 102f), new Vector2(-45f, 297f));
        var boxImage = box.AddComponent<Image>();
        boxImage.color = tipBoxColor;
        boxImage.raycastTarget = false;
        var border = box.AddComponent<Outline>();
        border.effectColor = tipBorderColor;
        border.effectDistance = new Vector2(2f, -2f);
        var border2 = box.AddComponent<Outline>();
        border2.effectColor = tipBorderColor;
        border2.effectDistance = new Vector2(-2f, 2f);

        GameObject tip = NewUI("Tip", box.transform);
        Stretch((RectTransform)tip.transform, Vector2.zero, Vector2.one, new Vector2(60f, 20f), new Vector2(-60f, -20f));
        tipText = tip.AddComponent<TextMeshProUGUI>();
        if (font != null) tipText.font = font;
        tipText.fontSize = 36f;
        tipText.color = tipTextColor;
        tipText.alignment = TextAlignmentOptions.Center;
        tipText.textWrappingMode = TextWrappingModes.Normal;
        tipText.raycastTarget = false;

        // 오른쪽 아래 "Loading..."
        GameObject loading = NewUI("LoadingText", videoCover.transform);
        var lrt = (RectTransform)loading.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0f);
        lrt.pivot = new Vector2(1f, 0f);
        lrt.anchoredPosition = new Vector2(-20f, 18f);
        lrt.sizeDelta = new Vector2(320f, 70f);
        videoLoadingText = loading.AddComponent<TextMeshProUGUI>();
        if (font != null) videoLoadingText.font = font;
        videoLoadingText.fontSize = 44f;
        videoLoadingText.fontStyle = FontStyles.Bold;
        videoLoadingText.color = Color.white;
        videoLoadingText.alignment = TextAlignmentOptions.BottomRight;
        videoLoadingText.textWrappingMode = TextWrappingModes.NoWrap;
        videoLoadingText.raycastTarget = false;

        // 인트로 로딩 아이콘: "Loading..." 왼쪽
        if (loadingIconController != null)
        {
            GameObject icon = NewUI("LoadingIcon", videoCover.transform);
            var irt = (RectTransform)icon.transform;
            irt.anchorMin = irt.anchorMax = new Vector2(1f, 0f);
            irt.pivot = new Vector2(1f, 0f);
            irt.anchoredPosition = new Vector2(-250f, 14f);
            irt.sizeDelta = new Vector2(76f, 76f);
            var iconImage = icon.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            var animator = icon.AddComponent<Animator>();
            animator.runtimeAnimatorController = loadingIconController;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        videoCover.SetActive(false);
        return true;
    }

    private GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private void PlayRandomVideo()
    {
        VideoClip clip = gameLoadingClips[UnityEngine.Random.Range(0, gameLoadingClips.Length)];
        if (clip == null) return;

        // 영상 크기에 맞는 RenderTexture (영상마다 해상도가 다름)
        int w = Mathf.Max(1, (int)clip.width), h = Mathf.Max(1, (int)clip.height);
        if (videoTexture == null || videoTexture.width != w || videoTexture.height != h)
        {
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
            videoTexture = new RenderTexture(w, h, 0) { name = "LoadingVideo (runtime)" };
        }
        videoPlayer.targetTexture = videoTexture;
        videoImage.texture = videoTexture;
        videoFitter.aspectRatio = (float)w / h;

        if (tipText != null && gameLoadingTips != null && gameLoadingTips.Length > 0)
            tipText.text = Localization.Get(gameLoadingTips[UnityEngine.Random.Range(0, gameLoadingTips.Length)]);

        videoCover.SetActive(true);
        videoImage.enabled = false;   // 첫 프레임이 나올 때까지 검은 화면
        videoPlayer.clip = clip;
        videoPlayer.Play();
    }

    private void StopVideo()
    {
        if (videoPlayer != null) videoPlayer.Stop();
        if (videoCover != null) videoCover.SetActive(false);
    }

    private void End(bool force)
    {
        if (!isShowing) return;

        timerTween?.Kill();

        float remaining = currentMinSeconds - (Time.unscaledTime - shownAt);
        if (!force && remaining > 0f)
        {
            timerTween = DOVirtual.DelayedCall(remaining, () => End(true), true);
            return;
        }

        isShowing = false;
        SetGroup(0f, false);     // 로딩 즉시 숨김
        StopVideo();
        BlockNavigation(false);
        StopLoops();

        // 상태를 모두 정리한 뒤 실행 (콜백이 Canvas를 꺼도 안전)
        Action done = pendingHidden;
        pendingHidden = null;
        done?.Invoke();
    }

    // 로딩 뒤에 가려진 버튼이 Enter로 눌리지 않게 한다
    private void BlockNavigation(bool block)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return;

        if (block)
        {
            es.SetSelectedGameObject(null);
            es.sendNavigationEvents = false;
            navigationBlocked = true;
        }
        else if (navigationBlocked)
        {
            es.sendNavigationEvents = true;
            navigationBlocked = false;
        }
    }

    private void StartDots()
    {
        dotTween?.Kill();
        SetDots(1);

        dotTween = DOTween.Sequence()
            .AppendCallback(() => SetDots(1)).AppendInterval(dotInterval)
            .AppendCallback(() => SetDots(2)).AppendInterval(dotInterval)
            .AppendCallback(() => SetDots(3)).AppendInterval(dotInterval)
            .SetLoops(-1)
            .SetUpdate(true);
    }

    private void StartSpin()
    {
        spinTween?.Kill();
        if (spinner == null) return;

        spinner.localRotation = Quaternion.identity;
        spinTween = spinner.DOLocalRotate(new Vector3(0f, 0f, -360f), 1.2f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetUpdate(true);
    }

    // 점 개수가 바뀌어도 글자 폭이 일정하도록, 안 쓰는 점은 투명 처리
    private void SetDots(int count)
    {
        currentDots = count;
        if (label == null)
        {
            if (videoLoadingText != null) videoLoadingText.text = $"{message}{new string('.', count)}<alpha=#00>{new string('.', DotCount - count)}";
            return;
        }

        label.text = $"{message}{new string('.', count)}<alpha=#00>{new string('.', DotCount - count)}";
        if (videoLoadingText != null) videoLoadingText.text = label.text;
    }

    private void StopLoops()
    {
        dotTween?.Kill();
        spinTween?.Kill();
    }

    private void KillAll()
    {
        timerTween?.Kill();
        StopLoops();
    }

    private void SetGroup(float alpha, bool blocks)
    {
        group.alpha = alpha;
        group.blocksRaycasts = blocks;
        group.interactable = false;
    }
}