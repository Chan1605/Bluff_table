using UnityEngine;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
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

    private void OnClickEnter() { Debug.Log("로비생성 패널"); }
    private void OnClickSetting() { Debug.Log("옵션 패널"); }
    private void OnClickStories() { Debug.Log("스토리 패널"); }
    private void OnClickExit() { Debug.Log("종료"); }

}