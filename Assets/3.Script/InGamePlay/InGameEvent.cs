using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InGameEvent
{
    public static event Action<CardType> OnRoundStarted; // 라운드 시작 시 뽑힌 테이블 카드 공지

    public static event Action<List<CardType>> OnCardDealt; // 플레이어 카드 불출 받음

    public static event Action<int> OnTurnChanged; // 턴 변경

    public static event Action<int, bool> OnCardsPlayed; // 플레이어 패 제출

    public static event Action<int> OnPlayerEliminated; // 구현 전

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
