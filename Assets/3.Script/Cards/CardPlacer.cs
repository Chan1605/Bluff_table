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
    [SerializeField] float dealSlotGap = 0.07f;      // 좌석 앞 5장 사이 간격
    [SerializeField] float dealMove = 0.4f;          // 한 장이 덱 → 좌석까지 걸리는 시간
    [SerializeField] float dealJump = 0.12f;         // 포물선 높이
    [SerializeField] float dealCardStagger = 0.04f;  // 같은 좌석 5장 사이 시차
    [SerializeField] float dealSeatGap = 0.15f;      // 좌석과 좌석 사이 쉬는 시간

    [SerializeField] bool revealEveryRound = false;

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

    // 배분 · 내 손패
    private readonly List<CardViewer> myFrontCards = new List<CardViewer>(); // 내 자리 앞 카드 (슬롯 순서)
    private List<CardType> pendingHand;   // 아직 입히지 못한 내 손패
    private bool handAppliedThisRound;

    // ───────────── 연출 길이 (서버 GameFlowTiming에 넘길 값) ─────────────

    public float RevealTotalDuration =>
          spreadStagger * (DeckSize - 1) + spreadMove
        + flipDuration + holdDuration + flipDuration
        + gatherStagger * (DeckSize - 1) + gatherMove;

    float DealGroupDuration => dealCardStagger * (HandSize - 1) + dealMove;

    public float DealDuration(int seatCount) => seatCount * (DealGroupDuration + dealSeatGap);

    // ───────────── 생명주기 ─────────────

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
    }

    void Start()
    {
        ResetToDeck();
        Debug.Log($"[연출] 20장 공개 {RevealTotalDuration:0.00}초 / 4인 배분 {DealDuration(MaxSeats):0.00}초");
    }

    // ───────────── 이벤트 ─────────────

    void HandleRoundStarted(CardType tableCard)
    {
        if (sequence_co != null) StopCoroutine(sequence_co);
        Table_cur = tableCard;
        pendingHand = null;
        sequence_co = StartCoroutine(RoundSequence());
    }

    void HandleCardDealt(List<CardType> hand)
    {
        // 라운드 중 두 번째 이후 호출은 카드 제출 후 손패 갱신 → 다음 단계에서 처리
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

        // if (announcer != null)
        //     yield return announcer.Play(Table_cur);

        yield return DealSequence();
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
    }

    Vector3 StackLocal(int i) => new Vector3(0, i * thickness, 0);

    // ───────────── 공개 격자 ─────────────

    /// <summary>0~5 King, 6~11 Queen, 12~17 Ace (열 번호 = CardType 값), 18~19 Joker</summary>
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

    // ───────────── 배분 ─────────────

    /// <summary>배분 대상: 접속 중(PlayerProfile 존재)이고 죽지 않은 좌석, 오름차순.</summary>
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

    /// <summary>좌석 앞 5장 가로 배치. 가운데(2번)가 앵커 위치.</summary>
    Vector3 DealSlotLocal(int slot) =>
        new Vector3((slot - (HandSize - 1) * 0.5f) * dealSlotGap, 0f, 0f);

    IEnumerator DealSequence()
    {
        List<int> seats = GetDealSeats();
        int localSeat = GetLocalSeat();
        int deckTop = cards.Length - 1;   // 덱 맨 위부터 꺼낸다

        foreach (int seat in seats)
        {
            Transform anchor = (seat >= 0 && seat < seatDealAnchors.Length) ? seatDealAnchors[seat] : null;
            if (anchor == null)
            {
                Debug.LogWarning($"[연출] {seat}번 좌석 배분 앵커가 없습니다.");
                continue;
            }

            // 5장을 한 묶음으로, 살짝 시차를 두고 동시에 날린다
            for (int slot = 0; slot < HandSize && deckTop >= 0; slot++)
            {
                CardViewer card = cards[deckTop--];
                Transform t = card.transform;
                t.SetParent(anchor, true);

                float delay = slot * dealCardStagger;
                t.DOLocalJump(DealSlotLocal(slot), dealJump, 1, dealMove).SetDelay(delay).SetEase(Ease.OutQuad);
                t.DOLocalRotateQuaternion(FaceDown, dealMove).SetDelay(delay);

                if (seat == localSeat)
                    myFrontCards.Add(card);   // 슬롯 순서 = 손패 인덱스 순서
            }

            yield return new WaitForSeconds(DealGroupDuration + dealSeatGap);
        }

        // 남은 카드(2~3인일 때)는 일단 덱에 그대로 둔다 → 다음 단계에서 테이블 아래로

        TryApplyMyHand();
    }

    /// <summary>내 카드가 내 자리에 놓였고 손패 값도 도착했으면 앞면 정보를 입힌다.</summary>
    void TryApplyMyHand()
    {
        if (pendingHand == null || myFrontCards.Count == 0) return;

        int count = Mathf.Min(pendingHand.Count, myFrontCards.Count);
        for (int i = 0; i < count; i++)
        {
            myFrontCards[i].Index = i;               // 서버 submitIndex 기준
            myFrontCards[i].SetFace(pendingHand[i]); // 아직 뒷면이라 화면엔 안 보임
        }

        pendingHand = null;
        handAppliedThisRound = true;
    }
}