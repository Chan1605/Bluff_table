using UnityEngine;

// 내 캐릭터(로컬 플레이어)의 머리를 1인칭 화면에서 숨긴다.
// 머리 본 스케일을 거의 0으로 줄여 코 · 부리 · 머리카락이 카메라에 걸리지 않게 하고,
// 머리 밑에 있던 1인칭 카메라는 떼어 내서 원래 머리 기준 위치 · 회전을 매 프레임 따라가게 한다 (스케일 영향 없음).
// 다른 플레이어 화면에서는 이 컴포넌트가 없으므로 머리가 정상으로 보인다.
// PlayerVisualSetup이 로컬 플레이어 캐릭터에만 붙인다.
[DefaultExecutionOrder(100)]   // HeadController(0)가 고개를 돌린 뒤에 카메라를 맞춘다
public class FirstPersonHeadHider : MonoBehaviour
{
    const float HiddenScale = 0.0001f;

    Transform head;
    Transform cam;
    Vector3 camOffset;        // 머리 회전 기준 카메라 위치 (월드 단위)
    Quaternion camRotOffset;  // 머리 회전 기준 카메라 회전
    Vector3 originalScale;

    public void Init(Transform firstPersonCamera)
    {
        var animator = GetComponent<Animator>();
        head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        cam = firstPersonCamera;
        if (head == null || cam == null)
        {
            Debug.LogWarning($"[1인칭] {name}: 머리 본 또는 카메라를 못 찾아 머리 숨김 생략", this);
            enabled = false;
            return;
        }

        // 스케일을 줄이기 전에, 머리 기준 카메라 자세를 기억해 둔다
        camOffset = Quaternion.Inverse(head.rotation) * (cam.position - head.position);
        camRotOffset = Quaternion.Inverse(head.rotation) * cam.rotation;
        originalScale = head.localScale;

        // 카메라가 머리 밑에 있으면 같이 작아지므로 캐릭터 루트로 옮긴다 (월드 자세 유지)
        if (cam.IsChildOf(head)) cam.SetParent(transform, true);
    }

    void LateUpdate()
    {
        if (head == null || cam == null) return;
        head.localScale = originalScale * HiddenScale;   // Humanoid 클립은 스케일을 쓰지 않아 한 번 줄이면 유지되지만 매 프레임 보장
        cam.SetPositionAndRotation(head.position + head.rotation * camOffset, head.rotation * camRotOffset);
    }

    void OnDisable()
    {
        if (head != null) head.localScale = originalScale;
    }
}
