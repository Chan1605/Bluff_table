using UnityEngine;
using UnityEngine.UI;

// 그래픽 탭: 프리셋을 고르면 세부 항목이 같이 바뀌고, 세부 항목을 바꾸면 프리셋이 Custom이 된다.
public class GraphicsSettingsUI : MonoBehaviour, ISettingsSection
{
    [SerializeField] private ArrowSelector preset;
    [SerializeField] private ArrowSelector quality;
    [SerializeField] private ArrowSelector antiAliasing;
    [SerializeField] private ArrowSelector antiAliasingQuality;
    [SerializeField] private ArrowSelector texture;
    [SerializeField] private ArrowSelector shadow;
    [SerializeField] private ArrowSelector fog;
    [SerializeField] private Toggle filmGrain;
    [SerializeField] private Toggle panini;
    [SerializeField] private Toggle vignette;

    private ArrowSelector[] Details => new[] { quality, antiAliasing, antiAliasingQuality, texture, shadow };

    private void OnEnable()
    {
        preset.Changed += OnPresetChanged;
        foreach (ArrowSelector detail in Details) detail.Changed += OnDetailChanged;
    }

    private void OnDisable()
    {
        preset.Changed -= OnPresetChanged;
        foreach (ArrowSelector detail in Details) detail.Changed -= OnDetailChanged;
    }

    public void Reload()
    {
        preset.SetOptions(GameSettings.PresetNames, GameSettings.GraphicsPreset);
        quality.SetOptions(QualitySettings.names, GameSettings.QualityLevel);
        antiAliasing.SetOptions(GameSettings.AntiAliasingNames, GameSettings.AntiAliasing);
        antiAliasingQuality.SetOptions(GameSettings.LowMediumHigh, GameSettings.AntiAliasingQuality);
        texture.SetOptions(GameSettings.LowMediumHigh, GameSettings.TextureQuality);
        shadow.SetOptions(GameSettings.ShadowNames, GameSettings.ShadowQuality);
        fog.SetOptions(GameSettings.OffOn, GameSettings.Fog ? 1 : 0);

        filmGrain.SetIsOnWithoutNotify(GameSettings.FilmGrainOn);
        panini.SetIsOnWithoutNotify(GameSettings.PaniniOn);
        vignette.SetIsOnWithoutNotify(GameSettings.VignetteOn);
    }

    public void Apply()
    {
        PlayerPrefs.SetInt(GameSettings.KeyPreset, preset.Index);
        PlayerPrefs.SetInt(GameSettings.KeyQuality, quality.Index);
        PlayerPrefs.SetInt(GameSettings.KeyAntiAliasing, antiAliasing.Index);
        PlayerPrefs.SetInt(GameSettings.KeyAntiAliasingQuality, antiAliasingQuality.Index);
        PlayerPrefs.SetInt(GameSettings.KeyTexture, texture.Index);
        PlayerPrefs.SetInt(GameSettings.KeyShadow, shadow.Index);
        PlayerPrefs.SetInt(GameSettings.KeyFog, fog.Index);
        PlayerPrefs.SetInt(GameSettings.KeyFilmGrain, filmGrain.isOn ? 1 : 0);
        PlayerPrefs.SetInt(GameSettings.KeyPanini, panini.isOn ? 1 : 0);
        PlayerPrefs.SetInt(GameSettings.KeyVignette, vignette.isOn ? 1 : 0);
    }

    public void ResetDefaults()
    {
        GameSettings.DeleteKeys(GameSettings.GraphicsKeys);
    }

    private void OnPresetChanged(int index)
    {
        if (index == GameSettings.CustomPreset) return;

        GameSettings.GetPresetValues(index, out int q, out int aa, out int aaQ, out int tex, out int sh);
        quality.SetOptions(QualitySettings.names, q);   // SetOptions는 Changed를 보내지 않음
        antiAliasing.SetOptions(GameSettings.AntiAliasingNames, aa);
        antiAliasingQuality.SetOptions(GameSettings.LowMediumHigh, aaQ);
        texture.SetOptions(GameSettings.LowMediumHigh, tex);
        shadow.SetOptions(GameSettings.ShadowNames, sh);
    }

    private void OnDetailChanged(int _)
    {
        preset.SetOptions(GameSettings.PresetNames, GameSettings.CustomPreset);
    }
}
