using UnityEngine;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private Button enterBtn;
    [SerializeField] private Button settingBtn;
    [SerializeField] private Button exitBtn;
    [SerializeField] private Button StoriesBtn;

    private void OnEnable()
    {
        enterBtn.onClick.AddListener(OnClickEnter);
        settingBtn.onClick.AddListener(OnClickSetting);
        StoriesBtn.onClick.AddListener(OnClickStories);
        exitBtn.onClick.AddListener(OnClickExit);
    }

    private void OnDisable()
    {
        enterBtn.onClick.RemoveListener(OnClickEnter);
        settingBtn.onClick.RemoveListener(OnClickSetting);
        StoriesBtn.onClick.RemoveListener(OnClickStories);
        exitBtn.onClick.RemoveListener(OnClickExit);
    }

    private void OnClickEnter() { panelManager.Open(PanelId.Play); }
    private void OnClickSetting() { panelManager.Open(PanelId.Settings); }
    private void OnClickStories() { Debug.Log("스토리 패널"); }
    private void OnClickExit() { Debug.Log("종료"); }

}