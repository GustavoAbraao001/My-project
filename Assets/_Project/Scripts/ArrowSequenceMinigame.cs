
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ArrowSequenceMinigame : MonoBehaviour
{
    [Header("Interface")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text sequenceLabel;
    [SerializeField] private Image sharedTimerFill;

    [Header("Dificuldade")]
    [SerializeField, Min(1)] private int startingLength = 3;
    [SerializeField, Min(0.5f)] private float addArrowEverySeconds = 4f;

    [Header("Controles")]
    [SerializeField] private bool acceptArrowKeys = true;
    [SerializeField] private bool acceptWASD = false;

    [Header("Movimentação")]
    [SerializeField] private bool hideRobotMovementWhileOpen = true;
    [SerializeField] private Behaviour robotMovement;

    [Header("Animação do Robô")]
    [SerializeField] private RobotAnimatorDriver robotAnimator;

    [Header("Áudio")]
    [SerializeField] private AudioClip arrowSfx;
    [SerializeField] private AudioClip wrongArrowSfx;

    private static readonly string[] ArrowPrompts =
    {
        "\u2191",
        "\u2192",
        "\u2193",
        "\u2190"
    };

    private readonly List<int> sequence = new List<int>();

    private ProblemArea problem;
    private float nextGrowthAt;
    private int progress;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (panel)
            panel.SetActive(false);
    }

    private void Update()
    {
        if (PauseController.IsPaused)
            return;

        if (!IsOpen || !problem || !problem.IsActive)
        {
            if (IsOpen)
                Close();

            return;
        }

        if (sharedTimerFill)
            sharedTimerFill.fillAmount = problem.NormalizedTime;

        if (Time.time >= nextGrowthAt)
        {
            AddArrow();
            nextGrowthAt = Time.time + CurrentArrowInterval();
        }

        ReadInput();
    }

    public void Open(ProblemArea target)
    {
        if (!target || !target.IsActive)
            return;

        problem = target;

        sequence.Clear();
        progress = 0;
        IsOpen = true;

        if (panel)
            panel.SetActive(true);

        if (hideRobotMovementWhileOpen && robotMovement)
            robotMovement.enabled = false;

        if (robotAnimator)
            robotAnimator.BeginRepair();

        for (int i = 0; i < startingLength; i++)
            sequence.Add(Random.Range(0, 4));

        nextGrowthAt = Time.time + CurrentArrowInterval();

        RefreshLabel();

        Debug.Log("[ArrowSequenceMinigame] Minigame aberto.");
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
            return;

        if (WasDirectionPressed(Key.UpArrow, Key.W))
        {
            Submit(0);
            return;
        }

        if (WasDirectionPressed(Key.RightArrow, Key.D))
        {
            Submit(1);
            return;
        }

        if (WasDirectionPressed(Key.DownArrow, Key.S))
        {
            Submit(2);
            return;
        }

        if (WasDirectionPressed(Key.LeftArrow, Key.A))
        {
            Submit(3);
        }
    }

    private bool WasDirectionPressed(Key arrowKey, Key wasdKey)
    {
        bool arrowPressed =
            acceptArrowKeys &&
            Keyboard.current[arrowKey].wasPressedThisFrame;

        bool wasdPressed =
            acceptWASD &&
            Keyboard.current[wasdKey].wasPressedThisFrame;

        return arrowPressed || wasdPressed;
    }

    private void Submit(int value)
    {
        if (sequence.Count == 0)
            return;

        if (sequence[progress] != value)
        {
            progress = 0;

            if (AudioManager.Instance)
                AudioManager.Instance.PlaySfx(wrongArrowSfx);

            RefreshLabel();
            return;
        }

        if (AudioManager.Instance)
            AudioManager.Instance.PlaySfx(arrowSfx);

        progress++;

        if (progress >= sequence.Count)
        {
            problem.Resolve();
            Close();
            return;
        }

        RefreshLabel();
    }

    private void AddArrow()
    {
        sequence.Add(Random.Range(0, 4));
        RefreshLabel();
    }

    private float CurrentArrowInterval()
    {
        float difficulty =
            HouseholdDifficulty.Instance
                ? HouseholdDifficulty.Instance.ArrowIntervalMultiplier
                : 1f;

        return addArrowEverySeconds * difficulty;
    }

    private void RefreshLabel()
    {
        if (!sequenceLabel)
        {
            Debug.LogWarning(
                "[ArrowSequenceMinigame] SequenceLabel não foi atribuído!",
                this
            );

            return;
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < sequence.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');

            if (i == progress)
                builder.Append("<color=#FFD34E>");

            builder.Append(ArrowPrompts[sequence[i]]);

            if (i == progress)
                builder.Append("</color>");
        }

        sequenceLabel.text = builder.ToString();
        sequenceLabel.ForceMeshUpdate();
    }

    private void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        if (robotAnimator)
            robotAnimator.EndRepair();

        if (panel)
            panel.SetActive(false);

        if (hideRobotMovementWhileOpen && robotMovement)
            robotMovement.enabled = true;

        problem = null;
    }
}
