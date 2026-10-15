using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LiarsBar.Tools
{
    // Map_Bar.fbx 머티리얼 → URP Lit 생성 · 연결 (원작 매핑 JSON 기준)
    public static class MapBarMaterialBuilder
    {
        private const string RootFolder = "Assets/Art/Map_Bar";
        private const string FbxPath = RootFolder + "/Map_Bar.fbx";
        private const string JsonPath = RootFolder + "/Map_Bar_Materials.json";
        private const string MaterialFolderName = "Materials";
        private const string MaterialFolder = RootFolder + "/" + MaterialFolderName;
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        [Serializable]
        private class MaterialList
        {
            public MaterialEntry[] materials;
        }

        [Serializable]
        private class MaterialEntry
        {
            public string name;
            public string baseMap;
            public string normalMap;
            public string maskMap;
            public string emissionMap;
            public float[] baseColor;
            public bool emission;
            public float[] emissionColor;
            public float metallic;
            public float smoothness;
            public float normalScale;
            public bool transparent;
            public bool alphaClip;
            public float cutoff;
            public bool doubleSided;
            public float[] tiling;
            public float[] offset;
        }

        [MenuItem("Tools/Map_Bar/Build Materials")]
        private static void BuildMaterials()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            if (json == null)
            {
                Debug.LogError($"[Map_Bar] 매핑 파일이 없습니다: {JsonPath}");
                return;
            }

            Shader lit = Shader.Find(LitShaderName);
            if (lit == null)
            {
                Debug.LogError($"[Map_Bar] 셰이더를 찾을 수 없습니다: {LitShaderName}");
                return;
            }

            ModelImporter importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[Map_Bar] FBX를 찾을 수 없습니다: {FbxPath}");
                return;
            }

            MaterialList list = JsonUtility.FromJson<MaterialList>(json.text);
            if (list?.materials == null || list.materials.Length == 0)
            {
                Debug.LogError("[Map_Bar] 매핑 파일에 머티리얼이 없습니다.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder(RootFolder, MaterialFolderName);

            HashSet<string> unmatched = CollectFbxMaterialNames(importer);

            List<Material> builtMaterials = new List<Material>();
            int missingTextures = 0;
            foreach (MaterialEntry entry in list.materials)
            {
                Material material = GetOrCreateMaterial(entry.name, lit);
                missingTextures += ApplyEntry(material, entry);
                EditorUtility.SetDirty(material);
                builtMaterials.Add(material);

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), material);
                unmatched.Remove(entry.name);
            }

            // 다른 에셋까지 저장하지 않도록 만든 머티리얼만 저장
            foreach (Material material in builtMaterials)
                AssetDatabase.SaveAssetIfDirty(material);
            importer.SaveAndReimport();

            Debug.Log($"[Map_Bar] 머티리얼 {builtMaterials.Count}개 생성 · 연결 완료 (못 찾은 텍스처 {missingTextures}개)");
            if (unmatched.Count > 0)
                Debug.LogWarning($"[Map_Bar] 매핑에 없는 FBX 머티리얼 {unmatched.Count}개: {string.Join(", ", unmatched)}");
        }

        private static HashSet<string> CollectFbxMaterialNames(ModelImporter importer)
        {
            HashSet<string> names = new HashSet<string>(
                AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Material>().Select(m => m.name));

            // 이미 연결된 것도 포함
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> pair in importer.GetExternalObjectMap())
            {
                if (pair.Key.type == typeof(Material))
                    names.Add(pair.Key.name);
            }
            return names;
        }

        private static Material GetOrCreateMaterial(string sourceName, Shader lit)
        {
            string safeName = string.Join("_", sourceName.Split(Path.GetInvalidFileNameChars()));
            string path = $"{MaterialFolder}/{safeName}.mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = lit;
            }
            return material;
        }

        // 반환값: 못 찾은 텍스처 수
        private static int ApplyEntry(Material material, MaterialEntry entry)
        {
            int missing = 0;

            Texture2D baseMap = LoadTexture(entry.baseMap, ref missing);
            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", ToColor(entry.baseColor, Color.white));
            material.SetTextureScale("_BaseMap", ToVector(entry.tiling, Vector2.one));
            material.SetTextureOffset("_BaseMap", ToVector(entry.offset, Vector2.zero));

            Texture2D normalMap = LoadTexture(entry.normalMap, ref missing);
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", entry.normalScale);
            SetKeyword(material, "_NORMALMAP", normalMap != null);

            // MaskMap: R 금속성 · G AO · A 부드러움
            Texture2D maskMap = LoadTexture(entry.maskMap, ref missing);
            material.SetTexture("_MetallicGlossMap", maskMap);
            material.SetTexture("_OcclusionMap", maskMap);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Metallic", entry.metallic);
            material.SetFloat("_Smoothness", entry.smoothness);
            material.SetFloat("_OcclusionStrength", 1f);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", maskMap != null);
            SetKeyword(material, "_OCCLUSIONMAP", maskMap != null);

            Texture2D emissionMap = LoadTexture(entry.emissionMap, ref missing);
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", entry.emission ? ToColor(entry.emissionColor, Color.black) : Color.black);
            SetKeyword(material, "_EMISSION", entry.emission);
            material.globalIlluminationFlags = entry.emission
                ? MaterialGlobalIlluminationFlags.BakedEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            material.SetFloat("_Cull", entry.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
            material.SetFloat("_AlphaClip", entry.alphaClip ? 1f : 0f);
            material.SetFloat("_Cutoff", entry.cutoff);
            SetKeyword(material, "_ALPHATEST_ON", entry.alphaClip);
            ApplySurface(material, entry.transparent, entry.alphaClip);

            return missing;
        }

        private static void ApplySurface(Material material, bool transparent, bool alphaClip)
        {
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);

            if (transparent)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                material.renderQueue = alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            }
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
        }

        private static Texture2D LoadTexture(string fileName, ref int missing)
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string path = $"{RootFolder}/{fileName}";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogWarning($"[Map_Bar] 텍스처 없음: {path}");
                missing++;
            }
            return texture;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }

        private static Color ToColor(float[] values, Color fallback)
        {
            if (values == null || values.Length < 4) return fallback;
            return new Color(values[0], values[1], values[2], values[3]);
        }

        private static Vector2 ToVector(float[] values, Vector2 fallback)
        {
            if (values == null || values.Length < 2) return fallback;
            return new Vector2(values[0], values[1]);
        }
    }
}
