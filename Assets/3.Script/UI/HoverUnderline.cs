using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverUnderline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform underline;
    [SerializeField] private float duration = 0.15f;    // 펼쳐지는 시간
    [SerializeField] private Ease showEase = Ease.OutQuad;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    private Tween tween;
    private bool selected;   // 선택된 탭은 마우스가 나가도 밑줄 유지

    private void Awake()
    {
        underline.localScale = new Vector3(0f, 1f, 1f);
    }

    private void OnDisable()
    {
        tween?.Kill();
        underline.localScale = new Vector3(0f, 1f, 1f);
    }

    private void OnDestroy()
    {
        tween?.Kill();
    }

    public void OnPointerEnter(PointerEventData eventData) => Play(1f, showEase);
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!selected) Play(0f, hideEase);
    }

    public void SetSelected(bool value)
    {
        selected = value;
        Play(value ? 1f : 0f, value ? showEase : hideEase);
    }

    private void Play(float targetX, Ease ease)
    {
        tween?.Kill();
        tween = underline.DOScaleX(targetX, duration)
                         .SetEase(ease)
                         .SetUpdate(true);
    }
}