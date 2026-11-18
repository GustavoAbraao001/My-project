
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
[RequireComponent(typeof(NavMeshAgent))]
public sealed class BabyPatrol : MonoBehaviour
{
    [Header("Coordenação")]
    [SerializeField] private BabyTaskCoordinator coordinator;

    [Header("Passeio entre tarefas")]
    [SerializeField, Min(0)] private int minRoamStopsBetweenTasks = 1;
    [SerializeField, Min(0)] private int maxRoamStopsBetweenTasks = 3;

    [Tooltip("Quantidade mínima de passeios quando a dificuldade estiver no máximo.")]
    [SerializeField, Min(0)] private int finalMinRoamStops = 0;

    [Tooltip("Quantidade máxima de passeios quando a dificuldade estiver no máximo.")]
    [SerializeField, Min(0)] private int finalMaxRoamStops = 1;

    [SerializeField, Min(0f)] private float minPauseAtRoamPoint = 0.5f;
    [SerializeField, Min(0f)] private float maxPauseAtRoamPoint = 2f;

    [Header("Chegada e limites")]
    [SerializeField, Min(0.1f)] private float arrivalDistance = 0.25f;
    [SerializeField, Min(1f)] private float maxTravelSeconds = 15f;
    [SerializeField, Min(0.1f)] private float retryDelaySeconds = 1f;
    [SerializeField, Min(0f)] private float waitAfterProblemStarts = 0.5f;
    [SerializeField, Min(0.1f)] private float problemStartGraceSeconds = 3f;

    [Header("Colisões")]
    [Tooltip("Desativa todos os Colliders deste NPC e dos seus filhos. Inclui Trigger Colliders.")]
    [SerializeField] private bool disableAllChildColliders = true;

    [Tooltip("Desativa a evasão entre NavMeshAgents.")]
    [SerializeField] private bool disableAgentAvoidance = true;

    [Header("Depuração")]
    [SerializeField] private bool showDebugLogs = false;

	[Header("Animação")]
	[SerializeField] private Animator characterAnimator;

	[SerializeField] private string movementParameter = "MoveSpeed";
	[SerializeField] private string problemParameter = "IsCausingProblem";

	[SerializeField, Min(0f)] private float animationDampTime = 0.1f;

	private int movementHash;
	private int problemHash;

	private bool hasMovementParameter;
	private bool hasProblemParameter;

	public ProblemArea CurrentTaskArea { get; private set; }

    private NavMeshAgent agent;
    private ProblemArea previousTask;
    private Transform previousRoamPoint;
    private Coroutine patrolRoutine;
    private bool hasStarted;

    private float baseAgentSpeed;

    private ProblemArea subscribedProblem;
    private Action<ProblemArea> problemStartedHandler;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        baseAgentSpeed = agent.speed;

		if (!characterAnimator)
			characterAnimator = GetComponentInChildren<Animator>(true);

		CacheAnimationParameters();

		ConfigureCollisions();
        ConfigureAgentAvoidance();
    }

    private void Start()
    {
        if (!coordinator)
            coordinator = BabyTaskCoordinator.Instance;

        if (!coordinator)
        {
            Debug.LogError(
                $"{name}: nenhum BabyTaskCoordinator foi atribuído ou encontrado na cena.",
                this
            );

            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                $"{name}: o NavMeshAgent começou fora do NavMesh.",
                this
            );
        }

        hasStarted = true;

        ApplyDifficultySettings();
        patrolRoutine = StartCoroutine(PatrolLoop());
    }

    private void Update()
    {
        ApplyDifficultySettings();
		UpdateAnimation();
	}

    private void OnEnable()
    {
        if (hasStarted && coordinator && patrolRoutine == null)
            patrolRoutine = StartCoroutine(PatrolLoop());
    }

    private void OnDisable()
    {
        UnsubscribeProblemStarted();

        if (patrolRoutine != null)
        {
            StopCoroutine(patrolRoutine);
            patrolRoutine = null;
        }

        if (coordinator && CurrentTaskArea)
            coordinator.ReleaseTask(this, CurrentTaskArea);

        CurrentTaskArea = null;

        if (agent && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void ConfigureCollisions()
    {
        if (!disableAllChildColliders)
            return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        foreach (Collider item in colliders)
        {
            if (item)
                item.enabled = false;
        }
    }

	private void CacheAnimationParameters()
	{
		if (!characterAnimator)
		{
			Debug.LogWarning(
				$"{name}: Animator do NPC não foi atribuído.",
				this
			);

			return;
		}

		movementHash = Animator.StringToHash(movementParameter);
		problemHash = Animator.StringToHash(problemParameter);

		foreach (AnimatorControllerParameter parameter in characterAnimator.parameters)
		{
			if (parameter.nameHash == movementHash &&
				parameter.type == AnimatorControllerParameterType.Float)
			{
				hasMovementParameter = true;
			}

			if (parameter.nameHash == problemHash &&
				parameter.type == AnimatorControllerParameterType.Bool)
			{
				hasProblemParameter = true;
			}
		}
	}

	private void UpdateAnimation()
	{
		if (!characterAnimator)
			return;

		if (hasMovementParameter)
		{
			float movementSpeed = agent.isStopped
				? 0f
				: agent.velocity.magnitude;

			characterAnimator.SetFloat(
				movementHash,
				movementSpeed,
				animationDampTime,
				Time.deltaTime
			);
		}
	}

	private void SetCausingProblem(bool value)
	{
		if (characterAnimator && hasProblemParameter)
		{
			characterAnimator.SetBool(problemHash, value);
		}
	}
	private void ConfigureAgentAvoidance()
    {
        if (!disableAgentAvoidance || !agent)
            return;

        agent.obstacleAvoidanceType =
            ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    private void ApplyDifficultySettings()
    {
        if (!agent)
            return;

        float speedMultiplier = HouseholdDifficulty.Instance
            ? HouseholdDifficulty.Instance.PatrolSpeedMultiplier
            : 1f;

        agent.speed = baseAgentSpeed * speedMultiplier;
    }

    private float GetDifficultyProgress()
    {
        return HouseholdDifficulty.Instance
            ? HouseholdDifficulty.Instance.DifficultyProgress
            : 0f;
    }

    private float GetPauseMultiplier()
    {
        return HouseholdDifficulty.Instance
            ? HouseholdDifficulty.Instance.PatrolPausedMultiplier
            : 1f;
    }

    private float GetRetryMultiplier()
    {
        return HouseholdDifficulty.Instance
            ? HouseholdDifficulty.Instance.PatrolRetryMultiplier
            : 1f;
    }

    private IEnumerator PatrolLoop()
    {
        while (true)
        {
            if (!coordinator.TryReserveTask(
                this,
                previousTask,
                out ProblemArea task))
            {
                if (showDebugLogs)
                {
                    Debug.Log(
                        $"{name}: sem tarefa livre; vai passear e tentar novamente.",
                        this
                    );
                }

                yield return RoamOnce();

                float retryDelay = retryDelaySeconds * GetRetryMultiplier();

                if (retryDelay > 0f)
                    yield return new WaitForSeconds(retryDelay);

                continue;
            }

            CurrentTaskArea = task;

            bool reachedTask = false;

            yield return TravelTo(
                task.BabyApproachPoint,
                result => reachedTask = result
            );

            if (!reachedTask)
            {
                previousTask = task;

                coordinator.ReleaseTask(this, task);
                CurrentTaskArea = null;

                yield return RoamOnce();

                float retryDelay = retryDelaySeconds * GetRetryMultiplier();

                if (retryDelay > 0f)
                    yield return new WaitForSeconds(retryDelay);

                continue;
            }

            agent.isStopped = true;

			SetCausingProblem(true);

			if (showDebugLogs)
            {
                Debug.Log(
                    $"{name}: chegou à área {task.name}; aguardando o problema.",
                    this
                );
            }

            bool problemStarted = task.IsActive;

            subscribedProblem = task;

            problemStartedHandler = area =>
            {
                if (area == task)
                    problemStarted = true;
            };

            task.ProblemStarted += problemStartedHandler;

            float waitLimit = Time.time +
                Mathf.Max(problemStartGraceSeconds, task.DelayBeforeProblem + 1f);

            while (!problemStarted &&
                   !task.IsActive &&
                   Time.time < waitLimit)
            {
                yield return null;
            }

            UnsubscribeProblemStarted();

            if (problemStarted || task.IsActive)
            {
                previousTask = task;

                if (showDebugLogs)
                {
                    Debug.Log(
                        $"{name}: o problema {task.name} começou.",
                        this
                    );
                }

                float waitAfterStart =
                    waitAfterProblemStarts * GetPauseMultiplier();

                if (waitAfterStart > 0f)
                    yield return new WaitForSeconds(waitAfterStart);
            }
            else
            {
                previousTask = task;

                if (showDebugLogs)
                {
                    Debug.LogWarning(
                        $"{name}: não recebeu o início de {task.name}. " +
                        "Confira Babies To Watch, Activation Radius e Baby Approach Point.",
                        this
                    );
                }
            }

            coordinator.ReleaseTask(this, task);
            CurrentTaskArea = null;

			SetCausingProblem(false);

			int roamStops = GetCurrentRoamStopCount();

            if (showDebugLogs)
            {
                Debug.Log(
                    $"{name}: fará {roamStops} parada(s) antes de procurar outra tarefa.",
                    this
                );
            }

            for (int i = 0; i < roamStops; i++)
                yield return RoamOnce();
        }
    }

    private int GetCurrentRoamStopCount()
    {
        float progress = GetDifficultyProgress();

        int minStops = Mathf.RoundToInt(
            Mathf.Lerp(
                minRoamStopsBetweenTasks,
                finalMinRoamStops,
                progress
            )
        );

        int maxStops = Mathf.RoundToInt(
            Mathf.Lerp(
                maxRoamStopsBetweenTasks,
                finalMaxRoamStops,
                progress
            )
        );

        minStops = Mathf.Max(0, minStops);
        maxStops = Mathf.Max(minStops, maxStops);

        return UnityEngine.Random.Range(minStops, maxStops + 1);
    }

    private IEnumerator RoamOnce()
    {
        Transform roamPoint = coordinator.GetRandomWanderPoint(
            previousRoamPoint
        );

        if (!roamPoint)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    $"{name}: não existem pontos de passeio válidos.",
                    this
                );
            }

            yield break;
        }

        bool arrived = false;

        yield return TravelTo(
            roamPoint,
            result => arrived = result
        );

        if (!arrived)
            yield break;

        previousRoamPoint = roamPoint;

        agent.isStopped = true;

        float basePause = UnityEngine.Random.Range(
            minPauseAtRoamPoint,
            Mathf.Max(minPauseAtRoamPoint, maxPauseAtRoamPoint)
        );

        float pause = basePause * GetPauseMultiplier();

        if (showDebugLogs)
        {
            Debug.Log(
                $"{name}: pausa de passeio de {pause:0.0}s.",
                this
            );
        }

        if (pause > 0f)
            yield return new WaitForSeconds(pause);
    }

    private IEnumerator TravelTo(
        Transform destination,
        Action<bool> completed)
    {
        if (!destination)
        {
            Debug.LogWarning(
                $"{name}: destino de patrulha vazio.",
                this
            );

            completed(false);
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning(
                $"{name}: não está sobre o NavMesh.",
                this
            );

            completed(false);
            yield break;
        }

        agent.isStopped = false;
        agent.stoppingDistance = arrivalDistance;
        agent.ResetPath();

        if (!agent.SetDestination(destination.position))
        {
            Debug.LogWarning(
                $"{name}: NavMeshAgent recusou o destino {destination.name}.",
                this
            );

            completed(false);
            yield break;
        }

        float deadline = Time.time + maxTravelSeconds;
        bool reached = false;

        while (Time.time < deadline)
        {
            if (agent.pathPending)
            {
                yield return null;
                continue;
            }

            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning(
                    $"{name}: caminho até {destination.name} não é completo " +
                    $"({agent.pathStatus}).",
                    this
                );

                break;
            }

            if (agent.remainingDistance <= arrivalDistance)
            {
                reached = true;
                break;
            }

            if (!agent.hasPath)
            {
                Debug.LogWarning(
                    $"{name}: perdeu o caminho até {destination.name}.",
                    this
                );

                break;
            }

            yield return null;
        }

        if (!reached)
        {
            if (Time.time >= deadline)
            {
                Debug.LogWarning(
                    $"{name}: excedeu o limite de tempo para alcançar " +
                    $"{destination.name}.",
                    this
                );
            }

            agent.isStopped = true;
            agent.ResetPath();
        }

        completed(reached);
    }

    private void UnsubscribeProblemStarted()
    {
        if (subscribedProblem && problemStartedHandler != null)
        {
            subscribedProblem.ProblemStarted -= problemStartedHandler;
        }

        subscribedProblem = null;
        problemStartedHandler = null;
    }
}
