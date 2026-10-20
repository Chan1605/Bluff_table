using UnityEngine;

// 내 캐릭터(로컬 플레이어)의 머리를 1인칭 화면에서 숨긴다.
// 머리 뼈 스케일을 거의 0으로 줄여 코 · 귀 · 머리카락이 카메라에 걸리지 않게 하고,
// 머리 밑에 있던 1인칭 카메라는 떼어 내서 원래 머리 기준 위치 · 회전을 매 프레임 따라가게 한다 (스케일 영향 없음).
// 숨길 뼈: HeadController의 HeadPivot(목과 머리 사이) 우선 → 없으면 Humanoid Head 뼈.
// 원작 리그는 귀 · 코 일부가 Head가 아니라 HeadPivot에 붙어 있어서 HeadPivot을 줄여야 전부 사라진다.
// PlayerVisualSetup이 로컬 플레이어 캐릭터에만 붙인다.
[DefaultExecutionOrder(100)]   // HeadController(0)가 고개를 돌린 뒤에 카메라를 맞춘다
public class FirstPersonHeadHider : MonoBehaviour
{
    private const float HiddenScale = 0.0001f;
    private const int MaxRetryFrames = 30;   // Animator 초기화가 늦을 때 머리 뼈를 다시 찾는 프레임 수

    private Transform head;
    private Transform cam;
    private Vector3 camOffset;        // 머리 회전 기준 카메라 위치 (월드 단위)
    private Quaternion camRotOffset;  // 머리 회전 기준 카메라 회전
    private Vector3 originalScale;
    private bool ready;
    private int retryFrames;

    public void Init(Transform firstPersonCamera, Transform headPivot = null)
    {
        cam = firstPersonCamera;
        head = headPivot != null ? headPivot : FindHumanHead();
        TrySetup();
    }

    private Transform FindHumanHead()
    {
        var animator = GetComponent<Animator>();
        return animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    }

    private void TrySetup()
    {
        if (head == null) head = FindHumanHead();
        if (head == null || cam == null) return;

        // 스케일을 줄이기 전에, 머리 기준 카메라 자세를 기억해 둔다
        camOffset = Quaternion.Inverse(head.rotation) * (cam.position - head.position);
        camRotOffset = Quaternion.Inverse(head.rotation) * cam.rotation;
        originalScale = head.localScale;

        // 카메라가 머리 밑에 있으면 같이 작아지므로 캐릭터 루트로 옮긴다 (월드 자세 유지)
        if (cam.IsChildOf(head)) cam.SetParent(transform, true);
        ready = true;
    }

    private void LateUpdate()
    {
        if (!ready)
        {
            if (++retryFrames <= MaxRetryFrames) TrySetup();
            if (!ready && retryFrames == MaxRetryFrames)
                Debug.LogWarning($"[1인칭] {name}: 머리 뼈({(head == null ? "없음" : head.name)}) 또는 카메라({(cam == null ? "없음" : cam.name)})를 못 찾아 머리 숨김 생략", this);
            if (!ready) return;
        }

        head.localScale = originalScale * HiddenScale;   // 클립이 스케일을 쓰지 않아 한 번 줄이면 유지되지만 매 프레임 보장
        cam.SetPositionAndRotation(head.position + head.rotation * camOffset, head.rotation * camRotOffset);
    }

    private void OnDisable()
    {
        if (ready && head != null) head.localScale = originalScale;
    }
}
