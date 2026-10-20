using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LocalizationTools
{
    private const string TablePath = "Assets/Resources/Localization/Strings.csv";
    private const string UiPrefabFolder = "Assets/2.Model/Prefabs/UI";
    private static readonly string[] ExtraPrefabs = { "Assets/2.Model/Prefabs/Player.prefab" };

    // 스크립트가 글자를 읽기만 하는 필드 (번역 대상에 포함)
    private static readonly HashSet<string> ReadOnlyFields = new() { "settingsTitle" };

    [MenuItem("Tools/LiarsBar/Localization/Add LocalizedText (Prefabs + Open Scenes)")]
    private static void AddLocalizedTexts()
    {
        if (!File.Exists(TablePath))
        {
            Debug.LogError($"[Localization] {TablePath} 가 없습니다.");
            return;
        }
        Dictionary<string, string> table = Localization.ParseTable(File.ReadAllText(TablePath));

        int prefabCount = 0, added = 0;
        var paths = new List<string>(ExtraPrefabs);
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { UiPrefabFolder }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));

        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = AddTo(root.transform, table, false);
                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabCount++;
                    added += count;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        int sceneAdded = 0;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            int count = 0;
            foreach (GameObject go in scene.GetRootGameObjects())
                count += AddTo(go.transform, table, true);

            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
            sceneAdded += count;
        }

        Debug.Log($"[Localization] 프리팹 {prefabCount}개에 {added}개, 열린 씬에 {sceneAdded}개 LocalizedText를 붙였습니다." +
                  (sceneAdded > 0 ? " 씬을 저장하세요." : ""));
    }

    [MenuItem("Tools/LiarsBar/Localization/Reload Table (Play Mode)")]
    private static void ReloadTable()
    {
        AssetDatabase.Refresh();
        Localization.Reload();
    }

    private static int AddTo(Transform root, Dictionary<string, string> table, bool inScene)
    {
        HashSet<Object> driven = CollectScriptDrivenTexts(root);
        int count = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponent<LocalizedText>() != null) continue;
            if (text.GetComponentInParent<TMP_InputField>(true) != null) continue;
            if (text.GetComponentInParent<TMP_Dropdown>(true) != null) continue;
            if (driven.Contains(text)) continue;
            if (!table.ContainsKey(text.text.Trim())) continue;
            if (inScene && PrefabUtility.IsPartOfPrefabInstance(text)) continue;   // 프리팹 쪽에서 처리

            if (inScene) Undo.AddComponent<LocalizedText>(text.gameObject);
            else text.gameObject.AddComponent<LocalizedText>();
            count++;
        }
        return count;
    }

    // 우리 스크립트(Assembly-CSharp)의 Inspector 필드에 연결된 TMP = 스크립트가 글자를 바꾸는 텍스트
    private static HashSet<Object> CollectScriptDrivenTexts(Transform root)
    {
        var driven = new HashSet<Object>();
        foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null || mb is LocalizedText || mb is SettingsRowHint) continue;   // RowHint는 색만 바꿈
            if (mb.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;

            var so = new SerializedObject(mb);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.ObjectReference || !(p.objectReferenceValue is TMP_Text t)) continue;
                if (ReadOnlyFields.Contains(p.name)) continue;
                driven.Add(t);
            }
        }
        return driven;
    }
}
