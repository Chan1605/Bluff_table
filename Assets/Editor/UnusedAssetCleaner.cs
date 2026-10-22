using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 미사용 리소스 정리 창: UnusedAssetsReport.csv를 읽어 분류별로 골라 휴지통으로 옮긴다 (OS 휴지통에서 복구 가능)
// 기본 체크 = "바로 지워도 됨" 분류. "확인 후" 분류는 직접 체크해야 지워진다
// 지우기 직전에 사용 여부를 다시 계산해서, 그 사이 쓰이게 된 에셋은 건너뛴다
// 메뉴: Tools / 정리 / 미사용 리소스 정리 창   (먼저 "미사용 리소스 리포트 만들기" 실행)
public class UnusedAssetCleaner : EditorWindow
{
    private const string ReportFile = "UnusedAssetsReport.csv";

    private class Item
    {
        public string Path;
        public long SizeKb;
        public bool On;
    }

    private class Category
    {
        public string Title;
        public string Hint;
        public bool Safe;
        public Func<string, bool> Match;
        public readonly List<Item> Items = new();
        public bool Expanded;
        public long SizeKb => Items.Sum(i => i.SizeKb);
    }

    private readonly List<Category> categories = new();
    private Vector2 scroll;
    private string loadedInfo;

    [MenuItem("Tools/정리/미사용 리소스 정리 창")]
    private static void Open()
    {
        var window = GetWindow<UnusedAssetCleaner>("미사용 리소스 정리");
        window.minSize = new Vector2(560f, 420f);
        window.Load();
    }

    private static List<Category> CreateCategories() => new()
    {
        // ── 바로 지워도 됨 ──
        new Category { Title = "예전 캐릭터 세트", Hint = "6.Materials/Characters · Prefabs/Characters/FBX", Safe = true,
            Match = p => p.StartsWith("Assets/6.Materials/Characters/") || p.StartsWith("Assets/2.Model/Prefabs/Characters/FBX/") },
        new Category { Title = "리핑 덤프 미사용 스킨", Hint = "Texture2D · Mesh · Material · Original/Bristle 텍스처·메시·머티리얼", Safe = true,
            Match = p => p.StartsWith("Assets/Texture2D/") || p.StartsWith("Assets/Mesh/") || p.StartsWith("Assets/Material/")
                         || (p.StartsWith("Assets/2.Model/Original/Bristle/") && !p.Contains("/AnimationClip/") && !p.Contains("/AnimatorController/")) },
        new Category { Title = "웹폰트", Hint = "9.Font/web (woff · css 등)", Safe = true,
            Match = p => p.StartsWith("Assets/9.Font/web/") },
        new Category { Title = "오브젝트 미연결 텍스처 · 임시 머티리얼", Hint = "6.Materials/Object · Art/Map_Bar · 6.Materials/Temp", Safe = true,
            Match = p => p.StartsWith("Assets/6.Materials/Object/") || p.StartsWith("Assets/Art/Map_Bar/") || p.StartsWith("Assets/6.Materials/Temp/") },
        new Category { Title = "기타 (상점 모션 · 옛 입력 · RenderTexture · 테스트 프리팹)", Hint = "Art/Anim · 10.Input · UI/Video · DummyGameFlow · Object/FBX", Safe = true,
            Match = p => p.StartsWith("Assets/Art/Anim/") || p.StartsWith("Assets/10.Input/") || p.StartsWith("Assets/4.Sprite/UI/Video/")
                         || p.StartsWith("Assets/2.Model/Prefabs/DummyGameFlow") || p.StartsWith("Assets/2.Model/Prefabs/Object/FBX/") },

        // ── 확인 후 ──
        new Category { Title = "에디터 툴 원본 (유지 권장)", Hint = "_Body 프리팹 · Blorf 컨트롤러 · 원작 클립 → Override 다시 만들기에 필요", Safe = false,
            Match = p => p.StartsWith("Assets/2.Model/Original/Characters/_Export/Origin/") || p.StartsWith("Assets/AnimatorController/")
                         || p.StartsWith("Assets/AnimationClip/") || p.StartsWith("Assets/2.Model/Original/Bristle/") },
        new Category { Title = "Pretendard 원본 폰트", Hint = "PretendardVariable.ttf 하나만 남기는 걸 권장 (기본 체크 해제됨)", Safe = false,
            Match = p => p.StartsWith("Assets/9.Font/public/") },
        new Category { Title = "아직 안 붙인 사운드", Hint = "사운드 담당 확인", Safe = false,
            Match = p => p.StartsWith("Assets/8.Audio/") },
        new Category { Title = "UI 스프라이트", Hint = "키 안내 · 아이콘 등, 쓸 계획 없으면 삭제", Safe = false,
            Match = p => p.StartsWith("Assets/4.Sprite/") },
        new Category { Title = "패널 프리팹 원본", Hint = "씬은 연결 풀린 복사본을 씀", Safe = false,
            Match = p => p.StartsWith("Assets/2.Model/Prefabs/UI/") || p.StartsWith("Assets/2.Model/Prefabs/introuiPrefabs/") },
        new Category { Title = "초기 연출 애니메이터 (LiarsBar/Presentation)", Hint = "팀원 확인", Safe = false,
            Match = p => p.StartsWith("Assets/LiarsBar/") },
        new Category { Title = "테스트 씬 (JE_TestScene)", Hint = "JE 님 확인", Safe = false,
            Match = p => p.StartsWith("Assets/1.Scene/") },
        new Category { Title = "분류 안 됨", Hint = "리포트에 새로 생긴 항목", Safe = false, Match = _ => true },
    };

    private void Load()
    {
        categories.Clear();
        categories.AddRange(CreateCategories());

        if (!File.Exists(ReportFile))
        {
            loadedInfo = $"{ReportFile}이 없습니다. Tools / 정리 / 미사용 리소스 리포트 만들기를 먼저 실행하세요.";
            return;
        }

        int count = 0;
        foreach (string line in File.ReadAllLines(ReportFile).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            List<string> cells = SplitCsv(line);
            if (cells.Count < 3 || !File.Exists(cells[0])) continue;   // 이미 지운 항목은 제외

            long.TryParse(cells[2], out long size);
            Category category = categories.First(c => c.Match(cells[0]));
            category.Items.Add(new Item { Path = cells[0], SizeKb = size, On = category.Safe });
            count++;
        }
        categories.RemoveAll(c => c.Items.Count == 0);
        loadedInfo = $"리포트 {File.GetLastWriteTime(ReportFile):MM-dd HH:mm} · 남은 후보 {count}개";
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("체크한 에셋을 휴지통으로 옮깁니다 (OS 휴지통에서 복구 가능). 지우기 전에 Git 커밋을 권장합니다.\n" +
                                "메인 프로젝트에서만 실행하세요 (ParrelSync 클론은 Assets > Refresh만).", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(loadedInfo ?? string.Empty);
            if (GUILayout.Button("다시 읽기", GUILayout.Width(90f))) Load();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        bool? lastSafe = null;
        foreach (Category c in categories)
        {
            if (lastSafe != c.Safe)
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(c.Safe ? "바로 지워도 됨" : "확인 후 (기본 체크 해제)", EditorStyles.boldLabel);
                lastSafe = c.Safe;
            }
            DrawCategory(c);
        }
        EditorGUILayout.EndScrollView();

        List<Item> chosen = categories.SelectMany(c => c.Items).Where(i => i.On).ToList();
        EditorGUILayout.Space(4f);
        using (new EditorGUI.DisabledScope(chosen.Count == 0))
        {
            if (GUILayout.Button($"선택한 {chosen.Count}개 ({chosen.Sum(i => i.SizeKb) / 1024f:0.0} MB) 휴지통으로 이동", GUILayout.Height(32f)))
                TrashSelected(chosen);
        }
    }

    private static void DrawCategory(Category c)
    {
        int on = c.Items.Count(i => i.On);
        using (new EditorGUILayout.HorizontalScope())
        {
            bool all = on == c.Items.Count;
            EditorGUI.showMixedValue = on > 0 && !all;
            bool toggled = EditorGUILayout.Toggle(all, GUILayout.Width(16f));
            EditorGUI.showMixedValue = false;
            if (toggled != all) foreach (Item i in c.Items) i.On = toggled;

            c.Expanded = EditorGUILayout.Foldout(c.Expanded, $"{c.Title}   {on}/{c.Items.Count}개 · {c.SizeKb / 1024f:0.0} MB", true);
        }
        EditorGUILayout.LabelField("      " + c.Hint, EditorStyles.miniLabel);

        if (!c.Expanded) return;
        EditorGUI.indentLevel += 2;
        foreach (Item i in c.Items)
            i.On = EditorGUILayout.ToggleLeft($"{i.Path}   ({i.SizeKb} KB)", i.On);
        EditorGUI.indentLevel -= 2;
    }

    private void TrashSelected(List<Item> chosen)
    {
        if (!EditorUtility.DisplayDialog("휴지통으로 이동",
                $"{chosen.Count}개 에셋을 휴지통으로 옮깁니다.\n(지우기 직전에 사용 여부를 다시 확인해서, 사용 중인 것은 건너뜁니다)",
                "이동", "취소"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("미사용 리소스 정리", "사용 여부 다시 확인 중...", 0.2f);
            HashSet<string> used = UnusedAssetReport.CollectUsed();
            List<string> skipped = chosen.Where(i => used.Contains(i.Path)).Select(i => i.Path).ToList();
            string[] paths = chosen.Where(i => !used.Contains(i.Path) && File.Exists(i.Path)).Select(i => i.Path).ToArray();

            EditorUtility.DisplayProgressBar("미사용 리소스 정리", $"{paths.Length}개 휴지통으로 이동 중...", 0.6f);
            var failed = new List<string>();
            AssetDatabase.MoveAssetsToTrash(paths, failed);

            int emptyFolders = TrashEmptyFolders(paths);
            AssetDatabase.Refresh();

            Debug.Log($"[미사용 리소스 정리] 이동 {paths.Length - failed.Count}개, 빈 폴더 {emptyFolders}개 정리" +
                      (skipped.Count > 0 ? $"\n  사용 중이라 건너뜀 {skipped.Count}개:\n    " + string.Join("\n    ", skipped) : string.Empty) +
                      (failed.Count > 0 ? $"\n  실패 {failed.Count}개:\n    " + string.Join("\n    ", failed) : string.Empty));
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Load();
    }

    // 지운 에셋이 있던 폴더 중 비어 버린 폴더 정리 (안쪽부터)
    private static int TrashEmptyFolders(IEnumerable<string> removedPaths)
    {
        var folders = new HashSet<string>();
        foreach (string p in removedPaths)
        {
            string dir = Path.GetDirectoryName(p)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir) && dir != "Assets")
            {
                folders.Add(dir);
                dir = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            }
        }

        int count = 0;
        foreach (string folder in folders.OrderByDescending(f => f.Length))
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            bool empty = Directory.GetFiles(folder).All(f => f.EndsWith(".meta")) && Directory.GetDirectories(folder).Length == 0;
            if (empty && AssetDatabase.MoveAssetToTrash(folder)) count++;
        }
        return count;
    }

    private static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        bool quoted = false;
        foreach (char ch in line)
        {
            if (ch == '"') quoted = !quoted;
            else if (ch == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(ch);
        }
        cells.Add(cell.ToString());
        return cells;
    }
}
