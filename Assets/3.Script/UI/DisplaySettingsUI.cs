using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplaySettingsUI : MonoBehaviour, ISettingsSection
{
    [SerializeField] private ArrowSelector resolution;
    [SerializeField] private ArrowSelector vSync;
    [SerializeField] private ArrowSelector maxFps;
    [SerializeField] private Toggle windowMode;       // ON = 창 모드
    [SerializeField] private TMP_Text windowModeText;
    [SerializeField] private Slider brightness;
    [SerializeField] private TMP_Text brightnessText;

    private static readonly string[] VSyncLabels = { "Off", "On" };
    private static readonly int[] FpsValues = { 30, 60, 120, 144, 240, -1 };
    private static readonly string[] FpsLabels = { "30", "60", "120", "144", "240", "Unlimited" };

    private readonly List<Vector2Int> resolutions = new();

    private void OnEnable()
    {
        windowMode.onValueChanged.AddListener(OnWindowModeChanged);
        brightness.onValueChanged.AddListener(OnBrightnessChanged);
    }

    private void OnDisable()
    {
        windowMode.onValueChanged.RemoveListener(OnWindowModeChanged);
        brightness.onValueChanged.RemoveListener(OnBrightnessChanged);
    }

    // 설정 패널이 열릴 때 호출 저장된 값(없으면 현재 값)으로 UI를 되돌림.
    // Apply 하지 않은 변경은 버려진다.
    public void Reload()
    {
        BuildResolutionList();

        int w = PlayerPrefs.GetInt(GameSettings.KeyResW, Screen.width);
        int h = PlayerPrefs.GetInt(GameSettings.KeyResH, Screen.height);
        int resIndex = resolutions.IndexOf(new Vector2Int(w, h));
        if (resIndex < 0) resIndex = resolutions.Count - 1;
        resolution.SetOptions(resolutions.ConvertAll(r => $"{r.x}x{r.y}").ToArray(), resIndex);

        vSync.SetOptions(VSyncLabels, PlayerPrefs.GetInt(GameSettings.KeyVSync, 0));

        int fpsIndex = Array.IndexOf(FpsValues, PlayerPrefs.GetInt(GameSettings.KeyMaxFps, 60));
        if (fpsIndex < 0) fpsIndex = 1;
        maxFps.SetOptions(FpsLabels, fpsIndex);

        bool windowed = PlayerPrefs.GetInt(GameSettings.KeyWindowed, 1) == 1;
        windowMode.SetIsOnWithoutNotify(windowed);
        UpdateWindowModeText(windowed);

        float value = GameSettings.Brightness;
        brightness.SetValueWithoutNotify(value);
        UpdateBrightnessText(value);
    }

    // 값만 기록한다. 실제 적용(해상도 · 밝기 등)은 SettingsMenuUI가 GameSettings로 한 번에 한다.
    public void Apply()
    {
        Vector2Int res = resolutions[resolution.Index];

        PlayerPrefs.SetInt(GameSettings.KeyResW, res.x);
        PlayerPrefs.SetInt(GameSettings.KeyResH, res.y);
        PlayerPrefs.SetInt(GameSettings.KeyVSync, vSync.Index);
        PlayerPrefs.SetInt(GameSettings.KeyMaxFps, FpsValues[maxFps.Index]);
        PlayerPrefs.SetInt(GameSettings.KeyWindowed, windowMode.isOn ? 1 : 0);
        PlayerPrefs.SetFloat(GameSettings.KeyBrightness, brightness.value);
    }

    public void ResetDefaults()
    {
        GameSettings.DeleteKeys(GameSettings.DisplayKeys);
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();

        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }

        if (resolutions.Count == 0)
            resolutions.Add(new Vector2Int(Screen.width, Screen.height));
    }

    private void OnWindowModeChanged(bool isOn) => UpdateWindowModeText(isOn);
    private void OnBrightnessChanged(float value) => UpdateBrightnessText(value);

    private void UpdateWindowModeText(bool isOn) => windowModeText.text = isOn ? "ON" : "OFF";
    private void UpdateBrightnessText(float value) => brightnessText.text = value.ToString("0.#");
}
