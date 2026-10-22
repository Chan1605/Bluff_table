using DG.Tweening;
using LiarsBar.Server;
using TMPro;
using UnityEngine;

// 타이틀 오른쪽 아래 내 전적 (LIAR'S NEWS 아래)
// 서버가 로그인 때 DB에서 읽어 SyncVar(wins · gamesPlayed)로 보낸 값을 표시하고, 게임 후 갱신되면 PlayerProfile.Changed로 다시 그린다
// 오브젝트는 Tools / UI / 타이틀 전적 패널 만들기 (열린 씬) 로 생성
[RequireComponent(typeof(CanvasGroup))]
public class TitleStatsUI : MonoBehaviour
{
    [SerializeField] private TMP_Text header;
    [SerializeField] private TMP_Text nickname;
    [SerializeField] private TMP_Text tier;
    [SerializeField] private TMP_Text record;
    [SerializeField] private TMP_Text winRate;
    [SerializeField] private TMP_Text winRateLabel;
    [SerializeField] private TMP_Text games;
    [SerializeField] private RectTransform barFill;
    [SerializeField] private float fadeSeconds = 0.4f;
    [SerializeField] private float countSeconds = 0.8f;
    [SerializeField] private Color winColor = new Color32(238, 205, 122, 255);
    [SerializeField] private Color loseColor = new Color32(220, 70, 60, 255);

    private CanvasGroup group;
    private string shownNick;
    private int shownWins = -1;
    private int shownGames = -1;
    private Tween fade;
    private float fadeTarget;
    private Tween count;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
    }

    private void OnEnable()
    {
        PlayerProfile.Changed += HandleProfileChanged;
        Localization.LanguageChanged += Redraw;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Changed -= HandleProfileChanged;
        Localization.LanguageChanged -= Redraw;
        DOTween.Kill(this);
        fade = count = null;
        shownNick = null;
        if (group != null) group.alpha = 0f;   // 다시 켜질 때 페이드 인 + 카운트업
    }

    private void HandleProfileChanged(PlayerProfile p)
    {
        if (p == PlayerProfile.Local) Refresh();
    }

    private void Redraw()
    {
        shownNick = null;
        Refresh();
    }

    private void Refresh()
    {
        PlayerProfile me = PlayerProfile.Local;
        if (me == null || !me.IsLoggedIn)
        {
            FadeTo(0f);
            return;
        }

        FadeTo(1f);

        int targetWins = me.HasStats ? me.Wins : 0;
        int targetGames = me.HasStats ? me.GamesPlayed : 0;
        if (me.Nickname == shownNick && targetWins == shownWins && targetGames == shownGames) return;

        if (header != null) header.text = Localization.Get("YOUR RECORD");
        if (winRateLabel != null) winRateLabel.text = Localization.Get("WIN RATE");
        if (nickname != null) nickname.text = me.Nickname;
        shownNick = me.Nickname;

        if (!me.HasStats)
        {
            // 전적 로딩 전 · DB 없이 로그인(오프라인 테스트)
            shownWins = shownGames = 0;
            if (tier != null) tier.text = string.Empty;
            if (record != null) record.text = "-";
            if (winRate != null) winRate.text = "--";
            if (games != null) games.text = Localization.Get("NO RECORD");
            SetBar(0f);
            return;
        }

        // 이전 표시값 → 새 값으로 숫자 · 승률 막대가 올라가며 바뀜 (처음엔 0부터)
        int fromWins = Mathf.Max(0, shownWins), fromGames = Mathf.Max(0, shownGames);
        shownWins = targetWins;
        shownGames = targetGames;

        count?.Kill();
        count = DOVirtual.Float(0f, 1f, countSeconds, t =>
            {
                int w = Mathf.RoundToInt(Mathf.Lerp(fromWins, targetWins, t));
                int g = Mathf.RoundToInt(Mathf.Lerp(fromGames, targetGames, t));
                Draw(w, g, Mathf.Lerp(RecordFormat.Rate(fromWins, fromGames), RecordFormat.Rate(targetWins, targetGames), t));
            })
            .SetEase(Ease.OutCubic)
            .SetTarget(this)
            .OnComplete(() => Draw(targetWins, targetGames, RecordFormat.Rate(targetWins, targetGames)));
    }

    private void FadeTo(float alpha)
    {
        if (Mathf.Approximately(group.alpha, alpha) || (fade != null && Mathf.Approximately(fadeTarget, alpha))) return;
        fadeTarget = alpha;
        fade?.Kill();
        fade = group.DOFade(alpha, fadeSeconds).SetTarget(this).OnKill(() => fade = null);
    }

    private void Draw(int wins, int gamesPlayed, float barRate)
    {
        string w = ColorUtility.ToHtmlStringRGB(winColor);
        string l = ColorUtility.ToHtmlStringRGB(loseColor);

        if (record != null)
            record.text = $"<color=#{w}>{wins}</color> {Localization.Get("W")}   <color=#{l}>{gamesPlayed - wins}</color> {Localization.Get("L")}";
        if (winRate != null) winRate.text = RecordFormat.RateText(wins, gamesPlayed);
        if (games != null) games.text = $"{gamesPlayed} {Localization.Get("GAMES")}";
        if (tier != null) tier.text = RecordFormat.Tier(wins, gamesPlayed);
        SetBar(barRate);
    }

    private void SetBar(float rate)
    {
        if (barFill == null) return;
        Vector2 max = barFill.anchorMax;
        max.x = Mathf.Clamp01(rate);
        barFill.anchorMax = max;
    }
}
