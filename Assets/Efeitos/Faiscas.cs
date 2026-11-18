using UnityEngine;
using System.Collections;

public class Faiscas : MonoBehaviour
{
    [SerializeField] private ParticleSystem faisca;

    [SerializeField] private float duracaoFaiscaMin;
    [SerializeField] private float duracaoFaiscaMax;

    [SerializeField] private float esperaMin;
    [SerializeField] private float esperaMax;

    void Start()
    {
        StartCoroutine(LoopParticula());
    }

    IEnumerator LoopParticula()
    {
        while (true)
        {
            faisca.Play();

            yield return new WaitForSeconds(Random.Range(duracaoFaiscaMin, duracaoFaiscaMax));

            faisca.Stop();

            yield return new WaitForSeconds(Random.Range(esperaMin, esperaMax));
        }
    }
}
