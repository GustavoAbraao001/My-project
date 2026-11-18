
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BackgroundRouteMover : MonoBehaviour
{
    private static readonly List<BackgroundRouteMover> ActiveMovers =
        new List<BackgroundRouteMover>();

    [Serializable]
    private sealed class RoutePoint
    {
        [SerializeField] private Transform point;

        [SerializeField, Min(0f)]
        private float waitSeconds;

        public Transform Point => point;
        public float WaitSeconds => waitSeconds;
    }

    [Header("Percurso")]
    [SerializeField] private RoutePoint[] routePoints;

    [SerializeField, Min(0)]
    private int startingPointIndex;

    [SerializeField, Min(0.01f)]
    private float moveSpeed = 2f;

    [SerializeField, Min(1f)]
    private float rotationSpeed = 360f;

    [SerializeField, Min(0.01f)]
    private float arrivalDistance = 0.1f;

    [SerializeField]
    private bool preserveHeight = true;

    [SerializeField]
    private Vector3 rotationOffsetEuler;

    [Header("Controle de trânsito")]
    [SerializeField]
    private bool avoidOtherVehicles = false;

    [SerializeField]
    private Transform trafficGroup;

    [SerializeField, Min(0.1f)]
    private float minimumFollowDistance = 2f;

    [SerializeField, Min(0.2f)]
    private float slowdownDistance = 5f;

    [SerializeField, Min(0.1f)]
    private float maximumLateralDistance = 1.5f;

    [SerializeField, Min(0.1f)]
    private float acceleration = 2f;

    [SerializeField, Min(0.1f)]
    private float deceleration = 5f;

    [Header("Pausa")]
    [SerializeField]
    private bool pauseWhenGamePaused = true;

    [Header("Animação (Opcional)")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string animatorSpeedParameter = "MoveSpeed";

    [SerializeField, Min(0f)]
    private float animatorMovingValue = 4f;

    [SerializeField, Min(0f)]
    private float animatorDampTime = 0.15f;

    private int currentPointIndex = -1;

    private bool isWaiting;
    private float waitRemaining;
    private float currentSpeed;
    private Quaternion initialRotation;
    private Vector3 initialForward;

    private int animatorSpeedHash;
    private bool hasValidAnimatorParameter;

    private void Awake()
    {
        currentSpeed = moveSpeed;

        initialRotation = transform.rotation;

        initialForward = Vector3.ProjectOnPlane(
            transform.forward,
            Vector3.up
        ).normalized;

        if (initialForward.sqrMagnitude < 0.001f)
            initialForward = Vector3.forward;

        CacheAnimatorParameter();
    }

    private void OnEnable()
    {
        if (!ActiveMovers.Contains(this))
            ActiveMovers.Add(this);

        currentPointIndex = FindNextValidPoint(startingPointIndex);

        isWaiting = false;
        waitRemaining = 0f;
        currentSpeed = moveSpeed;

        if (avoidOtherVehicles && !trafficGroup)
        {
            Debug.LogWarning(
                $"BackgroundRouteMover em {name}: Traffic Group não foi atribuído.",
                this
            );
        }
    }

    private void OnDisable()
    {
        ActiveMovers.Remove(this);
        SetAnimatorMoving(false);
    }

    private void Update()
    {
        if (pauseWhenGamePaused && PauseController.IsPaused)
        {
            SetAnimatorMoving(false);
            return;
        }

        if (currentPointIndex < 0)
        {
            SetAnimatorMoving(false);
            return;
        }

        float deltaTime = Time.deltaTime;

        if (isWaiting)
        {
            waitRemaining -= deltaTime;
            currentSpeed = 0f;

            SetAnimatorMoving(false);

            if (waitRemaining > 0f)
                return;

            isWaiting = false;
            AdvanceToNextPoint();
        }

        if (currentPointIndex < 0)
            return;

        RoutePoint currentPoint = routePoints[currentPointIndex];

        if (currentPoint == null || !currentPoint.Point)
        {
            AdvanceToNextPoint();
            return;
        }

        Vector3 destination = currentPoint.Point.position;

        if (preserveHeight)
            destination.y = transform.position.y;

        Vector3 direction = destination - transform.position;
        float remainingDistance = direction.magnitude;

        if (remainingDistance <= arrivalDistance)
        {
            transform.position = destination;
            OnReachedPoint(currentPoint);
            return;
        }

        Vector3 travelDirection = direction.normalized;
        RotateTowardsDirection(travelDirection, deltaTime);

        float nearestVehicleGap;

        float desiredSpeed = GetTrafficSpeedLimit(
            travelDirection,
            out nearestVehicleGap
        );

        float speedChange = desiredSpeed < currentSpeed
            ? deceleration
            : acceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            desiredSpeed,
            speedChange * deltaTime
        );

        float movementDistance = currentSpeed * deltaTime;

        if (!float.IsPositiveInfinity(nearestVehicleGap))
        {
            float availableDistance = Mathf.Max(
                0f,
                nearestVehicleGap - minimumFollowDistance
            );

            movementDistance = Mathf.Min(
                movementDistance,
                availableDistance
            );
        }

        movementDistance = Mathf.Min(
            movementDistance,
            remainingDistance
        );

        transform.position += travelDirection * movementDistance;

        SetAnimatorMoving(movementDistance > 0.001f);
    }
    private void RotateTowardsDirection(
        Vector3 direction,
        float deltaTime)
    {
        Vector3 flatDirection = direction;

        if (preserveHeight)
            flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude < 0.001f)
            return;

        flatDirection.Normalize();

        Quaternion alignment = Quaternion.FromToRotation(
            initialForward,
            flatDirection
        );

        Quaternion targetRotation =
            alignment *
            initialRotation *
            Quaternion.Euler(rotationOffsetEuler);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * deltaTime
        );
    }

    private float GetTrafficSpeedLimit(
        Vector3 travelDirection,
        out float nearestGap)
    {
        nearestGap = float.PositiveInfinity;

        if (!avoidOtherVehicles || !trafficGroup)
            return moveSpeed;

        Vector3 flatDirection = Vector3.ProjectOnPlane(
            travelDirection,
            Vector3.up
        ).normalized;

        if (flatDirection.sqrMagnitude < 0.001f)
            return moveSpeed;

        foreach (BackgroundRouteMover other in ActiveMovers)
        {
            if (!other || other == this)
                continue;

            if (!other.isActiveAndEnabled)
                continue;

            if (!other.avoidOtherVehicles ||
                other.trafficGroup != trafficGroup)
            {
                continue;
            }

            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;

            float forwardDistance = Vector3.Dot(
                offset,
                flatDirection
            );

            if (forwardDistance <= 0.05f ||
                forwardDistance > slowdownDistance)
            {
                continue;
            }

            Vector3 lateralOffset =
                offset - flatDirection * forwardDistance;

            if (lateralOffset.sqrMagnitude >
                maximumLateralDistance * maximumLateralDistance)
            {
                continue;
            }

            nearestGap = Mathf.Min(
                nearestGap,
                forwardDistance
            );
        }

        if (float.IsPositiveInfinity(nearestGap))
            return moveSpeed;

        if (nearestGap <= minimumFollowDistance)
            return 0f;

        float distanceFactor = Mathf.InverseLerp(
            minimumFollowDistance,
            slowdownDistance,
            nearestGap
        );

        return moveSpeed * distanceFactor;
    }

    private void OnReachedPoint(RoutePoint point)
    {
        float waitDuration = Mathf.Max(0f, point.WaitSeconds);

        if (waitDuration > 0f)
        {
            isWaiting = true;
            waitRemaining = waitDuration;
            currentSpeed = 0f;

            SetAnimatorMoving(false);
        }
        else
        {
            AdvanceToNextPoint();
        }
    }

    private void AdvanceToNextPoint()
    {
        if (routePoints == null || routePoints.Length == 0)
        {
            currentPointIndex = -1;
            return;
        }

        int nextIndex = (currentPointIndex + 1) % routePoints.Length;

        currentPointIndex = FindNextValidPoint(nextIndex);
    }

    private int FindNextValidPoint(int startIndex)
    {
        if (routePoints == null || routePoints.Length == 0)
            return -1;

        for (int i = 0; i < routePoints.Length; i++)
        {
            int index = (startIndex + i) % routePoints.Length;

            RoutePoint point = routePoints[index];

            if (point != null && point.Point)
                return index;
        }

        return -1;
    }

    private void CacheAnimatorParameter()
    {
        hasValidAnimatorParameter = false;

        if (!animator ||
            string.IsNullOrWhiteSpace(animatorSpeedParameter))
            return;

        animatorSpeedHash = Animator.StringToHash(
            animatorSpeedParameter
        );

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == animatorSpeedHash &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                hasValidAnimatorParameter = true;
                return;
            }
        }

        Debug.LogWarning(
            $"O Animator de {name} não possui o parâmetro Float " +
            $"'{animatorSpeedParameter}'.",
            this
        );
    }

    private void SetAnimatorMoving(bool moving)
    {
        if (!animator || !hasValidAnimatorParameter)
            return;

        animator.SetFloat(
            animatorSpeedHash,
            moving ? animatorMovingValue : 0f,
            animatorDampTime,
            Time.deltaTime
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (routePoints == null || routePoints.Length == 0)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;

        for (int i = 0; i < routePoints.Length; i++)
        {
            if (routePoints[i] == null || !routePoints[i].Point)
                continue;

            Vector3 currentPosition = routePoints[i].Point.position;

            Gizmos.DrawWireSphere(currentPosition, 0.15f);

            int nextIndex = FindNextValidPoint(
                (i + 1) % routePoints.Length
            );

            if (nextIndex >= 0 && nextIndex != i)
            {
                Gizmos.DrawLine(
                    currentPosition,
                    routePoints[nextIndex].Point.position
                );
            }
        }

        Gizmos.color = previousColor;
    }

    private void OnValidate()
    {
        minimumFollowDistance = Mathf.Max(0.1f, minimumFollowDistance);

        slowdownDistance = Mathf.Max(
            minimumFollowDistance + 0.1f,
            slowdownDistance
        );

        startingPointIndex = Mathf.Max(0, startingPointIndex);
    }
}
