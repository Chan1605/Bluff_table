using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BindPoseResetter : EditorWindow
{
    GameObject model;
    List<Transform> keepRoots = new List<Transform> { null };

    [MenuItem("Tools/Bind Pose Resetter")]
    static void Open() => GetWindow<BindPoseResetter>("Bind Pose Resetter");

    void OnGUI()
    {
        model = (GameObject)EditorGUILayout.ObjectField("Model", model, typeof(GameObject), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("원래 자세를 유지할 본 (이 본의 '자식들'만 유지)", EditorStyles.boldLabel);
        for (int i = 0; i < keepRoots.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            keepRoots[i] = (Transform)EditorGUILayout.ObjectField(keepRoots[i], typeof(Transform), true);
            if (GUILayout.Button("-", GUILayout.Width(24))) { keepRoots.RemoveAt(i); break; }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ 추가")) keepRoots.Add(null);

        EditorGUILayout.Space();
        GUI.enabled = model;
        if (GUILayout.Button("Reset To Bind Pose")) Reset();
        GUI.enabled = true;
    }

    void Reset()
    {
        var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var targets = new Dictionary<Transform, Matrix4x4>();

        foreach (var smr in smrs.OrderByDescending(s => s.bones.Length))
        {
            Transform[] bones = smr.bones;
            Matrix4x4[] bp = smr.sharedMesh ? smr.sharedMesh.bindposes : null;
            if (bp == null || bp.Length != bones.Length) continue;

            Matrix4x4 meshToWorld = smr.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] && !targets.ContainsKey(bones[i]))
                    targets[bones[i]] = meshToWorld * bp[i].inverse;
        }

        // 유지할 본: 지정한 본의 자식들 (지정한 본 자신은 바인드 포즈로 이동)
        var keep = new HashSet<Transform>();
        foreach (var root in keepRoots.Where(r => r))
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root) keep.Add(t);

        var toMove = targets.Keys.Where(t => !keep.Contains(t)).ToArray();
        Undo.RecordObjects(toMove, "Reset To Bind Pose");

        foreach (var bone in toMove.OrderBy(Depth))
        {
            Matrix4x4 m = targets[bone];
            bone.SetPositionAndRotation(m.GetColumn(3), m.rotation);
        }

        Debug.Log($"본 {toMove.Length}개를 바인드 포즈로 이동, {keep.Count}개는 원래 자세 유지");
    }

    static int Depth(Transform t) { int d = 0; while (t.parent) { d++; t = t.parent; } return d; }
}