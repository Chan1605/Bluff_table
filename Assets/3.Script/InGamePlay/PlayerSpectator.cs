using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerSpectator : MonoBehaviour
{
    private SpectatorGroup specGroup;

    public void StartSpectating()
    {
        specGroup = FindAnyObjectByType<SpectatorGroup>();

        if(specGroup != null)
        {
            specGroup.ActivateSpectatorMode();
        }
    }

    public void OnSpectateInput(int direction)
    {
        if(specGroup != null)
        {
            specGroup.ChangeCamera(direction);
        }
    }
}
