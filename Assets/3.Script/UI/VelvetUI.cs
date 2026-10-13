using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine;

public class VelvetUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button backBtn;
    [SerializeField] private VideoPlayer video;

    private void OnEnable()
    {
        backBtn.onClick.AddListener(OnClickBack);
        video.Stop();
        video.Play();
    }

    private void OnDisable()
    {
        backBtn.onClick.RemoveListener(OnClickBack);
        video.Stop();
    }


    private void OnClickBack() { panelManager.Back(); }
}
