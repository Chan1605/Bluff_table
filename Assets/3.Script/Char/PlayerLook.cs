using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] float sensitivity = 0.1f;
    [SerializeField] Vector2 yawLimit = new(-70f, 70f);   
    [SerializeField] Vector2 pitchLimit = new(-40f, 40f);

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }

    private Transform head;
    private bool isApplied;
    private Quaternion lastBaseRotation;
    private Quaternion lastAppliedRotation;

    public Transform Head => head;

    void Awake()
    {
        head = animator.GetBoneTransform(HumanBodyBones.Head);
    }

    public void AddLookDelta(Vector2 delta) 
    {
        SetLookAngles(Yaw + delta.x * sensitivity,
                      Pitch - delta.y * sensitivity);
    }

    public void SetLookAngles(float yaw, float pitch)
    {
        Yaw = Mathf.Clamp(yaw, yawLimit.x, yawLimit.y);
        Pitch = Mathf.Clamp(pitch, pitchLimit.x, pitchLimit.y);
    }

    private void LateUpdate()
    {
        if (head == null) return;

        Quaternion baseRotation = (isApplied && head.rotation == lastAppliedRotation)
        ? lastBaseRotation
        : head.rotation;

        Quaternion offset = Quaternion.AngleAxis(Yaw, transform.up)
                          * Quaternion.AngleAxis(Pitch, transform.right);

        head.rotation = offset * baseRotation;

        lastBaseRotation = baseRotation;
        lastAppliedRotation = head.rotation;
        isApplied = true;
    }
}
