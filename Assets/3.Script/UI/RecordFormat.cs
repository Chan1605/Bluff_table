// 전적 글자 공용 (타이틀 패널 · 승자 HUD · 대기실 이름표)
public static class RecordFormat
{
    public static float Rate(int wins, int games) => games > 0 ? (float)wins / games : 0f;

    public static string RateText(int wins, int games) => games > 0 ? $"{Rate(wins, games) * 100f:0.#}%" : "--";

    // "12W 5L" / "12승 5패"
    public static string Short(int wins, int games) =>
        $"{wins}{Localization.Get("W")} {games - wins}{Localization.Get("L")}";

    // 승수 기준 칭호 (원작 술집 분위기)
    public static string Tier(int wins, int games)
    {
        string key = games <= 0 ? "NEWCOMER"
                   : wins < 3 ? "PATRON"
                   : wins < 10 ? "REGULAR"
                   : wins < 25 ? "HIGH ROLLER"
                   : "LEGEND OF THE BAR";
        return Localization.Get(key);
    }
}
