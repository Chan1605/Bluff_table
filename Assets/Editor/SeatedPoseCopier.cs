using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 앉은 캐릭터의 자세(Humanoid 근육값)를 다른 캐릭터에 복사 · .anim으로 저장
public class SeatedPoseCopier : EditorWindow
{
    private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
    private const float SeatedLegThreshold = 0.2f;   // 앉은 자세면 허벅지 앞뒤 근육이 이 값보다 큼

    [SerializeField] private Animator source;
    [SerializeField] private Animator target;
    [SerializeField] private bool copyBody = true;

    [MenuItem("Tools/Seated Pose Copier")]
    private static void Open() => GetWindow<SeatedPoseCopier>("Seated Pose Copier");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("원본(앉은 모델)의 자세를 대상에 복사합니다. 둘 다 Humanoid Avatar가 필요합니다.", MessageType.Info);

        source = (Animator)EditorGUILayout.ObjectField("원본 (앉은 모델)", source, typeof(Animator), true);
        target = (Animator)EditorGUILayout.ObjectField("대상", target, typeof(Animator), true);
        copyBody = EditorGUILayout.Toggle(new GUIContent("엉덩이 높이 · 기울기 복사", "끄면 관절 각도(근육)만 복사"), copyBody);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!IsHumanoid(source)))
        {
            if (GUILayout.Button("원본 자세 → .anim 저장")) SaveClip();
        }
        using (new EditorGUI.DisabledScope(!IsHumanoid(source) || !IsHumanoid(target) || source == target))
        {
            if (GUILayout.Button("대상에 자세 복사")) CopyPose();
        }
    }

    private static bool IsHumanoid(Animator animator)
    {
        return animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
    }

    private static HumanPose ReadPose(Animator animator)
    {
        HumanPose pose = default;
        using (HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform))
            handler.GetHumanPose(ref pose);
        return pose;
    }

    // Avatar를 앉은 자세 기준으로 만들면 근육값이 0 근처로 나와 복사해도 서 있게 됨
    private static bool LooksSeated(HumanPose pose)
    {
        int left = System.Array.IndexOf(HumanTrait.MuscleName, "Left Upper Leg Front-Back");
        int right = System.Array.IndexOf(HumanTrait.MuscleName, "Right Upper Leg Front-Back");
        if (left < 0 || right < 0) return true;
        return Mathf.Abs(pose.muscles[left]) > SeatedLegThreshold || Mathf.Abs(pose.muscles[right]) > SeatedLegThreshold;
    }

    private void CopyPose()
    {
        HumanPose sourcePose = ReadPose(source);
        if (!LooksSeated(sourcePose))
            Debug.LogWarning($"[SeatedPose] '{source.name}'의 다리 근육값이 0 근처입니다. 원본 FBX Avatar가 앉은 자세로 만들어졌을 수 있습니다 (Configure → Enforce T-Pose).", source);

        using (HumanPoseHandler handler = new HumanPoseHandler(target.avatar, target.transform))
        {
            HumanPose targetPose = default;
            handler.GetHumanPose(ref targetPose);

            targetPose.muscles = (float[])sourcePose.muscles.Clone();
            if (copyBody)
            {
                // bodyPosition은 Avatar 키 기준으로 정규화된 값이라 체형이 달라도 그대로 써도 됨
                targetPose.bodyPosition = sourcePose.bodyPosition;
                targetPose.bodyRotation = sourcePose.bodyRotation;
            }

            Undo.RegisterFullObjectHierarchyUndo(target.gameObject, "Copy Seated Pose");
            handler.SetHumanPose(ref targetPose);
        }

        foreach (Transform t in target.GetComponentsInChildren<Transform>(true))
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);

        Debug.Log($"[SeatedPose] '{source.name}' → '{target.name}' 자세 복사 완료");
    }

    private void SaveClip()
    {
        string path = EditorUtility.SaveFilePanelInProject("앉은 자세 클립 저장", source.name + "_Sit", "anim", "저장할 위치를 고르세요");
        if (string.IsNullOrEmpty(path)) return;

        HumanPose pose = ReadPose(source);
        if (!LooksSeated(pose))
            Debug.LogWarning($"[SeatedPose] '{source.name}'의 다리 근육값이 0 근처입니다. 원본 FBX Avatar를 확인하세요.", source);

        AnimationClip clip = new AnimationClip();
        for (int i = 0; i < pose.muscles.Length; i++)
            SetCurve(clip, ToCurveName(HumanTrait.MuscleName[i]), pose.muscles[i]);

        if (copyBody)
        {
            SetCurve(clip, "RootT.x", pose.bodyPosition.x);
            SetCurve(clip, "RootT.y", pose.bodyPosition.y);
            SetCurve(clip, "RootT.z", pose.bodyPosition.z);
            SetCurve(clip, "RootQ.x", pose.bodyRotation.x);
            SetCurve(clip, "RootQ.y", pose.bodyRotation.y);
            SetCurve(clip, "RootQ.z", pose.bodyRotation.z);
            SetCurve(clip, "RootQ.w", pose.bodyRotation.w);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        Selection.activeObject = clip;
        Debug.Log($"[SeatedPose] 클립 저장: {path}");
    }

    private static void SetCurve(AnimationClip clip, string attribute, float value)
    {
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Animator), attribute),
            AnimationCurve.Constant(0f, 1f, value));
    }

    // HumanTrait 이름("Left Index 1 Stretched") → 클립 속성 이름("LeftHand.Index.1 Stretched")
    private static string ToCurveName(string muscle)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            foreach (string finger in Fingers)
            {
                string prefix = $"{side} {finger} ";
                if (muscle.StartsWith(prefix))
                    return $"{side}Hand.{finger}.{muscle.Substring(prefix.Length)}";
            }
        }
        return muscle;
    }
}
