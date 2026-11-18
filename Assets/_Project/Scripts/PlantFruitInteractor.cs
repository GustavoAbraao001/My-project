using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class PlantFruitInteractor : MonoBehaviour
{
    [Header("Interação")]
    [SerializeField] private Transform interactionOrigin;
    [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;
    [SerializeField] private Key collectKey = Key.F;

    [Header("Interface")]
    [SerializeField] private TMP_Text promptText;

    private PlantFruitSource nearestPlant;

    private void Awake()
    {
        if (promptText) promptText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (PauseController.IsPaused)
        {
            SetPrompt("");
            return;
        }

        nearestPlant = FindNearestReadyPlant();
        if (!nearestPlant)
        {
            SetPrompt("");
            return;
        }

        int fruitCount = nearestPlant.StoredFruits;
        int points = nearestPlant.PointsForAllStoredFruit;
        SetPrompt($"{collectKey.ToString().ToUpperInvariant()} - Coletar {fruitCount} fruto(s) (+{points})");

        if (Keyboard.current != null && Keyboard.current[collectKey].wasPressedThisFrame)
            nearestPlant.TryHarvest();
    }

    private PlantFruitSource FindNearestReadyPlant()
    {
        PlantFruitSource best = null;
        float bestSqrDistance = interactionRange * interactionRange;
        Vector3 origin = interactionOrigin ? interactionOrigin.position : transform.position;

        foreach (PlantFruitSource plant in PlantFruitSource.ActivePlants)
        {
            if (!plant || !plant.HasFruit) continue;

            Vector3 offset = plant.transform.position - origin;
            offset.y = 0f;
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr <= bestSqrDistance)
            {
                bestSqrDistance = distanceSqr;
                best = plant;
            }
        }

        return best;
    }

    private void SetPrompt(string message)
    {
        if (!promptText) return;
        bool show = !string.IsNullOrWhiteSpace(message);
        promptText.gameObject.SetActive(show);
        if (show) promptText.text = message;
    }
}
