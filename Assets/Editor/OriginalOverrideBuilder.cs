using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 선택한 원작 Sit 프리팹마다 Bristle_OG를 원본으로 하는 Override 컨트롤러를 만들고 그 캐릭터 Animator에 넣는다.
// 클립은 그 캐릭터 원작 컨트롤러(_Body 프리팹의 Animator)에서 같은 역할의 상태를 찾아 가져온다.
// 메뉴: Tools / 원작 캐릭터 / 선택한 Sit 프리팹 Override 만들기
public static class OriginalOverrideBuilder
{
    private const string BaseControllerPath = "Assets/Art/Anim/OG_Override/Bristle_OG.controller";
    private const string OutputFolder = "Assets/Art/Anim/OG_Override";

    // Bristle_OG에 들어 있는 Bristle 클립 → 원작 컨트롤러의 상태 이름
    private static readonly Dictionary<string, string> ClipToState = new()
    {
        { "bristle_2_elmasada", "NoCard" },
        { "Bristle_el_masada_Yeni", "blorfidle_scubby" },
        { "bristle_kart_gecis", "Kart_Gecis" },
        { "bristle_kart_tutma_Yeni", "blorfidle_look" },
        { "Bristle_kart_kapatma_0", "kartkapama" },
        { "bristle_kart_atma", "blorfthrow_scubby" },
        { "bristle_fuck_you_sol", "Emote1-Left" },
        { "bristle_taunt", "Emote1-Mid" },
        { "bristle_fuck_you_sag", "Emote1-Right" },
        { "bristle_you_are_dead", "Emote2" },
        { "bristle_grr_v02", "Emote3" },

        // 러시안 룰렛 (원작 Blorf 컨트롤러: Roulet → Dead / RouletEmpty)
        { "Bristle_silah_trigger", "Roulet" },        // 총 집기 → 머리에 대기 → 방아쇠
        { "Bristle_silah_bos_0", "RouletEmpty" },     // 빈 약실 → 안도 → 내려놓기
        { "Bristle_silah_dolu_uzun", "Dead" },        // 발사 → 사망
        { "bristle_win_deck", "Winner" },             // 마지막 1인 승리
        { "Bristle_reloadyeni", "ScubyReload" },      // 게임 시작 장전 → 테이블에 내려놓기
    };

    [MenuItem("Tools/원작 캐릭터/선택한 Sit 프리팹 Override 만들기")]
    private static void BuildSelected()
    {
        var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        if (baseController == null)
        {
            Debug.LogError($"[Override] 원본 컨트롤러가 없음: {BaseControllerPath}");
            return;
        }
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Art/Anim", "OG_Override");

        var log = new StringBuilder("[Override] 결과\n");
        foreach (GameObject sit in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string sitPath = AssetDatabase.GetAssetPath(sit);
            if (!sitPath.EndsWith(".prefab")) continue;
            log.AppendLine("  " + BuildOne(baseController, sit, sitPath));
        }
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    private static string BuildOne(AnimatorController baseController, GameObject sit, string sitPath)
    {
        string name = sit.name.EndsWith("_Sit") ? sit.name.Substring(0, sit.name.Length - 4) : sit.name;
        if (name == "Bristle") return "Bristle: 건너뜀 (Bristle_OG 원본 그대로 사용)";

        AnimatorController original = FindOriginalController(name);
        if (original == null) return $"{name}: ❌ 원작 컨트롤러를 못 찾음 ({name}_Body 프리팹의 Animator 확인)";

        var states = new Dictionary<string, Motion>();
        foreach (AnimatorControllerLayer layer in original.layers) CollectStates(layer.stateMachine, states);

        var overrideController = new AnimatorOverrideController(baseController) { name = name + "_OG" };
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(pairs);

        var missing = new List<string>();
        int replaced = 0;
        for (int i = 0; i < pairs.Count; i++)
        {
            AnimationClip baseClip = pairs[i].Key;
            if (!ClipToState.TryGetValue(baseClip.name, out string stateName)) continue;
            if (states.TryGetValue(stateName, out Motion motion) && motion is AnimationClip clip)
            {
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseClip, clip);
                replaced++;
            }
            else missing.Add(stateName);
        }
        overrideController.ApplyOverrides(pairs);

        string outPath = $"{OutputFolder}/{name}_OG.overrideController";
        AssetDatabase.DeleteAsset(outPath);
        AssetDatabase.CreateAsset(overrideController, outPath);

        // Sit 프리팹 Animator에 연결
        GameObject root = PrefabUtility.LoadPrefabContents(sitPath);
        try
        {
            var animator = root.GetComponent<Animator>();
            if (animator != null) animator.runtimeAnimatorController = overrideController;
            PrefabUtility.SaveAsPrefabAsset(root, sitPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return missing.Count == 0
            ? $"{name}: ✅ {replaced}/{ClipToState.Count} 교체 ({original.name}) → {outPath}"
            : $"{name}: ⚠ {replaced}/{ClipToState.Count} 교체 ({original.name}), 원작에 없는 상태: {string.Join(", ", missing)} (Bristle 클립 유지)";
    }

    private static AnimatorController FindOriginalController(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name}_Body t:Prefab"))
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var animator = body != null ? body.GetComponent<Animator>() : null;
            if (animator == null) continue;
            RuntimeAnimatorController c = animator.runtimeAnimatorController;
            while (c is AnimatorOverrideController o) c = o.runtimeAnimatorController;
            if (c is AnimatorController ac) return ac;
        }
        return null;
    }

    private static void CollectStates(AnimatorStateMachine machine, Dictionary<string, Motion> states)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (!states.ContainsKey(child.state.name)) states[child.state.name] = child.state.motion;
        foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
            CollectStates(sub.stateMachine, states);
    }
}
