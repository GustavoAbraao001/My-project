using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public sealed class ExtinguisherPickup : MonoBehaviour
{
    private static readonly List<ExtinguisherPickup> allPickups = new List<ExtinguisherPickup>();
    public static IReadOnlyList<ExtinguisherPickup> AllPickups => allPickups;

    [Header("Posição carregada")]
    [SerializeField] private Vector3 carryLocalPosition = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private Vector3 carryLocalEulerAngles = new Vector3(0f, 0f, 25f);
    [SerializeField, Min(0f)] private float orbitRadius = 0.45f;
    [SerializeField, Min(0f)] private float orbitDegreesPerSecond = 120f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float bobCyclesPerSecond = 1.5f;

    [Header("Efeito")]
    [SerializeField] private ParticleSystem sprayEffect;

    public bool IsCarried { get; private set; }
    public bool IsAvailable => isAvailable && !IsCarried && !isConsumed && isActiveAndEnabled;
    public static int AvailableCount
    {
        get
        {
            int count = 0;
            foreach (ExtinguisherPickup pickup in allPickups)
                if (pickup && pickup.IsAvailable) count++;
            return count;
        }
    }

    private bool isAvailable = true;
    private bool isConsumed;
    private Collider[] colliders;
    private Rigidbody body;
    private Quaternion carryRotation;
    private float bobPhase;
    private float orbitAngleDegrees;

    private void Awake()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        body = GetComponent<Rigidbody>();
        carryRotation = Quaternion.Euler(carryLocalEulerAngles);
    }

    private void OnEnable()
    {
        if (!allPickups.Contains(this)) allPickups.Add(this);
    }

    private void OnDisable() => allPickups.Remove(this);
    private void OnDestroy() => allPickups.Remove(this);

    private void Update()
    {
        if (!IsCarried) return;

        bobPhase += Time.deltaTime * bobCyclesPerSecond * Mathf.PI * 2f;
        orbitAngleDegrees = Mathf.Repeat(orbitAngleDegrees + orbitDegreesPerSecond * Time.deltaTime, 360f);

        float orbitRadians = orbitAngleDegrees * Mathf.Deg2Rad;
        Vector3 orbitOffset = new Vector3(
            Mathf.Cos(orbitRadians) * orbitRadius,
            Mathf.Sin(bobPhase) * bobAmplitude,
            Mathf.Sin(orbitRadians) * orbitRadius);

        transform.localPosition = carryLocalPosition + orbitOffset;
        transform.localRotation = carryRotation * Quaternion.Euler(0f, orbitAngleDegrees, 0f);
    }

    public bool AttachTo(Transform carryAnchor)
    {
        if (!IsAvailable || !carryAnchor) return false;

        IsCarried = true;
        SetCollidersEnabled(false);
        if (body)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        transform.SetParent(carryAnchor, false);
        bobPhase = 0f;
        orbitAngleDegrees = 0f;
        transform.localPosition = carryLocalPosition + Vector3.right * orbitRadius;
        transform.localRotation = carryRotation;
        return true;
    }

    public void BeginSpraying()
    {
        if (sprayEffect && !sprayEffect.isPlaying) sprayEffect.Play();
    }

    public void StopSpraying()
    {
        if (sprayEffect && sprayEffect.isPlaying)
            sprayEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
    public IEnumerator ConsumeAndVanish(float fallSpeed, float vanishDuration)
    {
        if (isConsumed) yield break;

        isConsumed = true;
        isAvailable = false;
        IsCarried = false;
        StopSpraying();
        transform.SetParent(null, true);
        SetCollidersEnabled(false);

        Vector3 initialScale = transform.localScale;
        float duration = Mathf.Max(0.05f, vanishDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float delta = Time.deltaTime;
            elapsed += delta;
            transform.position += Vector3.down * fallSpeed * delta;
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, elapsed / duration);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetCollidersEnabled(bool value)
    {
        if (colliders == null) return;
        foreach (Collider item in colliders)
            if (item) item.enabled = value;
    }
}
