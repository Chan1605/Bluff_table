using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class CardPlacer : MonoBehaviour
{
    const int DeckSize = 20;
    const int PerType = 6;
    const int NonJoker = 18;

    [SerializeField] private Transform tableCenter;
    [SerializeField] private Transform cardPool;

    [Header("카드 배치 정보")]
    [SerializeField] private float gapX = 0.09f, gapZ = 0.06f, thickness = 0.005f;

    [Header("공개 연출 시간 (초)")]
    [SerializeField] private float spreadMove = 0.3f;
    [SerializeField] private float spreadStagger = 0.03f;
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private float flipRowStagger = 0.06f;
    [SerializeField] private float flipJump = 0.03f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float gatherMove = 0.35f;
    [SerializeField] private float gatherStagger = 0.02f;

    [SerializeField] bool revealEveryRound = false;

    static readonly Quaternion FaceDown = Quaternion.Euler(90f, -7f, 0f);

    private CardViewer[] cards;
    private Coroutine sequence_co;
    private bool hasRevealed;

    public float RevealTotalDuration =>
          spreadStagger * (DeckSize - 1) + spreadMove
        + flipDuration + holdDuration + flipDuration
        + gatherStagger * (DeckSize - 1) + gatherMove;

    void Awake()
    {
        cards = cardPool.GetComponentsInChildren<CardViewer>(true);
        if (cards.Length != DeckSize)
            Debug.LogWarning($"[연출] 카드가 {cards.Length}장입니다. {DeckSize}장이어야 합니다.");
    }

    void OnEnable() => InGameEvent.OnRoundStarted += HandleRoundStarted;

    void OnDisable()
    {
        InGameEvent.OnRoundStarted -= HandleRoundStarted;
        if (sequence_co != null) { StopCoroutine(sequence_co); sequence_co = null; }
    }

    void Start()
    {
        ResetToDeck();
        Debug.Log($"[연출] 20장 공개 연출 길이: {RevealTotalDuration:0.00}초");
    }

    void HandleRoundStarted(CardType tableCard)
    {
        if (sequence_co != null) StopCoroutine(sequence_co);

        if (!revealEveryRound && hasRevealed)
        {
            ResetToDeck();
            return;
        }

        hasRevealed = true;
        sequence_co = StartCoroutine(RevealSequence());
    }

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
            //t.localRotation = StackRotation();
        }
    }

    //Vector3 StackLocal(int i) => new Vector3(
    //    Random.Range(-stackJitterPos, stackJitterPos),
    //    i * cardThickness,
    //    Random.Range(-stackJitterPos, stackJitterPos));

    Vector3 StackLocal(int i) => new Vector3(0, i * thickness, 0);

    //Quaternion StackRotation() =>
    //Quaternion.Euler(0f, Random.Range(-stackJitterYaw, stackJitterYaw), 180f);

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

        // 3) 앞쪽부터 물결처럼 뒤집어 공개 (살짝 들렸다가 뒤집힘)
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.DOLocalJump(SpreadLocal(i), flipJump, 1, flipDuration);
            t.DOLocalRotateQuaternion(Quaternion.identity, flipDuration);
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

        // 6) 중앙 덱으로 모으기 (0번부터 깔려서 쌓임)
        for (int i = 0; i < n; i++)
        {
            Transform t = cards[i].transform;
            t.SetParent(tableCenter, true);

            float delay = i * gatherStagger;
            t.DOLocalMove(StackLocal(i), gatherMove).SetDelay(delay).SetEase(Ease.InOutQuad);
            //t.DOLocalRotateQuaternion(StackRotation(), gatherMove).SetDelay(delay);
        }
        yield return new WaitForSeconds(gatherStagger * (n - 1) + gatherMove);

        sequence_co = null;
        // 다음 단계: 배분 (OnCardDealt 수신 후 덱 맨 위부터 좌석 슬롯으로)
    }
}
