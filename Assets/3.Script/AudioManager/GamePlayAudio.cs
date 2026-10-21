using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayAudio : MonoBehaviour
{
    [Header("오디오 소스")]
    [SerializeField] private AudioSource audioSource;

    [Header("게임 오디오")]
    [Header("BGM")]
    [SerializeField] private AudioClip deck_BGM;

    [Header("AMB")]
    [SerializeField] private AudioClip deck_AMB;

    [Header("SFX")]
    [SerializeField] private AudioClip gameStart_SFX;

    [SerializeField] private AudioClip cardFlip_SFX;
    [SerializeField] private AudioClip cardCollect_SFX;
    [SerializeField] private AudioClip cardDivide_SFX;
    [SerializeField] private AudioClip whistle_SFX;

    [SerializeField] private AudioClip cardThrow_SFX;
    [SerializeField] private AudioClip hitTable_SFX;

    private List<AudioSource> sfxPool = new List<AudioSource>();

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

        newSource.spatialBlend = audioSource.spatialBlend;
        newSource.rolloffMode = audioSource.rolloffMode;
        newSource.minDistance = audioSource.minDistance;
        newSource.maxDistance = audioSource.maxDistance;
        newSource.volume = audioSource.volume;

        sfxPool.Add(newSource);
        return newSource;
    }
    private void OnEnable()
    {
        if(audioSource !=null)
        {
            sfxPool.Add(audioSource);
        }
    }
    private void Start()
    {
        InGameEvent.OnEntryInGame += EntrySound;
        InGameEvent.OnDeckListShown += DeckListShownSound;
        InGameEvent.OnCardsSubmitted += SubmitSound;
        InGameEvent.OnLiarCalled += PunchTableSound;
        InGameEvent.OnLiarCardsRevealed += CardFlipSound;
    }

    private void OnDisable()
    {
        InGameEvent.OnEntryInGame -= EntrySound;
        InGameEvent.OnDeckListShown -= DeckListShownSound;
        InGameEvent.OnCardsSubmitted -= SubmitSound;
        InGameEvent.OnLiarCalled -= PunchTableSound;
        InGameEvent.OnLiarCardsRevealed -= CardFlipSound;
    }

    public void EntrySound()
    {
        AudioManager.Instance.PlaySFX(gameStart_SFX);
    }

    public void DeckListShownSound(bool b)
    {
        StartCoroutine(cardShuffleSound_co());
    }

    public void SubmitSound(int seat, int targetSeat, int[] submittedCards)
    {
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardThrow_SFX);
    }

    public void PunchTableSound(int i, int j)
    {
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), hitTable_SFX);
    }

    public void CardFlipSound(int i, int[] j, bool[] k)
    {
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardFlip_SFX);
    }



    #region 연출


    private IEnumerator cardShuffleSound_co()
    {
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardThrow_SFX);

        yield return 0.5f;

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardFlip_SFX);

        yield return 0.5f;

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardCollect_SFX);

        yield return 0.5f;

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), whistle_SFX);

        yield return 2f;

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
    }

    private IEnumerator cardOpenSound_co()
    {
        yield return null;

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
    }

    #endregion

}
