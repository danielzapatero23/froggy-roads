using UnityEngine;

public enum DireccionCoche { Izquierda, Derecha }

public class MovimientoCoche : MonoBehaviour
{
    public float velocidad = 5f;

    public DireccionCoche direccion = DireccionCoche.Derecha;

    void Update()
    {
        Vector3 vector = direccion == DireccionCoche.Derecha ? Vector3.right : Vector3.left;
        transform.Translate(vector * velocidad * Time.deltaTime, Space.World);

      
        if (transform.position.x > 120|| transform.position.x < -120||
            transform.position.y > 200|| transform.position.y < -200)
        {
           
            gameObject.SetActive(false);
        }
    }
}