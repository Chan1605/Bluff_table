using UnityEngine;

// 대기실 씬(LobbyScene)에 두는 카메라 자리. 있으면 LobbyCameraFraming이 자동 구도 대신 이 위치 · 방향 · 화각을 쓴다.
// 원작 로비 카메라(Slot1 정면, 눈높이, 세로 FOV 60)를 그대로 재현하기 위함. 씬이 내려가면 자동 구도로 돌아간다.
public class LobbyCameraAnchor : MonoBehaviour
{
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 60f;

    public static LobbyCameraAnchor Current { get; private set; }
    public float FieldOfView => fieldOfView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Current = null;

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }
}
