using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 좌석 하나의 손패 5장. 카드는 풀처럼 재사용하고, 슬롯 Transform이 손 모양 배치를 정한다.
    /// 슬롯 방향만 좌석마다 잘 잡아 두면 (내 좌석은 카드 앞면이 카메라를 향하게, 상대는 뒷면이 보이게)
    /// 모든 좌석이 같은 카드 Animator Controller를 공유한다.
    /// </summary>
    public class HandView : MonoBehaviour
    {
        public const int HandSize = 5;

        [Tooltip("손패 위치 5개. 순서 = CardView.Index")]
        [SerializeField] Transform[] slots = new Transform[HandSize];
        [Tooltip("slots와 같은 순서의 카드 5장")]
        [SerializeField] CardView[] cards = new CardView[HandSize];

        CardRank[] pendingFaces;
        Material[] faceMaterials;
        bool isLocal;

        public IReadOnlyList<CardView> Cards => cards;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++) cards[i].Index = i;
        }

        public void Setup(bool local, Material[] faces)
        {
            isLocal = local;
            faceMaterials = faces;
        }

        /// <summary>slot번째 카드를 덱에서 날린다. 순서·간격은 PresentationDirector가 좌석을 돌며 정한다.</summary>
        public void DealOne(int slot, Transform deck)
        {
            var card = cards[slot];
            card.ClearFace();
            if (isLocal && pendingFaces != null && slot < pendingFaces.Length)
                ApplyFace(card, pendingFaces[slot]);
            card.Deal(deck, slots[slot]);
            card.SetClickable(isLocal);
        }

        /// <summary>내 손패 정보. 배분 전에 오면 보관했다가 DealOne에서 입힌다.</summary>
        public void SetLocalFaces(CardRank[] ranks)
        {
            pendingFaces = ranks;
            for (int i = 0; i < cards.Length && i < ranks.Length; i++)
                if (cards[i].InHand) ApplyFace(cards[i], ranks[i]);
        }

        public void SetLifted(IReadOnlyList<int> selected)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool lifted = false;
                for (int k = 0; k < selected.Count; k++) if (selected[k] == i) { lifted = true; break; }
                cards[i].SetLifted(lifted);
            }
        }

        /// <summary>
        /// 낼 카드를 고른다. 내 좌석은 서버가 확정한 슬롯 인덱스, 상대는 장수만 알므로 오른쪽 끝부터.
        /// </summary>
        public List<CardView> TakeForPlay(int count, int[] slotIndices)
        {
            var result = new List<CardView>(count);
            if (slotIndices != null)
            {
                foreach (int i in slotIndices)
                    if (i >= 0 && i < cards.Length && cards[i].InHand) result.Add(cards[i]);
            }
            for (int i = cards.Length - 1; i >= 0 && result.Count < count; i--)
                if (cards[i].InHand && !result.Contains(cards[i])) result.Add(cards[i]);
            return result;
        }

        public void CollectAll(Transform deck)
        {
            foreach (var c in cards) c.Collect(deck);
        }

        public void HideAll()
        {
            pendingFaces = null;
            foreach (var c in cards) c.HideNow();
        }

        public void ApplyFace(CardView card, CardRank rank)
        {
            Material m = null;
            if (faceMaterials != null && (int)rank < faceMaterials.Length) m = faceMaterials[(int)rank];
            card.SetFace(rank, m);
        }
    }
}
