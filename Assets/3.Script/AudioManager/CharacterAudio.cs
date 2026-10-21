using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAudio : MonoBehaviour
{
    [SerializeField] private AudioSource gunSource;
    [SerializeField] private AudioSource voiceSource;

    [Header("캐릭터 보이스 클립")]
    [SerializeField] private AudioClip[] emoteClips;
    [SerializeField] private AudioClip deathClip;

    [Header("총 관련 SFX 클립")]
    [SerializeField] private AudioClip gunShot;
    [SerializeField] private AudioClip gunReload;
    [SerializeField] private AudioClip bullet;
    [SerializeField] private AudioClip spin;

    [SerializeField] private AudioClip bloodSFX;


    private void Awake()
    {
        gunSource = GetComponentInChildren<AudioSource>();

        if(voiceSource == null)
        {
            voiceSource.spatialBlend = 1.0f;
        }
    }

    private void PlayGunshot()
    {
        if(gunSource != null && gunShot!= null)
        {
            gunSource.PlayOneShot(gunShot);
        }

    }
    public void PlayGunLoad()
    {
        if (gunSource != null && gunShot != null)
        {
            gunSource.PlayOneShot(gunReload);
        }
    }

    public void PlayVoice(AudioClip voiceClip)
    {
        if(voiceSource != null && voiceClip != null)
        {
            voiceSource.PlayOneShot(voiceClip);
        }
    }

    #region 연출용

    private IEnumerator gunLoadSound_co()
    {
        yield return null;
    }

    private IEnumerator gunSpinSound_co()
    {
        yield return null;
    }



    public IEnumerator UserDieSound_co()
    {
        yield return null;
    }


    #endregion
}
