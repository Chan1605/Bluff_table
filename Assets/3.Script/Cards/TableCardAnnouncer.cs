using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 라운드 테이블 카드 선언 연출.
/// 덱 위치에서 선언 카드가 떠올라 커지며 Y축으로 회전하고, 위에 "QUEEN'S TABLE" 같은 글자가 뜬다.
/// 끝나면 덱으로 다시 들어가며 사라진다.
///
/// 선언 카드는 배분용 20장과 별개의 카드다 (20장을 하나라도 빼면 4인 배분이 모자란다).
/// CardTableDirector가 공개 연출 뒤, 배분 앞에 Play()를 호출한다.
/// </summary>
public class TableCardAnnouncer : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform pivot;
    [SerializeField] private CardViewer card;
    [SerializeField] private Transform labelRoot;
    [SerializeField] private CanvasGroup labelGroup;
    [SerializeField] private Text labelText;
    [SerializeField] private Transform deckRoot;

    [Header("위치 · 크기")]
    [SerializeField] private float riseHeight = 0.25f;      // 덱 위로 떠오르는 높이
    [SerializeField] private float shownScale = 2.5f;       // 떠올랐을 때 크기 (배)
    [SerializeField] private float labelAboveCard = 0.15f;  // 카드 위로 글자가 뜨는 높이

    [Header("시간 (초)")]
    [SerializeField] private float riseDuration = 0.5f;
    [SerializeField] private float spinDuration = 2.0f;
    [SerializeField] private int spinTurns = 2;
    [SerializeField] private float sinkDuration = 0.4f;

    public float Duration => riseDuration + spinDuration + sinkDuration;

    void Awake()
    {
        pivot.gameObject.SetActive(false);
        labelRoot.gameObject.SetActive(false);
    }

    static string TableName(CardType type) => type switch
    {
        CardType.King  => "KING'S TABLE",
        CardType.Queen => "QUEEN'S TABLE",
        CardType.Ace   => "ACE'S TABLE",
        _              => $"{type}'S TABLE"
    };

    public IEnumerator Play(CardType tableCard)
    {
        Vector3 start = deckRoot.position;
        Vector3 shown = start + Vector3.up * riseHeight;

        // 준비: 덱 위치에 작게, 앞면 정보 입히기
        pivot.DOKill();
        labelGroup.DOKill();
        pivot.position = start;
        pivot.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(deckRoot.forward, Vector3.up));
        pivot.localScale = Vector3.one;
        pivot.gameObject.SetActive(true);
        card.SetFace(tableCard);

        labelText.text = TableName(tableCard);
        labelRoot.position = shown + Vector3.up * (labelAboveCard * shownScale);
        labelGroup.alpha = 0f;
        labelRoot.gameObject.SetActive(true);

        // 1) 떠오르며 커지기 + 글자 나타나기
        pivot.DOMove(shown, riseDuration).SetEase(Ease.OutBack);
        pivot.DOScale(shownScale, riseDuration).SetEase(Ease.OutBack);
        labelGroup.DOFade(1f, riseDuration);
        yield return new WaitForSeconds(riseDuration);

        // 2) 제자리 회전 — 어느 좌석에서 봐도 앞면이 한 번 이상 지나간다
        pivot.DORotate(new Vector3(0f, 360f * spinTurns, 0f), spinDuration, RotateMode.WorldAxisAdd)
             .SetEase(Ease.InOutSine);
        yield return new WaitForSeconds(spinDuration);

        // 3) 덱으로 들어가며 작아지기 + 글자 사라지기
        pivot.DOMove(start, sinkDuration).SetEase(Ease.InBack);
        pivot.DOScale(1f, sinkDuration).SetEase(Ease.InBack);
        labelGroup.DOFade(0f, sinkDuration);
        yield return new WaitForSeconds(sinkDuration);

        card.SetHidden();
        pivot.gameObject.SetActive(false);
        labelRoot.gameObject.SetActive(false);
    }
}
