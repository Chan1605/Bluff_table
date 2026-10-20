using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameFlowTiming
{
    // 코루틴 시간 제어
    public const float GameEntry = 1f;
    public const float TableSetting = 1f;
    public const float DividingDeck = 1f;
    public const float TurnTimer = 10f;
    public const float DealWaitTimeout = 25f;   // 손패 배분 연출이 끝났다는 신호를 기다리는 최대 시간 (못 받으면 그냥 첫 턴 시작)


    public const float LierReveal = 1f;
    public const float RouletteAnimation = 1f;
    public const float ResultDisplay = 2f;
    public const float WinnerSeq = 10f;

}
