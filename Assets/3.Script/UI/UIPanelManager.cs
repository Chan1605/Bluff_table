using System.Collections.Generic;
using UnityEngine;

public class UIPanelManager : MonoBehaviour
{
    [SerializeField] private UIPanel[] panels;
    [SerializeField] private PanelId startPanel = PanelId.Title;
    [SerializeField] private float fadeDuration = 0.2f;

    private readonly Dictionary<PanelId, UIPanel> map = new();
    private readonly Stack<PanelId> history = new();

    private UIPanel current;
    private bool isTransitioning;

    private void Awake()
    {
        foreach (UIPanel panel in panels)
        {
            map[panel.Id] = panel;

            if (panel.Id == startPanel) panel.ShowImmediate();
            else panel.HideImmediate();
        }

        current = map[startPanel];
    }

    public void Open(PanelId id)
    {
        if (isTransitioning) return;

        if (!map.TryGetValue(id, out UIPanel next))
        {
            Debug.LogWarning($"등록되지 않은 패널: {id}");
            return;
        }
        if (next == current) return;

        history.Push(current.Id);
        Switch(next);
    }

    // Back 버튼의 OnClick에 Inspector로 직접 연결해도 됨
    public void Back()
    {
        if (isTransitioning || history.Count == 0) return;

        Switch(map[history.Pop()]);
    }

    private void Switch(UIPanel next)
    {
        UIPanel prev = current;
        current = next;
        isTransitioning = true;

        prev.Hide(fadeDuration, () =>
        {
            next.Show(fadeDuration, () => isTransitioning = false);
        });
    }
}