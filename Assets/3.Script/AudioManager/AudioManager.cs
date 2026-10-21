using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance = null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    [SerializeField] private AudioSource bgmAudio;
    [SerializeField] private AudioSource sfxAudio;


    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmAudio == null) return;

        bgmAudio.clip = clip;
        bgmAudio.loop = true;
        bgmAudio.Play();
        
    }

    public void StopBGM()
    {
        bgmAudio.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxAudio == null) return;
        sfxAudio.PlayOneShot(clip);
    }



}
