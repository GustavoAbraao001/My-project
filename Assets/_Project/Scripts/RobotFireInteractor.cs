
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
public sealed class RobotFireInteractor : MonoBehaviour
{
    [Header("Robô")]
    [SerializeField] private Transform interactionOrigin;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private Behaviour robotMovement;

    [SerializeField, Min(0.1f)]
    private float pickupRange = 1.5f;

    [SerializeField, Min(0.1f)]
    private float fireUseRange = 1.8f;

    [Header("Uso do extintor")]
    [SerializeField, Min(0.1f)]
    private float sprayDuration = 1.2f;

    [SerializeField, Min(0.05f)]
    private float extinguishFadeDuration = 1.5f;

    [SerializeField, Min(0f)]
    private float dropSpeed = 1.2f;

    [SerializeField, Min(0.05f)]
    private float vanishDuration = 0.6f;

    [Header("Interface")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private UnityEvent onOutOfExtinguishersDuringFire;

    private ExtinguisherPickup carriedPickup;
    private Coroutine useRoutine;

    private bool gameOverTriggered;
    private bool movementChangedForSpray;
    private bool movementWasEnabledBeforeSpray;
    public bool IsCarryingExtinguisher => carriedPickup != null;

    private void Awake()
    {
        if (promptText)
            promptText.gameObject.SetActive(false);

        if (gameOverPanel)
            gameOverPanel.SetActive(false);
    }

    private void OnDisable()
    {
        RestoreRobotMovement();
    }

    private void Update()
    {
        if (PauseController.IsPaused ||
            gameOverTriggered ||
            useRoutine != null)
        {
            SetPrompt("");
            return;
        }

        if (!carryAnchor)
        {
            SetPrompt(
                "RobotFireInteractor: atribua o Carry Anchor no Inspector."
            );

            return;
        }
        if (!carriedPickup)
            carriedPickup = null;

        FireIncident nearestFire = FindNearestBurningFire();
        if (carriedPickup)
        {
            if (!nearestFire)
            {
                SetPrompt("");
                return;
            }

            if (IsWithinRange(nearestFire.transform, fireUseRange))
            {
                SetPrompt("Apagando incêndio automaticamente...");

                useRoutine = StartCoroutine(
                    UseExtinguisher(nearestFire)
                );
            }
            else
            {
                SetPrompt("Aproxime-se do incêndio.");
            }

            return;
        }
        if (!FireIncident.AnyFireBurning)
        {
            SetPrompt("");
            return;
        }
        if (ExtinguisherPickup.AvailableCount == 0)
        {
            TriggerGameOver();
            return;
        }
        ExtinguisherPickup nearestPickup =
            FindNearestAvailablePickup(out float pickupDistanceSqr);

        if (nearestPickup &&
            pickupDistanceSqr <= pickupRange * pickupRange)
        {
            if (nearestPickup.AttachTo(carryAnchor))
            {
                carriedPickup = nearestPickup;

                SetPrompt("Extintor coletado automaticamente.");
            }
            else
            {
                SetPrompt("");
                Debug.LogWarning(
                    "Não foi possível prender o extintor ao Carry Anchor.",
                    nearestPickup
                );
            }
        }
        else
        {
            SetPrompt("Aproxime-se de um extintor.");
        }
    }

    private IEnumerator UseExtinguisher(FireIncident target)
    {
        ExtinguisherPickup usedPickup = carriedPickup;

        if (!usedPickup || !target ||
            !target.IsBurning || target.IsExtinguishing)
        {
            useRoutine = null;
            yield break;
        }
        movementWasEnabledBeforeSpray =
            robotMovement && robotMovement.enabled;

        movementChangedForSpray = movementWasEnabledBeforeSpray;
        if (movementChangedForSpray)
            robotMovement.enabled = false;

        usedPickup.BeginSpraying();
        yield return new WaitForSeconds(sprayDuration);
        if (target && target.IsBurning && !target.IsExtinguishing)
        {
            yield return target.ExtinguishGradually(
                extinguishFadeDuration
            );
        }

        usedPickup.StopSpraying();

        RestoreRobotMovement();

        carriedPickup = null;
        yield return usedPickup.ConsumeAndVanish(
            dropSpeed,
            vanishDuration
        );

        useRoutine = null;
        if (FireIncident.AnyFireBurning &&
            ExtinguisherPickup.AvailableCount == 0)
        {
            TriggerGameOver();
        }
    }

    private FireIncident FindNearestBurningFire()
    {
        FireIncident best = null;
        float bestDistance = float.PositiveInfinity;

        Vector3 origin = interactionOrigin
            ? interactionOrigin.position
            : transform.position;

        foreach (FireIncident incident in FireIncident.Incidents)
        {
            if (!incident ||
                !incident.IsBurning ||
                incident.IsExtinguishing)
            {
                continue;
            }

            float distance = GroundDistance(
                origin,
                incident.transform.position
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = incident;
            }
        }

        return best;
    }

    private ExtinguisherPickup FindNearestAvailablePickup(
        out float bestSqrDistance)
    {
        ExtinguisherPickup best = null;

        bestSqrDistance = float.PositiveInfinity;

        Vector3 origin = interactionOrigin
            ? interactionOrigin.position
            : transform.position;

        foreach (ExtinguisherPickup pickup in ExtinguisherPickup.AllPickups)
        {
            if (!pickup || !pickup.IsAvailable)
                continue;

            float sqrDistance = GroundDistanceSquared(
                origin,
                pickup.transform.position
            );

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                best = pickup;
            }
        }

        return best;
    }

    private bool IsWithinRange(Transform target, float range)
    {
        if (!target)
            return false;

        Vector3 origin = interactionOrigin
            ? interactionOrigin.position
            : transform.position;

        return GroundDistanceSquared(
            origin,
            target.position
        ) <= range * range;
    }

    private static float GroundDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt(GroundDistanceSquared(a, b));
    }

    private static float GroundDistanceSquared(Vector3 a, Vector3 b)
    {
        Vector3 offset = a - b;
        offset.y = 0f;

        return offset.sqrMagnitude;
    }

    private void TriggerGameOver()
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;
        SetPrompt("");

        if (GameFlowManager.Instance)
        {
            GameFlowManager.Instance.TriggerGameOver();
        }
        else
        {
            if (ArcadeScoreManager.Instance)
                ArcadeScoreManager.Instance.SetPassiveScoringEnabled(false);

            if (gameOverPanel)
                gameOverPanel.SetActive(true);

            Debug.LogError(
                "Game Over ocorreu, mas não há GameFlowManager na cena.",
                this
            );
        }

        onOutOfExtinguishersDuringFire?.Invoke();

        Debug.LogWarning(
            "Há um incêndio ativo e não restam extintores. Fim da partida.",
            this
        );
    }

    private void RestoreRobotMovement()
    {
        if (movementChangedForSpray && robotMovement)
        {
            robotMovement.enabled = movementWasEnabledBeforeSpray;
        }

        movementChangedForSpray = false;
    }

    private void SetPrompt(string message)
    {
        if (!promptText)
            return;

        bool show = !string.IsNullOrWhiteSpace(message);

        promptText.gameObject.SetActive(show);

        if (show)
            promptText.text = message;
    }
}
