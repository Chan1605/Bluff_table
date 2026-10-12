using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button backBtn;

    private void OnEnable()
    {
        backBtn.onClick.AddListener(OnClickBack);
    }

    private void OnDisable()
    {
        backBtn.onClick.RemoveListener(OnClickBack);
    }


    private void OnClickBack() { panelManager.Back(); }
}
