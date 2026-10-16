using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [System.Serializable]
    private class Tab
    {
        public string title;        // 헤더에 표시할 이름 (예: DISPLAY)
        public Button button;
        public GameObject content;
    }

    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private TMP_Text headerTabText;
    [SerializeField] private Tab[] tabs;
    [SerializeField] private DisplaySettingsUI displaySettings;
    [SerializeField] private Button backBtn;
    [SerializeField] private Button applyBtn;

    private UnityAction[] tabActions;

    private void Awake()
    {
        tabActions = new UnityAction[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i; // 클로저용 로컬 복사
            tabActions[i] = () => SelectTab(index);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].button.onClick.AddListener(tabActions[i]);

        backBtn.onClick.AddListener(OnClickBack);
        applyBtn.onClick.AddListener(OnClickApply);

        displaySettings.Reload(); // 열 때마다 저장된 값으로 초기화
        SelectTab(0);
    }

    private void OnDisable()
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].button.onClick.RemoveListener(tabActions[i]);

        backBtn.onClick.RemoveListener(OnClickBack);
        applyBtn.onClick.RemoveListener(OnClickApply);
    }

    private void SelectTab(int index)
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].content.SetActive(i == index);

        headerTabText.text = tabs[index].title;
    }

    private void OnClickBack() { panelManager.Back(); }
    private void OnClickApply() { displaySettings.Apply(); }
}