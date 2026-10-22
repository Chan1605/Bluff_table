using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 원작 파티클(총구 화염 · 스파크 · 피) 머티리얼을 URP 파티클 셰이더로 바꾼다
// 원인: 원작 커스텀 셰이더(DisAdd 등) 머티리얼이 URP Lit · 불투명으로 변환됐고, 텍스처는 원작 전용 슬롯(_Texture 등)에만 있어
//       _BaseMap이 비어 있음 → 파티클 하나하나가 흰 사각형으로 보임
// 처리: Sit 프리팹의 ParticleSystemRenderer가 쓰는 머티리얼만 골라
//   - 셰이더 Universal Render Pipeline/Particles/Unlit, 투명
//   - 이름에 Add가 있으면 가산(Additive: 총구 빛 · 스파크), 아니면 알파 블렌드(피 · 연기)
//   - Disto(굴절) 머티리얼은 URP에서 같은 효과가 없어 투명도 0으로 숨김
//   - 텍스처는 원작 슬롯(_Texture, Texture2D_xxx 등)에서 찾아 _BaseMap으로 옮김
//   - 텍스처가 아예 없는 가산 머티리얼(원작 셰이더가 모양을 계산으로 그리던 것, 예: Flat02 → 총구 Spikes)은 흰 덩어리가 되므로 숨김
//   - 원본 그림에 알파가 있는데 임포트에서 빠진 텍스처는 알파를 다시 켬 (안 그러면 사각형 그대로 보임)
// 이미 변환한 머티리얼도 다시 검사한다 (여러 번 실행해도 같은 결과)
// 메뉴: Tools / 원작 캐릭터 / 파티클 머티리얼(흰 사각형) - 미리보기 · URP 파티클로 변환
public static class ParticleMaterialFixer
{
    private const string MenuRoot = "Tools/원작 캐릭터/";
    private const string SitFolder = "Assets/2.Model/Original/Characters/_Export/Sit_Character";
    private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

    // 원작 텍스처 슬롯 우선순위 (모양을 그리는 쪽 먼저, 디졸브 · 노이즈는 마지막)
    private static readonly string[] PreferredSlots = { "_BaseMap", "_MainTex", "_Texture", "_ColorTexture", "_GradientShape" };
    private static readonly string[] SkipSlots = { "_DissolveMask", "_DistoMask", "_DistortionNoiseTexture", "_GradientMap", "_BumpMap", "unity_" };

    private enum Mode { Additive, Alpha, Hidden }

    [MenuItem(MenuRoot + "파티클 머티리얼(흰 사각형) - 미리보기")]
    private static void Preview() => Run(false);

    [MenuItem(MenuRoot + "파티클 머티리얼(흰 사각형) - URP 파티클로 변환")]
    private static void Apply() => Run(true);

    private static void Run(bool write)
    {
        Shader particle = Shader.Find(ParticleShader);
        if (particle == null) { Debug.LogError($"[파티클 머티리얼] 셰이더 없음: {ParticleShader}"); return; }

        var targets = new SortedDictionary<string, Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SitFolder }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null) continue;
            foreach (ParticleSystemRenderer r in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                foreach (Material m in r.sharedMaterials.Concat(new[] { r.trailMaterial }))
                {
                    string path = m != null ? AssetDatabase.GetAssetPath(m) : null;
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;   // 기본 내장 머티리얼 제외
                    targets[path] = m;
                }
        }

        var log = new StringBuilder($"[파티클 머티리얼] {(write ? "변환" : "미리보기")}: {targets.Count}개\n");
        foreach (var pair in targets)
        {
            Material m = pair.Value;
            Texture tex = FindTexture(m, out string slot);
            Mode mode = ModeOf(m.name, tex != null);
            log.AppendLine($"  {pair.Key}  ({m.shader?.name} → {mode}, 텍스처 {(tex != null ? $"{tex.name} ← {slot}" : "없음")})");
            if (!write) continue;

            Color color = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            Undo.RecordObject(m, "Particle Material Fix");
            m.shader = particle;
            if (tex != null) { m.SetTexture("_BaseMap", tex); FixAlphaImport(tex, log); }
            if (mode == Mode.Hidden) color.a = 0f;
            m.SetColor("_BaseColor", color);
            SetupTransparent(m, mode == Mode.Additive);
            EditorUtility.SetDirty(m);
        }

        if (write) AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    private static Mode ModeOf(string name, bool hasTexture)
    {
        if (name.Contains("Disto")) return Mode.Hidden;
        if (!name.Contains("Add")) return Mode.Alpha;           // 텍스처 없는 알파 머티리얼(Flesh_Balls)은 메쉬 파티클이라 단색 그대로
        return hasTexture ? Mode.Additive : Mode.Hidden;
    }

    private static void FixAlphaImport(Texture tex, StringBuilder log)
    {
        if (!(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter ti)) return;
        if (!ti.DoesSourceTextureHaveAlpha() || ti.alphaSource != TextureImporterAlphaSource.None) return;

        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        log.AppendLine($"    → {tex.name}: 임포트에서 빠진 알파를 다시 켬");
    }

    // 셰이더를 바꾸기 전, 저장된 텍스처 슬롯 전체에서 모양 텍스처를 찾는다 (원작 전용 슬롯은 새 셰이더에서 안 보이므로 직렬화 값으로 읽음)
    private static Texture FindTexture(Material m, out string slot)
    {
        var found = new Dictionary<string, Texture>();
        SerializedProperty envs = new SerializedObject(m).FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; envs != null && i < envs.arraySize; i++)
        {
            SerializedProperty pair = envs.GetArrayElementAtIndex(i);
            string name = pair.FindPropertyRelative("first").stringValue;
            var tex = pair.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (tex != null) found[name] = tex;
        }

        foreach (string s in PreferredSlots)
            if (found.TryGetValue(s, out Texture t)) { slot = s; return t; }
        foreach (var pair in found)
            if (!SkipSlots.Any(pair.Key.StartsWith)) { slot = pair.Key; return pair.Value; }
        foreach (var pair in found)
            if (pair.Key == "_DissolveMask") { slot = pair.Key; return pair.Value; }

        slot = null;
        return null;
    }

    // URP 파티클 셰이더 GUI가 하는 투명 설정을 그대로 적용
    private static void SetupTransparent(Material m, bool additive)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);          // 0 Alpha, 2 Additive
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }
}
