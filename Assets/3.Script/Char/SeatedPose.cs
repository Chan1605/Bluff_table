using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class SeatedPose : MonoBehaviour
{
    [Header("좌석 정렬")]
    [SerializeField] Transform seatPoint;          
    [SerializeField] Vector3 extraOffset;          // 캐릭터별 미세 보정

    [Header("발 IK (다리 짧은 캐릭터는 비워 두기)")]
    [SerializeField] Transform leftFootTarget, rightFootTarget;
    [SerializeField] Transform leftKneeHint, rightKneeHint;
    [Range(0, 1)] [SerializeField] float footWeight = 1f;

    Animator animator;
    bool aligned;

    void Awake() => animator = GetComponent<Animator>();

    IEnumerator Start()
    {
        yield return null;
        Align();
    }

    // ---------- 좌석 정렬 ----------

    [ContextMenu("Align Now")]
    public void Align()
    {
        if (aligned || seatPoint == null) return;

        if (seatPoint.IsChildOf(transform))
        {
            Debug.LogError($"{name}: SeatPoint가 ModelRoot 아래에 있습니다. 의자 쪽으로 옮기세요.", this);
            return;
        }

        animator.Update(0f);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        transform.position += (seatPoint.position + extraOffset) - hips.position;
        aligned = true;
    }

    // ---------- 발 IK ----------

    public void SetFootWeight(float w) => footWeight = Mathf.Clamp01(w);   // 사망 시 0

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || footWeight <= 0f) return;

        SetFoot(AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, leftFootTarget, leftKneeHint);
        SetFoot(AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, rightFootTarget, rightKneeHint);
    }

    void SetFoot(AvatarIKGoal goal, AvatarIKHint hint, Transform target, Transform knee)
    {
        if (target == null) return;

        animator.SetIKPositionWeight(goal, footWeight);
        animator.SetIKRotationWeight(goal, footWeight);
        animator.SetIKPosition(goal, target.position);
        animator.SetIKRotation(goal, target.rotation);

        if (knee != null)
        {
            animator.SetIKHintPositionWeight(hint, footWeight);
            animator.SetIKHintPosition(hint, knee.position);
        }
    }
}