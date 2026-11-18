using UnityEngine;
using UnityEngine.InputSystem;

public enum ProblemInteractionDetectionMode
{
    GizmoRadius,
    TriggerVolume,
    Either,
    Both
}
public sealed class RobotProblemInteractor : MonoBehaviour
{
    [SerializeField] private Transform interactionOrigin;
    [SerializeField] private ProblemInteractionDetectionMode detectionMode = ProblemInteractionDetectionMode.Either;
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private ArrowSequenceMinigame minigame;
    private ProblemArea nearest;

    private void Update()
    {
        if (PauseController.IsPaused)
        {
            if (interactPrompt) interactPrompt.SetActive(false);
            return;
        }

        nearest = FindNearestProblem();
        bool canInteract = nearest && nearest.IsActive && minigame && !minigame.IsOpen;
        if (interactPrompt) interactPrompt.SetActive(canInteract);
        if (canInteract && PressedInteract()) minigame.Open(nearest);
    }

    private bool PressedInteract()
    {
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool rightClick = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        return space || rightClick;
    }

    private ProblemArea FindNearestProblem()
    {
        ProblemArea best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 origin = interactionOrigin ? interactionOrigin.position : transform.position;
        foreach (ProblemArea p in ProblemArea.Areas)
        {
            if (!p || !p.IsActive || !IsEligibleForInteraction(p, origin)) continue;

            Vector3 offset = p.transform.position - origin;
            offset.y = 0f;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr < bestSqr)
            {
                bestSqr = distanceSqr;
                best = p;
            }
        }
        return best;
    }

    private bool IsEligibleForInteraction(ProblemArea problem, Vector3 playerPosition)
    {
        bool insideGizmoRadius = problem.IsPlayerInsideInteractionRadius(playerPosition);
        bool insideTrigger = problem.IsPlayerInsideInteractionTrigger(playerPosition);

        switch (detectionMode)
        {
            case ProblemInteractionDetectionMode.GizmoRadius:
                return insideGizmoRadius;
            case ProblemInteractionDetectionMode.TriggerVolume:
                return insideTrigger;
            case ProblemInteractionDetectionMode.Both:
                return insideGizmoRadius && insideTrigger;
            case ProblemInteractionDetectionMode.Either:
            default:
                return insideGizmoRadius || insideTrigger;
        }
    }
}
