
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class AudioSettingsUI : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider ambienceSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Textos de porcentagem (opcional)")]
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text ambienceValueText;
    [SerializeField] private TMP_Text sfxValueText;

    private AudioManager audioManager;

    private void Start()
    {
        audioManager = AudioManager.Instance;

        if (!audioManager)
        {
            Debug.LogError(
                "AudioSettingsUI não encontrou o AudioManager.",
                this
            );

            enabled = false;
            return;
        }

        ConfigureSlider(
            masterSlider,
            audioManager.MasterVolume,
            OnMasterChanged,
            masterValueText
        );

        ConfigureSlider(
            musicSlider,
            audioManager.MusicVolume,
            OnMusicChanged,
            musicValueText
        );

        ConfigureSlider(
            ambienceSlider,
            audioManager.AmbienceVolume,
            OnAmbienceChanged,
            ambienceValueText
        );

        ConfigureSlider(
            sfxSlider,
            audioManager.SfxVolume,
            OnSfxChanged,
            sfxValueText
        );
    }

    private void ConfigureSlider(
        Slider slider,
        float initialValue,
        UnityAction<float> callback,
        TMP_Text valueText
    )
    {
        if (!slider)
        {
            Debug.LogWarning(
                "AudioSettingsUI: Um Slider não foi atribuído.",
                this
            );

            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = initialValue;

        slider.onValueChanged.AddListener(callback);

        UpdateValueText(valueText, initialValue);
    }

    private void OnMasterChanged(float value)
    {
        audioManager.SetMasterVolume(value);
        UpdateValueText(masterValueText, value);
    }

    private void OnMusicChanged(float value)
    {
        audioManager.SetMusicVolume(value);
        UpdateValueText(musicValueText, value);
    }

    private void OnAmbienceChanged(float value)
    {
        audioManager.SetAmbienceVolume(value);
        UpdateValueText(ambienceValueText, value);
    }

    private void OnSfxChanged(float value)
    {
        audioManager.SetSfxVolume(value);
        UpdateValueText(sfxValueText, value);
    }

    private void UpdateValueText(TMP_Text label, float value)
    {
        if (label)
            label.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnDisable()
    {
        if (audioManager)
            audioManager.SaveVolumeSettings();
    }

    private void OnDestroy()
    {
        if (masterSlider)
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);

        if (musicSlider)
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);

        if (ambienceSlider)
            ambienceSlider.onValueChanged.RemoveListener(OnAmbienceChanged);

        if (sfxSlider)
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);

        if (audioManager)
            audioManager.SaveVolumeSettings();
    }
}
