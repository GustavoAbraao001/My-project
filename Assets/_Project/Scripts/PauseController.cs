using UnityEngine;
using UnityEngine.InputSystem;
public sealed class PauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    [SerializeField] private GameObject pausePanel;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel) pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (GameFlowManager.IsGameOver) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (GameFlowManager.IsGameOver) return;
        if (IsPaused) return;
        previousTimeScale = Time.timeScale;
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void Resume()
    {
        if (GameFlowManager.IsGameOver) return;
        if (!IsPaused) return;
        Time.timeScale = previousTimeScale;
        IsPaused = false;
        if (pausePanel) pausePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (!IsPaused) return;
        Time.timeScale = previousTimeScale;
        IsPaused = false;
    }
}
