
using UnityEngine;

public sealed class HouseholdDifficulty : MonoBehaviour
{
    public static HouseholdDifficulty Instance { get; private set; }

    [Header("Progressão")]
    [SerializeField, Min(0.1f)]
    private float rampDurationSeconds = 300f;

    [Tooltip("Quanto maior o valor, mais acelerado será o crescimento no final.")]
    [SerializeField, Range(0f, 5f)]
    private float exponentialGrowth = 2.5f;

    [Header("Multiplicadores finais")]
    [SerializeField, Range(0.2f, 1f)]
    public float finalDeadLineMultiplier = 0.65f;

    [SerializeField, Range(0.1f, 1f)]
    private float finalPatrolPausedMultiplier = 0.35f;

    [SerializeField, Range(0.2f, 1f)]
    private float finalArrowIntervalMultiplier = 0.45f;

    [SerializeField, Range(1f, 2f)]
    private float finalPatrolSpeedMultiplier = 1.3f;

    [SerializeField, Range(0.1f, 1f)]
    private float finalPatrolRetryMultiplier = 0.4f;

    private float elapsed;

    public float ElapsedSeconds => elapsed;

    public float DifficultyProgress
    {
        get
        {
            float normalizedTime = Mathf.Clamp01(
                elapsed / rampDurationSeconds
            );

            if (exponentialGrowth <= 0.001f)
                return normalizedTime;

            float numerator = Mathf.Exp(
                exponentialGrowth * normalizedTime
            ) - 1f;

            float denominator = Mathf.Exp(exponentialGrowth) - 1f;

            return Mathf.Clamp01(numerator / denominator);
        }
    }

    public float DeadLineMultiplier =>
        Mathf.Lerp(1f, finalDeadLineMultiplier, DifficultyProgress);

    public float PatrolPausedMultiplier =>
        Mathf.Lerp(1f, finalPatrolPausedMultiplier, DifficultyProgress);

    public float ArrowIntervalMultiplier =>
        Mathf.Lerp(1f, finalArrowIntervalMultiplier, DifficultyProgress);

    public float PatrolSpeedMultiplier =>
        Mathf.Lerp(1f, finalPatrolSpeedMultiplier, DifficultyProgress);

    public float PatrolRetryMultiplier =>
        Mathf.Lerp(1f, finalPatrolRetryMultiplier, DifficultyProgress);

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
    }

    public void ResetDifficulty()
    {
        elapsed = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
