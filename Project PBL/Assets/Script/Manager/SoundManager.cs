using UnityEngine;
using UnityEngine.Audio;
using Unity.Netcode;

public class SoundManager : NetworkBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    [Header("BGM Audio Clips")]
    [SerializeField] private AudioClip bgmInGameClip;

    [Header("SFX Audio Clips")]
    [SerializeField] private AudioClip sfxSleddingEffectClip;
    [SerializeField] private AudioClip sfxIntroClip;
    [SerializeField] private AudioClip sfxCameraIntroClip;
    [SerializeField] private AudioClip[] sfxUIClickClips;
    [SerializeField] private AudioClip sfxGoalClip;
    [SerializeField] private AudioClip sfxSleddinClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    public void PlayGameplayBgm()
    {
        if (bgmAudioSource == null || bgmInGameClip == null)
        {
            Debug.LogError("[SoundManager] AudioSource atau clip BGM gameplay belum diatur.", this);
            return;
        }

        if (bgmAudioSource.isPlaying && bgmAudioSource.clip == bgmInGameClip)
        {
            return;
        }

        bgmAudioSource.clip = bgmInGameClip;
        bgmAudioSource.loop = true;
        bgmAudioSource.Play();
    }

    public void PlayIntroSfx()
    {
        PlaySfx(sfxIntroClip, "intro");
    }

    public void PlaySleddingSfx()
    {
        PlaySfx(sfxSleddinClip, "sledding");
    }

    public void PlaySleddingEffectSfx()
    {
        PlaySfx(sfxSleddingEffectClip, "sleddingeffect");
    }

    public void PlayCameraIntroSfx()
    {
        PlaySfx(sfxCameraIntroClip, "camera intro");
    }

    public void PlayUIClickSfx(int index)
    {
        if (sfxUIClickClips == null || sfxUIClickClips.Length <= index || sfxUIClickClips[index] == null)
        {
            Debug.LogError($"[SoundManager] AudioSource atau clip SFX UI click index {index} belum diatur.", this);
            return;
        }

        PlaySfx(sfxUIClickClips[index], $"UI click index {index}");
    }

    public void PlayGoalSfx()
    {
        PlaySfx(sfxGoalClip, "goal");
    }

    private void PlaySfx(AudioClip clip, string clipName)
    {
        if (sfxAudioSource == null || clip == null)
        {
            Debug.LogError($"[SoundManager] AudioSource atau clip SFX {clipName} belum diatur.", this);
            return;
        }

        sfxAudioSource.PlayOneShot(clip);
    }
}
