using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


// 번역 표: Assets/Resources/Localization/Strings.csv (en,ko 두 칸, 팀원이 엑셀/메모장으로 추가 가능)
public static class Localization
{
    public const int English = 0;
    public const int Korean = 1;
    private const string TablePath = "Localization/Strings";

    private static Dictionary<string, string> korean;

    public static int Language { get; private set; } = English;

    // 언어가 바뀌면 LocalizedText 등이 다시 그린다
    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        korean = null;
        Language = English;
        LanguageChanged = null;
    }

    public static void SetLanguage(int language)
    {
        language = Mathf.Clamp(language, English, Korean);
        if (language == Language) return;

        Language = language;
        LanguageChanged?.Invoke();
    }

    public static string Get(string english)
    {
        if (string.IsNullOrEmpty(english) || Language == English) return english;

        Load();
        return korean.TryGetValue(english.Trim(), out string text) && text.Length > 0 ? text : english;
    }

    // 표를 다시 읽고 화면 갱신 (CSV를 고친 뒤 플레이 중 확인용)
    public static void Reload()
    {
        korean = null;
        LanguageChanged?.Invoke();
    }

    private static void Load()
    {
        if (korean != null) return;

        TextAsset asset = Resources.Load<TextAsset>(TablePath);
        if (asset == null)
        {
            Debug.LogWarning($"[Localization] Resources/{TablePath}.csv 를 찾지 못했습니다.");
            korean = new Dictionary<string, string>();
            return;
        }
        korean = ParseTable(asset.text);
    }

    // "#"으로 시작하는 줄과 머리글(en,ko)은 무시
    public static Dictionary<string, string> ParseTable(string csv)
    {
        var table = new Dictionary<string, string>();

        foreach (string raw in csv.Split('\n'))
        {
            string line = raw.TrimEnd('\r').TrimStart('\uFEFF');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith("#")) continue;

            List<string> cells = ParseLine(line);
            if (cells.Count < 2) continue;

            string key = cells[0].Trim();
            if (key.Length == 0 || key == "en") continue;   // 머리글(en,ko) 건너뜀
            table[key] = cells[1].Trim();
        }
        return table;
    }

    private static List<string> ParseLine(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString());
        return cells;
    }
}
