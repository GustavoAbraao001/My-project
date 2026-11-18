using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
public class RobotClickMover : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Camera worldCamera;
    [Tooltip("Modelo 3D que gira ao andar. Se vazio, gira o próprio objeto.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Efeito mostrado no local clicado (opcional).")]
    [SerializeField] private ClickMarker clickMarker;

    [Header("Clique")]
    [Tooltip("Layer(s) do chão clicável.")]
    [SerializeField] private LayerMask walkableLayer;
    [SerializeField] private float rayDistance = 200f;
    [Tooltip("Ligado: o raio atravessa móveis/paredes e só enxerga o chão. " +
             "Desligado: se algo na frente do chão for clicado, o clique é ignorado.")]
    [SerializeField] private bool clickThroughObstacles = true;
    [SerializeField] private bool ignoreClicksOverUI = true;
    [Tooltip("Segurar o botão esquerdo faz o robô seguir o cursor (como no LoL).")]
    [SerializeField] private bool holdToFollowCursor = true;
    [SerializeField] private float holdRepathInterval = 0.1f;

    [Header("NavMesh")]
    [Tooltip("Raio de busca do NavMesh perto do ponto clicado.")]
    [SerializeField] private float navMeshSnapDistance = 1f;
    [Tooltip("Se o ponto for inalcançável, vai até o ponto mais próximo possível.")]
    [SerializeField] private bool allowPartialPath = true;

    [Header("Movimento arcade")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("0 = vira instantaneamente. Valores > 0 = graus por segundo.")]
    [SerializeField] private float turnSpeed = 0f;
    [Tooltip("Se o robô não sair do lugar por este tempo, o movimento é cancelado.")]
    [SerializeField] private float stuckTimeout = 0.35f;

    private NavMeshAgent agent;
    private NavMeshPath path;

    private Vector3[] route;
    private int routeIndex;

    private bool pressStartedOverUI;
    private float nextHoldTime;

    private Vector3 lastPosition;
    private float lastExpectedMove;
    private float stuckTimer;
    private readonly Dictionary<UnityEngine.Object, float> movementSpeedModifiers =
     new Dictionary<UnityEngine.Object, float>();

    public bool IsMoving => route != null;
    public float AnimationSpeed => IsMoving ? moveSpeed : 0f;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
        agent.speed = moveSpeed;
        agent.autoBraking = false;
        agent.updateRotation = false;
    }

    private void Start()
    {
        if (worldCamera == null)
        {
            Debug.LogError(
                "RobotClickMover precisa de uma câmera. Arraste a câmera para World Camera ou marque-a MainCamera.",
                this);
            enabled = false;
            return;
        }

        if (walkableLayer.value == 0)
        {
            Debug.LogError(
                "Walkable Layer está vazio: nenhum clique será aceito. Escolha a layer do chão.",
                this);
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "O robô não começou sobre um NavMesh. Confira a posição do Robot e se a superfície foi assada.",
                this);
        }
    }

    private void OnDisable()
    {
        StopMoving();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return; // pausado
        }

        ReadInput();
        FollowRoute();
    }
    public bool MoveTo(Vector3 worldPoint)
    {
        return TrySetDestination(worldPoint, out _);
    }

    
   public void SetMovementSpeedModifier(
    UnityEngine.Object source,
    float multiplier)
    {
        if (source == null)
            return;

        movementSpeedModifiers[source] =
            Mathf.Clamp(multiplier, 0.1f, 1f);
    }

    public void RemoveMovementSpeedModifier(UnityEngine.Object source)
    {
        if (source == null)
            return;

        movementSpeedModifiers.Remove(source);
    
}
    public void StopMoving()
    {
        route = null;
        routeIndex = 0;
        stuckTimer = 0f;
        lastExpectedMove = 0f;
    }

    private float GetMovementSpeedMultiplier()
    {
        float result = 1f;

        foreach (float modifier in movementSpeedModifiers.Values)
        {
            result = Mathf.Min(result, modifier);
        }

        return result;
    }

    private void ReadInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        bool pressed = mouse.leftButton.wasPressedThisFrame;

        if (pressed)
        {
            pressStartedOverUI = ignoreClicksOverUI && IsPointerOverUI();
        }

        if (pressStartedOverUI)
        {
            return; // clique começou numa UI: arrastar para o chão não deve mover o robô
        }

        bool held = !pressed &&
                    holdToFollowCursor &&
                    mouse.leftButton.isPressed &&
                    Time.time >= nextHoldTime;

        if (!pressed && !held)
        {
            return;
        }

        nextHoldTime = Time.time + holdRepathInterval;
        TryMoveToScreenPoint(mouse.position.ReadValue(), pressed);
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void TryMoveToScreenPoint(Vector2 screenPoint, bool showMarker)
    {
        Ray ray = worldCamera.ScreenPointToRay(screenPoint);
        RaycastHit hit;

        if (clickThroughObstacles)
        {
            if (!Physics.Raycast(ray, out hit, rayDistance, walkableLayer, QueryTriggerInteraction.Ignore))
            {
                return;
            }
        }
        else
        {
            if (!Physics.Raycast(ray, out hit, rayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if ((walkableLayer.value & (1 << hit.collider.gameObject.layer)) == 0)
            {
                return;
            }
        }

        bool ok = TrySetDestination(hit.point, out Vector3 finalPoint);

        if (showMarker && clickMarker != null)
        {
            clickMarker.Show(ok ? finalPoint : hit.point, ok);
        }
    }

    private bool TrySetDestination(Vector3 worldPoint, out Vector3 destination)
    {
        destination = worldPoint;

        if (!agent.isOnNavMesh)
        {
            return false;
        }

        if (!NavMesh.SamplePosition(worldPoint, out NavMeshHit navHit, navMeshSnapDistance, agent.areaMask))
        {
            return false;
        }

        if (!agent.CalculatePath(navHit.position, path))
        {
            return false;
        }

        bool complete = path.status == NavMeshPathStatus.PathComplete;
        bool partial = path.status == NavMeshPathStatus.PathPartial;

        if (!complete && !(allowPartialPath && partial))
        {
            return false;
        }

        Vector3[] corners = path.corners;

        if (corners.Length < 2)
        {
            if (!complete)
            {
                return false;
            }

            StopMoving();
            destination = navHit.position;
            return true;
        }

        route = corners;
        routeIndex = 1; // corners[0] é a posição atual
        stuckTimer = 0f;
        lastExpectedMove = 0f;
        destination = corners[corners.Length - 1];
        return true;
    }

    private void FollowRoute()
    {
        if (route == null)
        {
            return;
        }

        if (!agent.isOnNavMesh)
        {
            StopMoving();
            return;
        }

        Vector3 start = transform.position;
        if (lastExpectedMove > 0f)
        {
            Vector3 moved = start - lastPosition;
            moved.y = 0f;

            if (moved.magnitude < lastExpectedMove * 0.1f)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckTimeout)
                {
                    StopMoving();
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        float effectiveSpeed =
            moveSpeed * GetMovementSpeedMultiplier();

        float remaining = effectiveSpeed * Time.deltaTime; Vector3 total = Vector3.zero;
        Vector3 position = start;
        Vector3 facing = Vector3.zero;

        while (remaining > 0f && routeIndex < route.Length)
        {
            Vector3 toCorner = route[routeIndex] - position;
            toCorner.y = 0f;
            float distance = toCorner.magnitude;

            if (distance <= 0.0001f)
            {
                routeIndex++;
                continue;
            }

            Vector3 direction = toCorner / distance;
            facing = direction;

            if (distance <= remaining)
            {
                total += toCorner;
                position += toCorner;
                remaining -= distance;
                routeIndex++;
            }
            else
            {
                total += direction * remaining;
                remaining = 0f;
            }
        }

        if (facing != Vector3.zero)
        {
            Face(facing);
        }

        if (total.sqrMagnitude > 0f)
        {
            agent.Move(total);
            lastPosition = start;
            lastExpectedMove = total.magnitude;
        }

        if (routeIndex >= route.Length)
        {
            StopMoving();
        }
    }

    private void Face(Vector3 direction)
    {
        Transform target = visualRoot != null ? visualRoot : transform;
        Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);

        target.rotation = turnSpeed <= 0f
            ? desired
            : Quaternion.RotateTowards(target.rotation, desired, turnSpeed * Time.deltaTime);
    }
    private void OnDrawGizmosSelected()
    {
        if (route == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Vector3 previous = transform.position;

        for (int i = routeIndex; i < route.Length; i++)
        {
            Gizmos.DrawLine(previous, route[i]);
            Gizmos.DrawSphere(route[i], 0.08f);
            previous = route[i];
        }
    }
}