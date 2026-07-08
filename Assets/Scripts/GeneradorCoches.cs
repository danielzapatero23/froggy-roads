using System.Collections.Generic; 
using UnityEngine;

public class GeneradorCoches : MonoBehaviour
{
    public GameObject cochePrefab;

    [Header("Configuración por carril (override en el Inspector de cada instancia)")]
    [Tooltip("Cuántos coches como máximo puede haber activos a la vez en este carril")]
    public int cantidad = 10;

    [Tooltip("Tiempo medio entre apariciones para este spawner")]
    public float tiempo = 1.6f;

    [Tooltip("Variación aleatoria ± sobre 'tiempo', para que no se sincronicen todos los carriles")]
    public float variacionTiempo = 0.4f;

    private List<GameObject> reserva;
    private float cronometro;
    private float proximoTiempo;

    void Start()
    {

        reserva = new List<GameObject>();

        for (int i = 0; i < cantidad; i++)
        {
            GameObject obj = Instantiate(cochePrefab);
            obj.SetActive(false);
            reserva.Add(obj);
        }

        proximoTiempo = Random.Range(tiempo - variacionTiempo, tiempo + variacionTiempo);
    }

    void Update()
    {
        cronometro += Time.deltaTime;

        if (cronometro >= proximoTiempo)
        {
            SacarCoche();
            cronometro = 0;
            proximoTiempo = Mathf.Max(0.3f, Random.Range(tiempo - variacionTiempo, tiempo + variacionTiempo));
        }
    }

    void SacarCoche()
    {
       
        for (int i = 0; i < reserva.Count; i++)
        {
            if (reserva[i].activeInHierarchy == false)
            {
               
                reserva[i].transform.position = transform.position;
                reserva[i].transform.rotation = Quaternion.identity; 

                // Encenderlo
                reserva[i].SetActive(true);
                return;
            }
        }
    }
}