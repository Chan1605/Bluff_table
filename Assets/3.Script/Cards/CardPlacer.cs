using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using LiarsBar.Server;   // PlayerProfile

public class CardPlacer : MonoBehaviour
{
    const int DeckSize = 20;
    const int PerType = 6;
    const int NonJoker = 18;
    const int HandSize = 5;
    const int MaxSeats = 4;

    [SerializeField] private Transform tableCenter;
    [SerializeField] private Transform cardPool;
    [SerializeField] Transform[] seatDealAnchors = new Transform[MaxSeats];
    [Tooltip("테이블 카드 선언 연출. 비워 두면 선언 없이 바로 배분한다.")]
    [SerializeField] TableCardAnnouncer announcer;

    [Header("카드 배치 정보")]
    [SerializeField] private float gapX = 0.09f, gapZ = 0.06f, thickness = 0.005f;

    [Header("카드 방향 (모델 축에 맞춰 조정)")]
    [SerializeField] private Vector3 faceDownEuler = new Vector3(90f, -7f, 0f);
    [SerializeField] private Vector3 faceUpEuler = new Vector3(-90f, -7f, 0f);

    [Header("공개 연출 시간 (초)")]
    [SerializeField] private float spreadMove = 0.3f;
    [SerializeField] private float spreadStagger = 0.03f;
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private float flipJump = 0.03f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float gatherMove = 0.35f;
    [SerializeField] private float gatherStagger = 0.02f;

    [Header("배분")]
    [SerializeField] float dealMove = 0.35f;         
    [SerializeField] float dealCardStagger = 0.04f;  
    [SerializeField] float dealSpreadDelay = 0.1f;   
    [SerializeField] float dealSpread = 0.25f;
    [SerializeField] float fanAngle = 8f;             
    [SerializeField] float fanPivotDistance = 0.05f;
    [SerializeField] float dealSeatGap = 0.15f;

    [SerializeField] bool revealEveryRound = false;
    [Tooltip("끄면 공개 · 선언까지만 하고 테이블 앞 배분은 생략한다 (손패는 HandCardsSetup이 캐릭터 손으로 나눈다)")]
    [SerializeField] bool dealToTable = true;

    [Header("로컬 테스트 (네트워크 없이)")]
    [Tooltip("켜면 PlayerProfile 대신 아래 값으로 배분 대상을 정한다.")]
    [SerializeField] bool useDebugSeats = false;
    [SerializeField] int[] debugSeats = { 0, 1, 2, 3 };
    [SerializeField] int debugLocalSeat = 0;

    Quaternion FaceDown => Quaternion.Euler(faceDownEuler);
    Quaternion FaceUp => Quaternion.Euler(faceUpEuler);

    private CardViewer[] cards;
    private Coroutine sequence_co;
    private bool hasRevealed;
    private CardType Table_cur;
    private int deckRemaining;

    /// <summary>공개 · 선언 연출 중이면 true. HandCardsSetup이 끝날 때까지 기다렸다가 손패를 나눈다.</summary>
    public bool IsIntroPlaying { get; private set; }
    public Transform DeckPoint => tableCenter;

    /// <summary>덱 맨 위 카드를 한 장 숨긴다 (손패 배분 때 덱에서 한 장씩 빠지는 것처럼)</summary>
    public void PopDeckCard()
    {
        if (cards == null || deckRemaining <= 0) return;
        cards[--deckRemaining].gameObject.SetActive(false);
    }

    /// <summary>남은 덱 더미를 모두 숨긴다 (LIAR 판정 때 테이블에 지목당한 카드만 남기려고). 다음 라운드 ResetToDeck이 다시 켠다</summary>
    public void HideRemainingDeck()
    {
        while (cards != null && deckRemaining > 0) PopDeckCard();
    }

    // 배분 · 내 손패
    private readonly List<CardViewer> myFrontCards = new List<CardViewer>(); // 내 자리 앞 카드 (슬롯 순서)
    private List<CardType> pendingHand;   // 아직 입히지 못한 내 손패
    private bool handAppliedThisRound;

    // ───────────── 연출 길이 (서버 GameFlowTiming에 넘길 값) ─────────────

    public float RevealTotalDuration =>
          spreadStagger * (DeckSize - 1) + spreadMove
        + flipDuration + holdDuration + flipDuration
        + gatherStagger * (DeckSize - 1) + gatherMove;

    float DealGroupDuration =>
    dealCardStagger * (HandSize - 1) + dealMove + dealSpreadDelay + dealSpread;

    public float DealDuration(int seatCount) => seatCount * (DealGroupDuration + dealSeatGap);


    void Awake()
    {
        cards = cardPool.GetComponentsInChildren<CardViewer>(true);
        if (cards.Length != DeckSize)
            Debug.LogWarning($"[연출] 카드가 {cards.Length}장입니다. {DeckSize}장이어야 합니다.");
    }

    void OnEnable()
    {
        InGameEvent.OnRoundStarted += HandleRoundStarted;
        InGameEvent.OnCardDealt += HandleCardDealt;
    }

    void OnDisable()
    {
        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        InGameEvent.OnCardDealt -= HandleCardDealt;
        if (sequence_co != null) { StopCoroutine(sequence_co); sequence_co = null; }
        IsIntroPlaying = false;
    }

    void Start()
    {
        ResetToDeck();
        Debug.Log($"[연출] 20장 공개 {RevealTotalDuration:0.00}초 / 4인 배분 {DealDuration(MaxSeats):0.00}초");
    }


    void HandleRoundStarted(CardType tableCard)
    {
        if (sequence_co != null) StopCoroutine(sequence_co);
        Table_cur = tableCard;
        pendingHand = null;
        IsIntroPlaying = true;
        sequence_co = StartCoroutine(RoundSequence());
    }

    void HandleCardDealt(List<CardType> hand)
    {
        if (handAppliedThisRound) return;

        pendingHand = new List<CardType>(hand);
        TryApplyMyHand();   // 배분 전이면 보관만 되고, 배분 끝에 다시 시도
    }

    IEnumerator RoundSequence()
    {
        if (revealEveryRound || !hasRevealed)
        {
            hasRevealed = true;
            yield return RevealSequence();
        }
        else
        {
            ResetToDeck();
        }

        if (announcer != null)
            yield return announcer.Play(Table_cur);

        IsIntroPlaying = false;
        if (dealToTable) yield return DealSequence();
        sequence_co = null;
    }

    // ───────────── 덱 더미 ─────────────

    public void ResetToDeck()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            CardViewer c = cards[i];
            Transform t = c.transform;

            t.DOKill();
            c.gameObject.SetActive(true);
            c.Index = -1;
            c.SetHidden();

            t.SetParent(tableCenter, false);
            t.localPosition = StackLocal(i);
            t.localRotation = FaceDown;   // 이전 라운드 손패 방향이 남지 않게
        }

        myFrontCards.Clear();
        handAppliedThisRound = false;
        deckRemaining = cards.Length;
    }

    Vector3 StackLocal(int i) => new Vector3(0, i * thickness, 0);

    //0~5 King, 6~11 Queen, 12~17 Ace (열 번호 = CardType 값), 18~19 Joker
    static CardType TypeOf(int i) => i < NonJoker ? (CardType)(i / PerType) : CardType.Joker;

    Vector3 SpreadLocal(int i)
    {
        if (i < NonJoker)
        {
            int col = i / PerType;
            int row = i % PerType;
            return new Vector3((col - 1) * gapX, 0f, (row - 2.5f) * gapZ);
        }

        float z = (i == NonJoker ? -3.5f : 3.5f) * gapZ;
        return new Vector3(0f, 0f, z);
    }

    IEnumerator RevealSequence()
    {
        ResetToDeck();
        int n = cards.Length;

        // 1) 앞면 정보 입히기 — 아직 뒷면이 위라 화면에는 안 보인다
        for (int i = 0; i < n; i++)
            cards[i].SetFace(TypeOf(i));

        // 2) 덱 → 격자 (뒷면 상태)
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.SetParent(tableCenter, true);

            float delay = i * spreadStagger;
            t.DOLocalMove(SpreadLocal(i), spreadMove).SetDelay(delay).SetEase(Ease.OutCubic);
            t.DOLocalRotateQuaternion(FaceDown, spreadMove).SetDelay(delay);
        }
        yield return new WaitForSeconds(spreadStagger * (n - 1) + spreadMove);

        // 3) 한 번에 뒤집어 공개
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.DOLocalJump(SpreadLocal(i), flipJump, 1, flipDuration);
            t.DOLocalRotateQuaternion(FaceUp, flipDuration);
        }
        yield return new WaitForSeconds(flipDuration);

        // 4) 잠깐 보여주기
        yield return new WaitForSeconds(holdDuration);

        // 5) 한꺼번에 다시 뒷면으로 → 다 덮인 뒤 앞면 정보 지우기
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.DOLocalJump(SpreadLocal(i), flipJump, 1, flipDuration);
            t.DOLocalRotateQuaternion(FaceDown, flipDuration);
        }
        yield return new WaitForSeconds(flipDuration);

        for (int i = 0; i < n; i++)
            cards[i].SetHidden();

        // 6) 중앙 덱으로 모으기 (0번부터 깔려서 쌓임 → 마지막 번호가 맨 위)
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.SetParent(tableCenter, true);

            float delay = i * gatherStagger;
            t.DOLocalMove(StackLocal(i), gatherMove).SetDelay(delay).SetEase(Ease.InOutQuad);
        }
        yield return new WaitForSeconds(gatherStagger * (n - 1) + gatherMove);
    }


    List<int> GetDealSeats()
    {
        var seats = new List<int>();

        if (useDebugSeats)
        {
            seats.AddRange(debugSeats);
        }
        else
        {
            foreach (PlayerProfile p in PlayerProfile.All)
            {
                if (p == null) continue;
                if (p.TryGetComponent(out PlayerState state) && state.isDead) continue;
                seats.Add(p.Seat);
            }
        }

        seats.Sort();
        return seats;
    }

    int GetLocalSeat()
    {
        if (useDebugSeats) return debugLocalSeat;
        return PlayerProfile.Local != null ? PlayerProfile.Local.Seat : -1;
    }

    Vector3 BundleLocal(int slot) => new Vector3(0f, slot * thickness, 0f);

    private void FanPose(int slot, out Vector3 pos, out Quaternion rot)
    {
        float angle = (slot - (HandSize - 1) * 0.5f) * fanAngle;
        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        Vector3 pivot = new Vector3(0f, 0f, -fanPivotDistance);
        pos = pivot + yaw * new Vector3(0f, 0f, fanPivotDistance);
        pos.y = slot * thickness;   // 겹치는 카드가 깜빡이지 않게 한 장씩 높이 차이

        rot = yaw * FaceDown;
    }

    IEnumerator DealSequence()
    {
        List<int> seats = GetDealSeats();
        int localSeat = GetLocalSeat();
        int deckTop = cards.Length - 1;

        foreach (int seat in seats)
        {
            Transform anchor = (seat >= 0 && seat < seatDealAnchors.Length) ? seatDealAnchors[seat] : null;
            if (anchor == null)
            {
                Debug.LogWarning($"[연출] {seat}번 좌석 배분 앵커가 없습니다.");
                continue;
            }

            var group = new List<Transform>(HandSize);

            // 1) 덱에서 좌석 앞으로 직선 이동 → 앵커 중앙에 묶음으로 쌓임
            for (int slot = 0; slot < HandSize && deckTop >= 0; slot++)
            {
                CardViewer card = cards[deckTop--];
                Transform t = card.transform;
                t.SetParent(anchor, true);

                float delay = slot * dealCardStagger;
                t.DOLocalMove(BundleLocal(slot), dealMove).SetDelay(delay).SetEase(Ease.OutCubic);
                t.DOLocalRotateQuaternion(FaceDown, dealMove).SetDelay(delay);

                group.Add(t);
                if (seat == localSeat)
                    myFrontCards.Add(card);   // 슬롯 순서 = 손패 인덱스 순서
            }

            // 2) 다 도착하면 잠깐 멈췄다가 옆으로 펼쳐서 5장임을 보여 준다
            float spreadStart = dealCardStagger * (HandSize - 1) + dealMove + dealSpreadDelay;
            for (int slot = 0; slot < group.Count; slot++)
            {
                FanPose(slot, out Vector3 pos, out Quaternion rot);
                group[slot].DOLocalMove(pos, dealSpread).SetDelay(spreadStart).SetEase(Ease.OutCubic);
                group[slot].DOLocalRotateQuaternion(rot, dealSpread).SetDelay(spreadStart).SetEase(Ease.OutCubic);
            }

            yield return new WaitForSeconds(DealGroupDuration + dealSeatGap);
        }

        TryApplyMyHand();
    }


    void TryApplyMyHand()
    {
        if (pendingHand == null || myFrontCards.Count == 0) return;

        int count = Mathf.Min(pendingHand.Count, myFrontCards.Count);
        for (int i = 0; i < count; i++)
        {
            myFrontCards[i].Index = i;               
            myFrontCards[i].SetFace(pendingHand[i]); 
        }

        pendingHand = null;
        handAppliedThisRound = true;
    }
}