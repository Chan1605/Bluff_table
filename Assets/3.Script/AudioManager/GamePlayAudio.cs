using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GamePlayAudio : MonoBehaviour
{
    [Header("오디오 소스")]
    [SerializeField] private AudioSource audoSource;

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

    #region 연출


    private IEnumerator cardShuffleSound_co()
    {
        yield return null;
    }

    private IEnumerator cardOpenSound_co()
    {
        yield return null;
    }

    #endregion

}
