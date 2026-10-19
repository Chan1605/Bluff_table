using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임플레이 탭: 언어 / 마우스 감도 / 컨트롤러
// 값은 GameSettings.Language, MouseSensitivityMultiplier, Controller로 읽어서 쓰면 된다.
public class GameplaySettingsUI : MonoBehaviour, ISettingsSection
{
    [SerializeField] private ArrowSelector language;
    [SerializeField] private Slider mouseSensitivity;      // 0 ~ 100
    [SerializeField] private TMP_Text mouseSensitivityText;
    [SerializeField] private ArrowSelector controller;

    private void OnEnable()
    {
        if (mouseSensitivity != null) mouseSensitivity.onValueChanged.AddListener(UpdateSensitivityText);
    }

    private void OnDisable()
    {
        if (mouseSensitivity != null) mouseSensitivity.onValueChanged.RemoveListener(UpdateSensitivityText);
    }

    public void Reload()
    {
        language.SetOptions(GameSettings.LanguageNames, GameSettings.Language);
        controller.SetOptions(GameSettings.ControllerNames, GameSettings.Controller);

        mouseSensitivity.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        UpdateSensitivityText(mouseSensitivity.value);
    }

    public void Apply()
    {
        PlayerPrefs.SetInt(GameSettings.KeyLanguage, language.Index);
        PlayerPrefs.SetInt(GameSettings.KeyMouseSensitivity, Mathf.RoundToInt(mouseSensitivity.value));
        PlayerPrefs.SetInt(GameSettings.KeyController, controller.Index);
    }

    public void ResetDefaults()
    {
        GameSettings.DeleteKeys(GameSettings.GameplayKeys);
    }

    private void UpdateSensitivityText(float value)
    {
        if (mouseSensitivityText != null) mouseSensitivityText.text = Mathf.RoundToInt(value).ToString();
    }
}
