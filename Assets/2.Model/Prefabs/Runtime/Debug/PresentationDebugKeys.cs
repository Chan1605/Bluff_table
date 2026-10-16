using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 네트워크 없이 한 판을 흉내 내는 디버그 키 (기획서 46쪽: 확정 전엔 디버그 키로 더미 이벤트).
    /// 빈 씬에 테이블 · 좌석 · 카메라 · Director와 함께 두고 Play.
    ///
    ///   F1  게임 입장 (4인, 내 좌석 0)
    ///   F2  라운드 시작 (테이블 카드 랜덤) + 내 손패
    ///   1~5 내 카드 선택 토글 (최대 3장)
    ///   F3  내가 선택한 카드 제출
    ///   F4  다음 상대가 2장 제출
    ///   F5  다음 사람이 LIAR 고발
    ///   F6  공개 (랜덤 진실/거짓)
    ///   F7  룰렛 — 생존 / Shift+F7 사망
    ///   F8  상대 하나 끊김 탈락
    ///   F9  승리 (생존자 중 첫 번째)
    ///   F10 로비 복귀
    ///   ←/→ 관전 대상 변경
    /// </summary>
    public class PresentationDebugKeys : MonoBehaviour
    {
        [SerializeField] PresentationDirector director;

        readonly int[] seats = { 0, 1, 2, 3 };
        readonly List<int> selection = new();
        readonly HashSet<int> dead = new();
        int turn, lastPlayer = -1, lastCount, accused = -1, shooter = -1;
        CardRank tableRank;

        void Update()
        {
            if (Pressed(Map(KeyCode.F1))) { dead.Clear(); PresentationEvents.RaiseGameEntered(seats, 0); }
            if (Pressed(Map(KeyCode.F2))) StartRound();
            for (int i = 0; i < 5; i++) if (Pressed(Map(KeyCode.Alpha1 + i))) ToggleSelect(i);
            if (Pressed(Map(KeyCode.F3))) Play(0, selection.Count, selection.ToArray());
            if (Pressed(Map(KeyCode.F4))) Play(NextAlive(turn), 2, null);
            if (Pressed(Map(KeyCode.F5)) && lastPlayer >= 0)
            {
                int accuser = NextAlive(lastPlayer);
                accused = lastPlayer;
                PresentationEvents.RaiseLiarCalled(accuser, lastPlayer);
                shooter = accuser; // 일단 고발자, F6에서 결정
            }
            if (Pressed(Map(KeyCode.F6)) && accused >= 0) Resolve();
            if (Pressed(Map(KeyCode.F7)) && shooter >= 0)
            {
                bool die = ShiftHeld();
                PresentationEvents.RaiseShotResolved(shooter, !die);
                if (die) { dead.Add(shooter); PresentationEvents.RaisePlayerEliminated(shooter); }
            }
            if (Pressed(Map(KeyCode.F8)))
            {
                int s = NextAlive(0);
                if (s != 0) { dead.Add(s); PresentationEvents.RaisePlayerEliminated(s); }
            }
            if (Pressed(Map(KeyCode.F9))) PresentationEvents.RaiseMatchEnded(NextAlive(seats.Length - 1));
            if (Pressed(Map(KeyCode.F10))) PresentationEvents.RaiseReturnedToLobby();
            if (Pressed(Map(KeyCode.LeftArrow)) && director) director.CycleSpectate(-1);
            if (Pressed(Map(KeyCode.RightArrow)) && director) director.CycleSpectate(1);
        }

        void StartRound()
        {
            tableRank = (CardRank)Random.Range(0, 3);
            PresentationEvents.RaiseRoundStarted(tableRank);
            var hand = new CardRank[5];
            for (int i = 0; i < 5; i++) hand[i] = (CardRank)Random.Range(0, 4);
            PresentationEvents.RaiseCardsDealt(hand);
            selection.Clear();
            turn = dead.Contains(0) ? NextAlive(0) : 0;
            PresentationEvents.RaiseTurnChanged(turn);
        }

        void ToggleSelect(int i)
        {
            if (selection.Contains(i)) selection.Remove(i);
            else if (selection.Count < 3) selection.Add(i);
            PresentationEvents.RaiseSelectionChanged(selection);
        }

        void Play(int seat, int count, int[] idx)
        {
            if (count <= 0) return;
            PresentationEvents.RaiseCardsPlayed(seat, count, idx);
            lastPlayer = seat; lastCount = count;
            selection.Clear();
            PresentationEvents.RaiseSelectionChanged(selection);
            turn = NextAlive(seat);
            PresentationEvents.RaiseTurnChanged(turn);
        }

        void Resolve()
        {
            var cards = new CardRank[lastCount];
            var matches = new bool[lastCount];
            bool lie = Random.value < 0.5f;
            for (int i = 0; i < lastCount; i++)
            {
                bool m = !(lie && i == 0);
                matches[i] = m;
                cards[i] = m ? tableRank : (CardRank)(((int)tableRank + 1) % 3);
            }
            PresentationEvents.RaiseChallengeResolved(accused, cards, matches);
            if (lie) shooter = accused;
            accused = -1;
        }

        int NextAlive(int from)
        {
            for (int k = 1; k <= seats.Length; k++)
            {
                int s = (from + k) % seats.Length;
                if (!dead.Contains(s)) return s;
            }
            return from;
        }

        // ───── 입력 (New Input System / 구 Input 둘 다) ─────
#if ENABLE_INPUT_SYSTEM
        static Key Map(KeyCode code) => code switch
        {
            KeyCode.F1 => UnityEngine.InputSystem.Key.F1, KeyCode.F2 => UnityEngine.InputSystem.Key.F2,
            KeyCode.F3 => UnityEngine.InputSystem.Key.F3, KeyCode.F4 => UnityEngine.InputSystem.Key.F4,
            KeyCode.F5 => UnityEngine.InputSystem.Key.F5, KeyCode.F6 => UnityEngine.InputSystem.Key.F6,
            KeyCode.F7 => UnityEngine.InputSystem.Key.F7, KeyCode.F8 => UnityEngine.InputSystem.Key.F8,
            KeyCode.F9 => UnityEngine.InputSystem.Key.F9, KeyCode.F10 => UnityEngine.InputSystem.Key.F10,
            KeyCode.Alpha1 => UnityEngine.InputSystem.Key.Digit1, KeyCode.Alpha2 => UnityEngine.InputSystem.Key.Digit2,
            KeyCode.Alpha3 => UnityEngine.InputSystem.Key.Digit3, KeyCode.Alpha4 => UnityEngine.InputSystem.Key.Digit4,
            KeyCode.Alpha5 => UnityEngine.InputSystem.Key.Digit5,
            KeyCode.LeftArrow => UnityEngine.InputSystem.Key.LeftArrow,
            KeyCode.RightArrow => UnityEngine.InputSystem.Key.RightArrow,
            _ => UnityEngine.InputSystem.Key.None,
        };
        static bool Pressed(Key k) => Keyboard.current != null && k != UnityEngine.InputSystem.Key.None && Keyboard.current[k].wasPressedThisFrame;
        static bool ShiftHeld() => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
#else
        static KeyCode Map(KeyCode code) => code;
        static bool Pressed(KeyCode k) => Input.GetKeyDown(k);
        static bool ShiftHeld() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }
}
