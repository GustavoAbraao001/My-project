
using System;
using TMPro;
using UnityEngine;


public sealed class ArcadeScoreManager : MonoBehaviour
{
    public static ArcadeScoreManager Instance { get; private set; }

    [Header("Pontuação passiva")]
    [SerializeField, Min(0f)] private float pointsPerSecond = 5f;
    [SerializeField] private bool passiveScoringEnabled = true;

    [Header("Regras da pontuação")]
    [SerializeField] private bool allowNegativeScore = false;

    [Header("Interface")]
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private string labelPrefix = "Pontos: ";

    private double score;
    private int lastDisplayedScore = int.MinValue;

    public int CurrentScore => (int)Math.Floor(score);
    public float PointsPerSecond => pointsPerSecond;
    public bool PassiveScoringEnabled => passiveScoringEnabled;

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Há mais de um ArcadeScoreManager ativo nesta cena.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RefreshScoreLabel();
    }

    private void Update()
    {
        if (!passiveScoringEnabled || PauseController.IsPaused)
            return;

        score += pointsPerSecond * Time.deltaTime;
        RefreshScoreLabel();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddPoints(int amount)
    {
        if (amount <= 0)
            return;

        score += amount;
        RefreshScoreLabel();
    }

    public void RemovePoints(int amount)
    {
        if (amount <= 0)
            return;

        score -= amount;

        if (!allowNegativeScore)
            score = Math.Max(0d, score);

        RefreshScoreLabel();

        if (CurrentScore <= 0)
        {
            if (GameFlowManager.Instance)
            {
                GameFlowManager.Instance.TriggerGameOver();
            }
            else
            {
                Debug.LogError(
                    "A pontuação chegou a zero, mas não existe GameFlowManager ativo.",
                    this
                );
            }
        }
    }

    public void SetPassiveScoringEnabled(bool enabled)
    {
        passiveScoringEnabled = enabled;
    }

    private void RefreshScoreLabel()
    {
        int displayedScore = CurrentScore;

        if (displayedScore == lastDisplayedScore)
            return;

        lastDisplayedScore = displayedScore;

        if (scoreLabel)
            scoreLabel.text = labelPrefix + displayedScore;
    }
}
