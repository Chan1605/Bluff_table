using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 Sit 프리팹에서 셰이더가 깨진(URP에서 지원 안 됨 → 분홍색) 렌더러를 찾아 끈다.
// 예: Blood_Hit/Blood2/Decal (원작 HDRP 데칼 머티리얼) → 사망 시 테이블 위에 분홍 상자로 보임
// 렌더러만 끄므로(오브젝트 · 애니메이션 커브는 그대로) 나중에 머티리얼을 URP로 바꾸면 Renderer를 다시 켜면 된다.
// 메뉴: Tools / 원작 캐릭터 / 깨진 셰이더(분홍) 렌더러 - 미리보기 · 끄기
//
// + 클립이 바꿔 끼우는 머티리얼: 원작 사망 클립(silah_dolu)은 맞는 순간 몸 · 머리 머티리얼을 피 묻은 버전(kanli)으로 바꾼다
//   (m_PPtrCurves). 이 머티리얼들은 프리팹 렌더러에 안 붙어 있어서 팀원 URP 변환 툴에서 빠졌고 → 사망하면 캐릭터가 분홍색.
//   메뉴: Tools / 원작 캐릭터 / 클립이 바꾸는 머티리얼 - 미리보기 · URP Lit 변환 (팀원 'HDRP 머티리얼 → URP Lit 변환'을 그대로 사용)
public static class OriginalBrokenShaderFixer
{
    private const string MenuRoot = "Tools/원작 캐릭터/";
    private const string SitFolder = "Assets/2.Model/Original/Characters/_Export/Sit_Character";
    private static readonly string[] ClipFolders = { "Assets/AnimationClip", "Assets/2.Model/Original", "Assets/Art/Anim" };
    private const string ConvertMenu = MenuRoot + "HDRP 머티리얼 → URP Lit 변환 (선택 + 자식)";

    [MenuItem(MenuRoot + "클립이 바꾸는 머티리얼 - 미리보기")]
    private static void PreviewClipMaterials() => ConvertClipMaterials(false);

    [MenuItem(MenuRoot + "클립이 바꾸는 머티리얼 - URP Lit 변환")]
    private static void ApplyClipMaterials() => ConvertClipMaterials(true);

    private static void ConvertClipMaterials(bool convert)
    {
        var broken = new HashSet<Material>();
        var usedBy = new Dictionary<Material, string>();

        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", ClipFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".anim")) continue;
            // 머티리얼 교체 커브가 있는 클립만 연다 (클립이 커서 전부 불러오면 느림)
            if (!File.ReadAllText(Path.GetFullPath(path)).Contains("m_Materials.Array.data")) continue;

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            foreach (EditorCurveBinding b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                foreach (ObjectReferenceKeyframe k in AnimationUtility.GetObjectReferenceCurve(clip, b))
                {
                    if (k.value is Material m && IsBroken(m) && broken.Add(m)) usedBy[m] = clip.name;
                }
            }
        }

        var log = new StringBuilder($"[깨진 셰이더] 클립이 바꾸는 머티리얼 {(convert ? "변환" : "미리보기")}: {broken.Count}개\n");
        foreach (Material m in broken)
            log.AppendLine($"  {AssetDatabase.GetAssetPath(m)}  (셰이더 {(m.shader != null ? m.shader.name : "없음")}, 예: {usedBy[m]})");
        Debug.Log(log.ToString());

        if (!convert || broken.Count == 0) return;
        Selection.objects = broken.Cast<Object>().ToArray();
        if (!EditorApplication.ExecuteMenuItem(ConvertMenu))
            Debug.LogError($"[깨진 셰이더] 변환 메뉴를 못 찾음: {ConvertMenu}");
    }

    [MenuItem(MenuRoot + "깨진 셰이더(분홍) 렌더러 - 미리보기")]
    private static void Preview() => Run(false);

    [MenuItem(MenuRoot + "깨진 셰이더(분홍) 렌더러 - 끄기")]
    private static void Apply() => Run(true);

    private static void Run(bool write)
    {
        var log = new StringBuilder($"[깨진 셰이더] {(write ? "끄기" : "미리보기")} 결과\n");
        int total = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SitFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;
                    Material bad = r.sharedMaterials.FirstOrDefault(IsBroken);
                    if (bad == null) continue;

                    count++;
                    log.AppendLine($"  {root.name}: {AnimationUtility.CalculateTransformPath(r.transform, root.transform)}  (머티리얼 {bad.name}, 셰이더 {(bad.shader != null ? bad.shader.name : "없음")})");
                    if (write) r.enabled = false;
                }

                total += count;
                if (write && count > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        log.AppendLine($"  합계 {total}개");
        Debug.Log(log.ToString());
    }

    private static bool IsBroken(Material m)
    {
        if (m == null) return false;
        Shader s = m.shader;
        return s == null || s.name == "Hidden/InternalErrorShader" || !s.isSupported;
    }
}
