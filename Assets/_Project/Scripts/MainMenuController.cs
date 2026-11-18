using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Cena de gameplay")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    [Header("Botões opcionais")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        Time.timeScale = 1f;
        if (startButton) startButton.onClick.AddListener(StartGameplay);
        if (quitButton) quitButton.onClick.AddListener(QuitGame);
    }

    private void OnDestroy()
    {
        if (startButton) startButton.onClick.RemoveListener(StartGameplay);
        if (quitButton) quitButton.onClick.RemoveListener(QuitGame);
    }

    public void StartGameplay()
    {
        LoadScene(gameplaySceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("QuitGame funciona na build; no Editor, o jogo não fecha.", this);
    }

    private static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("O nome da cena de gameplay está vazio.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }
}
