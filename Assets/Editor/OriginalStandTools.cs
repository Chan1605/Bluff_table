using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원작 캐릭터 마무리 도구 (메뉴: Tools / 원작 캐릭터 /)
// 1) 테이블 총: <이름>_Body의 'Stylized Revolver'를 <이름>_Sit에 복사 (Bristle_Sit에 직접 넣은 것과 같은 규칙)
// 2) Stand 프리팹: <이름>_Body → <이름>_Stand (Scale = 원작 ÷ 0.9, Bristle 1.0 · 대기실 Idle · 총 · 스킨 정리) + Player 대기실 목록 교체 · ModelRoot Scale 1
// 3·4) 옛 FBX · Stand Model 폴더: 바깥 참조 확인 → 카드 메쉬는 복사본으로 옮기고 폴더 삭제(휴지통)
public static class OriginalStandTools
{
    private const string MenuRoot = "Tools/원작 캐릭터/";
    private const string ExportRoot = "Assets/2.Model/Original/Characters/_Export";
    private const string BodyFolder = ExportRoot + "/Origin";
    private const string SitFolder = ExportRoot + "/Sit_Character";
    private const string StandFolder = ExportRoot + "/Stand_Character";
    private const string IdleControllerFormat = "Assets/Art/Anim/{0}/{0}Idle.controller";
    private const string PlayerPrefabPath = "Assets/2.Model/Prefabs/Player.prefab";
    private const string CardMeshFolder = "Assets/Art/Mesh/Cards";
    private const string GunName = "Stylized Revolver";
    private const float ScaleRatio = 1.5f / 0.9f;
    // 대기실은 옛 모델과 같은 비율 (옛 Stand: ModelRoot 100 / 옛 Sit 150) → 테이블 크기의 2/3, Bristle = 1.0
    private const float StandScaleRatio = ScaleRatio * (100f / 150f);

    private static readonly string[] Names =
        { "Bristle", "Cupcake", "Dubai", "Foxy", "Gerk", "Kudo", "Scubby", "Skunk", "Tigre", "Toar", "Tusk" };

    private static readonly string[] OldFolders =
        { "Assets/2.Model/Prefabs/Characters/FBX", "Assets/2.Model/Prefabs/Characters/Stand Model" };

    private static readonly string[] UserExtensions =
        { ".prefab", ".unity", ".asset", ".controller", ".overrideController", ".mat", ".playable" };

    // ───── 1. 테이블 총 ─────

    [MenuItem(MenuRoot + "1. 테이블 총 복사 (Body → Sit 11종)")]
    private static void CopyTableGuns()
    {
        // Sit 루트가 Body × 1.667이라, 총은 월드 크기를 유지하도록 1 / 1.667 (Bristle_Sit과 같음)
        float ratio = 1f / ScaleRatio;
        var log = new StringBuilder("[테이블 총] 결과\n");

        foreach (string n in Names)
        {
            string bodyPath = $"{BodyFolder}/{n}_Body.prefab";
            string sitPath = $"{SitFolder}/{n}_Sit.prefab";
            if (!Exists(bodyPath) || !Exists(sitPath))
            {
                log.AppendLine($"  {n}: ❌ Body 또는 Sit 프리팹 없음");
                continue;
            }

            GameObject body = PrefabUtility.LoadPrefabContents(bodyPath);
            GameObject sit = PrefabUtility.LoadPrefabContents(sitPath);
            try
            {
                Transform src = FindRootChild(body.transform, GunName);
                if (src == null) { log.AppendLine($"  {n}: ⚠ Body에 총 없음"); continue; }
                if (FindRootChild(sit.transform, GunName) != null) { log.AppendLine($"  {n}: 건너뜀 (이미 있음)"); continue; }

                Transform copy = Object.Instantiate(src.gameObject, sit.transform, false).transform;
                copy.name = src.name;
                copy.localPosition = src.localPosition;
                copy.localRotation = src.localRotation;
                copy.localScale = src.localScale * ratio;
                copy.SetSiblingIndex(Mathf.Min(src.GetSiblingIndex(), sit.transform.childCount - 1));

                PrefabUtility.SaveAsPrefabAsset(sit, sitPath);
                log.AppendLine($"  {n}: ✅ 추가 (Scale {src.localScale.x:0.##} → {copy.localScale.x:0.##})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(body);
                PrefabUtility.UnloadPrefabContents(sit);
            }
        }
        Debug.Log(log.ToString());
    }

    // ───── 2. Stand 프리팹 ─────

    [MenuItem(MenuRoot + "2. Stand 프리팹 만들기 + 대기실 목록 교체")]
    private static void BuildStands()
    {
        EnsureFolder(StandFolder);
        var log = new StringBuilder("[Stand 조립] 결과\n");
        var built = new Dictionary<string, GameObject>();

        foreach (string n in Names)
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>($"{BodyFolder}/{n}_Body.prefab");
            var idle = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(string.Format(IdleControllerFormat, n));
            if (body == null || idle == null)
            {
                log.AppendLine($"  {n}: ❌ {(body == null ? "Body" : "Idle 컨트롤러")} 없음");
                continue;
            }

            string outPath = $"{StandFolder}/{n}_Stand.prefab";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(body);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = n + "_Stand";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale *= StandScaleRatio;

                var animator = instance.GetComponent<Animator>();
                if (animator == null) { log.AppendLine($"  {n}: ❌ 루트에 Animator 없음"); continue; }
                animator.runtimeAnimatorController = idle;
                animator.applyRootMotion = false;

                var removed = new List<string>();
                OriginalSkinCleaner.Clean(instance, false, removed);   // 대기실에는 테이블 총도 지움
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, outPath, out bool ok);
                if (ok) built[n] = saved;
                log.AppendLine(ok ? $"  {n}: ✅ {outPath} ({idle.name}, 정리 {removed.Count}개)" : $"  {n}: ❌ 저장 실패");
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
        }

        LinkPlayerPrefab(built, log);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    private static void LinkPlayerPrefab(Dictionary<string, GameObject> built, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var view = root.GetComponentInChildren<LobbyCharacterView>(true);
            if (view == null) { log.AppendLine("  Player: ❌ LobbyCharacterView 없음"); return; }

            var so = new SerializedObject(view);
            SerializedProperty list = so.FindProperty("characters");
            int linked = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                string display = entry.FindPropertyRelative("displayName").stringValue.Trim();
                string key = Names.FirstOrDefault(x => string.Equals(x, display, System.StringComparison.OrdinalIgnoreCase));
                if (key == null || !built.TryGetValue(key, out GameObject prefab))
                {
                    log.AppendLine($"  Player: ⚠ {i}번 '{display}' 연결 못 함");
                    continue;
                }
                entry.FindPropertyRelative("standModel").objectReferenceValue = prefab;
                linked++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // 옛 FBX(cm 단위)용으로 100이던 ModelRoot를 1로 (원작 몸체는 m 단위)
            var modelRoot = so.FindProperty("modelRoot").objectReferenceValue as Transform;
            string rootNote = "ModelRoot 없음";
            if (modelRoot != null)
            {
                rootNote = $"ModelRoot Scale {modelRoot.localScale.x:0.##} → 1";
                modelRoot.localScale = Vector3.one;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            log.AppendLine($"  Player: ✅ 대기실 목록 {linked}/{list.arraySize}개 교체, {rootNote}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ───── 3 · 4. 옛 FBX · Stand Model 정리 ─────

    [MenuItem(MenuRoot + "3. 옛 FBX · Stand Model 참조 확인 (삭제 안 함)")]
    private static void CheckOld() => CleanupOld(false);

    [MenuItem(MenuRoot + "4. 옛 FBX · Stand Model 정리 실행")]
    private static void RunOld() => CleanupOld(true);

    private static void CleanupOld(bool execute)
    {
        var doomed = new HashSet<string>(AssetDatabase.FindAssets("", OldFolders).Select(AssetDatabase.GUIDToAssetPath));
        Dictionary<string, List<string>> users = FindUsers(doomed);
        var log = new StringBuilder(execute ? "[옛 파일 정리] 실행\n" : "[옛 파일 정리] 확인만 (삭제 안 함)\n");
        AppendUsers(log, users);

        if (execute)
        {
            var meshMap = new Dictionary<Mesh, Mesh>();
            foreach (string path in users.Keys.Where(p => p.EndsWith(".prefab")))
                RepointMeshes(path, doomed, meshMap, log);
            AssetDatabase.SaveAssets();

            users = FindUsers(doomed);
            if (users.Count > 0)
            {
                log.AppendLine("❌ 아직 참조가 남아 있어 삭제하지 않았습니다. 위 파일을 먼저 정리하세요:");
                AppendUsers(log, users);
                Debug.LogError(log.ToString());
                return;
            }

            foreach (string folder in OldFolders)
                if (AssetDatabase.IsValidFolder(folder))
                    log.AppendLine($"  {(AssetDatabase.MoveAssetToTrash(folder) ? "🗑 휴지통으로" : "❌ 삭제 실패")}: {folder}");
            AssetDatabase.Refresh();
        }
        else if (users.Count > 0)
        {
            log.AppendLine("→ 프리팹 안의 카드 메쉬 참조는 4번 실행 때 복사본으로 자동 교체됩니다. 씬 · 컨트롤러 참조는 직접 정리해야 합니다.");
        }
        Debug.Log(log.ToString());
    }

    private static Dictionary<string, List<string>> FindUsers(HashSet<string> doomed)
    {
        var users = new Dictionary<string, List<string>>();
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || doomed.Contains(path)) continue;
            if (!UserExtensions.Contains(Path.GetExtension(path))) continue;

            string[] hits = AssetDatabase.GetDependencies(path, false).Where(doomed.Contains).ToArray();
            if (hits.Length > 0) users[path] = hits.ToList();
        }
        return users;
    }

    private static void AppendUsers(StringBuilder log, Dictionary<string, List<string>> users)
    {
        if (users.Count == 0) { log.AppendLine("  바깥 참조 없음"); return; }
        foreach (var kv in users)
            log.AppendLine($"  {kv.Key} → {string.Join(", ", kv.Value.Select(Path.GetFileName))}");
    }

    private static void RepointMeshes(string path, HashSet<string> doomed, Dictionary<Mesh, Mesh> meshMap, StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        int changed = 0;
        try
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                var so = new SerializedObject(c);
                SerializedProperty p = so.GetIterator();
                bool any = false;
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (p.objectReferenceValue is not Mesh mesh) continue;
                    if (!doomed.Contains(AssetDatabase.GetAssetPath(mesh))) continue;

                    p.objectReferenceValue = GetMeshCopy(mesh, meshMap);
                    any = true;
                    changed++;
                }
                if (any) so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (changed > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"  {Path.GetFileName(path)}: 메쉬 참조 {changed}개 → {CardMeshFolder}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Mesh GetMeshCopy(Mesh source, Dictionary<Mesh, Mesh> meshMap)
    {
        if (meshMap.TryGetValue(source, out Mesh copy)) return copy;

        EnsureFolder(CardMeshFolder);
        string safe = string.Join("_", source.name.Split(Path.GetInvalidFileNameChars()));
        copy = Object.Instantiate(source);
        copy.name = source.name;
        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{CardMeshFolder}/{safe}.asset"));
        meshMap[source] = copy;
        return copy;
    }

    // ───── 공용 ─────

    private static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;

    private static Transform FindRootChild(Transform root, string name)
    {
        foreach (Transform child in root)
            if (child.name.Trim() == name) return child;
        return null;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
