using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MovimientoJugador : MonoBehaviour
{

    public float distanciaSalto = 1f;
    public int vidas = 3;
    public float fuerzaRebote = 5f;

    [Header("Invulnerabilidad tras daño")]
    public float duracionInvulnerable = 0.8f;
    public float parpadeoIntervalo = 0.1f;

    [Header("Feel del salto")]
    public float duracionSalto = 0.1f;
    public float alturaSalto = 0.25f;
    public float intensidadSquash = 0.2f;


    public UIDocument hudVidas;
    public UIDocument documentoDerrota;
    public UIDocument documentoVictoria;


    public ParticleSystem particulasPlumas;
    public CamaraSeguimiento camara;

    [Header("Sonido")]
    public AudioClip clipSalto;
    [Range(0f, 1f)] public float volumenSalto = 0.4f;
    public AudioClip clipChoque;
    public AudioClip clipVictoria;
    public AudioClip clipDerrota;

    [Header("Música de fondo")]
    public AudioClip musicaFondo;
    [Range(0f, 1f)] public float volumenMusica = 0.5f;

    public InputActionAsset inputActions;
    private InputAction moveAction;
    private Label etiquetaVidas;
    private Rigidbody2D rb;
    private SpriteRenderer sprite;
    private AudioSource audioSource;
    private AudioSource audioMusica;
    private Vector3 escalaBase;
    private bool enSalto;
    private bool invulnerable;

    void Awake()
    {
        Time.timeScale = 1f;
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        escalaBase = transform.localScale;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        audioMusica = gameObject.AddComponent<AudioSource>();
        audioMusica.playOnAwake = false;
        audioMusica.loop = true;

        if (musicaFondo != null)
        {
            audioMusica.clip = musicaFondo;
            audioMusica.volume = volumenMusica;
            audioMusica.Play();
        }

        var mapa = inputActions.FindActionMap("Jugador");
        moveAction = mapa.FindAction("Move");
    }

    void OnEnable()
    {
        moveAction.Enable();

       
        if (hudVidas != null)
        {
            etiquetaVidas = hudVidas.rootVisualElement.Q<Label>("TextoVidas");
            ActualizarUI();
        }

      
        if (documentoDerrota != null)
        {
            documentoDerrota.rootVisualElement.style.display = DisplayStyle.None;

            var botonDerrota = documentoDerrota.rootVisualElement.Q<Button>("BotonReintentar");
            if (botonDerrota != null) botonDerrota.clicked += Reiniciar;
        }

        if (documentoVictoria != null)
        {
            documentoVictoria.rootVisualElement.style.display = DisplayStyle.None;

            var botonVictoria = documentoVictoria.rootVisualElement.Q<Button>("BotonReintentar");
            if (botonVictoria != null) botonVictoria.clicked += Reiniciar;
        }
    }

    void OnDisable() => moveAction.Disable();

    void Update()
    {
        if (Time.timeScale == 0) return;

        if (!enSalto && moveAction.triggered)
        {
            Vector2 dir = moveAction.ReadValue<Vector2>();
            if (dir != Vector2.zero) StartCoroutine(Saltar(dir));
        }
    }

    IEnumerator Saltar(Vector2 dir)
    {
        enSalto = true;

        if (clipSalto != null) audioSource.PlayOneShot(clipSalto, volumenSalto);

        if (dir == Vector2.left) sprite.flipX = true;
        else if (dir == Vector2.right) sprite.flipX = false;

        Vector3 inicio = transform.position;
        Vector3 destino = inicio + (Vector3)(dir * distanciaSalto);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracionSalto;
            float tc = Mathf.Clamp01(t);
            float fase = Mathf.Sin(tc * Mathf.PI);

            Vector3 arco = Vector3.up * fase * alturaSalto;
            transform.position = Vector3.Lerp(inicio, destino, tc) + arco;

            transform.localScale = new Vector3(
                escalaBase.x * (1f - fase * intensidadSquash * 0.5f),
                escalaBase.y * (1f + fase * intensidadSquash),
                escalaBase.z);

            yield return null;
        }

        transform.position = destino;
        transform.localScale = escalaBase;
        enSalto = false;
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        
        if (otro.CompareTag("Coche"))
        {
            if (invulnerable) return;

            if (clipChoque != null) audioSource.PlayOneShot(clipChoque);

            if (particulasPlumas != null)
            {
                particulasPlumas.transform.position = transform.position;
                particulasPlumas.Play();
            }


            Vector2 direccion = (transform.position - otro.transform.position).normalized;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.AddForce(direccion * fuerzaRebote, ForceMode2D.Impulse);
            }

            if (camara != null) camara.Sacudir();
            StartCoroutine(ReaccionDanio());

            vidas--;
            ActualizarUI();

            if (vidas <= 0) Morir();
        }

      
        if (otro.CompareTag("Meta"))
        {
            Ganar();
        }
    }

    void ActualizarUI()
    {
        if (etiquetaVidas != null) etiquetaVidas.text = "Vidas: " + vidas;
    }

    IEnumerator ReaccionDanio()
    {
        invulnerable = true;
        Color colorOriginal = sprite.color;

        sprite.color = Color.red;
        yield return new WaitForSecondsRealtime(0.1f);
        sprite.color = colorOriginal;

        float t = 0f;
        while (t < duracionInvulnerable)
        {
            t += Time.deltaTime;
            Color c = sprite.color;
            c.a = (Mathf.FloorToInt(t / parpadeoIntervalo) % 2 == 0) ? 0.35f : 1f;
            sprite.color = c;
            yield return null;
        }

        Color final = sprite.color;
        final.a = 1f;
        sprite.color = final;
        invulnerable = false;
    }

    void Morir()
    {
        Debug.Log("Derrota");

        audioMusica.Stop();
        if (clipDerrota != null) audioSource.PlayOneShot(clipDerrota);

        if (documentoDerrota != null)
        {
            documentoDerrota.sortingOrder = 1000;
            MostrarPanelAnimado(documentoDerrota);
        }
        Time.timeScale = 0f;
    }

    void Ganar()
    {
        Debug.Log("Victoria");

        audioMusica.Stop();
        if (clipVictoria != null) audioSource.PlayOneShot(clipVictoria);

        if (documentoVictoria != null)
        {
            documentoVictoria.sortingOrder = 1000;
            MostrarPanelAnimado(documentoVictoria);
        }
        Time.timeScale = 0f;
    }

    void Reiniciar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void MostrarPanelAnimado(UIDocument doc)
    {
        VisualElement root = doc.rootVisualElement;

        root.style.transitionProperty = new List<StylePropertyName> { "opacity", "scale" };
        root.style.transitionDuration = new List<TimeValue> { new TimeValue(300, TimeUnit.Millisecond) };
        root.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutBack) };

        root.style.opacity = 0f;
        root.style.scale = new StyleScale(new Scale(Vector3.one * 0.6f));
        root.style.display = DisplayStyle.Flex;

        root.schedule.Execute(() =>
        {
            root.style.opacity = 1f;
            root.style.scale = new StyleScale(new Scale(Vector3.one));
        }).ExecuteLater(20);
    }
}