using UnityEngine;
using UnityEngine.Rendering;

// Additive로 올라온 씬도 Ambient · Fog · 반사 세기는 Active Scene(TitleScene, 밝은 기본 Skybox)의 설정을 따른다.
// 이 오브젝트가 켜져 있는 동안만 값을 바꾸고, 꺼질 때(씬 언로드 포함) 원래 값으로 돌려놓는다. 대기실 LobbyScene에 둔다.
public class SceneLightingOverride : MonoBehaviour
{
    [Tooltip("주변광 (원작 로비처럼 어둡고 따뜻하게)")]
    [SerializeField] private Color ambientColor = new Color(0.08f, 0.065f, 0.055f);
    [SerializeField] private bool fog = true;
    [SerializeField] private Color fogColor = new Color(0.04f, 0.03f, 0.025f);
    [SerializeField, Range(0f, 0.2f)] private float fogDensity = 0.04f;
    [Tooltip("Skybox 반사 세기. 낮을수록 바닥 · 유리의 하늘색 반사가 줄어듦")]
    [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.3f;

    private bool applied;
    private AmbientMode savedAmbientMode;
    private Color savedAmbientLight, savedEquator, savedGround;
    private float savedAmbientIntensity;
    private bool savedFog;
    private Color savedFogColor;
    private FogMode savedFogMode;
    private float savedFogDensity;
    private float savedReflection;

    private void OnEnable()
    {
        savedAmbientMode = RenderSettings.ambientMode;
        savedAmbientLight = RenderSettings.ambientLight;
        savedEquator = RenderSettings.ambientEquatorColor;
        savedGround = RenderSettings.ambientGroundColor;
        savedAmbientIntensity = RenderSettings.ambientIntensity;
        savedFog = RenderSettings.fog;
        savedFogColor = RenderSettings.fogColor;
        savedFogMode = RenderSettings.fogMode;
        savedFogDensity = RenderSettings.fogDensity;
        savedReflection = RenderSettings.reflectionIntensity;

        applied = true;
        Apply();
    }

    private void OnDisable()
    {
        if (!applied) return;
        applied = false;

        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientLight;
        RenderSettings.ambientEquatorColor = savedEquator;
        RenderSettings.ambientGroundColor = savedGround;
        RenderSettings.ambientIntensity = savedAmbientIntensity;
        RenderSettings.fog = savedFog;
        RenderSettings.fogColor = savedFogColor;
        RenderSettings.fogMode = savedFogMode;
        RenderSettings.fogDensity = savedFogDensity;
        RenderSettings.reflectionIntensity = savedReflection;
    }

    // 플레이 중 Inspector에서 값을 바꾸면 바로 반영
    private void OnValidate()
    {
        if (applied && Application.isPlaying) Apply();
    }

    private void Apply()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.reflectionIntensity = reflectionIntensity;
    }
}
