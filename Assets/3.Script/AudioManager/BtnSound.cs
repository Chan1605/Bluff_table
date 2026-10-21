using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BtnSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    public void Setup(AudioClip hover, AudioClip click)
    {
        hoverSound = hover;
        clickSound = click;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(hoverSound);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clickSound);
        }
    }

}
