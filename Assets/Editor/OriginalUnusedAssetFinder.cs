using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 추출본 폴더에서 지금 게임이 안 쓰는 에셋을 찾아 Assets/_Unused로 옮긴다 (지우지 않음 → 확인 후 폴더째 삭제).
// '쓰는 것' = Build Settings 씬 + 캐릭터 Sit 프리팹 11종 + OG 컨트롤러가 참조하는 모든 에셋.
// 메뉴: Tools / 원작 캐릭터 / 안 쓰는 원작 에셋 찾기 (로그만) · 안 쓰는 원작 에셋 _Unused로 옮기기
public static class OriginalUnusedAssetFinder
{
    private static readonly string[] ScanFolders =
    {
        "Assets/Texture2D", "Assets/Mesh", "Assets/Material", "Assets/AnimationClip", "Assets/AnimatorController",
        "Assets/AudioClip", "Assets/Sprite", "Assets/2.Model/Original", "Assets/2.Model/Avatar_Original",
    };
    private static readonly string[] RootAssets =
    {
        "Assets/2.Model/Original/Characters/_Export/Sit_Character",
        "Assets/Art/Anim/OG_Override",
        "Assets/2.Model/Prefabs",
    };
    private const string UnusedFolder = "Assets/_Unused";

    [MenuItem("Tools/원작 캐릭터/안 쓰는 원작 에셋 찾기 (로그만)")]
    private static void Find() => Run(false);

    [MenuItem("Tools/원작 캐릭터/안 쓰는 원작 에셋 _Unused로 옮기기")]
    private static void Move()
    {
        if (EditorUtility.DisplayDialog("안 쓰는 원작 에셋 옮기기", $"쓰지 않는 원작 에셋을 {UnusedFolder}로 옮깁니다. 계속할까요?", "옮기기", "취소"))
            Run(true);
    }

    private static void Run(bool move)
    {
        // 쓰는 에셋 모으기
        var roots = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) roots.Add(s.path);
        foreach (string folder in RootAssets)
            if (AssetDatabase.IsValidFolder(folder))
                roots.AddRange(AssetDatabase.FindAssets("t:Prefab t:AnimatorController t:AnimatorOverrideController", new[] { folder })
                                            .Select(AssetDatabase.GUIDToAssetPath));
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));

        // 검사 폴더에서 안 쓰는 것
        var unused = new List<string>();
        long bytes = 0;
        foreach (string folder in ScanFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || used.Contains(path)) continue;
                if (path.StartsWith("Assets/2.Model/Original/Characters/_Export/Sit_Character")) continue;
                unused.Add(path);
                var fi = new FileInfo(path);
                if (fi.Exists) bytes += fi.Length;
            }
        }
        unused = unused.Distinct().OrderBy(p => p).ToList();

        var log = new StringBuilder($"[안 쓰는 원작 에셋] {unused.Count}개, {bytes / 1024f / 1024f:0.0}MB (기준 루트 {roots.Count}개)\n");
        foreach (string p in unused) log.AppendLine("  " + p);
        Debug.Log(log.ToString());

        if (!move || unused.Count == 0) return;
        if (!AssetDatabase.IsValidFolder(UnusedFolder)) AssetDatabase.CreateFolder("Assets", "_Unused");
        int moved = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string p in unused)
            {
                string dest = AssetDatabase.GenerateUniqueAssetPath($"{UnusedFolder}/{Path.GetFileName(p)}");
                if (string.IsNullOrEmpty(AssetDatabase.MoveAsset(p, dest))) moved++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        Debug.Log($"[안 쓰는 원작 에셋] {moved}개를 {UnusedFolder}로 옮김. 플레이 확인 후 _Unused 폴더를 지우세요.");
    }
}
