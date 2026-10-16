using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using LiarsBar.Server;


public class PlayerVisualSetup : NetworkBehaviour
{
    [SerializeField] private List<GameObject> SittingCharacter_pref;

    [ClientRpc]
    public void RpcSpawnVisual(PlayerProfile player)
    {
        if (player == null) return;

        int characterID = player.CharacterId;

        if(characterID<0 || characterID>= SittingCharacter_pref.Count) return;

        GameObject visualInstance = Instantiate(SittingCharacter_pref[characterID], player.transform);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;

        Debug.Log($"[인게임] 캐릭터 프리펩 생성됨. ID : {characterID}");
    }


}
