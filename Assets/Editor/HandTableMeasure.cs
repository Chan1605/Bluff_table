using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 플레이 중 앉은 캐릭터들의 손끝 높이와 테이블 윗면 높이를 비교해서, 테이블을 얼마나 올리면 되는지 알려 준다.
// 메뉴: Tools / 원작 캐릭터 / 손끝 ↔ 테이블 높이 재기 (플레이 중에만)
public static class HandTableMeasure
{
    private const string TableRendererName = "SM_GameTable";

    // 손끝 후보 (리그에 없는 뼈는 건너뜀) → 하나도 없으면 손목(Hand)
    private static readonly HumanBodyBones[] LeftTips =
    {
        HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftThumbDistal
    };
    private static readonly HumanBodyBones[] RightTips =
    {
        HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleDistal, HumanBodyBones.RightThumbDistal
    };

    [MenuItem("Tools/원작 캐릭터/손끝 ↔ 테이블 높이 재기 (플레이 중)")]
    private static void Measure()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[높이 재기] 플레이 중에, 캐릭터들이 의자에 앉은 뒤 실행하세요.");
            return;
        }

        Renderer table = FindTable();
        if (table == null)
        {
            Debug.LogWarning($"[높이 재기] '{TableRendererName}' 렌더러를 못 찾음");
            return;
        }
        float tableTop = table.bounds.max.y;

        var log = new StringBuilder($"[높이 재기] 테이블 윗면 Y = {tableTop:F3}\n");
        var diffs = new List<float>();
        foreach (SeatedPose seated in Object.FindObjectsByType<SeatedPose>(FindObjectsSortMode.None))
        {
            var animator = seated.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) continue;

            float left = LowestY(animator, LeftTips, HumanBodyBones.LeftHand);
            float right = LowestY(animator, RightTips, HumanBodyBones.RightHand);
            float tip = Mathf.Min(left, right);
            float diff = tip - tableTop;
            diffs.Add(diff);
            log.AppendLine($"  {seated.name}: 손끝 Y = {tip:F3} (왼 {left:F3} / 오 {right:F3}) → 테이블보다 {diff:+0.000;-0.000}");
        }

        if (diffs.Count == 0)
        {
            Debug.LogWarning("[높이 재기] 앉은 캐릭터(SeatedPose)를 못 찾음");
            return;
        }

        diffs.Sort();
        float average = 0f;
        foreach (float d in diffs) average += d;
        average /= diffs.Count;
        log.AppendLine($"  평균 {average:+0.000;-0.000} / 최소 {diffs[0]:+0.000;-0.000} / 최대 {diffs[diffs.Count - 1]:+0.000;-0.000}");
        log.AppendLine($"  → 테이블을 약 {average:+0.000;-0.000} (월드 단위) 옮기면 평균 손끝이 윗면에 닿음 (+면 올리기)");
        Debug.Log(log.ToString(), table);
    }

    private static Renderer FindTable()
    {
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (r.name == TableRendererName) return r;
        return null;
    }

    private static float LowestY(Animator animator, HumanBodyBones[] tips, HumanBodyBones fallback)
    {
        float lowest = float.MaxValue;
        foreach (HumanBodyBones bone in tips)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null) lowest = Mathf.Min(lowest, t.position.y);
        }
        if (lowest == float.MaxValue)
        {
            Transform hand = animator.GetBoneTransform(fallback);
            lowest = hand != null ? hand.position.y : float.NaN;
        }
        return lowest;
    }
}
