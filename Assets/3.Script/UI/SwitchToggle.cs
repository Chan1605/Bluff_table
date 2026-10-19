using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 원작 스타일 ON/OFF 스위치: Toggle 값에 맞춰 손잡이 위치와 글자를 바꾼다.
// SetIsOnWithoutNotify로 바뀐 값도 반영되도록 이벤트 대신 매 프레임 비교한다.
[RequireComponent(typeof(Toggle))]
public class SwitchToggle : MonoBehaviour
{
    [SerializeField] private RectTransform knob;
    [SerializeField] private float onX = 15f;
    [SerializeField] private float offX = -15f;
    [SerializeField] private TMP_Text stateText;   // ON / OFF (선택)
    [SerializeField] private Graphic knobGraphic;  // 꺼졌을 때 어둡게 (선택)
    [SerializeField] private Color onColor = Color.white;
    [SerializeField] private Color offColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    private Toggle toggle;
    private bool shown;
    private bool initialized;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
    }

    private void OnEnable()
    {
        initialized = false;
        LateUpdate();
    }

    private void LateUpdate()
    {
        bool isOn = toggle.isOn;
        if (initialized && isOn == shown) return;

        initialized = true;
        shown = isOn;

        if (knob != null) knob.anchoredPosition = new Vector2(isOn ? onX : offX, knob.anchoredPosition.y);
        if (stateText != null) stateText.text = isOn ? "ON" : "OFF";
        if (knobGraphic != null) knobGraphic.color = isOn ? onColor : offColor;
    }
}
