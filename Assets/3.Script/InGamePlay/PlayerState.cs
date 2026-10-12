using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerState : NetworkBehaviour
{

    [SyncVar]
    public bool isDead;

    [SyncVar]
    public bool isTurn;

    [Server]
    public void ResetState()
    {

    }
}
