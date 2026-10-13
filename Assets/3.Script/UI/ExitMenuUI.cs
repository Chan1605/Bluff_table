using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExitMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button backBtn;
    [SerializeField] private Button LeaveBtn;

    private void OnEnable()
    {
        backBtn.onClick.AddListener(OnClickBack);
        LeaveBtn.onClick.AddListener(() => { Application.Quit(); });
    }

    private void OnDisable()
    {
        backBtn.onClick.RemoveListener(OnClickBack);
    }


    private void OnClickBack() { panelManager.Back(); }
}
