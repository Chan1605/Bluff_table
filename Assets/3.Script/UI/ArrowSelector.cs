using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ArrowSelector : MonoBehaviour
{
    [SerializeField] private Button leftBtn;
    [SerializeField] private Button rightBtn;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private bool loop;   // 끝에서 반대쪽 끝으로 넘어갈지

    private string[] options = Array.Empty<string>();

    public int Index { get; private set; }

    private void OnEnable()
    {
        leftBtn.onClick.AddListener(OnClickLeft);
        rightBtn.onClick.AddListener(OnClickRight);
    }

    private void OnDisable()
    {
        leftBtn.onClick.RemoveListener(OnClickLeft);
        rightBtn.onClick.RemoveListener(OnClickRight);
    }

    public void SetOptions(string[] newOptions, int index)
    {
        options = newOptions;
        Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));
        Refresh();
    }

    private void OnClickLeft() => Step(-1);
    private void OnClickRight() => Step(1);

    private void Step(int dir)
    {
        if (options.Length == 0) return;

        int next = Index + dir;
        next = loop
            ? (next + options.Length) % options.Length
            : Mathf.Clamp(next, 0, options.Length - 1);

        if (next == Index) return;

        Index = next;
        Refresh();
    }

    private void Refresh()
    {
        valueText.text = options.Length > 0 ? options[Index] : string.Empty;
    }
}