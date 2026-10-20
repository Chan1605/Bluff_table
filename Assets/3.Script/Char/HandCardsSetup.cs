using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiarsBar.Server;
using LiarsBar.Presentation;
#if UNITY_EDITOR
using UnityEditor;
#endif

// DeckGameManager 프리팹(PlayerVisualSetup 옆)에 붙인다.
// 손패 연출 지휘: 라운드 시작 배분 → 캐릭터가 집어 듦 → 내 패 앞면 · 선택 시 들기 → 제출 시 테이블로.
// 카드 한 장 · 손패의 동작은 민규님 CardView · HandView가 하고, 여기서는 팀 이벤트(InGameEvent)를 그쪽 호출로 바꾼다.
public class HandCardsSetup : MonoBehaviour
{
    [Header("카드 (컴포넌트를 붙이면 자동으로 채워짐 · 우클릭 메뉴로 다시 채우기)")]
    [SerializeField] GameObject cardVisualPrefab;
    [SerializeField] RuntimeAnimatorController cardController;
    [SerializeField] Material shownMat;
    [SerializeField] Material hiddenMat;
    [Tooltip("CardType 순서: King, Queen, Ace, Joker")]
    [SerializeField] Mesh[] typeMeshes = new Mesh[4];

    [Header("손 (플레이 중 바로 반영)")]
    [Tooltip("날아가는 카드의 도착점 보정 (kart 기준, m). 손에 닿으면 모델의 kart로 바뀐다")]
    [SerializeField] Vector3 slotPositionOffset;
    [SerializeField] Vector3 slotEulerOffset;
    [SerializeField] float cardScale = 1f;
    [Tooltip("선택한 카드를 들어 올리는 방향 (kart 로컬 축)과 거리 (m)")]
    [SerializeField] Vector3 liftAxis = Vector3.up;
    [SerializeField] float liftDistance = 0.02f;
    [Tooltip("지금 가리키는 카드 테두리 (로비 캐릭터 아웃라인과 같은 색). 비워 두면 Resources/CardOutline 셰이더로 만든다")]
    [SerializeField] Material cardOutlineMaterial;
    [SerializeField] Color cardOutlineColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("테두리 두께 (m)")]
    [SerializeField] float cardOutlineWidth = 0.008f;
    [Tooltip("고른 카드에 덮는 금색 (알파 = 진하기). 비워 두면 Resources/CardSelected 셰이더로 만든다")]
    [SerializeField] Material cardSelectedMaterial;
    [SerializeField] Color cardSelectedColor = new Color(1f, 0.82f, 0.3f, 0.6f);
    [Tooltip("손 카드 배치(KartsParent/Kartlar · kart1~7) 기준 캐릭터들. 캐릭터와 같은 Animator Controller를 쓰는 기준의 배치를 복사한다 " +
             "(예: Sit_Table → Cupcake, 뚱뚱이용 컨트롤러 → Bristle). 맞는 기준이 없으면 모델 원래 배치")]
    [SerializeField] GameObject[] handLayoutReferences;

    [Header("테이블")]
    [Tooltip("Table 씬에 이 이름의 오브젝트가 있으면 덱 위치로 쓴다. 없으면 의자들의 중심 + 높이")]
    [SerializeField] string deckPointName = "DeckPoint";
    [SerializeField] float fallbackDeckHeight = 0.8f;
    [SerializeField] int pileCapacity = 24;
    [SerializeField] float pileRadius = 0.12f;
    [SerializeField] float pileStackHeight = 0.002f;

    [Header("배분")]
    [Tooltip("배분 시작 전 대기. 직전 라운드 사망 SyncVar가 도착할 시간")]
    [SerializeField] float dealStartDelay = 0.2f;
    [Tooltip("테이블 연출(CardPlacer)을 기다리는 최대 시간")]
    [SerializeField] float introTimeout = 15f;

    public GameObject CardVisualPrefab => cardVisualPrefab;
    public RuntimeAnimatorController CardController => cardController;
    public Material ShownMat => shownMat;
    public Material HiddenMat => hiddenMat;
    public Vector3 SlotPositionOffset => slotPositionOffset;
    public Vector3 SlotEulerOffset => slotEulerOffset;
    public float CardScale => cardScale;
    public Vector3 LiftAxis => liftAxis;
    public float LiftDistance => liftDistance;

    /// <summary>카드 테두리 재질 (없으면 셰이더에서 한 번 만든다). 플레이 중 Inspector에서 색 · 두께를 바꾸면 바로 반영</summary>
    public Material CardOutlineMaterial
    {
        get
        {
            if (cardOutlineMaterial == null)
            {
                Shader shader = Shader.Find("LiarsBar/CardOutline");
                if (shader == null) return null;
                cardOutlineMaterial = new Material(shader) { name = "CardOutline (runtime)" };
            }
            cardOutlineMaterial.SetColor(OutlineColorId, cardOutlineColor);
            cardOutlineMaterial.SetFloat(OutlineWidthId, cardOutlineWidth);
            return cardOutlineMaterial;
        }
    }
    /// <summary>고른 카드 금색 칠 재질 (플레이 중 Inspector에서 색을 바꾸면 바로 반영)</summary>
    public Material CardSelectedMaterial
    {
        get
        {
            if (cardSelectedMaterial == null)
            {
                Shader shader = Shader.Find("LiarsBar/CardSelected");
                if (shader == null) return null;
                cardSelectedMaterial = new Material(shader) { name = "CardSelected (runtime)" };
            }
            cardSelectedMaterial.SetColor(SelectedColorId, cardSelectedColor);
            return cardSelectedMaterial;
        }
    }
    static readonly int SelectedColorId = Shader.PropertyToID("_Color");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    /// <summary>같은 Animator Controller를 쓰는 기준 캐릭터 (없으면 null = 모델 원래 배치)</summary>
    public GameObject GetHandLayoutReference(RuntimeAnimatorController controller)
    {
        if (handLayoutReferences == null || controller == null) return null;
        foreach (var r in handLayoutReferences)
            if (r != null && r.TryGetComponent(out Animator a) && a.runtimeAnimatorController == controller) return r;
        return null;
    }

    [System.Serializable]
    public struct HandRotationFix
    {
        [Tooltip("이 컨트롤러를 쓰는 캐릭터에 적용")]
        public RuntimeAnimatorController controller;
        [Tooltip("손 카드 묶음(Kartlar)에 더할 로컬 회전. 패가 상대 쪽을 보면 한 축에 180을 넣어 본다 (플레이 중 바로 반영)")]
        public Vector3 kartlarEuler;
    }

    [Tooltip("컨트롤러별 손 카드 묶음 회전 보정 (예: Sit_Table_Big → 패 뒤집기)")]
    [SerializeField] HandRotationFix[] handRotationFixes;

    [System.Serializable]
    public struct HandCardMeshSet
    {
        [Tooltip("이 컨트롤러를 쓰는 캐릭터에 적용")]
        public RuntimeAnimatorController controller;
        [Tooltip("손에 든 카드 앞면 메쉬. 순서: King, Queen, Ace, Joker")]
        public Mesh[] typeMeshes;
    }

    [Tooltip("컨트롤러별 손 카드 앞면 메쉬 (원작 모델은 원작 카드 메쉬 clubs13 · clubs12 · clubs01 · clubs11). 없으면 Type Meshes 사용")]
    [SerializeField] HandCardMeshSet[] handCardMeshSets;

    public Mesh GetHandTypeMesh(RuntimeAnimatorController controller, int type)
    {
        if (handCardMeshSets != null && controller != null)
            foreach (var set in handCardMeshSets)
                if (UsesController(controller, set.controller) && set.typeMeshes != null &&
                    type >= 0 && type < set.typeMeshes.Length && set.typeMeshes[type] != null)
                    return set.typeMeshes[type];
        return GetTypeMesh(type);
    }

    // Override 컨트롤러면 원본(Base)까지 따라 올라가며 비교한다 → 원본 하나만 등록하면 그 Override를 쓰는 캐릭터 전부에 적용
    static bool UsesController(RuntimeAnimatorController controller, RuntimeAnimatorController target)
    {
        for (var c = controller; c != null; c = (c as AnimatorOverrideController)?.runtimeAnimatorController)
            if (c == target) return true;
        return false;
    }

    public Vector3 GetKartlarEuler(RuntimeAnimatorController controller)
    {
        if (handRotationFixes == null || controller == null) return Vector3.zero;
        foreach (var f in handRotationFixes) if (f.controller == controller) return f.kartlarEuler;
        return Vector3.zero;
    }

    readonly Dictionary<int, SeatHandCards> seats = new();
    readonly HashSet<int> eliminated = new();
    readonly List<Transform> pileSlots = new();
    GameObject tableRoot;
    Transform deckPoint;
    int pileCount;
    bool awaitingDealFaces;
    Coroutine roundRoutine;

    void OnEnable()
    {
        InGameEvent.OnEntryInGame += HandleEntryInGame;
        InGameEvent.OnReEntryInLobby += HandleReEntryInLobby;
        InGameEvent.OnRoundStarted += HandleRoundStarted;
        InGameEvent.OnCardDealt += HandleCardDealt;
        InGameEvent.OnCardsSubmitted += HandleCardsSubmitted;
        InGameEvent.OnHandSelectionChanged += HandleSelectionChanged;
        InGameEvent.OnHandCursorChanged += HandleCursorChanged;
        InGameEvent.OnTurnChanged += HandleTurnChanged;
        InGameEvent.OnPlayerEliminated += HandlePlayerEliminated;
        InGameEvent.OnLookRequested += HandleLookRequested;
        InGameEvent.OnCardsOpened += HandleCardsOpened;
        InGameEvent.OnEmotePlayed += HandleEmotePlayed;
    }

    void OnDisable()
    {
        InGameEvent.OnEntryInGame -= HandleEntryInGame;
        InGameEvent.OnReEntryInLobby -= HandleReEntryInLobby;
        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        InGameEvent.OnCardDealt -= HandleCardDealt;
        InGameEvent.OnCardsSubmitted -= HandleCardsSubmitted;
        InGameEvent.OnHandSelectionChanged -= HandleSelectionChanged;
        InGameEvent.OnHandCursorChanged -= HandleCursorChanged;
        InGameEvent.OnTurnChanged -= HandleTurnChanged;
        InGameEvent.OnPlayerEliminated -= HandlePlayerEliminated;
        InGameEvent.OnLookRequested -= HandleLookRequested;
        InGameEvent.OnCardsOpened -= HandleCardsOpened;
        InGameEvent.OnEmotePlayed -= HandleEmotePlayed;
    }

    void OnDestroy()
    {
        if (tableRoot != null) Destroy(tableRoot);
    }

    // ───── PlayerVisualSetup이 캐릭터 생성 직후 호출 ─────

    public void Attach(PlayerProfile player, GameObject visual)
    {
        if (player == null || visual == null) return;
        if (cardVisualPrefab == null || cardController == null)
        {
            Debug.LogError("[손패] HandCardsSetup에 카드 프리팹 · Card.controller가 비어 있음", this);
            return;
        }

        var hand = visual.AddComponent<SeatHandCards>();
        if (!hand.Build(this, player.Seat, player.isLocalPlayer, player.GetComponent<PlayerState>()))
        {
            Destroy(hand);
            return;
        }
        seats[player.Seat] = hand;
    }

    public void Unregister(SeatHandCards hand)
    {
        // 같은 좌석에 새 캐릭터가 먼저 등록됐을 수 있으니 자기 자신일 때만 뺀다
        if (seats.TryGetValue(hand.Seat, out var current) && current == hand) seats.Remove(hand.Seat);
    }

    public Mesh GetTypeMesh(int type) =>
        typeMeshes != null && type >= 0 && type < typeMeshes.Length ? typeMeshes[type] : null;

    public Transform NextPileSlot()
    {
        EnsureTableRoot();
        Transform slot = pileSlots[Mathf.Min(pileCount, pileSlots.Count - 1)];
        pileCount++;
        return slot;
    }

    // ───── 이벤트 ─────

    void HandleEntryInGame() { myTurn = false; openOnTurn = false; ResetAll(); }
    void HandleReEntryInLobby() => ResetAll();

    void ResetAll()
    {
        if (roundRoutine != null) StopCoroutine(roundRoutine);
        roundRoutine = null;
        awaitingDealFaces = false;
        eliminated.Clear();
        pileCount = 0;
        foreach (var s in seats.Values)
        {
            if (s == null) continue;
            s.Hand.HideAll();
            s.RefreshFaces();
            s.SetHasCards(false);
        }
    }

    void HandleRoundStarted(CardType tableCard)
    {
        // 라운드 시작 직후 오는 첫 OnCardDealt = 새 손패. 그 뒤로 오는 것 = 제출 후 남은 패
        awaitingDealFaces = true;
        if (roundRoutine != null) StopCoroutine(roundRoutine);
        roundRoutine = StartCoroutine(RoundIntro());
    }

    IEnumerator RoundIntro()
    {
        Transform deck = ResolveDeckPoint();

        // 1) 손 · 테이블에 남은 카드는 바로 숨긴다
        foreach (var s in seats.Values)
        {
            if (s == null) continue;
            s.Hand.HideAll();
            s.RefreshFaces();
            s.SetHasCards(false);
            s.ResetRound();
        }
        pileCount = 0;
        yield return new WaitForSeconds(dealStartDelay);

        // 2) 테이블 연출(CardPlacer: 20장 공개 · 테이블 카드 선언)이 있으면 끝날 때까지 기다린 뒤 그 덱에서 나눈다
        CardPlacer placer = FindFirstObjectByType<CardPlacer>();
        if (placer != null)
        {
            float giveUp = Time.time + introTimeout;
            while (placer != null && placer.IsIntroPlaying && Time.time < giveUp) yield return null;
            if (placer != null && placer.DeckPoint != null)
                deck.SetPositionAndRotation(placer.DeckPoint.position, placer.DeckPoint.rotation);
        }

        // 3) 살아 있는 좌석을 돌며 한 장씩 (비행 곡선은 Card.controller의 Deal 클립)
        List<SeatHandCards> alive = AliveSeatsInOrder();
        var stagger = new WaitForSeconds(PresentationTimings.CardDealStagger);
        for (int i = 0; i < HandView.HandSize; i++)
        {
            foreach (var s in alive)
            {
                if (s == null) continue;
                if (placer != null) placer.PopDeckCard();
                s.DealOne(i, deck);
                s.RefreshFaces();
                yield return stagger;
            }
        }

        // 4) 마지막 카드가 손에 닿으면 캐릭터가 패를 집어 듦 (kart_acma)
        yield return new WaitForSeconds(PresentationTimings.CardDealFlight);
        foreach (var s in alive) if (s != null) s.OnDealFinished();
        roundRoutine = null;

        // 서버에 배분 연출이 끝났다고 알림 → 모두 끝나면 첫 턴 · 타이머 시작
        if (PlayerState.Local != null) PlayerState.Local.CmdDealFinished();
    }

    void HandleCardDealt(List<CardType> hand)
    {
        SeatHandCards me = LocalSeat();
        if (me == null || hand == null) return;

        if (awaitingDealFaces)
        {
            awaitingDealFaces = false;
            var ranks = new CardRank[hand.Count];
            for (int i = 0; i < ranks.Length; i++) ranks[i] = (CardRank)hand[i];
            me.Hand.SetLocalFaces(ranks);   // 배분 전이면 보관했다가 DealOne에서 입힘
            me.RefreshFaces();
        }
        else
        {
            me.ResyncLocal(hand);
        }
    }

    void HandleCardsSubmitted(int seat, int count, int[] listIndices)
    {
        if (seats.TryGetValue(seat, out var s) && s != null) s.PlayCards(count, listIndices, this);
    }

    void HandleSelectionChanged(IReadOnlyList<int> listIndices)
    {
        SeatHandCards me = LocalSeat();
        if (me != null) me.SetLifted(me.ListToSlots(listIndices));
    }

    void HandleCursorChanged(int listIndex)
    {
        SeatHandCards me = LocalSeat();
        if (me == null) return;
        List<int> slots = listIndex >= 0 ? me.ListToSlots(new[] { listIndex }) : null;
        me.SetCursor(slots != null && slots.Count > 0 ? slots[0] : -1);
    }

    // 원작처럼: 내 차례가 되면 패를 자동으로 펼치고(배분이 끝난 뒤), 차례가 끝나면(내고 나면) 다시 덮는다.
    // 내 차례 동안은 스페이스로 덮을 수 없다 → 덮은 채로 내는 경우가 없다
    bool openOnTurn;
    bool myTurn;

    void HandleTurnChanged(int seat)
    {
        bool mine = PlayerProfile.Local != null && seat == PlayerProfile.Local.Seat;
        bool wasMine = myTurn;
        myTurn = mine;
        openOnTurn = mine;
        if (mine || !wasMine) return;

        SeatHandCards me = LocalSeat();
        if (me != null && me.IsOpen && PlayerState.Local != null) PlayerState.Local.CmdSetCardsOpen(false);
    }

    void Update()
    {
        UpdateOpenOnTurn();
    }

    void UpdateOpenOnTurn()
    {
        if (!openOnTurn) return;
        SeatHandCards me = LocalSeat();
        if (me == null || PlayerState.Local == null) { openOnTurn = false; return; }
        if (!me.CanToggle) return;   // 배분이 끝날 때까지 기다림

        openOnTurn = false;
        if (me.IsOpen) return;
        PlayerState.Local.CmdSetCardsOpen(true);
    }


    // 스페이스: 패를 다 받았으면 펼치기 ↔ 덮기 토글을 서버에 알린다 → 모든 클라이언트에서 애니메이션
    void HandleLookRequested()
    {
        if (myTurn) return;   // 내 차례에는 펼친 상태 고정
        SeatHandCards me = LocalSeat();
        if (me == null || !me.CanToggle || PlayerState.Local == null) return;
        PlayerState.Local.CmdSetCardsOpen(!me.IsOpen);
    }

    void HandleCardsOpened(int seat, bool open)
    {
        if (seats.TryGetValue(seat, out var s) && s != null) s.SetCardsOpen(open);
    }

    void HandleEmotePlayed(int seat, int emote)
    {
        if (seats.TryGetValue(seat, out var s) && s != null) s.PlayEmote(emote);
    }

    void HandlePlayerEliminated(int seat)
    {
        eliminated.Add(seat);
        if (seats.TryGetValue(seat, out var s) && s != null) s.SetHasCards(false);
    }

    // ───── 내부 ─────

    SeatHandCards LocalSeat()
    {
        foreach (var s in seats.Values) if (s != null && s.IsLocal) return s;
        return null;
    }

    List<SeatHandCards> AliveSeatsInOrder()
    {
        // 테이블 시계 방향 순서로 (턴 순서와 같게)
        var keys = new List<int>();
        foreach (int seat in SeatOrder.Clockwise(4)) if (seats.ContainsKey(seat)) keys.Add(seat);
        foreach (int seat in seats.Keys) if (!keys.Contains(seat)) keys.Add(seat);
        var list = new List<SeatHandCards>();
        foreach (int k in keys)
        {
            SeatHandCards s = seats[k];
            if (s != null && !s.IsDead && !eliminated.Contains(k)) list.Add(s);
        }
        return list;
    }

    Transform ResolveDeckPoint()
    {
        EnsureTableRoot();

        GameObject named = string.IsNullOrEmpty(deckPointName) ? null : GameObject.Find(deckPointName);
        if (named != null)
        {
            deckPoint.SetPositionAndRotation(named.transform.position, named.transform.rotation);
            return deckPoint;
        }

        // 대체: 테이블 의자들의 중심 + 높이
        Vector3 sum = Vector3.zero;
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            Transform chair = SeatAnchors.Get(SeatArea.Table, i);
            if (chair == null) continue;
            sum += chair.position;
            n++;
        }
        Vector3 center = n > 0 ? sum / n : Vector3.zero;
        deckPoint.SetPositionAndRotation(center + Vector3.up * fallbackDeckHeight, Quaternion.identity);
        return deckPoint;
    }

    void EnsureTableRoot()
    {
        if (tableRoot != null) return;

        tableRoot = new GameObject("HandCards (table)");
        DontDestroyOnLoad(tableRoot);
        deckPoint = new GameObject("DeckPoint (runtime)").transform;
        deckPoint.SetParent(tableRoot.transform, false);

        // 중앙에 뒷면으로 쌓이는 자리 (TableView와 같은 방식)
        pileSlots.Clear();
        var rng = new System.Random(7);
        for (int i = 0; i < Mathf.Max(1, pileCapacity); i++)
        {
            var t = new GameObject($"PileSlot_{i:00}").transform;
            t.SetParent(deckPoint, false);
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = Mathf.Sqrt((float)rng.NextDouble()) * pileRadius;
            t.localPosition = new Vector3(Mathf.Cos(a) * r, i * pileStackHeight, Mathf.Sin(a) * r);
            t.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            pileSlots.Add(t);
        }
    }

#if UNITY_EDITOR
    const string CardPrefabPath = "Assets/2.Model/Prefabs/Card.prefab";
    const string HandLayoutPath = "Assets/2.Model/Prefabs/Characters/Sit Model/Cupcake.prefab";
    const string CardControllerPath = "Assets/LiarsBar/Presentation/Animators/Card.controller";

    void Reset() => AutoFill();

    // Card.prefab(CardViewer)에 들어 있는 재질 · 메쉬를 그대로 가져온다. 비어 있는 칸만 채운다
    [ContextMenu("카드 에셋 자동 채우기")]
    void AutoFill()
    {
        if (cardVisualPrefab == null) cardVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardController == null) cardController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CardControllerPath);
        if (handLayoutReferences == null || handLayoutReferences.Length == 0)
            handLayoutReferences = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(HandLayoutPath) };

        if (cardVisualPrefab != null && cardVisualPrefab.TryGetComponent(out CardViewer viewer))
        {
            var so = new SerializedObject(viewer);
            if (shownMat == null) shownMat = so.FindProperty("ShownMat")?.objectReferenceValue as Material;
            if (hiddenMat == null) hiddenMat = so.FindProperty("hiddenMat")?.objectReferenceValue as Material;

            SerializedProperty meshes = so.FindProperty("typeMeshes");
            if (typeMeshes == null || typeMeshes.Length != 4) typeMeshes = new Mesh[4];
            for (int i = 0; meshes != null && i < 4 && i < meshes.arraySize; i++)
                if (typeMeshes[i] == null) typeMeshes[i] = meshes.GetArrayElementAtIndex(i).objectReferenceValue as Mesh;
        }
        EditorUtility.SetDirty(this);
    }
#endif
}
