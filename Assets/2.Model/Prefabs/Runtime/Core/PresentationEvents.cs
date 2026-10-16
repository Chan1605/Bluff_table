using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation
{
    public enum CardRank { King = 0, Queen = 1, Ace = 2, Joker = 3 }

    /// <summary>
    /// 연출이 듣는 이벤트의 단일 창구.
    /// 팀원 이벤트(DeckManager, PlayerState, HandSelection …)는 TeamEventBridge가 이쪽으로 옮겨 준다.
    /// 연출 스크립트는 이 클래스만 구독하므로, 팀 시그니처가 바뀌어도 브리지 한 곳만 고치면 된다.
    /// </summary>
    public static class PresentationEvents
    {
        /// <summary>참가 좌석(정렬됨), 내 좌석</summary>
        public static event Action<int[], int> GameEntered;
        public static event Action ReturnedToLobby;
        /// <summary>이번 라운드 테이블 카드</summary>
        public static event Action<CardRank> RoundStarted;
        /// <summary>내 손패. 상대 손패는 받지 않는다 (뒷면 5장으로 연출)</summary>
        public static event Action<CardRank[]> CardsDealt;
        public static event Action<int> TurnChanged;
        /// <summary>UI HandSelection이 보내는 선택 인덱스 (최대 3)</summary>
        public static event Action<IReadOnlyList<int>> SelectionChanged;
        /// <summary>서버가 확정한 제출. 좌석, 장수, 손패 인덱스(내 좌석일 때만, 없으면 null)</summary>
        public static event Action<int, int, int[]> CardsPlayed;
        /// <summary>고발자, 피고발자(직전 제출자)</summary>
        public static event Action<int, int> LiarCalled;
        /// <summary>피고발자, 공개 카드, 카드별 테이블 카드 일치 여부</summary>
        public static event Action<int, CardRank[], bool[]> ChallengeResolved;
        /// <summary>격발 좌석, 생존 여부</summary>
        public static event Action<int, bool> ShotResolved;
        public static event Action<int> PlayerEliminated;
        public static event Action<bool> CameraLockRequested;
        /// <summary>승자 좌석 (-1 = 없음)</summary>
        public static event Action<int> MatchEnded;

        /// <summary>좌석 → 닉네임. 브리지가 PlayerProfile 기반으로 바꿔 끼운다.</summary>
        public static Func<int, string> NicknameResolver = DefaultNickname;

        public static void RaiseGameEntered(int[] seats, int localSeat) => GameEntered?.Invoke(seats, localSeat);
        public static void RaiseReturnedToLobby() => ReturnedToLobby?.Invoke();
        public static void RaiseRoundStarted(CardRank rank) => RoundStarted?.Invoke(rank);
        public static void RaiseCardsDealt(CardRank[] myHand) => CardsDealt?.Invoke(myHand);
        public static void RaiseTurnChanged(int seat) => TurnChanged?.Invoke(seat);
        public static void RaiseSelectionChanged(IReadOnlyList<int> indices) => SelectionChanged?.Invoke(indices);
        public static void RaiseCardsPlayed(int seat, int count, int[] localIndices) => CardsPlayed?.Invoke(seat, count, localIndices);
        public static void RaiseLiarCalled(int accuser, int target) => LiarCalled?.Invoke(accuser, target);
        public static void RaiseChallengeResolved(int target, CardRank[] cards, bool[] matches) => ChallengeResolved?.Invoke(target, cards, matches);
        public static void RaiseShotResolved(int seat, bool survived) => ShotResolved?.Invoke(seat, survived);
        public static void RaisePlayerEliminated(int seat) => PlayerEliminated?.Invoke(seat);
        public static void RaiseCameraLockRequested(bool locked) => CameraLockRequested?.Invoke(locked);
        public static void RaiseMatchEnded(int winnerSeat) => MatchEnded?.Invoke(winnerSeat);

        static string DefaultNickname(int seat) => $"Player {seat + 1}";

        // Enter Play Mode 옵션에서 도메인 리로드를 끈 경우에도 이전 플레이의 구독이 남지 않게 한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            GameEntered = null; ReturnedToLobby = null; RoundStarted = null; CardsDealt = null;
            TurnChanged = null; SelectionChanged = null; CardsPlayed = null; LiarCalled = null;
            ChallengeResolved = null; ShotResolved = null; PlayerEliminated = null;
            CameraLockRequested = null; MatchEnded = null;
            NicknameResolver = DefaultNickname;
        }
    }
}
