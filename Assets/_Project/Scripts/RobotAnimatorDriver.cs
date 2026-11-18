
using UnityEngine;
[RequireComponent(typeof(Animator))]
public sealed class RobotAnimatorDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private RobotClickMover movementController;

    [Header("Animator Controller")]
    [SerializeField] private string speedParameterName = "MoveSpeed";
    [SerializeField] private string beginRepairTriggerName = "BeginRepair";
    [SerializeField] private string endRepairTriggerName = "EndRepair";
    [SerializeField, Min(0f)] private float dampTime = 0.3f;

    private int speedParameterHash;
    private int beginRepairTriggerHash;
    private int endRepairTriggerHash;

    private bool hasSpeedParameter;
    private bool hasBeginRepairTrigger;
    private bool hasEndRepairTrigger;

    private void Awake()
    {
        if (!animator)
            animator = GetComponent<Animator>();

        if (!movementController)
            movementController = GetComponentInParent<RobotClickMover>();

        if (!animator)
        {
            Debug.LogError(
                "RobotAnimatorDriver precisa de um Animator.",
                this
            );

            enabled = false;
            return;
        }

        animator.applyRootMotion = false;

        speedParameterHash =
            Animator.StringToHash(speedParameterName);

        beginRepairTriggerHash =
            Animator.StringToHash(beginRepairTriggerName);

        endRepairTriggerHash =
            Animator.StringToHash(endRepairTriggerName);

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == speedParameterHash &&
                parameter.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
            }

            if (parameter.nameHash == beginRepairTriggerHash &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasBeginRepairTrigger = true;
            }

            if (parameter.nameHash == endRepairTriggerHash &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                hasEndRepairTrigger = true;
            }
        }

        if (!hasSpeedParameter)
        {
            Debug.LogError(
                $"O Animator precisa de um parâmetro Float chamado '{speedParameterName}'.",
                this
            );
        }

        if (!movementController)
        {
            Debug.LogError(
                "RobotAnimatorDriver não encontrou RobotClickMover.",
                this
            );
        }
    }

    private void LateUpdate()
    {
        if (!animator || !hasSpeedParameter)
            return;

        float targetSpeed = 0f;

        if (movementController && movementController.enabled)
        {
            targetSpeed = movementController.AnimationSpeed;
        }

        animator.SetFloat(
            speedParameterHash,
            targetSpeed,
            dampTime,
            Time.deltaTime
        );
    }

    public void BeginRepair()
    {
        if (!animator || !hasBeginRepairTrigger)
            return;

        if (hasEndRepairTrigger)
            animator.ResetTrigger(endRepairTriggerHash);

        animator.SetTrigger(beginRepairTriggerHash);
    }

    public void EndRepair()
    {
        if (!animator || !hasEndRepairTrigger)
            return;

        if (hasBeginRepairTrigger)
            animator.ResetTrigger(beginRepairTriggerHash);

        animator.SetTrigger(endRepairTriggerHash);
    }
}
