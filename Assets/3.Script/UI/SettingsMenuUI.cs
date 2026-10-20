using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 세팅 탭 하나(디스플레이/게임플레이/그래픽/오디오)가 구현
public interface ISettingsSection
{
    void Reload();          // 저장된 값으로 UI를 되돌림 (적용 안 한 변경은 버림)
    void Apply();           // UI 값을 PlayerPrefs에 기록 (실제 적용은 GameSettings)
    void ResetDefaults();   // 저장된 값을 지워 기본값으로
}

// 타이틀 세팅 패널 / 인게임 ESC 세팅에서 같이 쓴다.
public class SettingsMenuUI : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        public string title;              // 헤더에 표시할 이름 (예: DISPLAY)
        public Button button;
        public HoverUnderline underline;  // 선택된 탭은 밑줄 유지
        public GameObject content;
    }

    [SerializeField] private UIPanelManager panelManager;
    [SerializeField] private TMP_Text settingsTitle;      // SETTINGS (탭 이름을 이 글자 뒤에 붙임)
    [SerializeField] private float headerTabGap = 50f;
    [SerializeField] private TMP_Text headerTabText;      // SETTINGS 옆 탭 이름
    [SerializeField] private RectTransform headerLine;    // 탭 이름 오른쪽 가로선
    [SerializeField] private float headerLineRight = 853f;
    [SerializeField] private float headerLineGap = 40f;
    [SerializeField] private Tab[] tabs;
    [SerializeField] private MonoBehaviour[] sections;    // ISettingsSection 구현 컴포넌트들
    [SerializeField] private Button backBtn;
    [SerializeField] private Button applyBtn;
    [SerializeField] private Button restoreBtn;

    public event Action BackRequested;

    private UnityAction[] tabActions;
    private int current = -1;

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
            if (tabs[i].button != null) tabs[i].button.onClick.AddListener(tabActions[i]);

        if (backBtn != null) backBtn.onClick.AddListener(OnClickBack);
        if (applyBtn != null) applyBtn.onClick.AddListener(OnClickApply);
        if (restoreBtn != null) restoreBtn.onClick.AddListener(OnClickRestore);

        Localization.LanguageChanged += OnLanguageChanged;

        ReloadSections(); // 열 때마다 저장된 값으로 초기화
        SelectTab(-1);
    }

    private void OnDisable()
    {
        for (int i = 0; i < tabs.Length; i++)
            if (tabs[i].button != null) tabs[i].button.onClick.RemoveListener(tabActions[i]);

        if (backBtn != null) backBtn.onClick.RemoveListener(OnClickBack);
        if (applyBtn != null) applyBtn.onClick.RemoveListener(OnClickApply);
        if (restoreBtn != null) restoreBtn.onClick.RemoveListener(OnClickRestore);
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    // 언어를 바꿔 Apply 하면 헤더(탭 이름)와 가로선 위치도 다시 맞춤.
    // SETTINGS 글자(LocalizedText)가 먼저 바뀐 뒤 재야 해서 프레임 끝에서 처리
    private bool relayout;

    private void OnLanguageChanged() { relayout = true; }

    private void LateUpdate()
    {
        if (!relayout) return;

        relayout = false;
        if (current >= 0) SelectTab(current);
    }

    // -1 = 탭 선택 없음 (목록 화면)
    private void SelectTab(int index)
    {
        current = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].content != null) tabs[i].content.SetActive(i == index);
            if (tabs[i].underline != null) tabs[i].underline.SetSelected(i == index);
        }

        bool open = index >= 0;
        if (headerTabText != null)
        {
            headerTabText.text = open ? Localization.Get(tabs[index].title) : string.Empty;
            if (open) LayoutHeaderTab();
        }
        if (headerLine != null)
        {
            headerLine.gameObject.SetActive(open);
            if (open) LayoutHeaderLine();
        }

        if (applyBtn != null) applyBtn.gameObject.SetActive(open);
        if (restoreBtn != null) restoreBtn.gameObject.SetActive(!open);
    }

    // 탭 이름을 SETTINGS 글자 끝 + 간격 위치로 옮김 (글자 크기가 바뀌어도 겹치지 않게)
    private void LayoutHeaderTab()
    {
        if (settingsTitle == null) return;

        RectTransform parent = (RectTransform)headerTabText.rectTransform.parent;
        float titleRight = TextLeft(settingsTitle, parent) + settingsTitle.GetPreferredValues(settingsTitle.text).x;
        float delta = titleRight + headerTabGap - TextLeft(headerTabText, parent);

        Vector3 local = headerTabText.rectTransform.localPosition;
        headerTabText.rectTransform.localPosition = new Vector3(local.x + delta, local.y, local.z);
    }

    // 왼쪽 정렬 글자의 시작 x (parent 좌표)
    private static float TextLeft(TMP_Text text, RectTransform parent)
    {
        Vector3[] corners = new Vector3[4];
        text.rectTransform.GetWorldCorners(corners);
        return parent.InverseTransformPoint(corners[0]).x + text.margin.x;
    }

    // 가로선을 탭 이름 끝에서 시작하도록 맞춤 (이름 길이가 탭마다 달라서)
    private void LayoutHeaderLine()
    {
        if (headerTabText == null) return;

        RectTransform parent = (RectTransform)headerLine.parent;
        float start = TextLeft(headerTabText, parent) + headerTabText.GetPreferredValues(headerTabText.text).x + headerLineGap;

        headerLine.anchorMin = headerLine.anchorMax = new Vector2(0.5f, 0.5f);
        headerLine.pivot = new Vector2(0f, 0.5f);
        headerLine.anchoredPosition = new Vector2(start - parent.rect.center.x, headerLine.anchoredPosition.y);
        headerLine.sizeDelta = new Vector2(Mathf.Max(0f, headerLineRight - start), headerLine.sizeDelta.y);
    }

    private void ReloadSections()
    {
        foreach (MonoBehaviour section in sections)
            if (section is ISettingsSection s) s.Reload();
    }

    // 탭 안에서 Back = 목록으로 (적용 안 한 변경은 버림), 목록에서 Back = 패널 닫기
    private void OnClickBack()
    {
        if (current >= 0)
        {
            ReloadSections();
            SelectTab(-1);
            return;
        }

        if (panelManager != null) panelManager.Back();
        else BackRequested?.Invoke();
    }

    private void OnClickApply()
    {
        foreach (MonoBehaviour section in sections)
            if (section is ISettingsSection s) s.Apply();

        PlayerPrefs.Save();
        GameSettings.ApplyDisplay();
        GameSettings.ApplyAll();
    }

    private void OnClickRestore()
    {
        foreach (MonoBehaviour section in sections)
            if (section is ISettingsSection s) s.ResetDefaults();

        PlayerPrefs.Save();
        GameSettings.ApplyDisplay();
        GameSettings.ApplyAll();
        ReloadSections();
    }
}
