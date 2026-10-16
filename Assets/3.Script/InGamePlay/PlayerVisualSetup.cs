using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using LiarsBar.Server;


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
        spawnedVisuals[player] = visualInstance;

        Debug.Log($"[인게임] 캐릭터 프리펩 생성됨. ID : {characterID}");
    }

    public void ClearVisuals()
    {
        foreach (GameObject visual in spawnedVisuals.Values)
        {
            if (visual != null)
            {
                Destroy(visual);
            }

        }
            spawnedVisuals.Clear();
    }
}
