using UnityEngine;

// 음악 · 대사용 AudioSource에 붙이면 세팅의 Music / Dialogue 볼륨을 따라간다.
// (Inspector에 넣어 둔 볼륨이 최대값, 거기에 설정 비율을 곱함)
[RequireComponent(typeof(AudioSource))]
public class VolumeChannel : MonoBehaviour
{
    public enum Channel { Music, Dialogue }

    [SerializeField] private Channel channel = Channel.Music;

    private AudioSource source;
    private float baseVolume;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        baseVolume = source.volume;
    }

    private void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    private void Apply()
    {
        float scale = channel == Channel.Music ? GameSettings.MusicVolume : GameSettings.DialogueVolume;
        source.volume = baseVolume * scale;
    }
}
