using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class InGameEvent
{
    public static event Action OnEntryInGame; // 로비에서 인게임 들어갈 때 실행됨

    public static event Action OnReEntryInLobby; // 인게임에서 로비 들어갈 때 실행됨

    public static event Action<CardType> OnRoundStarted; // 라운드 시작 시 뽑힌 테이블 카드 공지

    public static event Action<List<CardType>> OnCardDealt; // 플레이어 카드 불출 받음

    public static event Action<int> OnTurnChanged; // 턴 변경

    public static event Action<int[]> OnCardsPlayed; // 플레이어 패 제출

    public static event Action<int> OnPlayerEliminated; // 플레이어 사망 연출. 매개변수는 player seat입니다.

    public static event Action<int> OnPlayerSurvived; // 플레이어 생존 연출. 매개변수는 player seat 입니다.

    public static event Action OnMenuOpened; // 인게임 상에서 esc 누를 경우 실행됨

    public static event Action OnMenuClosed;

    public static event Action<int, int> OnLiarCalled; // LIAR 호출 (호출한 좌석, 룰렛을 돌릴 좌석) - UI 공지용

    public static event Action<int> OnWinnerAnnounced; // 게임 종료, 승자 좌석 (-1 = 없음) - 결과 화면용

    public static void TriggerOnEntryInGame()
    {
        OnEntryInGame?.Invoke();
    }

    public static void TriggerOnReEntryInLobby()
    {
        OnReEntryInLobby?.Invoke();
    }

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

    public static void TriggerOnCardsPlayed(int[] SelectIndexes)
    {
        OnCardsPlayed?.Invoke(SelectIndexes);
    }

    public static void TriggerOnPlayerEliminated(int seatNum)
    {
        OnPlayerEliminated?.Invoke(seatNum);
    }

    public static void TriggerOnPlayerSurvived(int seatNum)
    {
        OnPlayerSurvived?.Invoke(seatNum);
    }

    public static void TriggerOnMenuOpened()
    {
        OnMenuOpened?.Invoke();
    }

    public static void TriggerOnMenuClosed()
    {
        OnMenuClosed?.Invoke();
    }

    public static void TriggerOnLiarCalled(int callerSeat, int loserSeat)
    {
        OnLiarCalled?.Invoke(callerSeat, loserSeat);
    }

    public static void TriggerOnWinnerAnnounced(int winnerSeat)
    {
        OnWinnerAnnounced?.Invoke(winnerSeat);
    }
    public static event Action<int, int, int[]> OnCardsSubmitted; // 서버 확정 제출 (좌석, 장수, 제출 전 손패 기준 인덱스)
    public static event Action<IReadOnlyList<int>> OnHandSelectionChanged; // 내 손패 선택 변경 (손패 인덱스)
    public static event Action OnLookRequested; // 로컬: 스페이스(Look) 입력 → 패 펼치기 / 덮기 요청
    public static event Action<int, bool> OnCardsOpened; // seat번 플레이어가 패를 펼침(true) / 덮음(false) (모든 클라이언트)

    public static void TriggerOnLookRequested()
    {
        OnLookRequested?.Invoke();
    }

    public static void TriggerOnCardsOpened(int seat, bool open)
    {
        OnCardsOpened?.Invoke(seat, open);
    }

    public static void TriggerOnCardsSubmitted(int seat, int count, int[] indices)
    {
        OnCardsSubmitted?.Invoke(seat, count, indices);
    }

    public static void TriggerOnHandSelectionChanged(IReadOnlyList<int> indices)
    {
        OnHandSelectionChanged?.Invoke(indices);
    }
}
