using System.Collections;
using UnityEngine;

public class CamaraSeguimiento : MonoBehaviour
{
    public Transform pollo;
    public float suavizado = 0.08f;

    [Header("Shake")]
    public float shakeDuracion = 0.15f;
    public float shakeIntensidad = 0.18f;

    private Vector3 distancia;
    private Vector3 velocidad;
    private Vector3 offsetShake;

    void Start()
    {
        if (pollo != null)
        {
            distancia = transform.position - pollo.position;
        }
    }

    void LateUpdate()
    {
        if (pollo == null) return;

        Vector3 objetivo = pollo.position + distancia;
        objetivo.z = -10;

        Vector3 seguimiento = Vector3.SmoothDamp(transform.position, objetivo, ref velocidad, suavizado);
        transform.position = seguimiento + offsetShake;
    }

    public void Sacudir()
    {
        StopCoroutine(nameof(SacudirRutina));
        StartCoroutine(SacudirRutina());
    }

    IEnumerator SacudirRutina()
    {
        float t = 0f;
        while (t < shakeDuracion)
        {
            t += Time.deltaTime;
            float atenuacion = 1f - (t / shakeDuracion);
            offsetShake = Random.insideUnitCircle * shakeIntensidad * atenuacion;
            yield return null;
        }
        offsetShake = Vector3.zero;
    }
}