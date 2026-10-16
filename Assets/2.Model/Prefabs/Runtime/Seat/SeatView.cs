using System;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 테이블의 물리적 자리 하나 (내 자리 = 0, 왼쪽 · 정면 · 오른쪽).
    /// 서버 좌석 번호는 게임 시작 때 PresentationDirector가 배정한다.
    ///
    /// 이 스크립트가 하는 일은 "Animator 파라미터 세팅"과 "Animator가 보낸 신호 처리" 두 가지뿐이다.
    /// 룰렛의 집기 → 겨누기 → 정적 → 불발/발사 순서와 길이는 캐릭터 Animator Controller가 정한다.
    /// </summary>
    public class SeatView : MonoBehaviour
    {
        [Header("Animators")]
        [SerializeField] Animator character;
        [SerializeField] Animator revolver;

        [Header("Parts")]
        [SerializeField] NameTagView nameTag;
        [SerializeField] HandView hand;
        [SerializeField] GameObject characterRoot;
        [Tooltip("1인칭일 때 숨길 렌더러 (머리 등). 그림자는 남긴다.")]
        [SerializeField] Renderer[] hideInFirstPerson;

        [Header("Anchors")]
        [Tooltip("1인칭 카메라 위치 (눈높이)")]
        [SerializeField] Transform headAnchor;
        [Tooltip("다른 사람이 이 자리를 바라볼 때의 지점 (얼굴)")]
        [SerializeField] Transform focusPoint;
        [Tooltip("이 사람을 관전할 때 카메라 위치 (어깨 너머)")]
        [SerializeField] Transform spectateAnchor;

        [Header("Sound")]
        [SerializeField] AudioSource sfx;
        [SerializeField] AudioClip clickClip;
        [SerializeField] AudioClip fireClip;

        public int Seat { get; private set; } = -1;
        public bool IsLocal { get; private set; }
        public bool IsAlive { get; private set; }
        public bool IsOccupied => Seat >= 0;

        public HandView Hand => hand;
        public Transform HeadAnchor => headAnchor;
        public Transform FocusPoint => focusPoint != null ? focusPoint : transform;
        public Transform SpectateAnchor => spectateAnchor != null ? spectateAnchor : headAnchor;

        /// <summary>Animator 상태가 보낸 신호 중 좌석 밖(카메라 · 사운드)이 처리할 것</summary>
        public event Action<SeatView, SeatCue> CueRaised;

        public void Assign(int seat, string nickname, bool isLocal, Material[] faces)
        {
            Seat = seat;
            IsLocal = isLocal;
            IsAlive = true;
            if (characterRoot != null) characterRoot.SetActive(true);
            SetFirstPersonHidden(isLocal);
            hand.Setup(isLocal, faces);
            nameTag.Setup(nickname);
            // 1인칭에서 내 이름표는 보이지 않게
            nameTag.gameObject.SetActive(!isLocal);
            ResetAnimators();
        }

        /// <summary>참가하지 않은 자리 → 빈 의자</summary>
        public void Vacate()
        {
            Seat = -1;
            IsLocal = false;
            IsAlive = false;
            hand.HideAll();
            nameTag.gameObject.SetActive(false);
            if (characterRoot != null) characterRoot.SetActive(false);
        }

        // ───── 이벤트 → Animator 파라미터 ─────

        public void SetTurn(bool on)
        {
            character.SetBool(AnimIds.Character.IsTurnId, on);
            nameTag.SetTurn(on);
        }

        public void Trigger(int characterTriggerId) => character.SetTrigger(characterTriggerId);

        public void PlayRoulette(bool survived)
        {
            // 결과는 연출 전에 이미 확정 — 분기 조건을 먼저 넣고 시퀀스를 시작한다.
            character.SetBool(AnimIds.Character.SurvivedId, survived);
            character.SetTrigger(AnimIds.Character.RouletteId);
        }

        /// <summary>격발 없이 탈락 (끊김 등)</summary>
        public void ForceDead()
        {
            IsAlive = false;
            nameTag.SetDead(true);
            character.SetTrigger(AnimIds.Character.ForceDeadId);
        }

        public void ResetAnimators()
        {
            foreach (var a in new[] { character, revolver })
            {
                if (a == null || !a.isActiveAndEnabled) continue;
                a.Rebind();
                a.Update(0f);
            }
            nameTag.ResetAll();
        }

        // ───── Animator → 신호 ─────

        internal void HandleCue(SeatCue cue)
        {
            switch (cue)
            {
                case SeatCue.RevolverClick:
                    if (revolver != null) revolver.SetTrigger(AnimIds.Revolver.ClickId);
                    PlaySfx(clickClip);
                    nameTag.AddShot();
                    break;

                case SeatCue.RevolverFire:
                    if (revolver != null) revolver.SetTrigger(AnimIds.Revolver.FireId);
                    PlaySfx(fireClip);
                    nameTag.AddShot();
                    nameTag.SetDead(true);
                    IsAlive = false;
                    break;
            }
            CueRaised?.Invoke(this, cue);
        }

        void PlaySfx(AudioClip clip)
        {
            if (sfx != null && clip != null) sfx.PlayOneShot(clip);
        }

        void SetFirstPersonHidden(bool hidden)
        {
            if (hideInFirstPerson == null) return;
            foreach (var r in hideInFirstPerson)
                if (r != null)
                    r.shadowCastingMode = hidden
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
