using LiarsBar.Server;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인게임 HUD (원작 레이아웃)
// - 왼쪽 위: "QUEEN'S TABLE" + 초상 + 마지막 선언 (Waiting For First Claim / 이름 CLAIMS 2 x Queens / 이름 CALLED LIAR!)
// - 가운데: 다른 사람이 낸 선언 팝업
// - 내 차례: 위 가운데 타이머, 오른쪽 위 Your Bid + 조작 안내 / 내 차례 아닐 때: 오른쪽 아래 Look 안내
// - 결과: RETURNING TO LOBBY 카운트다운 + 승자 + 승자 전적 (이번 승리 반영: 13W 5L · WIN RATE 72.2%)
// 게임 로직은 건드리지 않고 InGameEvent만 듣는다. 화면 전환은 내 방 단계(PlayerProfile.Phase)를 따른다.
public class InGameHUD : MonoBehaviour
{
    [Header("Groups")]
    [SerializeField] private GameObject playingGroup;
    [SerializeField] private GameObject resultGroup;

    [Header("Table (왼쪽 위)")]
    [SerializeField] private TMP_Text tableTitle;

    /// <summary>"KING'S TABLE" 제목 글자. 다른 UI(DeckListUI 등)가 같은 폰트 · 머티리얼 · 스타일을 쓰려고 참조</summary>
    public TMP_Text TitleText => tableTitle;
    [SerializeField] private Image portrait;
    [Tooltip("CardType 순서: King, Queen, Ace, Joker. 비워 두면 기본 색 상자")]
    [SerializeField] private Sprite[] cardPortraits = new Sprite[4];
    [SerializeField] private TMP_Text claimName;
    [SerializeField] private TMP_Text claimVerb;
    [SerializeField] private TMP_Text claimValue;

    [Header("Claim popup (가운데)")]
    [SerializeField] private CanvasGroup popup;
    [SerializeField] private TMP_Text popupName;
    [SerializeField] private TMP_Text popupValue;
    [Tooltip("가운데에서 머무는 시간")]
    [SerializeField] private float popupHoldSeconds = 1.2f;
    [Tooltip("선언이 왼쪽 위 테이블 글자로 날아가며 사라지는 시간")]
    [SerializeField] private float popupFlySeconds = 0.5f;

    [Header("Turn")]
    [SerializeField] private GameObject myTurnGroup;     // Your Bid + 조작 안내
    [SerializeField] private GameObject timerGroup;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text chamberText;       // (n/6)
    [SerializeField] private GameObject idleHints;       // Look 안내

    [Header("Cylinder (남은 탄창)")]
    [Tooltip("탄창 그림. 비워 두면 My Turn Group 밑의 Cylinder 또는 Gun 이미지를 찾는다")]
    [SerializeField] private RectTransform cylinder;
    [Tooltip("탄창 안쪽 바탕 (roulette_fill). 탄창 테두리(roulette_line) 뒤에 깔린다")]
    [SerializeField] private Sprite cylinderFillSprite;
    [Tooltip("탄실에 올릴 ? 그림 (questionMark_closed). 비워 두면 글자 ?")]
    [SerializeField] private Sprite chamberMarkSprite;
    [Tooltip("탄실 6칸이 그림 중심에서 떨어진 거리 / ? 크기 (그려진 탄창 높이 대비). roulette_line 기준 0.28 / 0.36")]
    [SerializeField] private float chamberRadiusRatio = 0.28f;
    [SerializeField] private float chamberMarkRatio = 0.36f;

    [Header("Death (내가 총에 맞았을 때 화면 테두리 연출)")]
    [Tooltip("4.Sprite/UI/Die의 frame_00000~00049. 컴포넌트 ⋮ 메뉴 'Die 프레임 채우기'로 자동 연결")]
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private float deathFps = 30f;

    public Sprite[] DeathFrames => deathFrames;
    public float DeathFps => deathFps;

#if UNITY_EDITOR
    private const string DeathFramesFolder = "Assets/4.Sprite/UI/Die";

    // 50장 × 1920x1080이라 그대로 두면 메모리가 크다 → 1024 · 압축 · 밉맵 끔으로 바꾸고 이름순으로 연결
    [ContextMenu("Die 프레임 채우기 (+ 텍스처 1024 압축)")]
    private void FillDeathFrames()
    {
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { DeathFramesFolder }))
            paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal);

        var sprites = new System.Collections.Generic.List<Sprite>();
        foreach (string p in paths)
        {
            if (UnityEditor.AssetImporter.GetAtPath(p) is UnityEditor.TextureImporter ti)
            {
                bool dirty = ti.textureType != UnityEditor.TextureImporterType.Sprite || ti.maxTextureSize != 1024 || ti.mipmapEnabled
                             || ti.textureCompression == UnityEditor.TextureImporterCompression.Uncompressed;
                if (dirty)
                {
                    ti.textureType = UnityEditor.TextureImporterType.Sprite;
                    ti.spriteImportMode = UnityEditor.SpriteImportMode.Single;
                    ti.maxTextureSize = 1024;
                    ti.mipmapEnabled = false;
                    ti.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                    ti.SaveAndReimport();
                }
            }
            Sprite sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (sp != null) sprites.Add(sp);
        }
        UnityEditor.Undo.RecordObject(this, "Fill Death Frames");
        deathFrames = sprites.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[HUD] Die 프레임 {deathFrames.Length}장 연결 ({DeathFramesFolder})", this);
    }
#endif

    [Header("Result")]
    [SerializeField] private TMP_Text resultTimerText;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text winnerStatsText;   // 비워 두면 winnerText 아래에 자동 생성

    [Header("Colors")]
    [SerializeField] private Color kingColor = new Color(0.86f, 0.22f, 0.2f);
    [SerializeField] private Color queenColor = new Color(0.78f, 0.16f, 0.72f);
    [SerializeField] private Color aceColor = new Color(0.98f, 0.55f, 0.15f);
    [SerializeField] private Color jokerColor = new Color(0.4f, 0.75f, 0.4f);
    [SerializeField] private Color accentColor = new Color(0.93f, 0.82f, 0.45f);

    private const int Chambers = 6;

    private enum ClaimState { None, Waiting, Claim, Liar }

    private CardType tableCard;
    private bool hasTable;
    private ClaimState claimState;
    private int claimSeat = -1;
    private int claimCount;
    private int winnerSeat = -1;

    private float turnEndTime;
    private Sequence popupSeq;
    private Vector2 popupHome;
    private bool popupHomeSet;
    private float resultEndTime;
    private RoomPhase shownPhase = (RoomPhase)255;
    private GameObject[] chamberMarks;
    private int shownFired = -1;

    private void OnEnable()
    {
        InGameEvent.OnRoundStarted += HandleRoundStarted;
        InGameEvent.OnTurnChanged += HandleTurnChanged;
        InGameEvent.OnCardsSubmitted += HandleCardsSubmitted;
        InGameEvent.OnLiarCalled += HandleLiarCalled;
        InGameEvent.OnWinnerAnnounced += HandleWinner;
        Localization.LanguageChanged += RedrawTexts;

        shownPhase = (RoomPhase)255;
        if (popup != null) popup.alpha = 0f;
        SetMyTurn(false);
        RedrawTexts();
    }

    private void OnDisable()
    {
        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        InGameEvent.OnTurnChanged -= HandleTurnChanged;
        InGameEvent.OnCardsSubmitted -= HandleCardsSubmitted;
        InGameEvent.OnLiarCalled -= HandleLiarCalled;
        InGameEvent.OnWinnerAnnounced -= HandleWinner;
        Localization.LanguageChanged -= RedrawTexts;
        popupSeq?.Kill(true);
        popupSeq = null;
    }

    private void Update()
    {
        UpdatePhase();

        if (timerGroup != null && timerGroup.activeSelf && timerText != null)
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, turnEndTime - Time.time)).ToString();

        if (chamberText != null)
        {
            PlayerState me = PlayerState.Local;
            chamberText.text = me != null ? $"({me.currentChamber}/{Chambers})" : string.Empty;
        }
        UpdateCylinder();

        if (resultGroup != null && resultGroup.activeSelf && resultTimerText != null)
            resultTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, resultEndTime - Time.time)).ToString();
    }

    // 탄창 그림의 ?: 남은 칸만 표시 (1/6이면 6칸 중 1칸이 빈칸). 위에서부터 시계 방향으로 비운다
    private void UpdateCylinder()
    {
        PlayerState me = PlayerState.Local;
        int fired = me != null ? Mathf.Clamp(me.currentChamber, 0, Chambers) : 0;
        if (!EnsureChamberMarks() || fired == shownFired) return;

        shownFired = fired;
        for (int i = 0; i < chamberMarks.Length; i++)
            if (chamberMarks[i] != null) chamberMarks[i].SetActive(i >= fired);
    }

    // 탄창 그림(roulette_line: 빈 탄실 테두리) 위에 ? 6개를 코드로 올린다
    private bool EnsureChamberMarks()
    {
        if (chamberMarks != null) return true;

        if (cylinder == null && myTurnGroup != null)
        {
            Transform t = myTurnGroup.transform.Find("Cylinder");
            if (t == null) t = myTurnGroup.transform.Find("Gun");
            cylinder = t as RectTransform;
        }
        if (cylinder == null) return false;

        float height = DrawnHeight(cylinder);
        if (height <= 0f) return false;

        CreateCylinderFill();

        chamberMarks = new GameObject[Chambers];
        for (int i = 0; i < Chambers; i++)
        {
            var go = new GameObject($"Mark_{i}", typeof(RectTransform));
            go.layer = cylinder.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(cylinder, false);

            float angle = (90f - i * 60f) * Mathf.Deg2Rad;   // 맨 위부터 시계 방향
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * height * chamberRadiusRatio;
            rt.sizeDelta = Vector2.one * height * chamberMarkRatio;

            if (chamberMarkSprite != null)
            {
                var image = go.AddComponent<Image>();
                image.sprite = chamberMarkSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            else
            {
                var mark = go.AddComponent<TextMeshProUGUI>();
                if (chamberText != null) mark.font = chamberText.font;
                mark.text = "?";
                mark.fontSize = height * chamberMarkRatio * 0.6f;
                mark.fontStyle = FontStyles.Bold;
                mark.color = accentColor;
                mark.alignment = TextAlignmentOptions.Center;
                mark.textWrappingMode = TextWrappingModes.NoWrap;
                mark.raycastTarget = false;
            }
            chamberMarks[i] = go;
        }
        shownFired = -1;
        return true;
    }

    // 테두리 그림 바로 뒤(앞 형제)에 같은 크기의 바탕을 깐다 → 남색 바탕 + 노란 테두리
    private void CreateCylinderFill()
    {
        if (cylinderFillSprite == null || cylinder.parent == null) return;

        var go = new GameObject("CylinderFill", typeof(RectTransform));
        go.layer = cylinder.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(cylinder.parent, false);
        rt.SetSiblingIndex(cylinder.GetSiblingIndex());   // 탄창보다 먼저 그려지게
        rt.anchorMin = cylinder.anchorMin;
        rt.anchorMax = cylinder.anchorMax;
        rt.pivot = cylinder.pivot;
        rt.anchoredPosition = cylinder.anchoredPosition;
        rt.sizeDelta = cylinder.sizeDelta;
        rt.localScale = cylinder.localScale;
        rt.localRotation = cylinder.localRotation;

        var image = go.AddComponent<Image>();
        image.sprite = cylinderFillSprite;
        image.preserveAspect = !cylinder.TryGetComponent(out Image line) || line.preserveAspect;
        image.raycastTarget = false;
    }

    // 탄창 이미지가 비율 유지(Preserve Aspect)로 실제 그려지는 높이
    private static float DrawnHeight(RectTransform rt)
    {
        Rect r = rt.rect;
        if (rt.TryGetComponent(out Image image) && image.sprite != null && image.preserveAspect)
        {
            Vector2 s = image.sprite.rect.size;
            if (s.x > 0f && s.y > 0f) return Mathf.Min(r.height, r.width * s.y / s.x);
        }
        return Mathf.Min(r.width, r.height);
    }

    // 테이블 씬이 이벤트보다 늦게 올라올 수 있어서, 화면 전환은 방 단계로 판단
    private void UpdatePhase()
    {
        PlayerProfile me = PlayerProfile.Local;
        RoomPhase phase = me != null ? me.Phase : RoomPhase.Lobby;
        if (phase == shownPhase) return;

        shownPhase = phase;
        if (playingGroup != null) playingGroup.SetActive(phase == RoomPhase.Playing);
        if (resultGroup != null) resultGroup.SetActive(phase == RoomPhase.Result);

        if (phase == RoomPhase.Result)
        {
            LiarsNetworkManager manager = LiarsNetworkManager.Instance;
            float seconds = manager != null && manager.Config != null ? manager.Config.ResultSeconds : 10f;
            resultEndTime = Time.time + seconds;
            SetMyTurn(false);
            RedrawTexts();
        }
    }

    // ---------------------------------------------------------------- 이벤트

    private void HandleRoundStarted(CardType card)
    {
        tableCard = card;
        hasTable = true;
        claimState = ClaimState.Waiting;
        SetMyTurn(false);

        if (portrait != null)
        {
            int index = (int)card;
            Sprite sprite = cardPortraits != null && index < cardPortraits.Length ? cardPortraits[index] : null;
            portrait.sprite = sprite;
            portrait.color = sprite != null ? Color.white : CardColor(card) * 0.6f + new Color(0f, 0f, 0f, 0.4f);
        }
        RedrawTexts();
    }

    private void HandleTurnChanged(int seat)
    {
        bool mine = seat == MySeat;
        SetMyTurn(mine);
        if (mine) turnEndTime = Time.time + GameFlowTiming.TurnTimer;
    }

    private void HandleCardsSubmitted(int seat, int count, int[] indices)
    {
        claimState = ClaimState.Claim;
        claimSeat = seat;
        claimCount = count;
        RedrawTexts();

        ShowPopup(NameOf(seat), ClaimText(count), true);   // 내가 낸 것도 표시 (원작처럼 가운데 → 테이블 글자 쪽으로)
    }

    private void HandleLiarCalled(int callerSeat, int loserSeat)
    {
        claimState = ClaimState.Liar;
        claimSeat = callerSeat;
        SetMyTurn(false);
        RedrawTexts();
    }

    private void HandleWinner(int seat)
    {
        winnerSeat = seat;
        RedrawTexts();

        TMP_Text stats = EnsureWinnerStats();
        if (stats != null && stats.text.Length > 0)
        {
            stats.transform.DOKill(true);
            stats.transform.localScale = Vector3.one;
            stats.transform.DOPunchScale(Vector3.one * 0.12f, 0.5f, 6).SetDelay(0.6f);
        }
    }

    // ---------------------------------------------------------------- 표시

    private void SetMyTurn(bool mine)
    {
        if (myTurnGroup != null) myTurnGroup.SetActive(mine);
        if (timerGroup != null) timerGroup.SetActive(mine);
        if (idleHints != null) idleHints.SetActive(!mine);
    }

    // 가운데에서 커지며 나타났다가, 선언이면 왼쪽 위 테이블 글자 쪽으로 작아지며 날아가 사라진다 (DOTween)
    private void ShowPopup(string name, string value, bool flyToTable)
    {
        if (popup == null) return;

        RectTransform rt = (RectTransform)popup.transform;
        popupSeq?.Kill(true);   // 이전 연출은 끝 상태로 정리 (원위치 · 크기 복구)
        if (!popupHomeSet)
        {
            popupHome = rt.anchoredPosition;
            popupHomeSet = true;
        }

        if (popupName != null) popupName.text = name;
        if (popupValue != null) popupValue.text = value;

        rt.anchoredPosition = popupHome;
        rt.localScale = Vector3.one * 0.7f;
        popup.alpha = 0f;

        popupSeq = DOTween.Sequence()
            .Append(popup.DOFade(1f, 0.2f))
            .Join(rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
            .AppendInterval(popupHoldSeconds);

        if (flyToTable && TryGetTableTarget(rt, out Vector2 target))
        {
            popupSeq.Append(rt.DOAnchorPos(target, popupFlySeconds).SetEase(Ease.InQuad))
                    .Join(rt.DOScale(0.3f, popupFlySeconds).SetEase(Ease.InQuad))
                    .Join(popup.DOFade(0f, popupFlySeconds).SetEase(Ease.InQuad));
        }
        else
        {
            popupSeq.Append(popup.DOFade(0f, 0.3f));
        }

        popupSeq.OnComplete(() =>
        {
            popup.alpha = 0f;
            rt.anchoredPosition = popupHome;
            rt.localScale = Vector3.one;
        });
    }

    // 왼쪽 위 선언 글자(없으면 테이블 제목)의 위치를 팝업 기준 좌표로
    private bool TryGetTableTarget(RectTransform rt, out Vector2 target)
    {
        target = default;
        Graphic goal = claimValue != null ? claimValue : tableTitle;
        if (goal == null || !(rt.parent is RectTransform parent)) return false;

        Vector2 local = parent.InverseTransformPoint(goal.rectTransform.position);
        Rect r = parent.rect;
        Vector2 anchorPoint = r.min + Vector2.Scale(r.size, rt.anchorMin);
        target = local - anchorPoint;
        return true;
    }

    // 언어가 바뀌어도 다시 그릴 수 있게 상태에서 글자를 만든다
    private void RedrawTexts()
    {
        if (tableTitle != null)
        {
            if (!hasTable) tableTitle.text = string.Empty;
            else
            {
                string full = Localization.Get($"{tableCard.ToString().ToUpper()}'S TABLE");
                int space = full.IndexOf(' ');
                string hex = ColorUtility.ToHtmlStringRGB(CardColor(tableCard));
                tableTitle.text = space > 0 ? $"<color=#{hex}>{full.Substring(0, space)}</color>{full.Substring(space)}" : full;
            }
        }

        switch (claimState)
        {
            case ClaimState.Waiting:
                SetClaim(Localization.Get("Waiting For"), string.Empty, Localization.Get("First Claim"));
                break;
            case ClaimState.Claim:
                SetClaim(NameOf(claimSeat), Localization.Get("CLAIMS"), ClaimText(claimCount));
                break;
            case ClaimState.Liar:
                string hex = ColorUtility.ToHtmlStringRGB(accentColor);
                SetClaim(NameOf(claimSeat), Localization.Get("CALLED"), $"<color=#{hex}>{Localization.Get("LIAR!")}</color>");
                break;
            default:
                SetClaim(string.Empty, string.Empty, string.Empty);
                break;
        }

        if (winnerText != null) winnerText.text = winnerSeat >= 0 ? NameOf(winnerSeat) : string.Empty;

        TMP_Text stats = EnsureWinnerStats();
        if (stats != null) stats.text = winnerSeat >= 0 ? WinnerRecord(winnerSeat) : string.Empty;
    }

    // 게임 시작 때 찍어 둔 전적(PrevWins · PrevGamesPlayed) + 이번 승리
    // DB 저장 · 재조회는 결과 화면 중에 늦게 도착하므로 기다리지 않고 바로 계산한다. DB 없는 계정이면 빈칸
    private string WinnerRecord(int seat)
    {
        PlayerProfile p = FindProfile(seat);
        if (p == null || p.PrevGamesPlayed < 0) return string.Empty;

        int wins = p.PrevWins + 1;
        int games = p.PrevGamesPlayed + 1;
        string hex = ColorUtility.ToHtmlStringRGB(accentColor);
        return $"<color=#{hex}>{RecordFormat.Short(wins, games)}</color>   ·   {Localization.Get("WIN RATE")} <color=#{hex}>{RecordFormat.RateText(wins, games)}</color>";
    }

    private TMP_Text EnsureWinnerStats()
    {
        if (winnerStatsText != null || winnerText == null) return winnerStatsText;

        winnerStatsText = Instantiate(winnerText, winnerText.transform.parent);
        winnerStatsText.name = "[Gen] WinnerStats";
        if (winnerStatsText.TryGetComponent(out LocalizedText localized)) Destroy(localized);
        for (int i = winnerStatsText.transform.childCount - 1; i >= 0; i--)
            Destroy(winnerStatsText.transform.GetChild(i).gameObject);

        RectTransform rt = winnerStatsText.rectTransform;
        rt.anchoredPosition = winnerText.rectTransform.anchoredPosition - new Vector2(0f, winnerText.fontSize * 1.1f);
        winnerStatsText.enableAutoSizing = false;
        winnerStatsText.fontSize = winnerText.fontSize * 0.42f;
        winnerStatsText.fontStyle = FontStyles.Normal;
        winnerStatsText.color = new Color(0.85f, 0.8f, 0.72f);
        winnerStatsText.text = string.Empty;
        return winnerStatsText;
    }

    private void SetClaim(string name, string verb, string value)
    {
        if (claimName != null) claimName.text = name;
        if (claimVerb != null) claimVerb.text = verb;
        if (claimValue != null) claimValue.text = value;
    }

    // "2 x Kings"
    private string ClaimText(int count)
    {
        string card = tableCard.ToString();
        string word = Localization.Get(count > 1 ? card + "s" : card);
        string hex = ColorUtility.ToHtmlStringRGB(accentColor);
        return $"{count} <color=#{hex}>x</color> {word}";
    }

    private Color CardColor(CardType card) => card switch
    {
        CardType.King => kingColor,
        CardType.Queen => queenColor,
        CardType.Ace => aceColor,
        _ => jokerColor,
    };

    // PlayerState.SeatNum은 서버에서만 채워지므로 내 좌석은 PlayerProfile에서 읽는다
    private static int MySeat => PlayerProfile.Local != null ? PlayerProfile.Local.Seat : -1;

    private static string NameOf(int seat)
    {
        PlayerProfile p = FindProfile(seat);
        return p != null ? p.Nickname : $"{Localization.Get("Player")} {seat + 1}";
    }

    private static PlayerProfile FindProfile(int seat)
    {
        PlayerProfile me = PlayerProfile.Local;
        foreach (PlayerProfile p in PlayerProfile.All)
        {
            if (p == null || !p.IsInRoom || p.Seat != seat) continue;
            if (me != null && p.RoomId != me.RoomId) continue;
            return p;
        }
        return null;
    }
}
