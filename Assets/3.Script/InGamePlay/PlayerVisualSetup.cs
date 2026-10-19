using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using LiarsBar.Server;
using Unity.Cinemachine;


public class PlayerVisualSetup : NetworkBehaviour
{
    [SerializeField] private List<GameObject> SittingCharacter_pref;

    private readonly Dictionary<PlayerProfile, GameObject> spawnedVisuals = new();

    public override void OnStartClient()
    {
        base.OnStartClient();
        PlayerProfile.LocalPhaseChanged += OnPhaseChanged;
    }

    public override void OnStopClient()
    {
        PlayerProfile.LocalPhaseChanged -= OnPhaseChanged;
        ClearVisuals();
        base.OnStopClient();
    }

    private void OnPhaseChanged(RoomPhase phase)
    {
        if (phase == RoomPhase.Lobby)
        {
            ClearVisuals();
        }
    }

    [ClientRpc]
    public void RpcSpawnVisual(PlayerProfile player, int characterID)
    {
        if (player == null) return;
        if(characterID<0 ||
                        SittingCharacter_pref == null ||
            characterID >= SittingCharacter_pref.Count || 
            SittingCharacter_pref[characterID] ==null) return;

        if (spawnedVisuals.TryGetValue(player, out GameObject oldVisual))
        {
            if (oldVisual != null)
            {
                Destroy(oldVisual);
            }

        }

        GameObject visualInstance = Instantiate(SittingCharacter_pref[characterID], player.transform);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;

        // 남의 캐릭터에 달린 1인칭 카메라는 끈다 (HeadController가 없는 프리팹이어도 화면을 뺏기지 않게)
        foreach (CinemachineCamera cam in visualInstance.GetComponentsInChildren<CinemachineCamera>(true))
            cam.enabled = player.isLocalPlayer;

        if (player.TryGetComponent<PlayerState>(out PlayerState pState))
        {
            // 프리팹마다 HeadController가 루트 또는 머리 카메라 오브젝트에 붙어 있어서 자식까지 찾는다
            pState.AssignHeadController(visualInstance.GetComponentInChildren<HeadController>(true));
        }

        spawnedVisuals[player] = visualInstance;
        SetPlaceholderHidden(player, true);

        if (visualInstance.TryGetComponent(out SeatedPose seated))
            StartCoroutine(AlignToChair(player, seated));
        Debug.Log($"[인게임] 캐릭터 프리펩 생성됨. ID : {characterID}");
    }

    public void ClearVisuals()
    {
        foreach (KeyValuePair<PlayerProfile, GameObject> pair in spawnedVisuals)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value);
            }

            SetPlaceholderHidden(pair.Key, false);
        }
            spawnedVisuals.Clear();
    }

    // 테이블 씬이 로드되고 플레이어가 좌석으로 옮겨진 뒤, 내 의자의 SitPoint에 엉덩이를 맞춘다
    // (캐릭터 생성 RPC가 테이블 씬 로드보다 먼저 올 수 있어서 의자가 생길 때까지 기다림)
    private const float ChairWaitSeconds = 5f;
    private const string SitPointName = "SitPoint";

    private IEnumerator AlignToChair(PlayerProfile player, SeatedPose seated)
    {
        float giveUpTime = Time.time + ChairWaitSeconds;
        Transform chair = null;

        while (seated != null && player != null && Time.time < giveUpTime)
        {
            chair = SeatAnchors.Get(SeatArea.Table, player.Seat);
            if (chair != null) break;
            yield return null;
        }

        if (seated == null || player == null) yield break;
        if (chair == null)
        {
            Debug.LogWarning($"[인게임] 좌석 {player.Seat} 의자를 못 찾아 정렬 생략", seated);
            yield break;
        }

        yield return null;   // 의자 등록 직후 PlayerProfile이 좌석으로 옮긴 다음 프레임에 정렬
        if (seated == null) yield break;

        Transform sitPoint = chair.Find(SitPointName);
        seated.SetSeat(sitPoint != null ? sitPoint : chair);
    }

    // Player 프리팹 자체의 캡슐(자리 표시용)은 캐릭터가 붙어 있는 동안 숨긴다.
    // enabled는 PlayerProfile이 방 밖 숨김에 쓰므로 건드리지 않고 forceRenderingOff만 바꾼다.
    private static void SetPlaceholderHidden(PlayerProfile player, bool hidden)
    {
        if (player != null && player.TryGetComponent(out MeshRenderer capsule))
            capsule.forceRenderingOff = hidden;
    }
}
