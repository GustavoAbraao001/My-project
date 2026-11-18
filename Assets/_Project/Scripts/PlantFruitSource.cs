using System.Collections.Generic;
using UnityEngine;

public sealed class PlantFruitSource : MonoBehaviour
{
    private static readonly List<PlantFruitSource> activePlants = new List<PlantFruitSource>();
    public static IReadOnlyList<PlantFruitSource> ActivePlants => activePlants;

    [Header("Produção de frutos")]
    [SerializeField, Min(0.1f)] private float secondsPerFruit = 20f;
    [SerializeField, Min(1)] private int maxStoredFruits = 3;
    [SerializeField, Min(0)] private int pointsPerFruit = 25;

    [Header("Indicador visual")]
    [SerializeField] private ParticleSystem readyParticles;

    public int StoredFruits { get; private set; }
    public int PointsPerFruit => pointsPerFruit;
    public int PointsForAllStoredFruit => StoredFruits * pointsPerFruit;
    public bool HasFruit => StoredFruits > 0;

    private float productionTimer;

    private void Awake()
    {
        productionTimer = secondsPerFruit;
        SetReadyIndicator(false);
    }

    private void OnEnable()
    {
        if (!activePlants.Contains(this)) activePlants.Add(this);
    }

    private void OnDisable() => activePlants.Remove(this);
    private void OnDestroy() => activePlants.Remove(this);

    private void Update()
    {
        if (PauseController.IsPaused || StoredFruits >= maxStoredFruits) return;

        productionTimer -= Time.deltaTime;
        if (productionTimer <= 0f)
        {
            StoredFruits++;
            productionTimer += secondsPerFruit;
            SetReadyIndicator(true);
            Debug.Log($"{name} gerou um fruto. Armazenados: {StoredFruits}.", this);
        }
    }
    public bool TryHarvest()
    {
        if (!HasFruit) return false;

        ArcadeScoreManager scoreManager = ArcadeScoreManager.Instance;
        if (!scoreManager)
        {
            Debug.LogError("Não existe ArcadeScoreManager ativo; os frutos não foram coletados.", this);
            return false;
        }

        int harvested = StoredFruits;
        int points = harvested * pointsPerFruit;
        StoredFruits = 0;
        SetReadyIndicator(false);
        scoreManager.AddPoints(points);
        Debug.Log($"{name}: coletou {harvested} fruto(s) e ganhou {points} pontos.", this);
        return true;
    }

    private void SetReadyIndicator(bool ready)
    {
        if (!readyParticles) return;

        if (ready)
        {
            if (!readyParticles.gameObject.activeSelf)
                readyParticles.gameObject.SetActive(true);
            if (!readyParticles.isPlaying) readyParticles.Play();
        }
        else
        {
            if (readyParticles.isPlaying)
                readyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (readyParticles.gameObject != gameObject && readyParticles.gameObject.activeSelf)
                readyParticles.gameObject.SetActive(false);
        }
    }
}
