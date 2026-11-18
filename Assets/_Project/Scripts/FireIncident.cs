
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(ProblemArea))]
public sealed class FireIncident : MonoBehaviour
{
    private static readonly List<FireIncident> incidents = new List<FireIncident>();

    public static IReadOnlyList<FireIncident> Incidents => incidents;

    [System.Serializable]
    private sealed class FireSpreadStage
    {
        [SerializeField] private GameObject visual;

        [SerializeField, Min(0f)]
        private float delayBeforeActivation = 1.5f;

        [SerializeField, Min(0)]
        private int pointsPenalty = 10;

        public GameObject Visual => visual;
        public float DelayBeforeActivation => delayBeforeActivation;
        public int PointsPenalty => pointsPenalty;
    }

    private sealed class ParticleEmissionSnapshot
    {
        public ParticleSystem System;
        public float RateOverTime;
        public float RateOverDistance;
        public float MaxLifetime;
    }

    [Header("Referência")]
    [SerializeField] private ProblemArea sourceProblem;

    [Header("Fogo inicial")]
    [SerializeField] private GameObject fireVisual;

    [SerializeField, Min(0)]
    private int initialPointsPenalty = 5;

    [Header("Propagação do fogo")]
    [SerializeField] private FireSpreadStage[] spreadStages;

    [SerializeField]
    private bool repeatLastStagePenalty = true;

    [SerializeField, Min(0.1f)]
    private float repeatedLastStagePenaltyInterval = 1.5f;

    [Header("Apagamento gradual")]
    [SerializeField, Min(0.05f)]
    private float defaultExtinguishFadeDuration = 1.5f;

    public bool IsBurning { get; private set; }
    public bool IsExtinguishing { get; private set; }

    public static bool AnyFireBurning
    {
        get
        {
            foreach (FireIncident incident in incidents)
            {
                if (incident && incident.IsBurning && !incident.IsExtinguishing)
                    return true;
            }

            return false;
        }
    }

    private int nextSpreadStageIndex;
    private Coroutine progressionRoutine;

    private void Awake()
    {
        if (!sourceProblem)
            sourceProblem = GetComponent<ProblemArea>();

        SetVisualActive(fireVisual, false);

        if (spreadStages == null)
            return;

        foreach (FireSpreadStage stage in spreadStages)
        {
            if (stage != null)
                SetVisualActive(stage.Visual, false);
        }
    }

    private void OnEnable()
    {
        if (!incidents.Contains(this))
            incidents.Add(this);

        if (sourceProblem)
            sourceProblem.Failed += OnSourceProblemFailed;

        if (IsBurning && !IsExtinguishing)
            StartProgression();
    }

    private void OnDisable()
    {
        incidents.Remove(this);

        if (sourceProblem)
            sourceProblem.Failed -= OnSourceProblemFailed;

        StopProgression();
    }

    private void OnDestroy()
    {
        incidents.Remove(this);
    }

    private void OnSourceProblemFailed(ProblemArea area)
    {
        StartFire();
    }

    [ContextMenu("Debug/Start Fire")]
    public void StartFire()
    {
        if (IsBurning)
            return;

        IsBurning = true;
        IsExtinguishing = false;
        nextSpreadStageIndex = 0;

        SetVisualActive(fireVisual, true);

        ApplyPenalty(initialPointsPenalty);

        Debug.Log($"Incêndio iniciado em {name}.", this);

        StartProgression();
    }

    private void StartProgression()
    {
        if (!isActiveAndEnabled || !IsBurning || IsExtinguishing)
            return;

        if (progressionRoutine != null)
            return;

        bool hasStages = spreadStages != null && spreadStages.Length > 0;

        if (!hasStages)
            return;

        if (nextSpreadStageIndex >= spreadStages.Length && !repeatLastStagePenalty)
            return;

        progressionRoutine = StartCoroutine(ProgressFire());
    }

    private IEnumerator ProgressFire()
    {
        while (nextSpreadStageIndex < spreadStages.Length)
        {
            FireSpreadStage stage = spreadStages[nextSpreadStageIndex];

            if (stage == null)
            {
                nextSpreadStageIndex++;
                continue;
            }

            float delay = Mathf.Max(0f, stage.DelayBeforeActivation);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (!IsBurning || IsExtinguishing)
                break;
            SetVisualActive(stage.Visual, true);

            ApplyPenalty(stage.PointsPenalty);

            nextSpreadStageIndex++;
        }
        if (IsBurning &&
            !IsExtinguishing &&
            repeatLastStagePenalty &&
            spreadStages.Length > 0)
        {
            int lastIndex = spreadStages.Length - 1;
            int lastPenalty = spreadStages[lastIndex] != null
                ? spreadStages[lastIndex].PointsPenalty
                : 0;

            while (IsBurning && !IsExtinguishing)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0.1f, repeatedLastStagePenaltyInterval));

                if (!IsBurning || IsExtinguishing)
                    break;

                ApplyPenalty(lastPenalty);
            }
        }

        progressionRoutine = null;
    }

    private void ApplyPenalty(int amount)
    {
        if (amount <= 0)
            return;

        ArcadeScoreManager scoreManager = ArcadeScoreManager.Instance;

        if (scoreManager)
        {
            scoreManager.RemovePoints(amount);
            Debug.Log($"Incêndio em {name}: -{amount} pontos.", this);
        }
        else
        {
            Debug.LogWarning(
                "FireIncident não encontrou um ArcadeScoreManager ativo.",
                this);
        }
    }
    public IEnumerator ExtinguishGradually(float fadeDuration)
    {
        if (!IsBurning || IsExtinguishing)
            yield break;

        IsExtinguishing = true;
        StopProgression();

        List<ParticleEmissionSnapshot> snapshots = CaptureParticleSystems();

        float duration = Mathf.Max(0.05f, fadeDuration);
        float elapsed = 0f;
        float longestParticleLifetime = 0f;

        foreach (ParticleEmissionSnapshot snapshot in snapshots)
        {
            longestParticleLifetime = Mathf.Max(
                longestParticleLifetime,
                snapshot.MaxLifetime);
        }
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            foreach (ParticleEmissionSnapshot snapshot in snapshots)
            {
                if (!snapshot.System)
                    continue;

                ParticleSystem.EmissionModule emission = snapshot.System.emission;

                emission.rateOverTimeMultiplier = Mathf.Lerp(
                    snapshot.RateOverTime,
                    0f,
                    normalizedTime);

                emission.rateOverDistanceMultiplier = Mathf.Lerp(
                    snapshot.RateOverDistance,
                    0f,
                    normalizedTime);
            }

            yield return null;
        }
        foreach (ParticleEmissionSnapshot snapshot in snapshots)
        {
            if (snapshot.System)
            {
                snapshot.System.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }
        if (longestParticleLifetime > 0f)
            yield return new WaitForSeconds(longestParticleLifetime);
        RestoreParticleEmission(snapshots);

        FinishExtinguish();
    }
    public bool Extinguish()
    {
        if (!IsBurning || IsExtinguishing)
            return false;

        StopProgression();

        List<ParticleEmissionSnapshot> snapshots = CaptureParticleSystems();

        foreach (ParticleEmissionSnapshot snapshot in snapshots)
        {
            if (snapshot.System)
            {
                snapshot.System.Stop(
                    false,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        RestoreParticleEmission(snapshots);
        FinishExtinguish();

        return true;
    }

    private void FinishExtinguish()
    {
        IsBurning = false;
        IsExtinguishing = false;
        nextSpreadStageIndex = 0;

        SetVisualActive(fireVisual, false);

        if (spreadStages != null)
        {
            foreach (FireSpreadStage stage in spreadStages)
            {
                if (stage != null)
                    SetVisualActive(stage.Visual, false);
            }
        }

        if (sourceProblem)
            sourceProblem.ResetAfterConsequence();

        Debug.Log($"Incêndio apagado em {name}.", this);
    }

    private List<ParticleEmissionSnapshot> CaptureParticleSystems()
    {
        List<ParticleEmissionSnapshot> snapshots =
            new List<ParticleEmissionSnapshot>();

        HashSet<ParticleSystem> collected =
            new HashSet<ParticleSystem>();

        AddParticleSystemsFromVisual(fireVisual, collected, snapshots);

        if (spreadStages != null)
        {
            foreach (FireSpreadStage stage in spreadStages)
            {
                if (stage != null)
                    AddParticleSystemsFromVisual(stage.Visual, collected, snapshots);
            }
        }

        return snapshots;
    }

    private static void AddParticleSystemsFromVisual(
        GameObject visual,
        HashSet<ParticleSystem> collected,
        List<ParticleEmissionSnapshot> snapshots)
    {
        if (!visual)
            return;

        ParticleSystem[] systems =
            visual.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            if (!system || !collected.Add(system))
                continue;

            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.MainModule main = system.main;

            ParticleEmissionSnapshot snapshot = new ParticleEmissionSnapshot
            {
                System = system,
                RateOverTime = emission.rateOverTimeMultiplier,
                RateOverDistance = emission.rateOverDistanceMultiplier,
                MaxLifetime = Mathf.Max(0f, main.startLifetime.constantMax)
            };

            snapshots.Add(snapshot);
        }
    }

    private static void RestoreParticleEmission(
        List<ParticleEmissionSnapshot> snapshots)
    {
        foreach (ParticleEmissionSnapshot snapshot in snapshots)
        {
            if (!snapshot.System)
                continue;

            ParticleSystem.EmissionModule emission = snapshot.System.emission;

            emission.rateOverTimeMultiplier = snapshot.RateOverTime;
            emission.rateOverDistanceMultiplier = snapshot.RateOverDistance;
        }
    }
    private static void SetVisualActive(GameObject visual, bool active)
    {
        if (!visual)
            return;

        visual.SetActive(active);

        if (!active)
            return;

        ParticleSystem[] systems =
            visual.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            if (system && system.gameObject.activeInHierarchy && !system.isPlaying)
                system.Play(true);
        }
    }

    private void StopProgression()
    {
        if (progressionRoutine == null)
            return;

        StopCoroutine(progressionRoutine);
        progressionRoutine = null;
    }
}
