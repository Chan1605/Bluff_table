using DG.Tweening;
using UnityEngine;

// 패널이 켜질 때 살짝 작게 → 원래 크기로 커지며 나타난다 (로그인 패널 등, LoginPanelRestyler가 붙임)
[RequireComponent(typeof(CanvasGroup))]
public class PanelIntroFx : MonoBehaviour
{
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float fromScale = 0.96f;

    private CanvasGroup group;

    private void Awake() => group = GetComponent<CanvasGroup>();

    private void OnEnable()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        DOTween.Kill(this);
        group.alpha = 0f;
        transform.localScale = Vector3.one * fromScale;
        DOTween.To(() => group.alpha, a => group.alpha = a, 1f, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this);
        transform.DOScale(1f, duration).SetEase(Ease.OutCubic).SetUpdate(true).SetTarget(this);
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        if (group != null) group.alpha = 1f;
        transform.localScale = Vector3.one;
    }
}
