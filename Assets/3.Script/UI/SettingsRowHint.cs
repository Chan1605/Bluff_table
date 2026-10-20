using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 세팅 한 줄: 마우스를 올리면 항목 이름을 밝게 하고, 설명 칸이 있으면 설명을 보여준다.
public class SettingsRowHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text hintText;           // 그래픽 탭 오른쪽 설명 (선택)
    [SerializeField, TextArea] private string description;
    [SerializeField] private Color normalColor = new Color(0.78f, 0.76f, 0.72f, 1f);
    [SerializeField] private Color hoverColor = Color.white;

    private void OnEnable() { SetHover(false); }
    private void OnDisable() { SetHover(false); }

    public void OnPointerEnter(PointerEventData eventData) { SetHover(true); }
    public void OnPointerExit(PointerEventData eventData) { SetHover(false); }

    private void SetHover(bool hover)
    {
        if (label != null) label.color = hover ? hoverColor : normalColor;
        if (hintText == null) return;

        string localized = Localization.Get(description);
        if (hover) hintText.text = localized;
        else if (hintText.text == localized) hintText.text = string.Empty;
    }
}
