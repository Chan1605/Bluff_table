using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    private PlayerInput inputActions;

    public override void OnStartLocalPlayer()
    {
        inputActions = new PlayerInput();
        Debug.Log("[클라이언트] 로컬 인풋 시스템 연결 완료");
    }

    public override void OnStopClient()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }
}
