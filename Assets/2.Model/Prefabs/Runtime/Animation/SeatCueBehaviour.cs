using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 캐릭터 Animator 상태에 진입/이탈할 때 좌석으로 보내는 신호 종류.
    /// 코드가 "언제 카메라를 돌릴지, 언제 총을 쏠지"를 타이머로 세지 않고,
    /// Animator 상태 흐름이 시점을 정한다. 클립 길이를 바꾸면 신호 시점도 자동으로 따라간다.
    /// </summary>
    public enum SeatCue
    {
        None,
        CameraFocus,     // 이 좌석에 카메라 고정 · 줌
        CameraRelease,   // 고정 해제
        HeartbeatOn,     // BGM 끊고 심장 소리
        HeartbeatOff,
        RevolverClick,   // 불발 → 리볼버 Click, 이름표 격발 +1
        RevolverFire,    // 발사 → 리볼버 Fire, 이름표 흐리게
        DeadSettled,     // 쓰러진 자세 정착 → 내 좌석이면 관전 전환
    }

    /// <summary>
    /// 캐릭터 Animator Controller의 상태에 붙인다 (에디터 빌더가 자동으로 붙임).
    /// Animator가 자식 모델에 있어도 부모의 SeatView를 찾아 전달한다.
    /// </summary>
    public class SeatCueBehaviour : StateMachineBehaviour
    {
        public SeatCue onEnter = SeatCue.None;
        public SeatCue onExit = SeatCue.None;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onEnter != SeatCue.None) Send(animator, onEnter);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (onExit != SeatCue.None) Send(animator, onExit);
        }

        static void Send(Animator animator, SeatCue cue)
        {
            var seat = animator.GetComponentInParent<SeatView>();
            if (seat != null) seat.HandleCue(cue);
        }
    }
}
