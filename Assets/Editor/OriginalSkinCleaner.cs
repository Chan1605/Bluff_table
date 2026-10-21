using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 선택한 원작 Sit 프리팹에서 우리 게임에 안 쓰는 것을 지운다.
// 1) 'Stylized Revolver ' (허리 리볼버, 켜져 있음) · kartucan (날아가는 카드) · 오른손 kart1
// 2) 꺼져 있는 스킨: 이름에 christmas · halloween · scifi · cultural · gozluk(안경) · snowhat · kanat(날개) · drone · WrapMaster · gomlek(셔츠) · baslik(모자) 포함
// 남기는 것: 손 카드(KartsParent), 오른손 총(ElRevolver) · 총구 이펙트(Fx), 사망 피(Blood_Hit), 소리 오브젝트, 얼굴(Yuz) · 표정 메쉬
// 메뉴: Tools / 원작 캐릭터 / 선택한 Sit 프리팹 스킨 정리
public static class OriginalSkinCleaner
{
    private static readonly string[] SkinKeywords =
    {
        "christmas", "halloween", "scifi", "cultural", "cutural", "gozluk", "snowhat",
        "kanat", "drone", "wrapmaster", "gomlek", "baslik"
    };
    private static readonly string[] Protected = { "KartsParent", "ElRevolver", "Fx", "Blood_Hit", "Yuz" };

    [MenuItem("Tools/원작 캐릭터/선택한 Sit 프리팹 스킨 정리")]
    private static void CleanSelected()
    {
        var log = new StringBuilder("[스킨 정리] 결과\n");
        foreach (GameObject asset in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (!path.EndsWith(".prefab")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var targets = new List<Transform>();
                Collect(root.transform, root.transform, targets);
                var names = new List<string>();
                foreach (Transform t in targets)
                {
                    if (t == null) continue;   // 이미 지운 부모 밑
                    names.Add(t.name.Trim());
                    Object.DestroyImmediate(t.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  {asset.name}: {names.Count}개 삭제 → {string.Join(", ", names)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log(log.ToString());
    }

    private static void Collect(Transform root, Transform t, List<Transform> targets)
    {
        foreach (Transform child in t)
        {
            string name = child.name.Trim();
            if (System.Array.IndexOf(Protected, name) >= 0) continue;   // 이 밑은 통째로 보존

            if (ShouldDelete(root, child, name)) { targets.Add(child); continue; }
            Collect(root, child, targets);
        }
    }

    private static bool ShouldDelete(Transform root, Transform t, string name)
    {
        if (t.parent == root && (name == "Stylized Revolver" || name == "kartucan")) return true;
        if (name == "kart1" && t.parent != null && t.parent.name.EndsWith("RPalm") && !t.gameObject.activeSelf) return true;
        if (t.gameObject.activeSelf) return false;   // 스킨은 꺼져 있는 것만

        string lower = name.ToLowerInvariant();
        foreach (string k in SkinKeywords)
            if (lower.Contains(k)) return true;
        return false;
    }
}
