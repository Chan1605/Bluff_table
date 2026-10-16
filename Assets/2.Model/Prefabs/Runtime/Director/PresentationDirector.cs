using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 연출의 지휘자. PresentationEvents를 구독해서 "어느 Animator에 어떤 파라미터를 넣을지"만 정한다.
    /// 애니메이션의 길이 · 순서 · 이징은 각 Animator Controller가 갖고 있다.
    ///
    /// 코드에 남아 있는 시간 대기는 "여러 오브젝트를 순서대로" 움직일 때뿐이다
    /// (카드를 좌석마다 하나씩 날리기, 카드가 다 뒤집힌 뒤 리액션 시작). 한 오브젝트 안의 순서는 전부 Animator.
    /// </summary>
    public class PresentationDirector : MonoBehaviour
    {
        [Tooltip("물리적 자리 순서: 0 = 내 자리, 1 = 왼쪽, 2 = 정면, 3 = 오른쪽")]
        [SerializeField] SeatView[] seatViews = new SeatView[4];
        [SerializeField] TableView table;
        [SerializeField] CameraDirector cameraDirector;
        [Tooltip("CardRank 순서: K, Q, A, Joker")]
        [SerializeField] Material[] rankFaces = new Material[4];

        [Header("Sound")]
        [SerializeField] AudioSource bgm;
        [SerializeField] AudioSource heartbeat;

        readonly Dictionary<int, SeatView> bySeat = new();
        readonly List<CardView> lastPlay = new();
        readonly HashSet<int> pendingShotDeath = new();
        int localSeat = -1;
        int lastAccuser = -1;
        int spectateIndex;
        Coroutine roundRoutine;

        // ───── 구독 ─────

        void OnEnable()
        {
            PresentationEvents.GameEntered += OnGameEntered;
            PresentationEvents.ReturnedToLobby += OnReturnedToLobby;
            PresentationEvents.RoundStarted += OnRoundStarted;
            PresentationEvents.CardsDealt += OnCardsDealt;
            PresentationEvents.TurnChanged += OnTurnChanged;
            PresentationEvents.SelectionChanged += OnSelectionChanged;
            PresentationEvents.CardsPlayed += OnCardsPlayed;
            PresentationEvents.LiarCalled += OnLiarCalled;
            PresentationEvents.ChallengeResolved += OnChallengeResolved;
            PresentationEvents.ShotResolved += OnShotResolved;
            PresentationEvents.PlayerEliminated += OnPlayerEliminated;
            PresentationEvents.CameraLockRequested += OnCameraLockRequested;
            PresentationEvents.MatchEnded += OnMatchEnded;
            foreach (var s in seatViews) if (s != null) s.CueRaised += OnSeatCue;
        }

        void OnDisable()
        {
            PresentationEvents.GameEntered -= OnGameEntered;
            PresentationEvents.ReturnedToLobby -= OnReturnedToLobby;
            PresentationEvents.RoundStarted -= OnRoundStarted;
            PresentationEvents.CardsDealt -= OnCardsDealt;
            PresentationEvents.TurnChanged -= OnTurnChanged;
            PresentationEvents.SelectionChanged -= OnSelectionChanged;
            PresentationEvents.CardsPlayed -= OnCardsPlayed;
            PresentationEvents.LiarCalled -= OnLiarCalled;
            PresentationEvents.ChallengeResolved -= OnChallengeResolved;
            PresentationEvents.ShotResolved -= OnShotResolved;
            PresentationEvents.PlayerEliminated -= OnPlayerEliminated;
            PresentationEvents.CameraLockRequested -= OnCameraLockRequested;
            PresentationEvents.MatchEnded -= OnMatchEnded;
            foreach (var s in seatViews) if (s != null) s.CueRaised -= OnSeatCue;
        }

        // ───── 로비 ↔ 게임 ─────

        void OnGameEntered(int[] seats, int mySeat)
        {
            ResetEverything();
            localSeat = mySeat;
            bySeat.Clear();

            // 서버 좌석 → 물리 자리. 내 좌석이 항상 0번 자리(1인칭)에 오도록 회전시킨다.
            var taken = new bool[seatViews.Length];
            foreach (int seat in seats)
            {
                int physical = ((seat - mySeat) % seatViews.Length + seatViews.Length) % seatViews.Length;
                var view = seatViews[physical];
                view.Assign(seat, PresentationEvents.NicknameResolver(seat), seat == mySeat, rankFaces);
                bySeat[seat] = view;
                taken[physical] = true;
            }
            for (int i = 0; i < seatViews.Length; i++) if (!taken[i]) seatViews[i].Vacate();

            if (bySeat.TryGetValue(mySeat, out var me)) cameraDirector.AttachTo(me.HeadAnchor, false);
        }

        void OnReturnedToLobby() => ResetEverything();

        void ResetEverything()
        {
            StopAllCoroutines();
            roundRoutine = null;
            lastPlay.Clear();
            pendingShotDeath.Clear();
            lastAccuser = -1;
            foreach (var s in seatViews)
            {
                if (s == null) continue;
                s.Hand.HideAll();
                s.ResetAnimators();   // Rebind → 모든 Animator가 기본 상태(Idle/Hidden)로
            }
            table.ResetAll();
            cameraDirector.ResetAll();
            SetHeartbeat(false);
        }

        // ───── 라운드 ─────

        void OnRoundStarted(CardRank rank)
        {
            if (roundRoutine != null) StopCoroutine(roundRoutine);
            roundRoutine = StartCoroutine(RoundIntro(rank));
        }

        IEnumerator RoundIntro(CardRank rank)
        {
            // 1) 테이블 · 손에 남은 카드 회수
            bool anyVisible = false;
            foreach (var s in bySeat.Values)
                foreach (var c in s.Hand.Cards)
                    if (c.IsVisible) { anyVisible = true; break; }

            foreach (var s in bySeat.Values) s.Hand.CollectAll(table.DeckPoint);
            table.ClearRank();
            lastPlay.Clear();
            if (anyVisible) yield return new WaitForSeconds(PresentationTimings.CardCollect);
            table.ResetPile();

            // 2) 테이블 카드 표식 (등장 연출은 TableMarker Animator)
            table.ShowRank(rank);
            yield return new WaitForSeconds(PresentationTimings.TableMarkerPop);

            // 3) 한 장씩 좌석을 돌며 배분 (비행 곡선은 카드 Animator의 Deal 클립)
            var alive = AliveSeatsInOrder();
            var wait = new WaitForSeconds(PresentationTimings.CardDealStagger);
            for (int i = 0; i < HandView.HandSize; i++)
                foreach (var s in alive)
                {
                    s.Hand.DealOne(i, table.DeckPoint);
                    yield return wait;
                }
            roundRoutine = null;
        }

        void OnCardsDealt(CardRank[] myHand)
        {
            if (bySeat.TryGetValue(localSeat, out var me)) me.Hand.SetLocalFaces(myHand);
        }

        void OnTurnChanged(int seat)
        {
            foreach (var kv in bySeat) kv.Value.SetTurn(kv.Key == seat);
        }

        void OnSelectionChanged(IReadOnlyList<int> indices)
        {
            if (bySeat.TryGetValue(localSeat, out var me)) me.Hand.SetLifted(indices);
        }

        // ───── 제출 · 고발 · 공개 ─────

        void OnCardsPlayed(int seat, int count, int[] localIndices)
        {
            if (!bySeat.TryGetValue(seat, out var s)) return;
            s.Trigger(AnimIds.Character.PlayCardsId);

            lastPlay.Clear();
            foreach (var card in s.Hand.TakeForPlay(count, seat == localSeat ? localIndices : null))
            {
                card.PlayTo(table.NextPileSlot());
                lastPlay.Add(card);
            }
        }

        void OnLiarCalled(int accuser, int target)
        {
            lastAccuser = accuser;
            if (bySeat.TryGetValue(accuser, out var a)) a.Trigger(AnimIds.Character.CallLiarId);
            if (bySeat.TryGetValue(target, out var t)) t.Trigger(AnimIds.Character.AccusedId);
            cameraDirector.Focus(table.Center);
        }

        void OnChallengeResolved(int target, CardRank[] cards, bool[] matches)
        {
            StartCoroutine(RevealRoutine(target, cards, matches));
        }

        IEnumerator RevealRoutine(int target, CardRank[] cards, bool[] matches)
        {
            bool lie = false;
            for (int i = 0; i < lastPlay.Count; i++)
            {
                var card = lastPlay[i];
                if (cards != null && i < cards.Length && bySeat.TryGetValue(target, out var owner))
                    owner.Hand.ApplyFace(card, cards[i]);
                bool match = matches != null && i < matches.Length && matches[i];
                if (!match) lie = true;
                card.Reveal(match);
            }

            yield return new WaitForSeconds(PresentationTimings.CardReveal);

            // 누가 쏠지는 로직이 보낸 일치 여부로만 정한다 (거짓 1장 이상 → 낸 쪽, 전부 진실 → 고발자)
            int loser = lie ? target : lastAccuser;
            int winner = lie ? lastAccuser : target;
            if (bySeat.TryGetValue(loser, out var l)) l.Trigger(AnimIds.Character.RevealLoseId);
            if (bySeat.TryGetValue(winner, out var w)) w.Trigger(AnimIds.Character.RevealWinId);
            cameraDirector.Release();
        }

        // ───── 룰렛 · 탈락 · 승리 ─────

        void OnShotResolved(int seat, bool survived)
        {
            if (!bySeat.TryGetValue(seat, out var s)) return;
            if (!survived) pendingShotDeath.Add(seat);
            // 이후 순서(카메라 줌 · 심장 소리 · 격발 · 쓰러짐)는 캐릭터 Animator의 상태 신호가 이끈다
            s.PlayRoulette(survived);
        }

        void OnPlayerEliminated(int seat)
        {
            if (!bySeat.TryGetValue(seat, out var s)) return;
            if (pendingShotDeath.Remove(seat)) return;   // 룰렛 사망 → 이미 Death 시퀀스 진행 중
            s.ForceDead();                               // 끊김 등 → 바로 쓰러진 자세
            if (seat == localSeat) StartSpectate();
        }

        void OnCameraLockRequested(bool locked) => cameraDirector.SetInputLocked(locked);

        void OnMatchEnded(int winnerSeat)
        {
            SetHeartbeat(false);
            foreach (var s in bySeat.Values) s.SetTurn(false);
            if (bySeat.TryGetValue(winnerSeat, out var w))
            {
                w.Trigger(AnimIds.Character.VictoryId);
                // 내가 이겼으면 1인칭이라 바라볼 대상이 없다 → 테이블 중심을 돌며 보여준다
                cameraDirector.PlayVictory(w.IsLocal ? table.Center : w.FocusPoint);
            }
        }

        // ───── Animator 상태 신호 ─────

        void OnSeatCue(SeatView seat, SeatCue cue)
        {
            switch (cue)
            {
                case SeatCue.CameraFocus:
                    // 내 룰렛은 1인칭 그대로 줌만, 남의 룰렛은 그 사람 얼굴로 (관전 중이면 관전 대상 기준)
                    cameraDirector.Focus(seat.IsLocal ? null : seat.FocusPoint);
                    break;
                case SeatCue.CameraRelease:
                    cameraDirector.Release();
                    break;
                case SeatCue.HeartbeatOn:
                    SetHeartbeat(true);
                    break;
                case SeatCue.HeartbeatOff:
                    SetHeartbeat(false);
                    break;
                case SeatCue.DeadSettled:
                    pendingShotDeath.Remove(seat.Seat);
                    if (seat.IsLocal) StartSpectate();
                    break;
            }
        }

        // ───── 관전 ─────

        void StartSpectate()
        {
            spectateIndex = 0;
            SpectateCurrent();
        }

        /// <summary>좌/우 키 입력 → 관전 대상 변경. 입력은 UI 담당이 호출하거나 디버그 키에서 호출.</summary>
        public void CycleSpectate(int dir)
        {
            if (!cameraDirector.IsSpectating) return;
            spectateIndex += dir;
            SpectateCurrent();
        }

        void SpectateCurrent()
        {
            var alive = AliveSeatsInOrder();
            alive.RemoveAll(s => s.IsLocal);
            if (alive.Count == 0) return;
            spectateIndex = ((spectateIndex % alive.Count) + alive.Count) % alive.Count;
            cameraDirector.AttachTo(alive[spectateIndex].SpectateAnchor, true);
        }

        // ───── 유틸 ─────

        List<SeatView> AliveSeatsInOrder()
        {
            var list = new List<SeatView>();
            foreach (var s in seatViews) if (s != null && s.IsOccupied && s.IsAlive) list.Add(s);
            return list;
        }

        void SetHeartbeat(bool on)
        {
            if (heartbeat != null) { if (on) heartbeat.Play(); else heartbeat.Stop(); }
            if (bgm != null) bgm.mute = on;
        }
    }
}
