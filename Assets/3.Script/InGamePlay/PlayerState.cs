using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerState : NetworkBehaviour
{
    public static PlayerState Local { get; private set; }
    
    public int SeatNum;

    [SyncVar] public bool isDead;
    [SyncVar] public bool isTurn;
    [SyncVar] public bool isEscaped;

    [SyncVar] public int currentChamber;
    public int bulletPosition;


    [SerializeField] public DeckGamePlayManager deckManager;

    public override void OnStartLocalPlayer()
    {
        Local = this;
    }
    public override void OnStopClient()
    {
        if (Local == this) Local = null;
    }

    [Server]
    public void ResetState()
    {
        isDead = false;
        isTurn = false;
        isEscaped = false;

        currentChamber = 0;
        bulletPosition = UnityEngine.Random.Range(0, 6);
    }

    [Server]
    public void RoundReset()
    {
        isEscaped = false;
    }

    [TargetRpc]
    public void TargetGiveCards(List<CardType> handCards, bool hideUI)
    {
        if (!isLocalPlayer) return;
        if (hideUI) return;

        Debug.Log("[클라이언트] 나의 패 도착");
        for (int i = 0; i < handCards.Count; i++)
        {
            Debug.Log($"손패 {i} 번째의 카드 : {handCards[i]}");
        }
        InGameEvent.TriggerOnCardDealt(handCards);
    }

    [Command]
    public void SubmitCards(int[] indexes)
    {
        deckManager.PlayerSubmitCard(SeatNum, indexes);
    }

    [Command]
    public void CallLiar()
    {
        deckManager.CallLiar(SeatNum);
    }
}
