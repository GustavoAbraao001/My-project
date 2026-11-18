
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
public sealed class ProblemArea : MonoBehaviour
{
    private static readonly List<ProblemArea> areas = new List<ProblemArea>();

    public static IReadOnlyList<ProblemArea> Areas => areas;

    [Header("Detecção do bebê")]
    [SerializeField] private Transform[] babiesToWatch;
    [SerializeField] private Transform babyApproachPoint;
    [SerializeField, Min(0.1f)] private float activationRadius = 1.5f;
    [SerializeField, Min(0f)] private float delayBeforeProblem = 1.5f;

    [Header("Interação do robô")]
    [SerializeField, Min(0.1f)] private float playerInteractionRadius = 1.6f;
    [SerializeField] private Collider interactionTrigger;

    [Header("UI e visuais do problema")]
    [SerializeField] private GameObject problemPanel;
    [SerializeField] private Slider timerBar;

    [FormerlySerializedAs("alertImage")]
    [SerializeField] private Image yellowAlertImage;

    [SerializeField] private Image redAlertImage;

    [SerializeField] private GameObject activeProblemVisual;
    [SerializeField] private GameObject consequenceVisual;

    [Header("Prazo e alerta")]
    [SerializeField, Min(1f)] private float problemDuration = 18f;
    [SerializeField, Min(0.1f)] private float alertBlinksPerSecond = 2f;

    [Header("Ciclo e recuperação")]
    [Tooltip("Por quanto tempo o alerta vermelho pisca após uma falha.")]
    [SerializeField, Min(0f)] private float failedAlertDuration = 3f;

    [Tooltip("Tempo de espera antes de esta área poder gerar outro problema.")]
    [SerializeField, Min(0f)] private float retriggerCooldownSeconds = 60f;

    [Header("Sons opcionais")]
    [SerializeField] private AudioClip alertSfx;
    [SerializeField] private AudioClip resolvedSfx;
    [SerializeField] private AudioClip failedSfx;

    [Header("Diagnóstico")]
    [SerializeField] private bool showDebugLogs;

    public event Action<ProblemArea> Resolved;
    public event Action<ProblemArea> Failed;
    public event Action<ProblemArea> ProblemStarted;

    public bool IsActive { get; private set; }
    public bool HasFailed { get; private set; }

    public bool IsOnCooldown => cooldownRemaining > 0f;

    public float CooldownRemaining => cooldownRemaining;

    public float RemainingSeconds { get; private set; }

    public float NormalizedTime =>
        currentProblemDuration <= 0f
            ? 0f
            : Mathf.Clamp01(RemainingSeconds / currentProblemDuration);

    public Transform BabyApproachPoint =>
        babyApproachPoint ? babyApproachPoint : transform;

    public float DelayBeforeProblem => delayBeforeProblem;
    public float ActivationRadius => activationRadius;

    private bool activationPending;
    private bool triggeredThisVisit;

    private float currentProblemDuration;
    private float failedAlertRemaining;
    private float cooldownRemaining;

    private void OnEnable()
    {
        if (!areas.Contains(this))
            areas.Add(this);
    }

    private void Awake()
    {
        if (problemPanel)
            problemPanel.SetActive(false);

        if (activeProblemVisual)
            activeProblemVisual.SetActive(false);

        if (consequenceVisual)
            consequenceVisual.SetActive(false);

        if (interactionTrigger)
        {
            if (!interactionTrigger.isTrigger)
            {
                Debug.LogWarning(
                    $"{name}: Interaction Trigger precisa estar com Is Trigger ativado.",
                    this
                );
            }

            interactionTrigger.enabled = false;
        }

        SetAlertImages(false, false);

        if (timerBar)
        {
            timerBar.minValue = 0f;
            timerBar.maxValue = 1f;
            timerBar.interactable = false;
            timerBar.value = 1f;
            timerBar.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        if (babiesToWatch == null || babiesToWatch.Length == 0)
        {
            Debug.LogWarning(
                $"{name}: ProblemArea não tem bebês em Babies To Watch.",
                this
            );
        }
    }

    private void OnDisable()
    {
        areas.Remove(this);
        activationPending = false;
    }

    private void OnDestroy()
    {
        areas.Remove(this);
    }

    private void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = Mathf.Max(
                0f,
                cooldownRemaining - Time.deltaTime
            );
            if (cooldownRemaining <= 0f && HasFailed)
            {
                ResetAfterConsequence();
            }
        }

        bool babyNearby = IsAnyBabyInsideArea();
        if (!babyNearby &&
            !activationPending &&
            !IsActive &&
            !HasFailed &&
            !IsOnCooldown)
        {
            triggeredThisVisit = false;
        }

        if (babyNearby &&
            !triggeredThisVisit &&
            !activationPending &&
            !IsActive &&
            !HasFailed &&
            !IsOnCooldown)
        {
            triggeredThisVisit = true;
            activationPending = true;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"{name}: bebê entrou na área; aguardando {delayBeforeProblem:0.0}s.",
                    this
                );
            }

            StartCoroutine(ActivateAfterDelay());
        }
        if (IsActive)
        {
            RemainingSeconds = Mathf.Max(
                0f,
                RemainingSeconds - Time.deltaTime
            );

            if (timerBar)
                timerBar.value = NormalizedTime;

            UpdateYellowAlertBlink();

            if (RemainingSeconds <= 0f)
                ExpireProblem();

            return;
        }
        if (HasFailed && failedAlertRemaining > 0f)
        {
            UpdateRedAlertBlink();
        }
    }

    private IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeProblem);

        activationPending = false;

        if (!IsAnyBabyInsideArea() || IsOnCooldown || HasFailed)
        {
            triggeredThisVisit = false;
            yield break;
        }

        BeginProblem();
    }

    private void BeginProblem()
    {
        if (IsActive || HasFailed || IsOnCooldown)
            return;

        float difficulty = HouseholdDifficulty.Instance
            ? HouseholdDifficulty.Instance.finalDeadLineMultiplier
            : 1f;

        currentProblemDuration = problemDuration * difficulty;
        RemainingSeconds = currentProblemDuration;

        IsActive = true;

        if (problemPanel)
            problemPanel.SetActive(true);

        if (timerBar)
        {
            timerBar.gameObject.SetActive(true);
            timerBar.value = 1f;
        }

        if (interactionTrigger)
            interactionTrigger.enabled = true;

        if (activeProblemVisual)
            activeProblemVisual.SetActive(true);

        if (consequenceVisual)
            consequenceVisual.SetActive(false);

        failedAlertRemaining = 0f;

        SetAlertImages(true, false);

        if (AudioManager.Instance)
            AudioManager.Instance.PlaySfx(alertSfx);

        if (showDebugLogs)
        {
            Debug.Log(
                $"{name}: problema ativado; prazo de {currentProblemDuration:0.0}s.",
                this
            );
        }

        ProblemStarted?.Invoke(this);
    }

    private void UpdateYellowAlertBlink()
    {
        if (!yellowAlertImage)
            return;

        yellowAlertImage.enabled = ShouldShowBlinkFrame();
    }

    private void UpdateRedAlertBlink()
    {
        if (!redAlertImage)
            return;

        redAlertImage.enabled = ShouldShowBlinkFrame();

        failedAlertRemaining = Mathf.Max(
            0f,
            failedAlertRemaining - Time.deltaTime
        );

        if (failedAlertRemaining <= 0f)
        {
            HideProblemUI();
        }
    }

    private bool ShouldShowBlinkFrame()
    {
        float frequency = Mathf.Max(0.1f, alertBlinksPerSecond);

        return Mathf.Repeat(Time.time * frequency, 1f) < 0.5f;
    }

    private void SetAlertImages(bool showYellow, bool showRed)
    {
        if (yellowAlertImage)
            yellowAlertImage.enabled = showYellow;

        if (redAlertImage)
            redAlertImage.enabled = showRed;
    }

    private void HideProblemUI()
    {
        SetAlertImages(false, false);

        if (timerBar)
            timerBar.gameObject.SetActive(false);

        if (problemPanel)
            problemPanel.SetActive(false);
    }

    private void StartRetriggerCooldown(float minimumDuration = 0f)
    {
        cooldownRemaining = Mathf.Max(
            retriggerCooldownSeconds,
            minimumDuration
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"{name}: cooldown iniciado por {cooldownRemaining:0.0}s.",
                this
            );
        }
    }

    private void ExpireProblem()
    {
        if (!IsActive)
            return;

        IsActive = false;
        HasFailed = true;

        if (interactionTrigger)
            interactionTrigger.enabled = false;

        RemainingSeconds = 0f;
        if (timerBar)
        {
            timerBar.value = 0f;
            timerBar.gameObject.SetActive(false);
        }
        if (yellowAlertImage)
            yellowAlertImage.enabled = false;

        failedAlertRemaining = failedAlertDuration;

        if (problemPanel)
            problemPanel.SetActive(failedAlertDuration > 0f);

        if (redAlertImage)
            redAlertImage.enabled = failedAlertDuration > 0f;

        if (consequenceVisual)
            consequenceVisual.SetActive(true);
        StartRetriggerCooldown(failedAlertDuration);

        if (AudioManager.Instance)
            AudioManager.Instance.PlaySfx(failedSfx);

        Failed?.Invoke(this);

        if (failedAlertDuration <= 0f)
            HideProblemUI();

        if (showDebugLogs)
        {
            Debug.LogWarning(
                $"{name}: prazo esgotado; consequência ativada.",
                this
            );
        }
    }

    public void Resolve()
    {
        if (!IsActive)
            return;

        IsActive = false;

        if (interactionTrigger)
            interactionTrigger.enabled = false;

        RemainingSeconds = 0f;

        HideProblemUI();

        if (activeProblemVisual)
            activeProblemVisual.SetActive(false);

        StartRetriggerCooldown();

        if (AudioManager.Instance)
            AudioManager.Instance.PlaySfx(resolvedSfx);

        Resolved?.Invoke(this);

        if (showDebugLogs)
        {
            Debug.Log(
                $"{name}: problema resolvido; área em cooldown.",
                this
            );
        }
    }
    public void ResetAfterConsequence()
    {
        HasFailed = false;
        IsActive = false;

        if (interactionTrigger)
            interactionTrigger.enabled = false;

        RemainingSeconds = 0f;
        failedAlertRemaining = 0f;

        HideProblemUI();

        if (activeProblemVisual)
            activeProblemVisual.SetActive(false);

        if (consequenceVisual)
            consequenceVisual.SetActive(false);

        if (timerBar)
        {
            timerBar.value = 1f;
            timerBar.gameObject.SetActive(false);
        }
        triggeredThisVisit = IsAnyBabyInsideArea();

        if (showDebugLogs)
        {
            Debug.Log(
                $"{name}: consequência limpa. Cooldown restante: {cooldownRemaining:0.0}s.",
                this
            );
        }
    }

    private bool IsAnyBabyInsideArea()
    {
        if (babiesToWatch == null)
            return false;

        foreach (Transform baby in babiesToWatch)
        {
            if (!baby)
                continue;

            BabyPatrol patrol = baby.GetComponentInParent<BabyPatrol>();
            if (patrol && patrol.CurrentTaskArea != this)
                continue;

            if (IsBabyInsideArea(baby))
                return true;
        }

        return false;
    }

    public bool IsBabyInsideArea(Transform baby)
    {
        if (!baby)
            return false;

        return IsPositionInsideArea(baby.position);
    }

    public bool IsPositionInsideArea(
        Vector3 position,
        float extraRadius = 0f)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;

        float radius = activationRadius + Mathf.Max(0f, extraRadius);

        return offset.sqrMagnitude <= radius * radius;
    }

    public bool IsPlayerInsideInteractionRadius(Vector3 position)
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;

        return offset.sqrMagnitude <=
               playerInteractionRadius * playerInteractionRadius;
    }

    public bool IsPlayerInsideInteractionTrigger(Vector3 position)
    {
        if (!interactionTrigger ||
            !interactionTrigger.enabled ||
            !interactionTrigger.gameObject.activeInHierarchy)
        {
            return false;
        }

        Vector3 closestPoint = interactionTrigger.ClosestPoint(position);

        return (closestPoint - position).sqrMagnitude <= 0.0001f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsActive
            ? Color.yellow
            : HasFailed
                ? Color.red
                : IsOnCooldown
                    ? Color.cyan
                    : new Color(1f, 0.75f, 0.1f, 1f);

        DrawFloorCircle(transform.position, activationRadius);

        Gizmos.color = Color.cyan;
        DrawFloorCircle(transform.position, playerInteractionRadius);

        if (babyApproachPoint)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, babyApproachPoint.position);
            Gizmos.DrawSphere(babyApproachPoint.position, 0.12f);
        }
    }

    private static void DrawFloorCircle(Vector3 center, float radius)
    {
        const int segments = 48;

        Vector3 previous = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            Vector3 next = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
