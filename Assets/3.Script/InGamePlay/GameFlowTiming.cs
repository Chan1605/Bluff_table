using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameFlowTiming
{
    // 코루틴 시간 제어
    public const float GameEntry = 1f;
    public const float GunReload = 5.3f;        // 게임 시작 장전 연출 (원작 reload 클립 5.1초 + 전환) → 끝나면 첫 라운드(덱 공개)
    public const float TableSetting = 1f;
    public const float DividingDeck = 1f;
    public const float TurnTimer = 30f;
    public const float SceneLoadTimeout = 40f;  // 모든 플레이어가 테이블 씬을 불러올 때까지 기다리는 최대 시간
    public const float DealWaitTimeout = 25f;   // 손패 배분 연출이 끝났다는 신호를 기다리는 최대 시간 (못 받으면 그냥 첫 턴 시작)


    public const float LiarCallPause = 1f;      // "LIAR!" 선언 후 카드를 뒤집기 전까지
    public const float LierReveal = 2.5f;       // 낸 카드를 뒤집어 초록(맞음) / 빨강(틀림)으로 보여 주는 시간
    public const float RouletteAnimation = 4.1f;  // 총 집기 → 머리에 대기 → 방아쇠 (원작 silah_trigger 클립 3.87~4초 + 전환). 이 순간 결과를 반영한다
    public const float ResultDisplay = 3f;        // 발사 / 빈 약실 이후 (silah_bos 약 2.7초, 사망은 관전 전환 2.5초) → 다음 라운드
    public const float WinnerSeq = 10f;

    public const float TransitionSpectator = 2.5f;

}
