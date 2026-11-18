
using UnityEngine;

public sealed class RiseOverTime : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField, Min(0f)] private float riseDistance = 1f;
    [SerializeField, Min(0.01f)] private float riseSpeed = 0.5f;
    [SerializeField, Min(0f)] private float startDelay = 0f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float delayRemaining;
    private bool isRising;

    public bool IsRising => isRising;

    private void OnEnable()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseDistance;
        delayRemaining = startDelay;
        isRising = riseDistance > 0f;
    }

    private void Update()
    {
        if (!isRising)
            return;

        if (delayRemaining > 0f)
        {
            delayRemaining -= Time.deltaTime;
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            riseSpeed * Time.deltaTime
        );

        if (transform.position == targetPosition)
            isRising = false;
    }

    public void RestartRise()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseDistance;
        delayRemaining = startDelay;
        isRising = riseDistance > 0f;
    }

    public void ResetToStart()
    {
        transform.position = startPosition;
        targetPosition = startPosition + Vector3.up * riseDistance;
        delayRemaining = startDelay;
        isRising = riseDistance > 0f;
    }
}
