using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// 1인칭 카메라(CinemachineCamera) · HeadController가 빠진 원작 Sit 프리팹을 고친다.
// 예: Toar_Sit — 'Virtual Camera/cm' 오브젝트는 있는데 컴포넌트가 없어서 인게임에서 로비 카메라가 그대로 남음
// 다른 캐릭터(정상 프리팹)의 렌즈 · 고개 회전 설정을 그대로 복사한다. 원작 리그 이름 'HeadPİvot'(터키어 İ)도 HeadPivot으로 인식.
// 메뉴: Tools / 원작 캐릭터 / 1인칭 카메라 없는 Sit 프리팹 고치기
public static class SitCameraFixer
{
    private const string SitFolder = "Assets/2.Model/Original/Characters/_Export/Sit_Character";

    [MenuItem("Tools/원작 캐릭터/1인칭 카메라 없는 Sit 프리팹 고치기")]
    private static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { SitFolder });

        // 기준: 카메라가 정상인 첫 Sit 프리팹
        HeadController reference = null;
        foreach (string guid in guids)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            HeadController h = go != null ? go.GetComponentInChildren<HeadController>(true) : null;
            if (h != null && h.firstPersonCamera != null) { reference = h; break; }
        }
        if (reference == null) { Debug.LogError("[1인칭 카메라] 기준이 될 정상 Sit 프리팹이 없음"); return; }

        var log = new StringBuilder($"[1인칭 카메라] 기준: {reference.transform.root.name}\n");
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<HeadController>(true) != null) continue;   // 이미 정상

                Transform pivot = Find(root.transform, t => Normalize(t.name) == "headpivot");
                Transform cmObject = Find(root.transform, t => t.name == "cm" && t.parent != null && t.parent.name == "Virtual Camera")
                                     ?? Find(root.transform, t => t.name == "Virtual Camera");
                if (pivot == null || cmObject == null)
                {
                    log.AppendLine($"  ❌ {root.name}: HeadPivot({(pivot != null ? "O" : "X")}) / Virtual Camera({(cmObject != null ? "O" : "X")})를 못 찾음 → 직접 확인 필요");
                    continue;
                }

                if (!cmObject.TryGetComponent(out CinemachineCamera cam)) cam = cmObject.gameObject.AddComponent<CinemachineCamera>();
                cam.Lens = reference.firstPersonCamera.Lens;
                cam.Priority = reference.firstPersonCamera.Priority;

                HeadController head = cmObject.gameObject.AddComponent<HeadController>();
                head.headPivot = pivot;
                head.firstPersonCamera = cam;
                head.lookSensitivity = reference.lookSensitivity;
                head.minPitch = reference.minPitch;
                head.maxPitch = reference.maxPitch;
                head.minYaw = reference.minYaw;
                head.maxYaw = reference.maxYaw;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                fixedCount++;
                log.AppendLine($"  ✅ {root.name}: {AnimationUtility.CalculateTransformPath(cmObject, root.transform)}에 CinemachineCamera + HeadController (HeadPivot = {pivot.name})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        log.AppendLine($"  고친 프리팹 {fixedCount}개");
        Debug.Log(log.ToString());
    }

    // 'HeadPİvot'(터키어 대문자 İ) 같은 원작 이름도 같은 것으로 본다
    private static string Normalize(string name) => name.Replace('İ', 'I').Replace('ı', 'i').ToLowerInvariant();

    private static Transform Find(Transform root, System.Func<Transform, bool> match)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (match(t)) return t;
        return null;
    }
}
