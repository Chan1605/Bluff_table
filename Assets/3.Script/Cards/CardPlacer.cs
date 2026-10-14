using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardPlacer : MonoBehaviour
{
    [SerializeField] private Transform tableCenter;
    [SerializeField] private Transform cardPool;
    [SerializeField] private CardType type;

    [Header("카드 배치 정보")]
    [SerializeField] private float gapX = 0.09f, gapZ = 0.06f, thickness = 0.005f;
}
