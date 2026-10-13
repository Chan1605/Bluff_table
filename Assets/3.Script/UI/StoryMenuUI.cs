using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StoryMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button backBtn;
    [SerializeField] GameObject[] PageGroup;
    [SerializeField] private Button LeftBtn;
    [SerializeField] private Button RightBtn;
     private bool loop = true;   

    private int currentPage;

    private void OnEnable()
    {
        backBtn.onClick.AddListener(OnClickBack);
        LeftBtn.onClick.AddListener(OnClickLeft);
        RightBtn.onClick.AddListener(OnClickRight);

        ShowPage(0);
    }

    private void OnDisable()
    {
        backBtn.onClick.RemoveListener(OnClickBack);
        LeftBtn.onClick.RemoveListener(OnClickLeft);
        RightBtn.onClick.RemoveListener(OnClickRight);
    }

    private void OnClickBack() { panelManager.Back(); }

    private void OnClickLeft() { MovePage(-1); }
    private void OnClickRight() { MovePage(1); }

    private void MovePage(int dir)
    {
        if (PageGroup.Length == 0) return;

        int next = currentPage + dir;
        next = loop
            ? (next + PageGroup.Length) % PageGroup.Length
            : Mathf.Clamp(next, 0, PageGroup.Length - 1);

        if (next == currentPage) return;

        ShowPage(next);
    }

    private void ShowPage(int index)
    {
        currentPage = index;

        for (int i = 0; i < PageGroup.Length; i++)
            PageGroup[i].SetActive(i == index);

    }
}