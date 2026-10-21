using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 3D 카드 한 장.
    ///
    /// 역할 분담 — "Animator가 언제 · 어떻게, 코드는 어디서 어디로"
    ///   Animator: 상태 흐름(분배 → 손패 → 선택 → 제출 → 공개 → 회수), 이동 타이밍과 이징(travel 곡선),
    ///             포물선 높이(arc 곡선), 들어 올림, 뒤집기, 일치/불일치 발광, 보이기/숨기기.
    ///   코드:     출발 위치와 도착 Transform만 정한다. 좌석마다 위치가 달라도 클립 하나를 공유할 수 있다.
    ///
    /// 프리팹 계층 (경로 이름이 클립 바인딩과 같아야 한다)
    ///   Card            ← Animator + CardView (루트 Transform은 코드가 움직임)
    ///   └ Visual        ← 들어 올림 (localPosition.y)
    ///     └ Flip        ← 뒤집기 (localEulerAngles.z 0 → 180). 0일 때 뒷면이 +Y(위)
    ///       ├ Back
    ///       ├ Face      ← faceRenderer (Flip의 -Y쪽 면)
    ///       ├ GlowMatch ← 일치 시 켜짐 (초록)
    ///       └ GlowMiss  ← 불일치 시 켜짐 (빨강)
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class CardView : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Renderer faceRenderer;
        [SerializeField] Collider clickCollider;

        // ▼ Animator 클립이 직접 쓰는 값. 이름을 바꾸면 클립 바인딩이 끊긴다.
        [Tooltip("0 = 출발 위치, 1 = 도착 Transform. 클립 곡선이 이징을 정한다.")]
        [SerializeField] float travel = 1f;
        [Tooltip("이동 중 위로 뜨는 높이(m). 클립 곡선.")]
        [SerializeField] float arc;

        Vector3 fromPos;
        Quaternion fromRot;
        Transform target;
        bool follow;   // true면 travel 곡선 없이 target에 그대로 붙는다 (Follow)

        /// <summary>손패 슬롯 번호 0~4. UI가 Raycast로 선택할 때 쓰는 값 (기획서 23쪽 CardView.Index).</summary>
        public int Index { get; set; }
        public CardRank? Rank { get; private set; }
        public bool InHand { get; private set; }
        public bool OnTable { get; private set; }
        public bool IsVisible => InHand || OnTable;

        void Reset() => animator = GetComponent<Animator>();

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            SetClickable(false);
        }

        public void SetFace(CardRank rank, Material faceMaterial)
        {
            Rank = rank;
            if (faceRenderer != null && faceMaterial != null) faceRenderer.sharedMaterial = faceMaterial;
        }

        public void ClearFace() => Rank = null;

        /// <summary>내 손패만 클릭 가능하게 (UI의 Raycast 입력 어댑터가 사용)</summary>
        public void SetClickable(bool on)
        {
            if (clickCollider != null) clickCollider.enabled = on;
        }

        public void Deal(Transform from, Transform handSlot)
        {
            SnapTo(from);
            BeginTravel(handSlot);
            InHand = true; OnTable = false;
            animator.SetBool(AnimIds.Card.LiftedId, false);
            animator.SetTrigger(AnimIds.Card.DealId);
        }

        public void SetLifted(bool lifted)
        {
            if (InHand) animator.SetBool(AnimIds.Card.LiftedId, lifted);
        }

        public void PlayTo(Transform pileSlot)
        {
            BeginTravel(pileSlot);
            InHand = false; OnTable = true;
            SetClickable(false);
            animator.SetBool(AnimIds.Card.LiftedId, false);
            animator.SetTrigger(AnimIds.Card.PlayId);
        }

        /// <summary>match는 로직이 보낸 카드별 일치 여부를 그대로 쓴다 (연출은 룰을 계산하지 않는다).</summary>
        public void Reveal(bool match)
        {
            animator.SetBool(AnimIds.Card.MatchId, match);
            animator.SetTrigger(AnimIds.Card.RevealId);
        }

        public void Collect(Transform deck)
        {
            if (!IsVisible) return;
            BeginTravel(deck);
            InHand = false; OnTable = false;
            SetClickable(false);
            animator.SetTrigger(AnimIds.Card.CollectId);
        }

        /// <summary>연출 코드가 Transform을 직접 움직일 때 (LIAR 판정 등): 상태 · 트리거는 그대로 두고 그 Transform에 붙는다</summary>
        public void Follow(Transform t)
        {
            target = t;
            follow = t != null;
            arc = 0f;
        }

        /// <summary>로비 복귀 등 즉시 초기화</summary>
        public void HideNow()
        {
            InHand = false; OnTable = false;
            SetClickable(false);
            ClearFace();
            target = null;
            follow = false;
            animator.Rebind();   // 기본 상태(Hidden)로 되돌린다
            animator.Update(0f);
        }

        void SnapTo(Transform t)
        {
            transform.SetPositionAndRotation(t.position, t.rotation);
        }

        void BeginTravel(Transform to)
        {
            fromPos = transform.position;
            fromRot = transform.rotation;
            target = to;
            follow = false;
            // Animator가 새 상태에 들어가기 전 한 프레임 동안 도착점으로 튀지 않게
            travel = 0f;
            arc = 0f;
        }

        // Animator(Update 이후)가 travel · arc를 쓴 다음 위치를 계산한다.
        void LateUpdate()
        {
            if (target == null) return;
            if (follow) { transform.SetPositionAndRotation(target.position, target.rotation); return; }
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(fromPos, target.position, travel) + Vector3.up * arc,
                Quaternion.SlerpUnclamped(fromRot, target.rotation, travel));
        }
    }
}
