using LiarsBar.Server;
using Unity.Cinemachine;
using UnityEngine;


[RequireComponent(typeof(PlayerProfile))]
public class LobbyCameraFraming : MonoBehaviour
{
    [SerializeField] private Vector3 cameraOffset = new Vector3(0.4f, 0.35f, -3.9f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0.4f, 0.05f, 0f);
    [SerializeField, Range(20f, 70f)] private float fieldOfView = 40f;
    [SerializeField] private int priority = 50;

    private PlayerProfile profile;
    private CinemachineCamera lobbyCam;

    private void Awake()
    {
        profile = GetComponent<PlayerProfile>();
    }

    private void OnEnable()
    {
        PlayerProfile.Added += OnProfileEvent;
        PlayerProfile.Changed += OnProfileEvent;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerProfile.Added -= OnProfileEvent;
        PlayerProfile.Changed -= OnProfileEvent;
        SetActive(false);
    }

    private void OnDestroy()
    {
        if (lobbyCam != null) Destroy(lobbyCam.gameObject);
    }

    private void OnProfileEvent(PlayerProfile changed)
    {
        if (changed == profile) Refresh();
    }

    private void Refresh()
    {
        bool lobby = profile.isLocalPlayer && profile.IsLoggedIn && profile.IsInRoom
                     && profile.Seat >= 0 && profile.Phase == RoomPhase.Lobby;
        SetActive(lobby);
    }

    private void LateUpdate()
    {
        if (lobbyCam != null && lobbyCam.gameObject.activeSelf) Place();
    }

    private void SetActive(bool active)
    {
        if (!active)
        {
            if (lobbyCam != null) lobbyCam.gameObject.SetActive(false);
            return;
        }

        if (lobbyCam == null) lobbyCam = CreateCamera();
        Place();
        lobbyCam.gameObject.SetActive(true);
    }

    private CinemachineCamera CreateCamera()
    {
        GameObject go = new GameObject("LobbyFramingCam (runtime)");
        go.SetActive(false);

        CinemachineCamera cam = go.AddComponent<CinemachineCamera>();
        cam.Priority.Enabled = true;
        cam.Priority.Value = priority;

        LensSettings lens = cam.Lens;
        lens.FieldOfView = fieldOfView;
        cam.Lens = lens;
        return cam;
    }

    private void Place()
    {
        Vector3 pos = transform.TransformPoint(cameraOffset);
        Vector3 look = transform.TransformPoint(lookOffset);
        lobbyCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));

        if (!Mathf.Approximately(lobbyCam.Lens.FieldOfView, fieldOfView))
        {
            LensSettings lens = lobbyCam.Lens;
            lens.FieldOfView = fieldOfView;   // 플레이 중 Inspector 조정 반영
            lobbyCam.Lens = lens;
        }
    }
}
