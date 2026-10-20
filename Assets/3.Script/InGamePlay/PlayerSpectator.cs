using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;


public class PlayerSpectator : MonoBehaviour
{
    private SpectatorGroup specGroup;
    public bool IsSpectating { get; private set; }
    public void StartSpectating()
    {
        specGroup = FindAnyObjectByType<SpectatorGroup>();

        if (specGroup == null) return;

        specGroup.ActivateSpectatorMode();
        IsSpectating = true;
    }

    public void StopSpectating()
    {
        IsSpectating = false;
        if(specGroup != null)
        {
            specGroup.DeActiveSpectatorMode();
            specGroup = null;
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
