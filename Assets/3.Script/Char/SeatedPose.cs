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

#if UNITY_EDITOR
    // 튜닝용: Inspector에서 Extra Offset · Seat Point를 바꾸면 바로 다시 정렬 (에디터 · 플레이 모두)
    // OnValidate 안에서는 Transform · Animator를 바로 건드리면 안 되므로 한 프레임 미룸
    void OnValidate()
    {
        if (seatPoint == null) return;
        UnityEditor.EditorApplication.delayCall -= RealignFromInspector;
        UnityEditor.EditorApplication.delayCall += RealignFromInspector;
    }

    void RealignFromInspector()
    {
        if (this == null || seatPoint == null) return;
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
        if (!Application.isPlaying && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(transform, "Seated Pose Align");
        aligned = false;
        Align();
    }
#endif

    // ---------- 좌석 정렬 ----------

    // 실행 중 좌석 지정 (PlayerVisualSetup이 내 좌석 의자의 SitPoint를 넘김)
    public void SetSeat(Transform seat)
    {
        seatPoint = seat;
        aligned = false;
        Align();
    }

    [ContextMenu("Align Now")]
    public void Align()
    {
        if (aligned || seatPoint == null) return;
        if (animator == null) animator = GetComponent<Animator>();   // 에디터(Align Now)에서는 Awake가 안 돌았음

        if (seatPoint.IsChildOf(transform))
        {
            Debug.LogError($"{name}: SeatPoint가 ModelRoot 아래에 있습니다. 의자 쪽으로 옮기세요.", this);
            return;
        }

        animator.Update(0f);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        // Extra Offset은 의자 기준(앞 = 의자 앞). 의자 회전이 0이던 테스트 씬에서 맞춘 값이 그대로 맞는다
        transform.position += (seatPoint.position + seatPoint.rotation * extraOffset) - hips.position;
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