using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 프리팹에서 읽은 '몸체 위치 − 의자 위치'(원작 단위)를 11종 Sit 프리팹의 SeatedPose에 넣고 원작 배치를 켠다.
// Extra Offset은 모든 캐릭터에 같은 값(공통 보정)을 넣는다. Bristle로 한 번 맞춘 뒤 이 메뉴로 다시 돌리면 나머지도 같아진다.
// 메뉴: Tools / 원작 캐릭터 / 원작 배치 값 넣기 (Sit 11종)
public static class OriginalSeatOffsetApplier
{
    private const string Folder = "Assets/2.Model/Original/Characters/_Export/Sit_Character";

    // 원작 Char_*_RankedDeck(Dubai = Blorf 1, Skunk = Rasim_Deck) 프리팹 기준 (x, y, z)
    private static readonly Dictionary<string, Vector3> Offsets = new()
    {
        { "Bristle", new Vector3(0f, 0.103f, -0.046f) },
        { "Tusk",    new Vector3(0f, 0.103f, -0.046f) },
        { "Toar",    new Vector3(0f, 0.099f, -0.025f) },
        { "Foxy",    new Vector3(0f, 0.150f, -0.057f) },
        { "Cupcake", new Vector3(0f, 0.033f,  0.043f) },
        { "Scubby",  new Vector3(0f, 0.032f, -0.057f) },
        { "Tigre",   new Vector3(0f, 0.032f, -0.057f) },
        { "Kudo",    new Vector3(0f, 0.019f,  0.030f) },
        { "Gerk",    new Vector3(0f, 0.020f,  0.132f) },
        { "Skunk",   new Vector3(0f, 0.024f,  0.132f) },
        { "Dubai",   new Vector3(0f, 0.019f,  0.121f) },
    };

    [MenuItem("Tools/원작 캐릭터/원작 배치 값 넣기 (Sit 11종)")]
    private static void Apply()
    {
        // 공통 보정값: Bristle_Sit에 지금 들어 있는 Extra Offset을 기준으로 쓴다 (원작 배치가 이미 켜져 있을 때만)
        Vector3 common = Vector3.zero;
        var bristle = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Bristle_Sit.prefab");
        if (bristle != null && bristle.TryGetComponent(out SeatedPose bp))
        {
            var so = new SerializedObject(bp);
            if (so.FindProperty("useOriginalOffset").boolValue) common = so.FindProperty("extraOffset").vector3Value;
        }

        var log = new StringBuilder($"[원작 배치] 공통 Extra Offset = {common}\n");
        foreach (var kv in Offsets)
        {
            string path = $"{Folder}/{kv.Key}_Sit.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { log.AppendLine($"  {kv.Key}: ❌ 프리팹 없음"); continue; }
            try
            {
                if (!root.TryGetComponent(out SeatedPose seated)) { log.AppendLine($"  {kv.Key}: ❌ SeatedPose 없음"); continue; }
                var so = new SerializedObject(seated);
                so.FindProperty("useOriginalOffset").boolValue = true;
                so.FindProperty("originalOffset").vector3Value = kv.Value;
                so.FindProperty("extraOffset").vector3Value = common;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  {kv.Key}: ✅ {kv.Value}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log(log.ToString());
    }
}
