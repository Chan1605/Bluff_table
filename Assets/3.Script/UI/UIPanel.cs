using System;
using DG.Tweening;
using UnityEngine;

public enum PanelId { Title, Play, Settings }

[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    [SerializeField] private PanelId id;

    private CanvasGroup group;
    private Tween fade;

    public PanelId Id => id;

    // 비활성 상태에서 먼저 호출돼도 안전하도록 지연 초기화
    private CanvasGroup Group => group != null ? group : (group = GetComponent<CanvasGroup>());

    private void OnDestroy()
    {
        fade?.Kill();
    }

    public void ShowImmediate()
    {
        fade?.Kill();
        gameObject.SetActive(true);
        Group.alpha = 1f;
        SetInteractable(true);
    }

    public void HideImmediate()
    {
        fade?.Kill();
        Group.alpha = 0f;
        SetInteractable(false);
        gameObject.SetActive(false);
    }

    public void Show(float duration, Action onDone = null)
    {
        fade?.Kill();
        gameObject.SetActive(true);
        Group.alpha = 0f;
        SetInteractable(false);

        fade = Group.DOFade(1f, duration).SetUpdate(true).OnComplete(() =>
        {
            SetInteractable(true);
            onDone?.Invoke();
        });
    }

    public void Hide(float duration, Action onDone = null)
    {
        fade?.Kill();
        SetInteractable(false);

        fade = Group.DOFade(0f, duration).SetUpdate(true).OnComplete(() =>
        {
            gameObject.SetActive(false);
            onDone?.Invoke();
        });
    }

    private void SetInteractable(bool value)
    {
        Group.interactable = value;
        Group.blocksRaycasts = value;
    }
}