
using UnityEngine;
[DisallowMultipleComponent]
public sealed class RobotStatusCone : MonoBehaviour
{
    private enum RobotStatus
    {
        Normal,
        ProblemActive,
        ProblemFailed,
        CarryingExtinguisher
    }

    [Header("Referências")]
    [SerializeField] private Renderer coneRenderer;
    [SerializeField] private RobotFireInteractor fireInteractor;

    [Header("Cores")]
    [SerializeField]
    private Color normalColor =
        new Color(0.2f, 0.85f, 0.25f);

    [SerializeField]
    private Color warningColor =
        new Color(1f, 0.8f, 0.05f);

    [SerializeField]
    private Color warningDimColor =
        new Color(0.45f, 0.3f, 0.02f);

    [SerializeField]
    private Color failedColor =
        new Color(1f, 0.08f, 0.05f);

    [SerializeField]
    private Color extinguisherColor =
        new Color(0.05f, 0.55f, 1f);

    [Header("Animação")]
    [SerializeField, Min(0.1f)]
    private float transitionSpeed = 5f;

    [SerializeField, Min(0.1f)]
    private float blinkFrequency = 2f;

    [SerializeField, Range(0f, 1f)]
    private float coneAlpha = 0.8f;

    private static readonly int ColorId =
        Shader.PropertyToID("_StatusColor");

    private static readonly int TransparencyId =
        Shader.PropertyToID("_StatusTransparency");

    private MaterialPropertyBlock propertyBlock;

    private RobotStatus currentStatus;
    private Color currentColor;

    private void Awake()
    {
        if (!coneRenderer)
            coneRenderer = GetComponent<Renderer>();

        if (!coneRenderer)
            coneRenderer = GetComponentInChildren<Renderer>(true);

        propertyBlock = new MaterialPropertyBlock();

        currentColor = normalColor;

        if (!coneRenderer)
        {
            Debug.LogError(
                $"{name}: nenhum Renderer foi encontrado para o Status Cone.",
                this
            );
        }
    }

    private void OnEnable()
    {
        currentStatus = GetCurrentStatus();
        currentColor = GetTargetColor(currentStatus);

        ApplyAppearance(currentColor);
    }

    private void Update()
    {
        if (!coneRenderer)
            return;
        currentStatus = GetCurrentStatus();

        Color targetColor = GetTargetColor(currentStatus);
        if (currentStatus == RobotStatus.ProblemActive)
        {
            float blink = Mathf.PingPong(
                Time.time * blinkFrequency,
                1f
            );

            targetColor = Color.Lerp(
                warningDimColor,
                warningColor,
                blink
            );
        }
        float interpolation = 1f -
            Mathf.Exp(-transitionSpeed * Time.deltaTime);

        currentColor = Color.Lerp(
            currentColor,
            targetColor,
            interpolation
        );

        ApplyAppearance(currentColor);
    }

    private RobotStatus GetCurrentStatus()
    {
        if (fireInteractor && fireInteractor.IsCarryingExtinguisher)
            return RobotStatus.CarryingExtinguisher;

        bool hasFailedProblem = FireIncident.AnyFireBurning;
        bool hasActiveProblem = false;

        foreach (ProblemArea area in ProblemArea.Areas)
        {
            if (!area)
                continue;

            if (area.HasFailed)
                hasFailedProblem = true;

            if (area.IsActive)
                hasActiveProblem = true;
        }

        if (hasFailedProblem)
            return RobotStatus.ProblemFailed;

        if (hasActiveProblem)
            return RobotStatus.ProblemActive;

        return RobotStatus.Normal;
    }

    private Color GetTargetColor(RobotStatus status)
    {
        switch (status)
        {
            case RobotStatus.ProblemActive:
                return warningColor;

            case RobotStatus.ProblemFailed:
                return failedColor;

            case RobotStatus.CarryingExtinguisher:
                return extinguisherColor;

            case RobotStatus.Normal:
            default:
                return normalColor;
        }
    }

    private void ApplyAppearance(Color color)
    {
        if (!coneRenderer || propertyBlock == null)
            return;
        coneRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetFloat(TransparencyId, coneAlpha);

        coneRenderer.SetPropertyBlock(propertyBlock);
    }
}
