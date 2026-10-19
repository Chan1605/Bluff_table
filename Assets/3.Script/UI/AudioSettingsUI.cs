using UnityEngine;
using UnityEngine.UI;

// 오디오 탭: 메인 / 음악 / 대사 볼륨 (0 ~ 1)
// 메인은 AudioListener 전체 볼륨, 음악·대사는 AudioSource에 VolumeChannel을 붙이면 따라간다.
public class AudioSettingsUI : MonoBehaviour, ISettingsSection
{
    [SerializeField] private Slider mainVolume;
    [SerializeField] private Slider musicVolume;
    [SerializeField] private Slider dialogueVolume;

    public void Reload()
    {
        mainVolume.SetValueWithoutNotify(GameSettings.MainVolume);
        musicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
        dialogueVolume.SetValueWithoutNotify(GameSettings.DialogueVolume);
    }

    public void Apply()
    {
        PlayerPrefs.SetFloat(GameSettings.KeyMainVolume, mainVolume.value);
        PlayerPrefs.SetFloat(GameSettings.KeyMusicVolume, musicVolume.value);
        PlayerPrefs.SetFloat(GameSettings.KeyDialogueVolume, dialogueVolume.value);
    }

    public void ResetDefaults()
    {
        GameSettings.DeleteKeys(GameSettings.AudioKeys);
    }
}
