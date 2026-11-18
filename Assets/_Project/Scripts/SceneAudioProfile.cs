
using UnityEngine;

[CreateAssetMenu(
    menuName = "CitizenTech/Audio/Scene Audio Profile",
    fileName = "AudioProfile_"
)]
public sealed class SceneAudioProfile : ScriptableObject
{
    [Header("Cena")]
    [SerializeField] private string sceneName;

    [Header("Áudio")]
    [SerializeField] private AudioClip music;
    [SerializeField] private AudioClip ambience;

    public string SceneName => sceneName;
    public AudioClip Music => music;
    public AudioClip Ambience => ambience;
}