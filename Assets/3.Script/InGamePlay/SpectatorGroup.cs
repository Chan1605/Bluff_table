using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class SpectatorGroup : MonoBehaviour
{
    [SerializeField] private CinemachineCamera[] virtualCameras;

    private int currentIndex = 0;

    public void ActivateSpectatorMode()
    {
        if (virtualCameras.Length == 0) return;
        currentIndex = 0;
        UpdatePriorities();

    }

    public void ChangeCamera(int direction)
    {
        if (virtualCameras.Length == 0) return;

        currentIndex += direction;

        if (currentIndex < 0) currentIndex = virtualCameras.Length - 1;
        else if (currentIndex >= virtualCameras.Length) currentIndex = 0;

        UpdatePriorities();
    }

    public void UpdatePriorities()
    {
        for (int i = 0; i< virtualCameras.Length; i++)
        {
            virtualCameras[i].Priority = (i == currentIndex) ? 20 : 0;
        }
    }

}
