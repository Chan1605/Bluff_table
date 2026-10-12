using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InGameEvent
{
    public static event Action<CardType> OnRoundStarted;

    public static event Action<List<CardType>> OnCardDealt;

    public static event Action<int> OnTurnChanged;

    public static event Action<int, bool> OnCardsPlayed;

    public static event Action<int> OnPlayerEliminated;

    public static void TriggerOnRoundStarted(CardType card)
    {
        OnRoundStarted?.Invoke(card);
    }

    public static void TriggerOnCardDealt(List<CardType> handCard)
    {
        OnCardDealt?.Invoke(handCard);
    }

    public static void TriggerOnTurnChanged(int turn)
    {
        OnTurnChanged?.Invoke(turn);
    }

    public static void TriggerOnCardsPlayed(int num, bool t)
    {
        OnCardsPlayed?.Invoke(num, t);
    }

    public static void TriggerOnPlayerEliminated(int seatNum)
    {
        OnPlayerEliminated?.Invoke(seatNum);
    }
}
