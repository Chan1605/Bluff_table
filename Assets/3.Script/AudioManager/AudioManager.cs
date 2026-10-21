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
    [SerializeField] private AudioSource ambAudio;
    [SerializeField] private AudioSource sfxAudio;

    private List<AudioSource> sfxPool = new List<AudioSource>();
    private void OnEnable()
    {
        if (sfxPool != null)
        {
            sfxPool.Add(sfxAudio);
        }
    }
    private AudioSource GetAvailablesource()
    {
        foreach (AudioSource source in sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        AudioSource newSource = gameObject.AddComponent<AudioSource>();

        newSource.spatialBlend = sfxAudio.spatialBlend;
        newSource.rolloffMode = sfxAudio.rolloffMode;
        newSource.minDistance = sfxAudio.minDistance;
        newSource.maxDistance = sfxAudio.maxDistance;
        newSource.volume = sfxAudio.volume;

        sfxPool.Add(newSource);
        return newSource;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null || bgmAudio == null) return;

        bgmAudio.clip = clip;
        bgmAudio.loop = true;
        bgmAudio.Play();
        
    }

    public void PlayAMB(AudioClip clip)
    {
        if (clip == null || ambAudio == null) return;

        ambAudio.clip = clip;
        ambAudio.loop = true;
        ambAudio.Play();

    }

    public void StopBGM()
    {
        bgmAudio.Stop();
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxPool == null) return;
        GetAvailablesource().PlayOneShot(clip);
    }

    public void Play3DSFX(AudioSource source, AudioClip clip)
    {
        if (clip == null || source == null) return;
        source.PlayOneShot(clip);
        Debug.Log($"[SFX 재생] {clip.name} / Source: {source.GetInstanceID()}");
    }



}
