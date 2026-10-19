using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 저장된 설정값(PlayerPrefs)을 읽고 게임에 적용하는 곳.
// 세팅 패널(UI)은 값을 고르고 저장만 하고, 실제 적용은 여기서 한다.
// 게임 시작 시 + 씬이 로드될 때마다 자동으로 다시 적용된다 (새 씬의 카메라/볼륨/라이트에도 반영되도록).
public static class GameSettings
{
    // Display (DisplaySettingsUI)
    public const string KeyResW = "Display.ResW";
    public const string KeyResH = "Display.ResH";
    public const string KeyVSync = "Display.VSync";
    public const string KeyMaxFps = "Display.MaxFps";
    public const string KeyWindowed = "Display.Windowed";
    public const string KeyBrightness = "Display.Brightness";

    // Gameplay
    public const string KeyLanguage = "Gameplay.Language";
    public const string KeyMouseSensitivity = "Gameplay.MouseSensitivity";
    public const string KeyController = "Gameplay.Controller";

    // Graphics
    public const string KeyPreset = "Graphics.Preset";
    public const string KeyQuality = "Graphics.Quality";
    public const string KeyAntiAliasing = "Graphics.AntiAliasing";
    public const string KeyAntiAliasingQuality = "Graphics.AntiAliasingQuality";
    public const string KeyTexture = "Graphics.Texture";
    public const string KeyShadow = "Graphics.Shadow";
    public const string KeyFog = "Graphics.Fog";
    public const string KeyFilmGrain = "Graphics.FilmGrain";
    public const string KeyPanini = "Graphics.Panini";
    public const string KeyVignette = "Graphics.Vignette";

    // Audio
    public const string KeyMainVolume = "Audio.Main";
    public const string KeyMusicVolume = "Audio.Music";
    public const string KeyDialogueVolume = "Audio.Dialogue";

    public static readonly string[] DisplayKeys = { KeyResW, KeyResH, KeyVSync, KeyMaxFps, KeyWindowed, KeyBrightness };
    public static readonly string[] GameplayKeys = { KeyLanguage, KeyMouseSensitivity, KeyController };
    public static readonly string[] GraphicsKeys =
    {
        KeyPreset, KeyQuality, KeyAntiAliasing, KeyAntiAliasingQuality, KeyTexture,
        KeyShadow, KeyFog, KeyFilmGrain, KeyPanini, KeyVignette,
    };
    public static readonly string[] AudioKeys = { KeyMainVolume, KeyMusicVolume, KeyDialogueVolume };

    // 선택지 (UI와 공유)
    public static readonly string[] OffOn = { "Off", "On" };
    public static readonly string[] LowMediumHigh = { "Low", "Medium", "High" };
    public static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
    public static readonly string[] AntiAliasingNames = { "Off", "FXAA", "SMAA", "TAA" };
    public static readonly string[] PresetNames = { "Low", "Medium", "High", "Custom" };
    public static readonly string[] LanguageNames = { "English", "Korean" };
    public static readonly string[] ControllerNames = { "Keyboard", "Gamepad" };
    public const int CustomPreset = 3;

    // 값이 적용될 때마다 (VolumeChannel 등 구독용)
    public static event Action Changed;

    // ---- 읽기 ----
    public static float Brightness => PlayerPrefs.GetFloat(KeyBrightness, 1f);

    public static int Language => PlayerPrefs.GetInt(KeyLanguage, 0);
    public static int MouseSensitivity => PlayerPrefs.GetInt(KeyMouseSensitivity, 50);          // 0 ~ 100
    public static float MouseSensitivityMultiplier => Mathf.Max(MouseSensitivity, 1) / 50f;     // 50 = 1배
    public static int Controller => PlayerPrefs.GetInt(KeyController, 0);

    public static int GraphicsPreset => PlayerPrefs.GetInt(KeyPreset, 2);
    public static int QualityLevel => Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()),
                                                  0, Mathf.Max(0, QualitySettings.names.Length - 1));
    public static int AntiAliasing => PlayerPrefs.GetInt(KeyAntiAliasing, 3);
    public static int AntiAliasingQuality => PlayerPrefs.GetInt(KeyAntiAliasingQuality, 2);
    public static int TextureQuality => PlayerPrefs.GetInt(KeyTexture, 2);
    public static int ShadowQuality => PlayerPrefs.GetInt(KeyShadow, 3);
    public static bool Fog => PlayerPrefs.GetInt(KeyFog, 1) == 1;
    public static bool FilmGrainOn => PlayerPrefs.GetInt(KeyFilmGrain, 1) == 1;
    public static bool PaniniOn => PlayerPrefs.GetInt(KeyPanini, 1) == 1;
    public static bool VignetteOn => PlayerPrefs.GetInt(KeyVignette, 1) == 1;

    public static float MainVolume => PlayerPrefs.GetFloat(KeyMainVolume, 1f);
    public static float MusicVolume => PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
    public static float DialogueVolume => PlayerPrefs.GetFloat(KeyDialogueVolume, 1f);

    // 프리셋 → 세부 항목 값
    public static void GetPresetValues(int preset, out int quality, out int aa, out int aaQuality, out int texture, out int shadow)
    {
        int last = Mathf.Max(0, QualitySettings.names.Length - 1);
        switch (preset)
        {
            case 0: quality = 0; aa = 0; aaQuality = 0; texture = 0; shadow = 1; break;
            case 1: quality = last / 2; aa = 1; aaQuality = 1; texture = 1; shadow = 2; break;
            default: quality = last; aa = 3; aaQuality = 2; texture = 2; shadow = 3; break;
        }
    }

    public static void DeleteKeys(string[] keys)
    {
        foreach (string key in keys) PlayerPrefs.DeleteKey(key);
    }

    // ---- 적용 ----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CaptureSceneDefaults();
        ApplyDisplay();
        ApplyAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) CaptureSceneDefaults();
        ApplyAll();
    }

    // 화면 모드/해상도는 저장된 값이 있을 때만 바꾼다 (처음 실행은 플레이어 설정 그대로)
    public static void ApplyDisplay()
    {
        if (PlayerPrefs.HasKey(KeyResW) && PlayerPrefs.HasKey(KeyResH))
        {
            bool windowed = PlayerPrefs.GetInt(KeyWindowed, 1) == 1;
            Screen.SetResolution(PlayerPrefs.GetInt(KeyResW), PlayerPrefs.GetInt(KeyResH),
                windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        }

        QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVSync, 0);          // 0: Off, 1: On
        Application.targetFrameRate = PlayerPrefs.GetInt(KeyMaxFps, 60);       // -1: 제한 없음
    }

    // 그래픽 · 볼륨(포스트 프로세싱) · 오디오
    public static void ApplyAll()
    {
        int quality = QualityLevel;
        if (QualitySettings.names.Length > 0 && QualitySettings.GetQualityLevel() != quality)
            QualitySettings.SetQualityLevel(quality, true);   // 렌더 파이프라인 에셋이 바뀔 수 있어 가장 먼저

        QualitySettings.globalTextureMipmapLimit = 2 - Mathf.Clamp(TextureQuality, 0, 2);   // High = 원본 해상도

        ApplyCameras();
        ApplyLights();
        ApplyVolumes();
        RenderSettings.fog = Fog && sceneFogDefault;

        AudioListener.volume = MainVolume;

        Changed?.Invoke();
    }

    private static bool sceneFogDefault = true;
    private static readonly Dictionary<VolumeComponent, bool> componentDefaults = new();
    private static readonly Dictionary<VolumeProfile, float> exposureDefaults = new();

    private static void CaptureSceneDefaults()
    {
        sceneFogDefault = RenderSettings.fog;
        componentDefaults.Clear();
        exposureDefaults.Clear();
    }

    private static void ApplyCameras()
    {
        AntialiasingMode mode = AntiAliasing switch
        {
            1 => AntialiasingMode.FastApproximateAntialiasing,
            2 => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
            3 => AntialiasingMode.TemporalAntiAliasing,
            _ => AntialiasingMode.None,
        };
        AntialiasingQuality aaQuality = (AntialiasingQuality)Mathf.Clamp(AntiAliasingQuality, 0, 2);

        foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (!cam.TryGetComponent(out UniversalAdditionalCameraData data)) continue;

            data.antialiasing = mode;
            data.antialiasingQuality = aaQuality;
            data.renderShadows = ShadowQuality > 0;
        }
    }

    // 그림자가 있던 라이트만 품질을 바꾼다 (그림자 없는 라이트를 켜지 않음)
    private static void ApplyLights()
    {
        int shadow = ShadowQuality;
        if (shadow <= 0) return;   // Off는 카메라에서 끔

        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.shadows == LightShadows.None) continue;

            light.shadows = shadow == 1 ? LightShadows.Hard : LightShadows.Soft;
            light.shadowResolution = shadow switch
            {
                1 => UnityEngine.Rendering.LightShadowResolution.Low,
                2 => UnityEngine.Rendering.LightShadowResolution.Medium,
                _ => UnityEngine.Rendering.LightShadowResolution.High,
            };
        }
    }

    // 씬 볼륨의 복제본(volume.profile)만 바꾼다. 원본 프로필 에셋은 건드리지 않음.
    // ON = 씬에서 설정한 그대로, OFF = 끔
    private static void ApplyVolumes()
    {
        float exposure = Mathf.Log(Mathf.Max(Brightness, 0.01f), 2f);   // 밝기 1 = 노출 변화 없음

        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (volume.sharedProfile == null) continue;
            VolumeProfile profile = volume.profile;

            SetOverride<FilmGrain>(profile, FilmGrainOn);
            SetOverride<PaniniProjection>(profile, PaniniOn);
            SetOverride<Vignette>(profile, VignetteOn);

            if (!volume.isGlobal) continue;

            bool created = false;
            if (!profile.TryGet(out ColorAdjustments color))
            {
                if (Mathf.Approximately(exposure, 0f)) continue;   // 밝기 기본값이면 굳이 추가하지 않음
                color = profile.Add<ColorAdjustments>();
                exposureDefaults[profile] = 0f;
                created = true;
            }
            // 씬에서 꺼 둔 Color Adjustments를 밝기 기본값 때문에 켜지 않음 (다른 보정값까지 켜지므로)
            if (!created && !color.active && Mathf.Approximately(exposure, 0f)) continue;
            if (!exposureDefaults.TryGetValue(profile, out float baseExposure))
            {
                baseExposure = color.postExposure.overrideState ? color.postExposure.value : 0f;
                exposureDefaults[profile] = baseExposure;
            }

            color.active = true;
            color.postExposure.Override(baseExposure + exposure);
        }
    }

    private static void SetOverride<T>(VolumeProfile profile, bool enabled) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component)) return;

        if (!componentDefaults.TryGetValue(component, out bool original))
        {
            original = component.active;
            componentDefaults[component] = original;
        }
        component.active = enabled && original;
    }
}
