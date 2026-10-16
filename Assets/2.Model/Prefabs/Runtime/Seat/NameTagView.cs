using TMPro;
using UnityEngine;

namespace LiarsBar.Presentation
{
    /// <summary>
    /// 발밑 이름표 (World Space Canvas). 닉네임 + 격발 횟수 "n|6".
    /// 하이라이트 · 흐리게 · 펄스는 전부 Animator(NameTag.controller)가 한다. 코드는 글자만 바꾼다.
    ///
    /// 계층
    ///   NameTag      ← Canvas(World) + CanvasGroup + Animator + NameTagView
    ///   ├ Highlight  ← 내 차례일 때 켜짐
    ///   └ Body       ← 격발 펄스 (scale)
    ///     ├ Name (TMP)
    ///     └ Shots (TMP)
    /// </summary>
    public class NameTagView : MonoBehaviour
    {
        public const int Chambers = 6;

        [SerializeField] Animator animator;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text shotsText;
        [Tooltip("카메라를 향해 수평으로 돌린다")]
        [SerializeField] bool billboard = true;

        int shots;

        public void Setup(string nickname)
        {
            nameText.text = nickname;
            shots = 0;
            RefreshShots();
            gameObject.SetActive(true);
        }

        public void SetTurn(bool on) => animator.SetBool(AnimIds.NameTag.IsTurnId, on);

        public void AddShot()
        {
            shots = Mathf.Min(shots + 1, Chambers);
            RefreshShots();
            animator.SetTrigger(AnimIds.NameTag.ShotId);
        }

        public void SetDead(bool dead) => animator.SetBool(AnimIds.NameTag.DeadId, dead);

        public void ResetAll()
        {
            shots = 0;
            RefreshShots();
            if (!animator.isActiveAndEnabled) return;
            animator.Rebind();
            animator.Update(0f);
        }

        void RefreshShots()
        {
            if (shotsText != null) shotsText.text = $"{shots}|{Chambers}";
        }

        void LateUpdate()
        {
            if (!billboard) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
