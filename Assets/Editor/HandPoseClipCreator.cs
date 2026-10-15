#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class HandPoseClipCreator
{
    [MenuItem("Tools/Create Hand Pinch Clip")]
    static void Create()
    {
        var clip = new AnimationClip { name = "Hand_Pinch" };
        string side = "LeftHand"; // 카드를 집는 손이 왼손이면 "LeftHand"

        // 엄지·검지는 맞닿게, 나머지는 살짝 말아 쥔 시작값 (Animation 창에서 조절)
        Set(clip, side, "Thumb", 0.3f, 0.2f, 0.2f, -0.2f);
        Set(clip, side, "Index", 0.2f, -0.2f, 0.0f, 0.0f);
        Set(clip, side, "Middle", -0.4f, -0.5f, -0.5f, 0.0f);
        Set(clip, side, "Ring", -0.5f, -0.6f, -0.6f, 0.0f);
        Set(clip, side, "Little", -0.6f, -0.7f, -0.7f, 0.0f);

        AssetDatabase.CreateAsset(clip, "Assets/Hand_Pinch.anim");
        AssetDatabase.SaveAssets();
        Selection.activeObject = clip;
    }

    static void Set(AnimationClip clip, string side, string finger,
                    float s1, float s2, float s3, float spread)
    {
        Key(clip, $"{side}.{finger}.1 Stretched", s1);
        Key(clip, $"{side}.{finger}.2 Stretched", s2);
        Key(clip, $"{side}.{finger}.3 Stretched", s3);
        Key(clip, $"{side}.{finger}.Spread", spread);
    }

    static void Key(AnimationClip clip, string muscle, float value)
    {
        var curve = AnimationCurve.Constant(0f, 0f, value);
        AnimationUtility.SetEditorCurve(clip,
            EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
    }
}
#endif