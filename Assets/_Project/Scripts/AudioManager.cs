
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource[] sfxSources;

    [Header("Scene Profiles")]
    [SerializeField] private SceneAudioProfile[] sceneProfiles;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer mixer;

    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string musicVolumeParameter = "MusicVolume";
    [SerializeField] private string ambienceVolumeParameter = "AmbienceVolume";
    [SerializeField] private string sfxVolumeParameter = "SfxVolume";
    private const string MasterKey = "Audio_Master";
    private const string MusicKey = "Audio_Music";
    private const string AmbienceKey = "Audio_Ambience";
    private const string SfxKey = "Audio_SFX";

    private int nextSfxSource;
    public float MasterVolume => ReadVolume(MasterKey);
    public float MusicVolume => ReadVolume(MusicKey);
    public float AmbienceVolume => ReadVolume(AmbienceKey);
    public float SfxVolume => ReadVolume(SfxKey);

    private static float ReadVolume(string key)
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(key, 1f));
    }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplySceneProfile(SceneManager.GetActiveScene().name);
        SetMasterVolume(0.4f);
        SetMusicVolume(1f);
        SetAmbienceVolume(1f);
        SetSfxVolume(1f);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
        {
            SaveVolumeSettings();
            Instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveVolumeSettings();
    }

    private void OnApplicationQuit()
    {
        SaveVolumeSettings();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneProfile(scene.name);
    }

    private void ApplySavedVolumes()
    {
        ApplyMixerVolume(masterVolumeParameter, MasterVolume);
        ApplyMixerVolume(musicVolumeParameter, MusicVolume);
        ApplyMixerVolume(ambienceVolumeParameter, AmbienceVolume);
        ApplyMixerVolume(sfxVolumeParameter, SfxVolume);
    }

    private void ApplySceneProfile(string sceneName)
    {
        SceneAudioProfile profile = null;

        if (sceneProfiles != null)
        {
            foreach (SceneAudioProfile candidate in sceneProfiles)
            {
                if (candidate && candidate.SceneName == sceneName)
                {
                    profile = candidate;
                    break;
                }
            }
        }

        PlayMusic(profile ? profile.Music : null);
        PlayAmbience(profile ? profile.Ambience : null);
    }

    public void PlayMusic(AudioClip clip)
    {
        SetLoop(musicSource, clip);
    }

    public void PlayAmbience(AudioClip clip)
    {
        SetLoop(ambienceSource, clip);
    }

    public void SetMasterVolume(float value)
    {
        SetVolume(masterVolumeParameter, MasterKey, value);
    }

    public void SetMusicVolume(float value)
    {
        SetVolume(musicVolumeParameter, MusicKey, value);
    }

    public void SetAmbienceVolume(float value)
    {
        SetVolume(ambienceVolumeParameter, AmbienceKey, value);
    }

    public void SetSfxVolume(float value)
    {
        SetVolume(sfxVolumeParameter, SfxKey, value);
    }

    private void SetVolume(string parameter, string key, float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        PlayerPrefs.SetFloat(key, normalized);

        ApplyMixerVolume(parameter, normalized);
    }

    private void ApplyMixerVolume(string parameter, float normalized)
    {
        if (!mixer || string.IsNullOrEmpty(parameter))
            return;

        normalized = Mathf.Clamp01(normalized);

        float decibels = normalized <= 0.0001f
            ? -80f
            : Mathf.Log10(normalized) * 20f;

        if (!mixer.SetFloat(parameter, decibels))
        {
            Debug.LogWarning(
                $"AudioManager: Não foi possível aplicar o parâmetro '{parameter}'. Confira o AudioMixer.",
                this
            );
        }
    }

    public void SaveVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (!clip || sfxSources == null || sfxSources.Length == 0)
            return;

        for (int i = 0; i < sfxSources.Length; i++)
        {
            int index = (nextSfxSource + i) % sfxSources.Length;

            AudioSource source = sfxSources[index];

            if (!source || source.isPlaying)
                continue;

            source.PlayOneShot(clip, Mathf.Clamp01(volume));

            nextSfxSource = (index + 1) % sfxSources.Length;
            return;
        }
        AudioSource fallback = sfxSources[nextSfxSource];

        if (fallback)
        {
            fallback.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        nextSfxSource = (nextSfxSource + 1) % sfxSources.Length;
    }

    private static void SetLoop(AudioSource source, AudioClip clip)
    {
        if (!source || (source.clip == clip && source.isPlaying))
            return;

        source.Stop();
        source.clip = clip;
        source.loop = true;

        if (clip)
            source.Play();
    }
}
