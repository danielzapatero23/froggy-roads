# P4-DZF — Clon de Crossy Road (Unity 2D)

Proyecto Unity (URP 2D) que imita Crossy Road: el jugador salta entre carriles esquivando coches y un tren hasta llegar a una meta. El nivel base está armado a mano en `SampleScene`, pero ahora se le añaden carriles extra generados por código antes de la meta (ver `GeneradorCarriles.cs`).

## Stack
- Unity URP 2D, UI Toolkit (no uGUI/Canvas), nuevo Input System.
- Físicas: `Rigidbody2D` + `Collider2D` (triggers para colisiones de juego).
- Object pooling manual para tráfico (sin `Instantiate`/`Destroy` en runtime).

## Escena principal
`Assets/Scripts/Scenes/SampleScene.unity` — única escena jugable, muy grande (~213k líneas), todo el nivel está colocado a mano (árboles, casas, carriles, spawners, meta).

## Scripts (`Assets/Scripts/*.cs`)

### `MovimientoJugador.cs`
- Va en el prefab `Jugador`. Movimiento por salto en grid animado por coroutine (`Saltar()`): interpola posición con arco (`Vector3.up * sin(π·t) * alturaSalto`) y aplica squash & stretch sobre `escalaBase` durante `duracionSalto` segundos. Valores actuales con buen feel: `duracionSalto 0.1`, `alturaSalto 0.25`, `intensidadSquash 0.2` (públicos, ajustables en Inspector). Bloquea nuevos saltos mientras `enSalto` es true (sin input buffering).
- El sprite (`chicken.png`) es de cara/top-down, no de 4 direcciones — **no se rota** nunca; solo se usa `sprite.flipX` para mirar izquierda/derecha. Arriba/abajo no cambian el sprite.
- Lee la acción `Move` del Input Actions asset `ControlesPollo.inputactions` (action map `Jugador`).
- Vidas: al chocar con algo tag `Coche` (si `invulnerable` es true, el golpe se ignora por completo — `return` antes de cualquier efecto):
  1. Reproduce partículas (`particulasPlumas`).
  2. Aplica impulso de rebote (`fuerzaRebote`) vía `Rigidbody2D`.
  3. Llama a `camara.Sacudir()` (campo público `CamaraSeguimiento`, **hay que arrastrarlo a mano en el Inspector de la instancia de escena**, no se puede enlazar desde el prefab).
  4. `ReaccionDanio()`: flash rojo 0.1s (tiempo real) y después parpadeo de alfa (1 ↔ 0.35 cada `parpadeoIntervalo` 0.1s) durante `duracionInvulnerable` (0.8s) — mientras dura, `invulnerable = true` y no se puede volver a perder vida.
  5. Resta vida y actualiza UI.
- Win condition: trigger con tag `Meta`.
- Sonido: 4 campos públicos `AudioClip` (`clipSalto`, `clipChoque`, `clipVictoria`, `clipDerrota`), todos `null` por defecto — el juego funciona igual sin sonido si no se arrastra ningún clip. Se reproducen con `audioSource.PlayOneShot()` (guardado con `if (clip != null)`) en `Saltar()`, en el golpe con `Coche`, en `Morir()` y en `Ganar()`. El `AudioSource` no está en el prefab; se obtiene con `GetComponent` o se añade con `AddComponent` en `Awake()` para no tener que editar el `.prefab`/`.unity` a mano. `PlayOneShot` sigue sonando aunque `Time.timeScale = 0` (pausa de derrota/victoria), porque el audio de Unity no depende del timeScale.
  - `clipSalto` se reproduce a `volumenSalto` (0.4 por defecto, no a volumen completo) porque al sonar en cada salto se hacía repetitivo/cansino a volumen normal.
  - Segundo `AudioSource` (`audioMusica`) independiente del de los SFX, añadido también por código en `Awake()`: si `musicaFondo` tiene un clip asignado, lo pone en loop y lo reproduce a `volumenMusica` (0.5 por defecto); se detiene con `audioMusica.Stop()` en `Morir()` y en `Ganar()`. Sin `musicaFondo` asignado, simplemente no suena música (no rompe nada).
- UI vía `UIDocument` (UI Toolkit): HUD de vidas (label `TextoVidas`, definido en `MenuJuego.uxml`), pantalla de derrota (`DerrotaUi.uxml`) y victoria (`VictoriaUI.uxml`).
  - `Morir()`/`Ganar()` pausan con `Time.timeScale = 0` y muestran el panel correspondiente vía `MostrarPanelAnimado()`: arranca en opacidad 0 / escala 0.6 y anima a opacidad 1 / escala 1 con `EaseOutBack` en 0.3s (efecto "pop" con rebote), usando las transition properties de UI Toolkit (`style.transitionProperty/Duration/TimingFunction`), no CSS en el `.uxml`.
  - Ambos UXML tienen un botón `BotonReintentar`; en `OnEnable()` se busca por nombre (`Q<Button>("BotonReintentar")`) y se suscribe a `Reiniciar()`, que pone `Time.timeScale = 1` y recarga la escena activa (`SceneManager.LoadScene`). `SampleScene` ya está en Build Settings, así que esto funciona out of the box.

### `MovimientoCoche.cs`
- Movimiento lineal con `direccion` (enum `DireccionCoche { Izquierda, Derecha }`, definido en el mismo archivo) × `velocidad`: se traduce a `Vector3.right`/`Vector3.left` en `Update()`. Antes era un `Vector3` libre (riesgo de coches mal configurados sin moverse o moviéndose en diagonal); el enum lo hace imposible de configurar mal desde el Inspector.
- Se autodesactiva (`SetActive(false)`) al salir de un rango fijo (x: ±120, y: ±200) — vuelve al pool en lugar de destruirse.
- Mismo script lo usa el tren (`Tren2.prefab`, tag `Coche` también).
- Los 7 prefabs que usan este componente (`Coche1/2/3 Derecha/Izquierda`, `Tren2`) tenían el valor de `direccion` guardado en el **asset del prefab** (no en la escena), así que al cambiar el tipo de campo se editaron a mano los 7 `.prefab` (YAML) para que el valor serializado pasara de `{x: 1, y: 0, z: 0}` a `1` (Derecha) o de `{x: -1, y: 0, z: 0}` a `0` (Izquierda) — si no, Unity no puede mapear el `Vector3` viejo al `enum` nuevo y todos los coches habrían vuelto a la dirección por defecto. Se confirmó por grep que `SampleScene.unity` no tiene overrides propios de `direccion`, así que no hizo falta tocar la escena.

### `GeneradorCoches.cs`
- Pool de `cantidad` instancias de `cochePrefab`, creadas una vez en `Start()` y reactivadas cada `tiempo` segundos (default `1.6`).
- Campo `variacionTiempo` (default `0.4`): cada spawn elige un intervalo aleatorio `tiempo ± variacionTiempo` (clamp mínimo 0.3s) en vez de uno fijo (1.2–2.0s con los defaults actuales), para que el tráfico no se sienta robótico/sincronizado entre carriles sin volverse impredecible. Se probó en `0` (fijo) y se devolvió a `0.4` por decisión de diseño.
- **Nota histórica (ya no aplica del todo)**: los 20 prefabs-instancia `SpawnDerecha/Izquierda/Tren` en `SampleScene` tienen guardados campos huérfanos (`prefabCoche`, `tiempoEntreCoches`) de una versión anterior del script, y solo `cochePrefab` está sobreescrito por instancia — `tiempo`/`cantidad` siguen en el valor del script para los 20 spawners salvo el caso de abajo, no hay diferenciación por carril guardada en la escena. **No se tocó el `.unity` directamente** (213k líneas, riesgo de corromperlo a mano); la solución fue la variación aleatoria en código.
- El tren va más espaciado que los coches: como las 4 instancias de `SpawnTren` en la escena no tenían override propio de `Tiempo`, se subió directamente en el **asset del prefab** `SpawnTren.prefab` (de `1.6` a `3`), afectando a las 4 a la vez sin tocar `SpawnDerecha`/`SpawnIzquierda` (son prefabs distintos) ni la escena.

### `GeneradorCarriles.cs`
- Genera `numeroCarriles` carriles adicionales por código en `Start()`, **antes** de la `Meta` (no sustituye el nivel a mano, lo alarga). Cada carril es 1 celda de alto (`Grid` del proyecto tiene `m_CellSize = {1,1}`, igual que `distanciaSalto` del jugador — 1 salto = 1 carril).
- No depende de ninguna pared para saber el ancho del nivel: las 4 `Pared*` de la escena son solo para que el jugador no se salga de la pantalla en móvil (clamp visual), **no marcan los límites de la carretera** — usarlas como referencia de ancho fue un error inicial que generó carriles/coches mal colocados. La versión correcta lee los límites reales con `tilemapSuelo.cellBounds` (hasta dónde está pintado el tilemap a mano).
- Pinta el suelo de los carriles nuevos reutilizando los **mismos tiles** que ya existen en el nivel: en vez de que alguien identifique nombres de tile en la Tile Palette (la paleta tiene 100+ tiles sin nombre semántico, solo `tilemap_N`), el script muestrea por código el tile que ya hay pintado en dos objetos de referencia (`muestraAsfalto`, `muestraHierba` — cualquier `SpawnDerecha/Izquierda` y cualquier `Arbol`, que ya están sobre asfalto/hierba) con `Tilemap.GetTile(WorldToCell(...))`, y repinta esa misma `TileBase` en las filas nuevas. El asfalto generado es liso (no replica bordes/líneas decorativas de la carretera a mano) — decisión consciente para simplificar.
- Cada carril generado se elige aleatoriamente como `Hierba`/`Carretera`/`Tren` (pesos `probabilidadCarretera`/`probabilidadTren`), con `maxCarrilesPeligrososSeguidos` para forzar un descanso de hierba y que no se generen carriles imposibles.
- Para carriles de tráfico, instancia por código (`Instantiate`, una sola vez al iniciar la partida, no en runtime por jugador) el prefab `SpawnDerecha`/`SpawnIzquierda`/`SpawnTren` correspondiente. **Importante**: esos prefabs no traen ningún coche asignado por defecto (en el nivel a mano, `cochePrefab` se asigna por instancia en el Inspector) — así que el script le asigna uno él mismo justo después de instanciar, elegido al azar de los arrays `cochesDerecha`/`cochesIzquierda`/`trenPrefab` (los 6 prefabs de coches + el tren, arrastrados en el Inspector). Sin esto, el `GeneradorCoches` de los spawners nuevos lanza `UnassignedReferenceException` al arrancar.
- Al terminar, mueve la `Meta` (`meta.position`) justo después del último carril generado, alargando el nivel sin tocar el resto a mano.
- El `GameObject` `GeneradorCarriles` se añadió directamente en `SampleScene.unity` por edición de archivo (no desde el editor), enlazando por código casi todas las referencias reutilizando fileIDs ya existentes en la escena (el `Tilemap`, un `SpawnIzquierda` y un `Arbol` ya colocados, y los prefabs de coches/spawners por su guid). El único campo que no se pudo enlazar así al principio fue `meta` (la instancia de `Meta` no tenía ningún fileID "stripped" ya materializado en el archivo para reutilizar); se resolvió añadiendo a mano una entrada `Transform ... stripped` nueva en el `.unity` apuntando a `m_PrefabInstance` de `Meta`, con un fileID nuevo elegido libremente (el número en sí no necesita coincidir con ningún hash de Unity, solo ser único en el archivo — es el mismo mecanismo que usa el editor cuando arrastras algo a un campo).

### `CamaraSeguimiento.cs`
- Sigue al `Transform` del jugador (`pollo`) con `Vector3.SmoothDamp` (campo `suavizado`, actual 0.08) en vez de copiar la posición 1:1 — da inercia suave a la cámara.
- `z` fijo en -10.
- Método público `Sacudir()`: dispara una coroutine de shake (`shakeDuracion` 0.15s, `shakeIntensidad` 0.18) que suma un offset aleatorio (`Random.insideUnitCircle`) atenuado linealmente a la posición final. Se llama desde `MovimientoJugador` al recibir daño.

## Prefabs clave (`Assets/Prefabs/`)
- `Jugador` — Rigidbody2D (gravity scale 0, freeze rotation), BoxCollider2D, `MovimientoJugador`.
- `Coche1/2/3 Derecha/Izquierda`, `Tren2` — tag `Coche`, collider trigger, `MovimientoCoche`.
- `SpawnDerecha`, `SpawnIzquierda`, `SpawnTren` — solo `GeneradorCoches`, sin geometría visible; se instancian repetidas veces en la escena (uno por carril) con distinto `cochePrefab` asignado.
- `Vias` — sprite estático de las vías de tren (decorativo, sin lógica).
- `Meta` — el prefab base no tiene collider ni tag (`Untagged`); el tag `Meta` y un `BoxCollider2D` trigger se añaden como **override en la instancia de escena**, no en el asset. Si se crea una meta nueva desde el prefab hay que recordar añadir ambas cosas a mano.
- `Arbol1-10`, `Casa1-3`, `LineaBlanca`, `LineaBlancaCortada` — decorado del nivel, sin scripts.

## Tags usados
- `Coche` — cualquier obstáculo móvil (coches y tren) que quita vida al jugador.
- `Meta` — trigger de victoria (solo en la instancia de escena, ver arriba).

## UI Toolkit (`Assets/UI Toolkit/`)
- `MenuJuego.uxml` — HUD con label de vidas.
- `DerrotaUi.uxml` / `VictoriaUI.uxml` — cada uno tiene un `VisualElement "Fondo"` (fondo negro semitransparente a pantalla completa) con el label de texto y un `ui:Button name="BotonReintentar"`. Ocultos por defecto (`style.display = None` en `OnEnable`) y mostrados con animación de pop (ver `MostrarPanelAnimado` arriba).
- `PantallasFin.uss` — estilos compartidos por ambos UXML (importados con `<Style src="PantallasFin.uss" />`):
  - `.titulo` / `.titulo-derrota` / `.titulo-victoria`: usan la fuente pixel `PressStart2P-Regular.ttf` (referenciada por GUID `f619b057ef203a8429920e3ffa7b9ab7`, fileID `12800000`) con contorno negro de 2px; rojo en derrota, verde en victoria.
  - `.boton-reintentar`: botón sin bordes redondeados (estética pixel), azul (`rgb(41, 121, 196)`) en ambas pantallas, con estados `:hover` (más claro + scale 1.05) y `:active` (más oscuro + scale 0.93). Antes había un color distinto por pantalla (rojo/verde); se unificó a azul a petición del usuario.

## Setup manual pendiente en el editor (no se puede hacer desde código/asset)
- En la instancia de `Jugador` en `SampleScene`, el campo `camara` (en `MovimientoJugador`) debe arrastrarse a mano apuntando a la `Main Camera` (la que tiene `CamaraSeguimiento`). Si se queda vacío, el juego funciona igual pero sin shake al recibir daño.
- Los 4 campos de sonido (`clipSalto`, `clipChoque`, `clipVictoria`, `clipDerrota`) y el de música (`musicaFondo`) en `MovimientoJugador` siguen vacíos por defecto — hay que arrastrar a mano los clips ya disponibles en `Assets/Audio/`: `salto.ogg`, `choque.ogg`, `victoria.ogg`, `derrota.ogg` (packs CC0 "Impact Sounds" y "Music Jingles" de Kenney) y `musica_fondo.wav` ("Arcade Song" de aqrezes, CC0, vía OpenGameArt.org — se usó esta fuente en vez del bundle de Kenney en itch.io porque ese pedía pago). Sin clips asignados el juego funciona igual, simplemente sin sonido/música.

## Mejoras pendientes / ideas para evolucionar el juego
- Probar en el editor `GeneradorCarriles` con `numeroCarriles` más alto (está en `10` para pruebas) y ajustar `probabilidadCarretera`/`probabilidadTren`/`maxCarrilesPeligrososSeguidos` a gusto.
- El usuario mencionó que se plantea "cambiar la mecánica de todo" más adelante — las 4 `Pared*` (clamp de pantalla en móvil) podrían quedar obsoletas o necesitar revisión si cambia el sistema de movimiento/cámara.
