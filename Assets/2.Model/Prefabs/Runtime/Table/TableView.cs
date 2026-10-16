using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 테이블 중앙: 덱 위치, 제출 카드가 쌓이는 중앙 원, 테이블 카드(K·Q·A) 표식.
    ///
    /// 표식 Animator 계층
    ///   TableMarker   ← Animator (TableMarker.controller)
    ///   └ Marker      ← 등장 팝 (scale)
    ///     ├ K
    ///     ├ Q
    ///     └ A         ← 상태(ShowK/ShowQ/ShowA)가 하나만 켠다
    /// </summary>
    public class TableView : MonoBehaviour
    {
        [SerializeField] Animator marker;
        [SerializeField] Transform deckPoint;
        [SerializeField] Transform tableCenter;
        [Header("중앙 원 (뒷면으로 쌓이는 자리)")]
        [SerializeField] Transform pileRoot;
        [SerializeField] int pileCapacity = 24;
        [SerializeField] float pileRadius = 0.12f;
        [SerializeField] float pileStackHeight = 0.002f;

        readonly List<Transform> pileSlots = new();
        int pileCount;

        public Transform DeckPoint => deckPoint;
        public Transform Center => tableCenter != null ? tableCenter : transform;

        void Awake()
        {
            // 흩어진 더미 느낌이 나도록 슬롯을 미리 만든다. 카드 Animator는 이 Transform으로 날아간다.
            var rng = new System.Random(7);
            for (int i = 0; i < pileCapacity; i++)
            {
                var t = new GameObject($"PileSlot_{i:00}").transform;
                t.SetParent(pileRoot, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * pileRadius;
                t.localPosition = new Vector3(Mathf.Cos(a) * r, i * pileStackHeight, Mathf.Sin(a) * r);
                t.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                pileSlots.Add(t);
            }
        }

        public Transform NextPileSlot()
        {
            var slot = pileSlots[Mathf.Min(pileCount, pileSlots.Count - 1)];
            pileCount++;
            return slot;
        }

        public void ResetPile() => pileCount = 0;

        public void ShowRank(CardRank rank)
        {
            marker.SetInteger(AnimIds.TableMarker.TableRankId, (int)rank);
            marker.SetTrigger(AnimIds.TableMarker.RoundStartId);
        }

        public void ClearRank() => marker.SetTrigger(AnimIds.TableMarker.ClearId);

        public void ResetAll()
        {
            ResetPile();
            marker.Rebind();
            marker.Update(0f);
        }
    }
}
