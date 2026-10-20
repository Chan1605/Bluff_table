using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// AssetRipper로 뽑은 원작 캐릭터를 우리 프로젝트(URP)에서 쓰기 위한 정리 도구.
// 1) Missing Script 제거  2) HDRP 머티리얼을 URP Lit으로 변환 (텍스처 · 색 · 마스크맵 옮김)
// 메뉴: Tools / 원작 캐릭터 /
public static class OriginalCharacterTools
{
    const string MenuRoot = "Tools/원작 캐릭터/";

    // ───── Missing Script 제거 ─────

    [MenuItem(MenuRoot + "Missing Script 제거 (선택 + 자식)")]
    static void RemoveMissingScripts()
    {
        int removed = 0;
        foreach (GameObject root in Selection.gameObjects)
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                Undo.RegisterCompleteObjectUndo(t.gameObject, "Remove Missing Scripts");
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
        Debug.Log($"[원작 정리] Missing Script {removed}개 제거");
    }

    // ───── HDRP → URP Lit ─────

    [MenuItem(MenuRoot + "HDRP 머티리얼 → URP Lit 변환 (선택 + 자식)")]
    static void ConvertMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[원작 정리] URP Lit 셰이더를 못 찾음. 프로젝트가 URP인지 확인하세요.");
            return;
        }

        // 선택한 오브젝트(자식 포함)의 머티리얼 + Project 창에서 직접 선택한 머티리얼
        var mats = new HashSet<Material>();
        foreach (GameObject root in Selection.gameObjects)
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null) mats.Add(m);
        foreach (Object o in Selection.objects)
            if (o is Material m) mats.Add(m);

        int converted = 0;
        var manual = new List<string>();
        foreach (Material m in mats)
        {
            if (m.shader == urpLit) continue;
            string path = AssetDatabase.GetAssetPath(m);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".mat")) continue;   // 내장 · FBX 내장 머티리얼은 건드리지 않음

            // 셰이더를 바꾸기 전에 저장된 HDRP 값을 읽어 둔다
            var saved = new SavedProps(m);
            Texture baseMap = saved.Tex("_BaseColorMap", "_BaseMap", "_MainTex");
            Texture normal = saved.Tex("_NormalMap", "_BumpMap");
            Texture mask = saved.Tex("_MaskMap");
            Texture emissive = saved.Tex("_EmissiveColorMap", "_EmissionMap");
            Color color = saved.Color(Color.white, "_BaseColor", "_Color");
            float smoothness = mask != null ? saved.Float(1f, "_SmoothnessRemapMax") : saved.Float(0.5f, "_Smoothness");
            float metallic = saved.Float(0f, "_Metallic");
            bool alphaClip = saved.Float(0f, "_AlphaCutoffEnable") > 0.5f;
            float cutoff = saved.Float(0.5f, "_AlphaCutoff");
            bool transparent = saved.Float(0f, "_SurfaceType") > 0.5f;

            Undo.RecordObject(m, "Convert to URP Lit");
            m.shader = urpLit;

            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);

            if (normal != null)
            {
                MarkNormalMap(normal);
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }

            // HDRP 마스크맵: R=Metallic, G=AO, A=Smoothness → URP MetallicGloss(R, A) + Occlusion(G)에 그대로 쓴다
            if (mask != null)
            {
                MarkLinear(mask);
                m.SetTexture("_MetallicGlossMap", mask);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_OcclusionMap", mask);
                m.EnableKeyword("_OCCLUSIONMAP");
            }

            if (emissive != null)
            {
                m.SetTexture("_EmissionMap", emissive);
                m.SetColor("_EmissionColor", Color.white);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", cutoff);
                m.EnableKeyword("_ALPHATEST_ON");
            }

            if (transparent) manual.Add(m.name);   // 반투명 설정은 키워드 · 블렌드 상태가 많아 Inspector에서 Surface Type만 바꿔 주면 됨
            EditorUtility.SetDirty(m);
            converted++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[원작 정리] 머티리얼 {converted}개 URP Lit으로 변환");
        if (manual.Count > 0)
            Debug.LogWarning($"[원작 정리] 원작이 반투명이던 머티리얼 (Inspector에서 Surface Type = Transparent로 바꿔 주세요): {string.Join(", ", manual)}");
    }

    static void MarkNormalMap(Texture tex)
    {
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter imp && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
        }
    }

    static void MarkLinear(Texture tex)
    {
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) is TextureImporter imp && imp.sRGBTexture)
        {
            imp.sRGBTexture = false;
            imp.SaveAndReimport();
        }
    }

    // 머티리얼 파일에 저장된 값을 셰이더와 상관없이 읽는다 (원작 HDRP 셰이더는 없어서 Material.GetTexture로는 못 읽음)
    class SavedProps
    {
        readonly Dictionary<string, Texture> textures = new();
        readonly Dictionary<string, float> floats = new();
        readonly Dictionary<string, Color> colors = new();

        public SavedProps(Material m)
        {
            var so = new SerializedObject(m);
            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; texEnvs != null && i < texEnvs.arraySize; i++)
            {
                var e = texEnvs.GetArrayElementAtIndex(i);
                textures[e.FindPropertyRelative("first").stringValue] =
                    e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            }
            var fl = so.FindProperty("m_SavedProperties.m_Floats");
            for (int i = 0; fl != null && i < fl.arraySize; i++)
            {
                var e = fl.GetArrayElementAtIndex(i);
                floats[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").floatValue;
            }
            var col = so.FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; col != null && i < col.arraySize; i++)
            {
                var e = col.GetArrayElementAtIndex(i);
                colors[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
            }
        }

        public Texture Tex(params string[] names)
        {
            foreach (string n in names)
                if (textures.TryGetValue(n, out Texture t) && t != null) return t;
            return null;
        }

        public float Float(float fallback, params string[] names)
        {
            foreach (string n in names)
                if (floats.TryGetValue(n, out float v)) return v;
            return fallback;
        }

        public Color Color(Color fallback, params string[] names)
        {
            foreach (string n in names)
                if (colors.TryGetValue(n, out Color c)) return c;
            return fallback;
        }
    }
}
