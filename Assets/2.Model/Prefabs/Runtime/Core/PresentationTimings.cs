namespace LiarsBar.Presentation
{
    /// <summary>
    /// 연출 시간표 (초). 기획서 원칙 3 "연출 길이는 숫자로 넘긴다"의 그 숫자.
    /// - 에디터 빌더가 자리표시 클립을 이 길이로 만든다.
    /// - 메인 로직(박지은님)은 이 값만큼 다음 단계를 늦춘다. 연출 완료 신호를 기다리지 않는다.
    /// 클립을 실제 에셋으로 바꾸면서 길이가 달라지면 여기 숫자를 같이 고친다.
    /// </summary>
    public static class PresentationTimings
    {
        // 카드
        public const float CardCollect = 0.35f;
        public const float CardDealFlight = 0.35f;
        public const float CardDealStagger = 0.08f;   // 카드 사이 간격
        public const float CardPlay = 0.40f;
        public const float CardReveal = 0.50f;
        public const float TableMarkerPop = 0.40f;

        // 캐릭터 리액션
        public const float PlayCards = 0.6f;
        public const float CallLiar = 1.0f;
        public const float Accused = 1.0f;
        public const float RevealReact = 1.2f;

        // 룰렛 (기획서 45쪽 ①~④)
        public const float RoulettePickUp = 0.8f;   // ① 집기
        public const float RouletteAim = 0.6f;      // ② 겨누기
        public const float RouletteHold = 2.0f;     // ③ 정적
        public const float RouletteRelief = 1.0f;   // ④-a 불발 리액션
        public const float RoulettePutDown = 0.6f;  //       총 내려놓기
        public const float RouletteDeath = 1.5f;    // ④-b 발사 · 쓰러짐

        public const float Victory = 3.0f;          // MatchEnd 후 OnGameOver까지 (기획서 46쪽 요청)

        /// <summary>라운드 시작 → 손패 배분 완료까지</summary>
        public static float RoundIntro(int seatCount = 4, int cardsPerSeat = 5) =>
            CardCollect + TableMarkerPop + CardDealStagger * (cardsPerSeat * seatCount - 1) + CardDealFlight;

        /// <summary>ShotResolved 수신 → 다음 라운드 시작 가능까지</summary>
        public static float Roulette(bool survived) =>
            RoulettePickUp + RouletteAim + RouletteHold +
            (survived ? RouletteRelief + RoulettePutDown : RouletteDeath);

        /// <summary>ChallengeResolved 수신 → ShotResolved를 보내도 되는 시점까지</summary>
        public static float RevealToShot => CardReveal + RevealReact;
    }
}
