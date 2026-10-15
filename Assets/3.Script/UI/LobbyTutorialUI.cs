using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


public class LobbyTutorialUI : MonoBehaviour
{
    [SerializeField] private RectTransform tutorialPanel;   
    [SerializeField] private Button guideBtn;              
    [SerializeField] private Button closeBtn;          
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private float slideDistance = 1400f;   // 화면 밖으로 빠지는 거리
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    private Vector2 homePos;
    private bool isOpen;
    private Tween tween;

    private void Awake()
    {
        if (tutorialPanel != null) homePos = tutorialPanel.anchoredPosition;
    }

    private void OnEnable()
    {
        if (guideBtn != null) guideBtn.onClick.AddListener(Show);
        if (closeBtn != null) closeBtn.onClick.AddListener(Hide);
        HideImmediate();
    }

    private void OnDisable()
    {
        if (guideBtn != null) guideBtn.onClick.RemoveListener(Show);
        if (closeBtn != null) closeBtn.onClick.RemoveListener(Hide);
        tween?.Kill();
    }

    private void OnDestroy()
    {
        tween?.Kill();
    }

    private Vector2 HiddenPos => homePos + Vector2.up * slideDistance;

    private void HideImmediate()
    {
        if (tutorialPanel == null) return;

        tween?.Kill();
        isOpen = false;
        tutorialPanel.anchoredPosition = homePos;
        tutorialPanel.gameObject.SetActive(false);
    }

    private void Show()
    {
        if (tutorialPanel == null || isOpen) return;

        isOpen = true;
        tween?.Kill();

        // 닫히는 도중이면 현재 위치에서 이어서, 꺼져 있었다면 화면 위에서 시작
        if (!tutorialPanel.gameObject.activeSelf)
        {
            tutorialPanel.gameObject.SetActive(true);
            tutorialPanel.anchoredPosition = HiddenPos;
        }

        tween = tutorialPanel.DOAnchorPos(homePos, duration)
            .SetEase(showEase)
            .SetUpdate(true);
    }

    private void Hide()
    {
        if (tutorialPanel == null || !isOpen) return;

        isOpen = false;
        tween?.Kill();

        tween = tutorialPanel.DOAnchorPos(HiddenPos, duration)
            .SetEase(hideEase)
            .SetUpdate(true)
            .OnComplete(() => tutorialPanel.gameObject.SetActive(false));
    }
}