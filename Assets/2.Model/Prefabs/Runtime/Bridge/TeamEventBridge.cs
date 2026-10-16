using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 팀원 코드의 이벤트 → PresentationEvents 변환기. 팀 시그니처가 바뀌면 이 파일만 고친다.
    ///
    /// 이벤트 표(카톡 공유본) 기준 매핑
    ///   OnEntryInGame      DeckManager.RpcEntryInGame      → GameEntered
    ///   OnReEntryInLobby   DeckManager.RpcReEntryInLobby   → ReturnedToLobby
    ///   OnRoundStarted     DeckManager.RpcStartRound       → RoundStarted
    ///   OnCardDealt        PlayerState.TargetGiveCards     → CardsDealt
    ///   OnTurnChanged      DeckManager.RpcTurnChanged      → TurnChanged
    ///   OnPlayerEliminated DeckManager.RpcAnnounceExecute  → PlayerEliminated
    ///   (UI) HandSelection.OnSelectionChanged              → SelectionChanged
    ///
    /// 표에 아직 없는 것 (연출에 필요 — 로직 담당에게 요청, 기획서 38 · 46쪽 이름 기준)
    ///   RpcPlayResolved(seat, count, slotIndices)         → CardsPlayed   ※ 서버 확정 후. UI의 OnCardsPlayed(제출 버튼)와 다르다
    ///   OnLiarCalled(accuser, target)                     → LiarCalled
    ///   RpcChallengeResolved(target, cards, matches[])    → ChallengeResolved   ※ 카드별 일치 여부 포함
    ///   RpcShotResolved(seat, survived)                   → ShotResolved        ※ 살아도 와야 한다 (지금 표는 사망만)
    ///   OnCameraLockRequested(bool)                       → CameraLockRequested
    ///   MatchEnd(winner) 후 3초 뒤 OnReEntryInLobby       → MatchEnded          ※ 승리 연출 시간 확보
    /// </summary>
    public class TeamEventBridge : MonoBehaviour
    {
        void OnEnable()
        {
            // TODO: 팀 이벤트 클래스 이름이 확정되면 주석을 풀고 맞춘다. 예)
            // GameEvents.OnEntryInGame      += HandleEntryInGame;
            // GameEvents.OnReEntryInLobby   += HandleReEntryInLobby;
            // GameEvents.OnRoundStarted     += HandleRoundStarted;
            // GameEvents.OnCardDealt        += HandleCardDealt;
            // GameEvents.OnTurnChanged      += HandleTurnChanged;
            // GameEvents.OnPlayerEliminated += HandlePlayerEliminated;
            // HandSelection.OnSelectionChanged += HandleSelectionChanged;

            // 닉네임은 서버 PlayerProfile에서 (using LiarsBar.Server;)
            // PresentationEvents.NicknameResolver = seat =>
            //     LiarsBar.Server.PlayerProfile.All.FirstOrDefault(p => p.Seat == seat)?.Nickname ?? $"Player {seat + 1}";
        }

        void OnDisable()
        {
            // 위에서 += 한 것을 그대로 -= 한다 (static 이벤트 해제 누락 → 씬 전환 후 MissingReferenceException)
        }

        // ───── 표에 있는 이벤트 ─────
        // 팀 이벤트의 인자 모양에 맞춰 시그니처를 고치면 된다.

        public void HandleEntryInGame(int[] seats, int localSeat) => PresentationEvents.RaiseGameEntered(seats, localSeat);
        public void HandleReEntryInLobby() => PresentationEvents.RaiseReturnedToLobby();
        public void HandleRoundStarted(int tableRank) => PresentationEvents.RaiseRoundStarted((CardRank)tableRank);
        public void HandleCardDealt(int[] myHandRanks)
        {
            var ranks = new CardRank[myHandRanks.Length];
            for (int i = 0; i < ranks.Length; i++) ranks[i] = (CardRank)myHandRanks[i];
            PresentationEvents.RaiseCardsDealt(ranks);
        }
        public void HandleTurnChanged(int seat) => PresentationEvents.RaiseTurnChanged(seat);
        public void HandlePlayerEliminated(int seat) => PresentationEvents.RaisePlayerEliminated(seat);
        public void HandleSelectionChanged(IReadOnlyList<int> indices) => PresentationEvents.RaiseSelectionChanged(indices);

        // ───── 요청 중인 이벤트 ─────

        public void HandlePlayResolved(int seat, int count, int[] slotIndices) => PresentationEvents.RaiseCardsPlayed(seat, count, slotIndices);
        public void HandleLiarCalled(int accuser, int target) => PresentationEvents.RaiseLiarCalled(accuser, target);
        public void HandleChallengeResolved(int target, int[] ranks, bool[] matches)
        {
            var cards = new CardRank[ranks.Length];
            for (int i = 0; i < cards.Length; i++) cards[i] = (CardRank)ranks[i];
            PresentationEvents.RaiseChallengeResolved(target, cards, matches);
        }
        public void HandleShotResolved(int seat, bool survived) => PresentationEvents.RaiseShotResolved(seat, survived);
        public void HandleCameraLock(bool locked) => PresentationEvents.RaiseCameraLockRequested(locked);
        public void HandleMatchEnd(int winnerSeat) => PresentationEvents.RaiseMatchEnded(winnerSeat);
    }
}
