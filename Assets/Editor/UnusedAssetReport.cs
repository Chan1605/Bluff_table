using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 미사용 리소스 리포트 (삭제는 하지 않음, 목록만 만든다)
// 사용 중 = 아래 "뿌리"에서 의존성(GetDependencies, 재귀)으로 닿는 에셋
//   - Build Settings에 켜진 씬
//   - Resources 폴더 전체 (Resources.Load)
//   - 렌더 파이프라인 · Preloaded Assets · ProjectSettings/*.asset 이 가리키는 에셋 (URP · Input System 설정 등)
//   - 런타임 코드(.cs, Editor 폴더 제외)에 "Assets/..." 문자열로 적힌 경로 (에디터 폴백 등)
//     ※ 에디터 툴이 경로로 읽는 에셋은 게임에 안 들어가므로 뿌리로 치지 않는다 (툴이 폴더 통째로 가리키면 전부 사용 중으로 잡히는 문제)
// 결과: 프로젝트 폴더/UnusedAssetsReport.csv (Assets 밖이라 임포트되지 않음) + 콘솔 요약
// 메뉴: Tools / 정리 / 미사용 리소스 리포트 만들기
public static class UnusedAssetReport
{
    private const string OutputFile = "UnusedAssetsReport.csv";

    // 외부 패키지 · 코드 폴더는 검사 제외
    private static readonly string[] SkipFolders =
    {
        "Assets/Mirror/", "Assets/ParrelSync/", "Assets/Plugins/", "Assets/Packages/",
        "Assets/TextMesh Pro/", "Assets/ScriptTemplates/",
    };

    private static readonly string[] SkipExtensions =
    {
        ".cs", ".asmdef", ".asmref", ".dll", ".so", ".dylib", ".jslib", ".rsp", ".config", ".xml", ".json", ".md", ".txt",
    };

    [MenuItem("Tools/정리/미사용 리소스 리포트 만들기")]
    private static void Run()
    {
        try
        {
            EditorUtility.DisplayProgressBar("미사용 리소스", "사용 중인 에셋 계산 중...", 0.1f);
            List<string> roots = CollectRoots(out Dictionary<string, string> rootReason);
            var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
            foreach (string r in roots) used.Add(r);

            EditorUtility.DisplayProgressBar("미사용 리소스", "전체 에셋 비교 중...", 0.6f);
            var rows = new List<(string path, string type, long size, string folder, string note)>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!IsCandidate(path) || used.Contains(path)) continue;
                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                long size = File.Exists(path) ? new FileInfo(path).Length : 0;
                rows.Add((path, type != null ? type.Name : "?", size, TopFolder(path), Note(path, type)));
            }
            rows.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            var csv = new StringBuilder("path,type,size_kb,top_folder,note\n");
            foreach (var r in rows)
                csv.AppendLine($"\"{r.path}\",{r.type},{r.size / 1024},\"{r.folder}\",\"{r.note}\"");

            // 사용 중이지만 Build에 없는 씬, 뿌리 목록도 같이 남김 (검토용)
            csv.AppendLine();
            csv.AppendLine("# roots");
            foreach (var pair in rootReason.OrderBy(p => p.Key)) csv.AppendLine($"# \"{pair.Key}\",{pair.Value}");
            File.WriteAllText(OutputFile, csv.ToString(), new UTF8Encoding(true));

            var log = new StringBuilder($"[미사용 리소스 v2] 후보 {rows.Count}개, {rows.Sum(r => r.size) / 1048576f:0.0} MB → {Path.GetFullPath(OutputFile)}\n");
            foreach (var g in rows.GroupBy(r => r.folder).OrderByDescending(g => g.Sum(r => r.size)))
                log.AppendLine($"  {g.Key}: {g.Count()}개, {g.Sum(r => r.size) / 1048576f:0.0} MB");
            Debug.Log(log.ToString());
            EditorUtility.RevealInFinder(OutputFile);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // 지금 사용 중인 에셋 경로 전부 (정리 창이 삭제 직전에 다시 확인할 때 씀)
    public static HashSet<string> CollectUsed()
    {
        List<string> roots = CollectRoots(out _);
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        foreach (string r in roots) used.Add(r);
        return used;
    }

    private static List<string> CollectRoots(out Dictionary<string, string> reason)
    {
        var map = new Dictionary<string, string>();
        void Add(string path, string why)
        {
            if (string.IsNullOrEmpty(path) || map.ContainsKey(path)) return;
            map[path] = why;
        }

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.enabled) Add(scene.path, "build scene");

        foreach (string path in AssetDatabase.GetAllAssetPaths())
            if (path.StartsWith("Assets/") && path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path))
                Add(path, "Resources");

        Add(AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline), "render pipeline");
        for (int i = 0; i < QualitySettings.names.Length; i++)
            Add(AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i)), "quality " + QualitySettings.names[i]);
        foreach (UnityEngine.Object o in PlayerSettings.GetPreloadedAssets())
            Add(AssetDatabase.GetAssetPath(o), "preloaded");

        // ProjectSettings가 GUID로 가리키는 에셋 (Input System · URP 글로벌 설정 · 아이콘 등)
        var guid = new Regex(@"guid: ([0-9a-f]{32})");
        foreach (string file in Directory.GetFiles("ProjectSettings", "*.asset"))
            foreach (Match m in guid.Matches(File.ReadAllText(file)))
                Add(AssetDatabase.GUIDToAssetPath(m.Groups[1].Value), "ProjectSettings/" + Path.GetFileName(file));

        // 코드에 경로 문자열로 적힌 에셋 · 폴더 (폴더면 그 안 전부)
        var literal = new Regex("\"(Assets/[^\"]+)\"");
        string[] allPaths = AssetDatabase.GetAllAssetPaths();
        foreach (string cs in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
        {
            string csPath = cs.Replace('\\', '/');
            if (SkipFolders.Any(csPath.StartsWith) || csPath.Contains("/Editor/")) continue;
            foreach (Match m in literal.Matches(File.ReadAllText(cs)))
            {
                string p = m.Groups[1].Value.TrimEnd('/');
                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (string a in allPaths)
                        if (a.StartsWith(p + "/") && !AssetDatabase.IsValidFolder(a)) Add(a, "code path " + Path.GetFileName(csPath));
                }
                else if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(p))) Add(p, "code path " + Path.GetFileName(csPath));
            }
        }

        map.Remove(string.Empty);
        reason = map;
        return map.Keys.ToList();
    }

    private static bool IsCandidate(string path)
    {
        if (!path.StartsWith("Assets/") || AssetDatabase.IsValidFolder(path)) return false;
        if (SkipFolders.Any(path.StartsWith)) return false;
        if (path.Contains("/Editor/") || path.Contains("/Resources/")) return false;
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return !SkipExtensions.Contains(ext);
    }

    private static string TopFolder(string path)
    {
        string[] parts = path.Split('/');
        return parts.Length > 2 ? parts[1] : "(Assets)";
    }

    // 코드에서 이름 · 셰이더 찾기로 쓸 수 있어 삭제 전 확인이 필요한 종류
    private static string Note(string path, Type type)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".unity") return "Build에 없는 씬";
        if (type == typeof(Shader) || ext == ".shadergraph" || ext == ".shadersubgraph" || ext == ".hlsl" || ext == ".cginc") return "셰이더: Shader.Find 확인";
        if (type == typeof(Font) || (type != null && type.Name == "TMP_FontAsset")) return "폰트: TMP Fallback 확인";
        if (type != null && typeof(ScriptableObject).IsAssignableFrom(type)) return "설정 에셋: 코드 참조 확인";
        if (ext == ".inputactions") return "입력 액션: 코드 생성 클래스 확인";
        return string.Empty;
    }
}
