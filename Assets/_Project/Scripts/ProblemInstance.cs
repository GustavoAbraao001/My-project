using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ProblemInstance : MonoBehaviour
{
    private static readonly List<ProblemInstance> activeProblems = new List<ProblemInstance>();
    public static IReadOnlyList<ProblemInstance> ActiveProblems => activeProblems;
    [SerializeField, Min(1f)] private float deadlineSeconds = 18f;
    [SerializeField] private Image worldTimerFill;
    [SerializeField] private GameObject alertIcon;
    [SerializeField] private GameObject consequenceVisual;
    [SerializeField] private AudioClip alertSfx;
    [SerializeField] private AudioClip resolvedSfx;
    [SerializeField] private AudioClip failedSfx;
    private float currentDeadline;

    public event Action<ProblemInstance> Resolved;
    public event Action<ProblemInstance> Failed;
    public float RemainingSeconds { get; private set; }
    public float NormalizedTime => currentDeadline <= 0f ? 0f : Mathf.Clamp01(RemainingSeconds / currentDeadline);
    public bool IsActive { get; private set; }

    private void OnEnable() { activeProblems.Add(this); Begin(); }
    private void OnDestroy() => activeProblems.Remove(this);

    private void Update()
    {
        if (!IsActive) return;
        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        if (worldTimerFill) worldTimerFill.fillAmount = NormalizedTime;
        if (RemainingSeconds <= 0f) Expire();
    }

    public void Begin()
    {
        float difficulty = HouseholdDifficulty.Instance ? HouseholdDifficulty.Instance.finalDeadLineMultiplier : 1f;
        currentDeadline = deadlineSeconds * difficulty;
        RemainingSeconds = currentDeadline;
        IsActive = true;
        if (alertIcon) alertIcon.SetActive(true);
        if (consequenceVisual) consequenceVisual.SetActive(false);
        if (worldTimerFill) worldTimerFill.fillAmount = 1f;
        if (AudioManager.Instance) AudioManager.Instance.PlaySfx(alertSfx);
    }

    public void Resolve()
    {
        if (!IsActive) return;
        IsActive = false;
        if (alertIcon) alertIcon.SetActive(false);
        if (AudioManager.Instance) AudioManager.Instance.PlaySfx(resolvedSfx);
        Resolved?.Invoke(this);
        Destroy(gameObject);
    }

    private void Expire()
    {
        IsActive = false;
        if (alertIcon) alertIcon.SetActive(false);
        if (consequenceVisual) consequenceVisual.SetActive(true);
        if (AudioManager.Instance) AudioManager.Instance.PlaySfx(failedSfx);
        Failed?.Invoke(this);
    }
}
