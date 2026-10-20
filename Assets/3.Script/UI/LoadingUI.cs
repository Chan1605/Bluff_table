using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        if (Ready()) instance.Begin(message, instance.timeoutSeconds, true, useBackground);
    }

    // 정해진 시간만 표시
    public static void ShowFor(string message, float seconds, bool useBackground = true)
    {
        if (Ready()) instance.Begin(message, seconds, false, useBackground);
    }

    public static void Hide()
    {
        if (instance != null) instance.End(false);
    }

    // 로딩이 실제로 끝난 뒤(최소 표시 시간 포함)에 onHidden 호출. 로딩 중이 아니면 즉시 호출
    public static void Hide(Action onHidden)
    {
        if (instance == null || !instance.isShowing)
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

    private void OnDisable()
    {
        KillAll();
        isShowing = false;
        pendingHidden = null;   // 다른 이유로 꺼진 경우 남은 콜백 폐기
        if (group != null) SetGroup(0f, false);
        BlockNavigation(false);
    }

    private void OnDestroy()
    {
        KillAll();
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

    private void Begin(string text, float autoHideSeconds, bool warnOnTimeout, bool useBackground)
    {
        message = Localization.Get(text ?? string.Empty);
        shownAt = Time.unscaledTime;
        SetCover(useBackground);

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

    // 단색 커버가 없으면 배경 커버를 그대로 쓴다
    private void SetCover(bool useBackground)
    {
        bool solid = !useBackground && coverSolid != null;
        if (coverBackground != null) coverBackground.SetActive(!solid);
        if (coverSolid != null) coverSolid.SetActive(solid);
    }

    private void End(bool force)
    {
        if (!isShowing) return;

        timerTween?.Kill();

        float remaining = minVisibleSeconds - (Time.unscaledTime - shownAt);
        if (!force && remaining > 0f)
        {
            timerTween = DOVirtual.DelayedCall(remaining, () => End(true), true);
            return;
        }

        isShowing = false;
        SetGroup(0f, false);     // 로딩 즉시 숨김
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
        if (label == null) return;

        label.text = $"{message}{new string('.', count)}<alpha=#00>{new string('.', DotCount - count)}";
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