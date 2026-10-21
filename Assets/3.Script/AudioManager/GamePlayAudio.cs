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

    [Header("딜링 사운드 조절")]
    [SerializeField] private float f_divideStart = 0f;
    [SerializeField] private float f_flipStart = 0f;
    [SerializeField] private float f_gether = 0f;
    [SerializeField] private float f_tableOpen = 0f;
    [SerializeField] private float f_tableClose = 0f;
    [SerializeField] private float f_dealt = 0f;

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
        Debug.Log($"[GamePlayAudio {GetInstanceID()}] DeckListShown: {b}");
        if (!b) return;

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

    public void CardFlipSound(int i, int[] card, bool[] k)
    {
        StartCoroutine(cardOpenSound_co(card.Length));
    }



    #region 연출


    private IEnumerator cardShuffleSound_co()
    {
        Debug.Log("카드 섞기 사운드 출력");
        yield return new WaitForSeconds(f_divideStart);
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);

        yield return new WaitForSeconds(f_flipStart);

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardFlip_SFX);

        yield return new WaitForSeconds(f_gether);

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardCollect_SFX);

        yield return new WaitForSeconds(f_tableOpen);

        AudioManager.Instance.PlaySFX(whistle_SFX);

        yield return new WaitForSeconds(f_tableClose);

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
        yield return new WaitForSeconds(f_dealt);
        /*
        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
        yield return new WaitForSeconds(f_dealt);

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
        yield return new WaitForSeconds(f_dealt);

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardDivide_SFX);
        */
    }

    private IEnumerator cardOpenSound_co(int cards) 
    {
        yield return 0.5; // 한번에 뒤집기
                           // 따로 뒤집을 경우 반복문 추가

        AudioManager.Instance.Play3DSFX(GetAvailablesource(), cardFlip_SFX);
    }

    #endregion

}
 
