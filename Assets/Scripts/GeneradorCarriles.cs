using UnityEngine;
using UnityEngine.Tilemaps;

public enum TipoCarril { Hierba, Carretera, Tren }

public class GeneradorCarriles : MonoBehaviour
{
    [Header("Referencias al tilemap existente")]
    public Tilemap tilemapSuelo;
    public Transform muestraAsfalto;
    public Transform muestraHierba;

    [Header("Meta (se aleja según los carriles generados)")]
    public Transform meta;

    [Header("Contenedores de la zona decorativa tras la meta (se desplazan junto con ella)")]
    public Transform[] contenedoresDecorado;

    [Header("Prefabs de spawners (ya existentes)")]
    public GameObject spawnDerechaPrefab;
    public GameObject spawnIzquierdaPrefab;
    public GameObject spawnTrenPrefab;

    [Header("Coches a asignar a los spawners generados")]
    public GameObject[] cochesDerecha;
    public GameObject[] cochesIzquierda;
    public GameObject trenPrefab;

    [Header("Configuración de generación")]
    public int numeroCarriles = 30;
    [Range(0f, 1f)] public float probabilidadCarretera = 0.55f;
    [Range(0f, 1f)] public float probabilidadTren = 0.1f;
    public int maxCarrilesPeligrososSeguidos = 2;

    [Header("Ritmo de tráfico por tipo de carril generado")]
    public float tiempoCoches = 1.6f;
    public int cantidadCoches = 10;
    public float tiempoTren = 3f;
    public int cantidadTren = 5;

    void Start()
    {
        TileBase tileAsfalto = tilemapSuelo.GetTile(tilemapSuelo.WorldToCell(muestraAsfalto.position));
        TileBase tileHierba = tilemapSuelo.GetTile(tilemapSuelo.WorldToCell(muestraHierba.position));

        BoundsInt limitesPintados = tilemapSuelo.cellBounds;
        int celdaXMin = limitesPintados.xMin;
        int celdaXMax = limitesPintados.xMax - 1;

        float xMundoMin = tilemapSuelo.CellToWorld(new Vector3Int(celdaXMin, 0, 0)).x;
        float xMundoMax = tilemapSuelo.CellToWorld(new Vector3Int(celdaXMax, 0, 0)).x;

        float yInicio = meta.position.y;
        DesplazarZonaDecorativaFinal(yInicio);

        int carrilesPeligrososSeguidos = 0;

        for (int i = 0; i < numeroCarriles; i++)
        {
            float y = yInicio + i;
            int celdaY = tilemapSuelo.WorldToCell(new Vector3(0, y, 0)).y;

            TipoCarril tipo = ElegirTipoCarril(ref carrilesPeligrososSeguidos);
            TileBase tilePintar = tipo == TipoCarril.Hierba ? tileHierba : tileAsfalto;

            for (int x = celdaXMin; x <= celdaXMax; x++)
            {
                tilemapSuelo.SetTile(new Vector3Int(x, celdaY, 0), tilePintar);
            }

            if (tipo == TipoCarril.Carretera)
            {
                bool derecha = Random.value < 0.5f;
                GameObject prefabSpawn = derecha ? spawnDerechaPrefab : spawnIzquierdaPrefab;
                float xSpawn = derecha ? xMundoMin : xMundoMax;
                GameObject instancia = Instantiate(prefabSpawn, new Vector3(xSpawn, y, 0), Quaternion.identity);

                GeneradorCoches generador = instancia.GetComponent<GeneradorCoches>();
                generador.tiempo = tiempoCoches;
                generador.cantidad = cantidadCoches;

                GameObject[] listaCoches = derecha ? cochesDerecha : cochesIzquierda;
                if (listaCoches != null && listaCoches.Length > 0)
                {
                    generador.cochePrefab = listaCoches[Random.Range(0, listaCoches.Length)];
                }
            }
            else if (tipo == TipoCarril.Tren)
            {
                float xSpawn = Random.value < 0.5f ? xMundoMin : xMundoMax;
                GameObject instancia = Instantiate(spawnTrenPrefab, new Vector3(xSpawn, y, 0), Quaternion.identity);

                GeneradorCoches generador = instancia.GetComponent<GeneradorCoches>();
                generador.tiempo = tiempoTren;
                generador.cantidad = cantidadTren;

                if (trenPrefab != null)
                {
                    generador.cochePrefab = trenPrefab;
                }
            }
        }
    }

    void DesplazarZonaDecorativaFinal(float yUmbral)
    {
        Vector3 desplazamiento = Vector3.up * numeroCarriles;

        foreach (Transform contenedor in contenedoresDecorado)
        {
            foreach (Transform hijo in contenedor)
            {
                if (hijo.position.y >= yUmbral - 0.5f)
                {
                    hijo.position += desplazamiento;
                }
            }
        }

        meta.position += desplazamiento;
    }

    TipoCarril ElegirTipoCarril(ref int carrilesPeligrososSeguidos)
    {
        if (carrilesPeligrososSeguidos >= maxCarrilesPeligrososSeguidos)
        {
            carrilesPeligrososSeguidos = 0;
            return TipoCarril.Hierba;
        }

        float dado = Random.value;
        TipoCarril tipo;
        if (dado < probabilidadTren) tipo = TipoCarril.Tren;
        else if (dado < probabilidadTren + probabilidadCarretera) tipo = TipoCarril.Carretera;
        else tipo = TipoCarril.Hierba;

        carrilesPeligrososSeguidos = tipo == TipoCarril.Hierba ? 0 : carrilesPeligrososSeguidos + 1;
        return tipo;
    }
}
