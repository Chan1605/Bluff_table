using System.IO;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// 원작 몸체 프리팹(_Body)을 선택하고 실행하면, 완성된 Bristle_Sit을 본떠 <이름>_Sit.prefab을 같은 폴더에 만든다.
// - 루트 Scale = 원작 Scale × (1.5 / 0.9)   (Bristle 기준 비율)
// - SeatedPose 복사 (Extra Offset 포함)
// - Animator: Bristle_Sit과 같은 컨트롤러 (나중에 캐릭터별 Override로 교체), Apply Root Motion 끔
// - 머리 밑 Virtual Camera/cm 에 CinemachineCamera · HeadController 복사, Head Pivot = HeadPivot 연결
// 스킨 정리 · 머티리얼 변환은 따로 한다.
// 메뉴: Tools / 원작 캐릭터 / 선택한 몸체로 Sit 프리팹 만들기
public static class OriginalSitPrefabBuilder
{
    private const string TemplatePath = "Assets/2.Model/Original/Bristle/Export/Bristle_Sit.prefab";
    private const float ScaleRatio = 1.5f / 0.9f;

    [MenuItem("Tools/원작 캐릭터/선택한 몸체로 Sit 프리팹 만들기")]
    private static void BuildSelected()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
        if (template == null)
        {
            Debug.LogError($"[Sit 조립] 기준 프리팹이 없음: {TemplatePath}");
            return;
        }

        var log = new StringBuilder("[Sit 조립] 결과\n");
        foreach (GameObject body in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string bodyPath = AssetDatabase.GetAssetPath(body);
            if (!bodyPath.EndsWith(".prefab")) continue;
            log.AppendLine("  " + BuildOne(template, body, bodyPath));
        }
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    private static string BuildOne(GameObject template, GameObject body, string bodyPath)
    {
        string baseName = body.name.EndsWith("_Body") ? body.name.Substring(0, body.name.Length - 5) : body.name;
        string outPath = $"{Path.GetDirectoryName(bodyPath).Replace('\\', '/')}/{baseName}_Sit.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(outPath) != null) return $"{baseName}: 건너뜀 (이미 있음 {outPath})";

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(body);
        try
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = baseName + "_Sit";
            float originalScale = instance.transform.localScale.x;
            instance.transform.localScale = instance.transform.localScale * ScaleRatio;

            // Animator
            var animator = instance.GetComponent<Animator>();
            var templateAnimator = template.GetComponent<Animator>();
            if (animator == null) return $"{baseName}: ❌ 루트에 Animator 없음";
            animator.runtimeAnimatorController = templateAnimator != null ? templateAnimator.runtimeAnimatorController : null;
            animator.applyRootMotion = false;

            // SeatedPose
            var templateSeated = template.GetComponent<SeatedPose>();
            if (templateSeated != null && instance.GetComponent<SeatedPose>() == null)
                CopyTo(templateSeated, instance);

            // 1인칭 카메라 + 고개
            string cameraNote;
            var templateHead = template.GetComponentInChildren<HeadController>(true);
            var templateCam = templateHead != null ? templateHead.firstPersonCamera : null;
            Transform camT = FindDeep(instance.transform, "cm");
            Transform pivot = FindDeep(instance.transform, "HeadPivot");
            if (camT == null || pivot == null || templateHead == null || templateCam == null)
            {
                cameraNote = $"⚠ 카메라 생략 (cm {(camT != null ? "O" : "X")} / HeadPivot {(pivot != null ? "O" : "X")})";
            }
            else
            {
                var cam = (CinemachineCamera)CopyTo(templateCam, camT.gameObject);
                var head = (HeadController)CopyTo(templateHead, camT.gameObject);
                head.headPivot = pivot;
                head.firstPersonCamera = cam;
                cameraNote = "카메라 · 고개 O";
            }

            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

            PrefabUtility.SaveAsPrefabAsset(instance, outPath, out bool ok);
            return ok
                ? $"{baseName}: ✅ {outPath} (Scale {originalScale:0.##} → {originalScale * ScaleRatio:0.##}, {cameraNote})"
                : $"{baseName}: ❌ 저장 실패";
        }
        finally
        {
            if (instance != null) Object.DestroyImmediate(instance);
        }
    }

    private static Component CopyTo(Component source, GameObject target)
    {
        ComponentUtility.CopyComponent(source);
        ComponentUtility.PasteComponentAsNew(target);
        Component[] all = target.GetComponents(source.GetType());
        return all[all.Length - 1];
    }

    private static Transform FindDeep(Transform root, string target)
    {
        if (root.name == target) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, target);
            if (found != null) return found;
        }
        return null;
    }
}
