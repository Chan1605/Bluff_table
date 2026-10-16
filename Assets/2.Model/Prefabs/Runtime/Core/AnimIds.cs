using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// Animator 파라미터 이름(문자열)과 해시를 한곳에 모은다.
    /// 문자열은 에디터 빌더가 컨트롤러를 만들 때, 해시는 런타임 SetTrigger/SetBool에 쓴다.
    /// 이름을 바꾸면 Tools > LiarsBar > Build Presentation Animators로 컨트롤러를 다시 만든다.
    /// </summary>
    public static class AnimIds
    {
        public static class Character
        {
            public const string IsTurn = "IsTurn";          // bool  내 차례
            public const string PlayCards = "PlayCards";    // trig  카드 내기
            public const string CallLiar = "CallLiar";      // trig  LIAR 고발
            public const string Accused = "Accused";        // trig  고발당함
            public const string RevealWin = "RevealWin";    // trig  공개 결과: 살았다
            public const string RevealLose = "RevealLose";  // trig  공개 결과: 쏴야 한다
            public const string Survived = "Survived";      // bool  룰렛 결과 (Roulette보다 먼저 세팅)
            public const string Roulette = "Roulette";      // trig  룰렛 시퀀스 시작
            public const string ForceDead = "ForceDead";    // trig  끊김 등 격발 없는 탈락
            public const string Victory = "Victory";        // trig  승리
            public const string Reset = "Reset";            // trig  로비 복귀

            public static readonly int IsTurnId = Animator.StringToHash(IsTurn);
            public static readonly int PlayCardsId = Animator.StringToHash(PlayCards);
            public static readonly int CallLiarId = Animator.StringToHash(CallLiar);
            public static readonly int AccusedId = Animator.StringToHash(Accused);
            public static readonly int RevealWinId = Animator.StringToHash(RevealWin);
            public static readonly int RevealLoseId = Animator.StringToHash(RevealLose);
            public static readonly int SurvivedId = Animator.StringToHash(Survived);
            public static readonly int RouletteId = Animator.StringToHash(Roulette);
            public static readonly int ForceDeadId = Animator.StringToHash(ForceDead);
            public static readonly int VictoryId = Animator.StringToHash(Victory);
            public static readonly int ResetId = Animator.StringToHash(Reset);
        }

        public static class Card
        {
            public const string Deal = "Deal";       // trig  덱 → 손패 슬롯
            public const string Lifted = "Lifted";   // bool  선택 시 들어 올림
            public const string Play = "Play";       // trig  손패 → 중앙 원 (뒷면)
            public const string Match = "Match";     // bool  공개 결과 (Reveal보다 먼저 세팅)
            public const string Reveal = "Reveal";   // trig  뒤집기
            public const string Collect = "Collect"; // trig  테이블 → 덱 회수
            public const string Hide = "Hide";       // trig  즉시 숨김

            public static readonly int DealId = Animator.StringToHash(Deal);
            public static readonly int LiftedId = Animator.StringToHash(Lifted);
            public static readonly int PlayId = Animator.StringToHash(Play);
            public static readonly int MatchId = Animator.StringToHash(Match);
            public static readonly int RevealId = Animator.StringToHash(Reveal);
            public static readonly int CollectId = Animator.StringToHash(Collect);
            public static readonly int HideId = Animator.StringToHash(Hide);
        }

        public static class NameTag
        {
            public const string IsTurn = "IsTurn"; // bool
            public const string Dead = "Dead";     // bool
            public const string Shot = "Shot";     // trig  격발 횟수 +1 펄스

            public static readonly int IsTurnId = Animator.StringToHash(IsTurn);
            public static readonly int DeadId = Animator.StringToHash(Dead);
            public static readonly int ShotId = Animator.StringToHash(Shot);
        }

        public static class Camera
        {
            public const string Focused = "Focused"; // bool  고정 · 줌
            public const string Victory = "Victory"; // trig  승자 주위 회전
            public const string Reset = "Reset";     // trig

            public static readonly int FocusedId = Animator.StringToHash(Focused);
            public static readonly int VictoryId = Animator.StringToHash(Victory);
            public static readonly int ResetId = Animator.StringToHash(Reset);
        }

        public static class TableMarker
        {
            public const string TableRank = "TableRank";   // int   0=K 1=Q 2=A (CardRank와 같음)
            public const string RoundStart = "RoundStart"; // trig
            public const string Clear = "Clear";           // trig

            public static readonly int TableRankId = Animator.StringToHash(TableRank);
            public static readonly int RoundStartId = Animator.StringToHash(RoundStart);
            public static readonly int ClearId = Animator.StringToHash(Clear);
        }

        public static class Revolver
        {
            public const string Click = "Click"; // trig  불발
            public const string Fire = "Fire";   // trig  발사
            public const string Reset = "Reset"; // trig

            public static readonly int ClickId = Animator.StringToHash(Click);
            public static readonly int FireId = Animator.StringToHash(Fire);
            public static readonly int ResetId = Animator.StringToHash(Reset);
        }
    }
}
