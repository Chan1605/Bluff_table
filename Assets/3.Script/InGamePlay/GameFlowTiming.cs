using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameFlowTiming
{
    // 코루틴 시간 제어
    public const float GameEntry = 1f;
    public const float TableSetting = 1f;
    public const float DividingDeck = 1f;
    public const float TurnTimer = 30f;
    public const float SceneLoadTimeout = 40f;  // 모든 플레이어가 테이블 씬을 불러올 때까지 기다리는 최대 시간
    public const float DealWaitTimeout = 25f;   // 손패 배분 연출이 끝났다는 신호를 기다리는 최대 시간 (못 받으면 그냥 첫 턴 시작)


    public const float LiarCallPause = 1f;      // "LIAR!" 선언 후 카드를 뒤집기 전까지
    public const float LierReveal = 2.5f;       // 낸 카드를 뒤집어 초록(맞음) / 빨강(틀림)으로 보여 주는 시간
    public const float RouletteAnimation = 1f;
    public const float ResultDisplay = 2f;
    public const float WinnerSeq = 10f;

    public const float TransitionSpectator = 2.5f;

}
