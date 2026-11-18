
using System.Collections.Generic;
using UnityEngine;
public sealed class BabyTaskCoordinator : MonoBehaviour
{
    public static BabyTaskCoordinator Instance { get; private set; }

    [Header("Áreas de problema")]
    [SerializeField] private ProblemArea[] taskAreas;

    [Header("Pontos de passeio")]
    [SerializeField] private Transform[] wanderingPoints;

    [Header("Depuração")]
    [SerializeField] private bool showDebugLogs = false;

    private readonly Dictionary<ProblemArea, BabyPatrol> reservations =
        new Dictionary<ProblemArea, BabyPatrol>();

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError(
                "Há mais de um BabyTaskCoordinator ativo na cena.",
                this
            );

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryReserveTask(
        BabyPatrol requester,
        ProblemArea previousTask,
        out ProblemArea selected)
    {
        selected = null;

        if (!requester || taskAreas == null || taskAreas.Length == 0)
            return false;
        List<ProblemArea> candidates = GetAvailableTasks(
            requester,
            previousTask,
            true
        );
        if (candidates.Count == 0)
        {
            candidates = GetAvailableTasks(
                requester,
                previousTask,
                false
            );
        }

        if (candidates.Count == 0)
            return false;
        int randomIndex = Random.Range(0, candidates.Count);

        selected = candidates[randomIndex];

        reservations[selected] = requester;

        if (showDebugLogs)
        {
            Debug.Log(
                $"{requester.name} escolheu aleatoriamente a tarefa: {selected.name}.",
                this
            );
        }

        return true;
    }

    private List<ProblemArea> GetAvailableTasks(
        BabyPatrol requester,
        ProblemArea previousTask,
        bool excludePrevious)
    {
        List<ProblemArea> candidates = new List<ProblemArea>();

        foreach (ProblemArea area in taskAreas)
        {
            if (!area)
                continue;

            if (excludePrevious && area == previousTask)
                continue;
            if (area.IsActive || area.HasFailed || area.IsOnCooldown)
                continue;
            if (reservations.TryGetValue(area, out BabyPatrol owner) &&
                owner != null &&
                owner != requester)
            {
                continue;
            }

            candidates.Add(area);
        }

        return candidates;
    }

    public void ReleaseTask(BabyPatrol requester, ProblemArea area)
    {
        if (!requester || !area)
            return;

        if (reservations.TryGetValue(area, out BabyPatrol owner) &&
            owner == requester)
        {
            reservations.Remove(area);

            if (showDebugLogs)
            {
                Debug.Log(
                    $"{requester.name} liberou a tarefa {area.name}.",
                    this
                );
            }
        }
    }

    public Transform GetRandomWanderPoint(Transform previousPoint)
    {
        if (wanderingPoints == null || wanderingPoints.Length == 0)
            return null;

        List<Transform> candidates = new List<Transform>();

        foreach (Transform point in wanderingPoints)
        {
            if (!point)
                continue;

            if (point == previousPoint && wanderingPoints.Length > 1)
                continue;

            candidates.Add(point);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
