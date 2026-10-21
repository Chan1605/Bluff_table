using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 원작 리핑 클립의 깨진 바인딩 경로 복구
//
// 리핑 도구가 경로 문자열을 못 찾은 커브는 "path_0x7E00D958_twLmVqN"처럼 저장된다 (0x 뒤 8자리 = 원래 경로의 CRC32).
// 그래서 몸(휴머노이드 근육)은 움직이지만, 총 바꿔 들기(테이블 총 끄기 · 손 총 켜기) · 공이 · 방아쇠 ·
// 총성 / 빈 약실 소리 · 총구 섬광 · 피 효과 같은 오브젝트 커브는 하나도 동작하지 않는다.
//
// 이 툴은 원작 캐릭터 프리팹(Sit · Stand · Origin)의 모든 Transform 경로로 CRC32 표를 만들고,
// 클립(.anim) 텍스트의 "path: path_0x…" 줄을 실제 경로로 바꾼다.
// 클립의 m_ClipBindingConstant(실행용 바인딩)는 이미 원래 경로의 CRC32를 갖고 있어서 경로 글자만 바꾸면 서로 맞는다.
//
// 사용: Tools > 원작 캐릭터 > 클립 경로 복구 - 미리보기(Dry Run)  → Console 결과 확인
//       Tools > 원작 캐릭터 > 클립 경로 복구 - 적용                  → 파일 수정 (git 커밋 후 실행 권장)
public static class RippedClipPathFixer
{
    private const string MenuRoot = "Tools/원작 캐릭터/";

    // 경로 표를 만들 프리팹 폴더 (원작 캐릭터 Sit · Stand · Origin)
    private static readonly string[] PrefabFolders = { "Assets/2.Model/Original/Characters" };

    // 고칠 클립을 찾을 폴더
    private static readonly string[] ClipFolders = { "Assets/AnimationClip", "Assets/2.Model/Original", "Assets/Art/Anim" };

    private static readonly Regex BrokenPath = new Regex(@"^(?<head>[ \t]*path: )path_0x(?<hash>[0-9A-Fa-f]{8})_[^\r\n]*", RegexOptions.Multiline);

    [MenuItem(MenuRoot + "클립 경로 복구 - 미리보기(Dry Run)")]
    private static void DryRun() => Run(false);

    [MenuItem(MenuRoot + "클립 경로 복구 - 적용")]
    private static void Apply()
    {
        if (!EditorUtility.DisplayDialog("클립 경로 복구",
                "원작 클립(.anim)의 깨진 경로(path_0x…)를 실제 경로로 바꿉니다.\n파일을 직접 수정하므로 git 커밋 후 실행하는 것을 권장합니다.",
                "적용", "취소"))
            return;
        Run(true);
    }

    private static void Run(bool write)
    {
        Dictionary<uint, string> table = BuildPathTable(out int prefabCount);
        Debug.Log($"[클립 경로 복구] 프리팹 {prefabCount}개에서 경로 {table.Count}개 수집");

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", ClipFolders);
        var changedFiles = new List<string>();
        int fixedTotal = 0, missingTotal = 0;
        var missingHashes = new HashSet<string>();

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!assetPath.EndsWith(".anim")) continue;
                if (EditorUtility.DisplayCancelableProgressBar("클립 경로 복구", assetPath, (float)i / guids.Length)) break;

                string fullPath = Path.GetFullPath(assetPath);
                string text = File.ReadAllText(fullPath);
                if (!text.Contains("path: path_0x")) continue;

                int fixedHere = 0, missingHere = 0;
                string result = BrokenPath.Replace(text, m =>
                {
                    uint hash = uint.Parse(m.Groups["hash"].Value, System.Globalization.NumberStyles.HexNumber);
                    if (!table.TryGetValue(hash, out string realPath))
                    {
                        missingHere++;
                        missingHashes.Add(m.Groups["hash"].Value);
                        return m.Value;
                    }
                    fixedHere++;
                    return m.Groups["head"].Value + Quote(realPath);
                });

                fixedTotal += fixedHere;
                missingTotal += missingHere;
                if (fixedHere == 0) continue;

                changedFiles.Add(assetPath);
                Debug.Log($"[클립 경로 복구] {assetPath}: 복구 {fixedHere}줄{(missingHere > 0 ? $", 못 찾음 {missingHere}줄" : "")}");
                if (write) File.WriteAllText(fullPath, result, new UTF8Encoding(false));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (write && changedFiles.Count > 0)
        {
            AssetDatabase.StartAssetEditing();
            try { foreach (string p in changedFiles) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();
        }

        string mode = write ? "적용" : "미리보기";
        Debug.Log($"[클립 경로 복구] {mode} 완료: 클립 {changedFiles.Count}개, 복구 {fixedTotal}줄, 못 찾음 {missingTotal}줄");
        if (missingHashes.Count > 0)
            Debug.LogWarning($"[클립 경로 복구] 프리팹에 없는 경로 해시 {missingHashes.Count}종 (해당 오브젝트가 프리팹에서 지워졌거나 다른 캐릭터 전용): {string.Join(", ", missingHashes)}");
    }

    // 프리팹 루트와 그 안의 모든 Animator 기준 상대 경로 → CRC32
    private static Dictionary<uint, string> BuildPathTable(out int prefabCount)
    {
        var table = new Dictionary<uint, string>();
        prefabCount = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabFolders))
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (root == null) continue;
            prefabCount++;

            var animRoots = new List<Transform> { root.transform };
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
                if (!animRoots.Contains(a.transform)) animRoots.Add(a.transform);

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform animRoot in animRoots)
            {
                foreach (Transform t in all)
                {
                    if (t == animRoot || !t.IsChildOf(animRoot)) continue;
                    string path = AnimationUtility.CalculateTransformPath(t, animRoot);
                    table[Crc32(path)] = path;
                }
            }
        }
        return table;
    }

    // YAML 문자열: 앞뒤 공백 · 특수문자가 있어도 안전하게 작은따옴표로 감싼다 ('는 ''로)
    private static string Quote(string s) => "'" + s.Replace("'", "''") + "'";

    private static uint[] crcTable;

    private static uint Crc32(string s)
    {
        if (crcTable == null)
        {
            crcTable = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                crcTable[n] = c;
            }
        }

        uint crc = 0xFFFFFFFFu;
        foreach (byte b in Encoding.UTF8.GetBytes(s))
            crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
