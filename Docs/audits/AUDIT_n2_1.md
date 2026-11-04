# AUDIT N2.1 — El campamento: mapa, guardias, anclas

**Unity no se abrió en esta sesión: nada se corrió ni se probó en el Editor ni en Play.** La escena `Campamento.unity` todavía no existe; la crea el menú. Lo único verificado es que el código compila.

Leídos: `CLAUDE.md`, `Fase-N2-Campamento.md`, `Docs/guiones/Guion-Nivel2.md` (`Docs/guiones/nivel2.md` no existe: el guion sigue sin copiar con ese nombre), `Fase-N1-Mapa-Tutorial.md` §1, `Fase-3C-Contador.md` §0. Graphify para los sistemas de *Touches* (el grafo está 12 commits atrás, así que abrí los archivos que iba a llamar). `LevelBuilder_NivelPorky` solo se leyó.

## ⚠ Lo primero que debe decidir David (no bloquea N2.1, sí los finales)

1. **`GameStateTracker` y `GameManager` no están en ninguna escena ni prefab del repo** (busqué sus GUID en todos los `.unity`, `.prefab` y `.asset`). Nada los crea por código. `PHASE-3A8-audit.md:86` ya registró el aviso «No hay gamestatetracker en la escena». No pude comprobar si David los agrega a mano al probar.
   - `Campamento` ya los trae (objeto `GameManagers`). **Pero viniendo de `NivelPorky` nace vacío:** las pistas, los interrogatorios y el estado de Centavo del Nivel 1 no existen, `HasRevolverFromEarlierScene` da `false` (Wilbur llega sin revólver) y la tabla L8 sale mal. En `NivelPorky`, Esc tampoco pausa.
   - **Arreglo:** o una línea en `LevelBuilder_NivelPorky` (dueña: Sesión A) o crearlos por código en `GameStateTracker.cs` (dueña: Sesión B). No lo hice: cambia el comportamiento del Nivel 1 mientras A trabaja en él, y N2.1 no lo pide. Recomiendo la segunda (no depende de regenerar ninguna escena).
2. **No hay pantalla de muerte en todo el juego:** `PlayerHealth.onPlayerDied` no tiene oyentes y `UIManager` no está en ninguna escena. Para cumplir L10/H17, `CampamentoLevelStart` escucha `OnDeath` y recarga la escena a los 2.5 s + fundido, **sin borrar el tracker**. El Nivel 1 sigue sin reinicio al morir.
3. **Pausa → Reiniciar borra la partida:** `GameManager.RetryCurrentLevel` llama `ResetState()` (pistas, interrogatorios, Centavo). Contradice N2.4 («reiniciar dentro de un nivel NO borra»). Queda para N2.4.
4. **`CluePickup` no muestra aviso ni texto:** ambos pasan por `UIManager`, que no existe. Con E la pista sí se registra (si hay tracker), suena y comenta la IA, pero el jugador no ve el `[E]` ni lee la pista. Es de Edgar; afecta también a las anclas de este nivel mientras tengan la pista automática.

---

- **Checklist de escena (qué necesita una escena de juego y cómo se puso cada cosa):**

  | Necesita | Cómo lo tiene `NivelPorky` | Cómo lo tiene `Campamento` |
  |---|---|---|
  | Player (cámara, AudioListener, armas, mira, munición, `PlayerAim`, holster, lluvia, pasos, `AudioLibraryLoader`) | `Player.prefab` sobre `PlayerSpawn` | Igual, sobre `PlayerSpawn` |
  | Prefabs al día | `TestPrefabGenerator.EnsurePrefabs(false)` | Igual |
  | Renderer | `RenderingSetup.Configure()` | Igual |
  | HUD | `HudBuilder.BuildPrefab` + `PlaceInOpenScene` | Solo `PlaceInOpenScene` (crea el prefab si falta) |
  | Mira | Dentro del Player (`RubberHoseCrosshair`) | Igual; no se crea otra |
  | Pausa | `PauseMenuSpawner` automático (`Resources/PauseMenu_Canvas`); necesita `GameManager` | Igual, y aquí sí hay `GameManager` |
  | AICompanion | Prefab en la raíz | Igual |
  | GameStateTracker + GameManager | **No están** (ver arriba) | Objeto raíz `GameManagers`; si ya vienen de otra escena, esta copia se destruye sola |
  | Audio | `AudioLibraryLoader` en el Player | Igual + `CampamentoAmbience` |
  | Volume | `NoirVolumeBuilder.CreateTechnicolorVolume` | Igual |
  | Cielo y niebla | `NightSkyBuilder.Build` | Igual, con niebla propia y el disco lejano más abajo (si no, tapa el río y el barranco) |
  | NavMesh | `NavMeshBuilder.BuildNavMesh` después de guardar | Igual |
  | Canvas de interrogatorio | `CentavoInterrogation` lo instancia al usarlo | Instancia en la escena (`InterrogationCanvas` se cierra solo en `Awake`), lista para N2.3 |
  | Diálogo de guardias | `DialogueUI` se crea sola | Igual |
  | Tutorial | N1.3 (Nivel 1) aún no existe | No aplica: las pistas salen una vez por sesión, en el Nivel 1 |
  | Build Settings | `BuildSceneOrder.Ensure` | Igual, con `Campamento` en la lista |

- **Mapa ASCII + tamaño de cada zona:** norte arriba, 1 columna = 1 m, 1 fila = 2 m, generado con las coordenadas del builder.

  ```
    80
                          HHHHHHH                 ~~~~~~
                          H     H                 ~~~~~~
                    HHHHHHHHHXHHHHHHHHH           ~~~~~~
                    H   2   0L    1   H           ~~~~~~
    70              HR  |   EEE   |  RH           ~~~~~~
                    H        C        H           ~~~~~~
                    H 3            B  H           ~~~~~~
                    H M |         |B  H           ~~~~~~
                    c        t     B  b           ~~~~~~
    60            HHHHHHHHHHH/HHbYHHHHHHH         ~~~~~~
                  H        H   H    A   H         ~~~~~~
                  H        Hc  /    e   +         ~~~~~~
       vvv       THHHHHHHHHHH/HHHHHHHHHHHT        ~~~~~~
       vvv       T              D        T        ~~~~~~
    50 vvv      T                         T       ~~~~~~
       vvv      !                         =       ~~~~~~
       vvv      !         S               ==      ~~~~~~
       vvv     !    o           o    Z     =      ~~~~~~
       vvv     !                     Z     =      ~~~~~~
    40 vvv     !         o             c   ==     ~~~~~~
       vvv    !!                            =     ~~~~~~
       vvv   !!      o               o       =    ~~~~~~
       vvv   !          o        o           ==   ~~~~~~
       vvv  !!                                =   ~~~~~~
    30 vvv !!                                  =  ~~~~~~
       vvv !           ............. HHHHHHHHH =  ~~~~~~
       vvv ! HHHHHHHcH .           . H       H =  ~~~~~~
       vvv ! H   a   H .           . H       H =  ~~~~~~
       vvv ! H       H . K         . H       H =  ~~~~~~
    20 vvv ! HHHH/HHHH .         G . HHHHHHHHH =  ~~~~~~
       vvv !           .           .    ^      =  ~~~~~~
       vvv !     ^     .     F     .           =  ~~~~~~
       vvv !           .           .           =  ~~~~~~
       vvv ! HHHHHHHHH .           .       ^   =  ~~~~~~
    10 vvv ! H       H .           .       +   =  ~~~~~~
       vvv ! HHHHHHHHH P............           =  ~~~~~~
       vvv !                                   =  ~~~~~~
       vvv #########   ...........     #########  ~~~~~~
                   #   .         .     #          ~~~~~~
     0             #   .         .  U  #          ~~~~~~
                   #   . S       .     #          ~~~~~~
                   #   .         .  U  #          ~~~~~~
                   #   .         .     #          ~~~~~~
                   #   ..........P     #          ~~~~~~
   -10             #####T###///###T#####          ~~~~~~
                        T         T               ~~~~~~
                        T         T               ~~~~~~
                        T         TT              ~~~~~~
                        T         T               ~~~~~~
   -20                  T         T               ~~~~~~
                        T         T               ~~~~~~
                        T         T               ~~~~~~
                       TT         T               ~~~~~~
                       T          T               ~~~~~~
   -30                 TT         T               ~~~~~~
                        T         T               ~~~~~~
                        T         TT              ~~~~~~
                        T          T              ~~~~~~
                        T          T              ~~~~~~
   -40                  T          T              ~~~~~~
                        T          T              ~~~~~~
                        T         TT              ~~~~~~
                        T         T               ~~~~~~
                        T    @    T               ~~~~~~
   -50                 TTTTTTTTTTTT               ~~~~~~

         |         |         |         |         |
       -20       -10       0         10        20
  ```

  **Leyenda.** `@` inicio · `S` centinela · `P` inicio de patrulla, `.` su ruta · `K` el Contable · `D` guardia de la puerta · `R` `Anchor_Refuerzo_A/B` · `L` `Anchor_LoboStart` · `0 1 2 3` `Anchor_LoboCover_*` · `C` `Anchor_ConfrontationCam` · `t` `Trigger_Salon` · `X` `Door_CuartoAtras` · `a` `Anchor_Clue_foto_reunion_01` · `A` `Anchor_Clue_carta_lobo_01` · `c` cartuchos · `+` botiquín · `b` balas · `T` árboles · `#` cerca de tablas · `=` baranda del río · `!` baranda rota del barranco · `H` muro · `/` marco abierto · `^` carpa · `F` fogata · `G` gramófono · `U` camioneta · `o` troncos o tablas · `Z` sierra · `E` escritorio del Lobo · `e` escritorio de la oficina · `|` columna · `B` barra · `M` billar · `Y` sofá · `~` río · `v` barranco.

  | Zona | Pedido | Hecho |
  |---|---|---|
  | Z0 Bosque | sendero de ~40 m | x -5..5, z -50..-10 (40 m; ~8 m de ancho entre árboles) |
  | Z1 Portón | 20 × 15 | x -10..10, z -10..5 (20 × 15) |
  | Z2 Barracones | 35 × 25 | x -17.5..18, z 5..30 (35.5 × 25) |
  | Z3 Patio | 25 × 20 | x ±13..18 (se cierra hacia el norte), z 30..50 (~30 × 20: algo más ancho, para que el río y el barranco sigan rectos) |
  | Z4 Guarida | salón ~18 × 14 | salón x -9..9, z 60..74 (18 × 14); pasillo 3 × 6; oficina 9.5 × 6; cuarto de atrás 6 × 4, cerrado |

- **Bordes: tipos usados, ¿kit de la Sesión A o propio? cosas que bloquean sin verse (debe ser "ninguna"):**
  - **Propio.** El kit de la Sesión A solo está en su rama `worktree-n1-1`, sin integrar a `integracion/dia4`; depender de él rompería la compilación. La forma de los árboles se copió de ahí. Cuando se integre se pueden unificar, pero no hace falta.
  - **Tipos:** árboles densos (bosque y esquinas de la cabaña, con 3 variantes cada ~6 m: matas, roca y tronco caído), cerca de tablas del aserradero de 1.6 m (`CartoonKit.Fence`, se ve entre las tablas), baranda sobre el río de 1.2 m con las luces de Porcópolis en tarjetas al otro lado, baranda rota sobre el barranco de 1.2 m. La fachada de la cabaña cierra el norte con porche, troncos y 4 ventanas encendidas.
  - **Altura:** no hay salto (`jumpForce = 0`), `stepOffset = 0.3`; lo mínimo es 1.0 m (N3). Las balas chocan con todos.
  - **Baranda rota:** nunca deja un hueco abierto con collider detrás. Donde falta el pasamanos hay tablas en cruz; el travesaño de abajo siempre está entero.
  - **N5 (variedad, ~8 m):** se cumple en árboles y barandas. **Se excede en la cerca del portón:** los costados de Z1 miden 15 m de cerca seguida, solo cortada por cajas y barriles apoyados. A revisar a ojo.
  - **Lo que bloquea sin verse: ninguna pieza, por diseño.** No se pudo mirar en el Editor. La `Fence` del kit usa cajas invisibles, pero en el mismo lugar de sus tablas. Sin volumen de seguridad: no hizo falta, y fuera de los bordes no hay colisión.
  - **Prueba automática:** al generar, `ValidateBorders` camina el nivel con la cápsula del jugador cada 0.5 m y reporta en consola si llega fuera del contorno o sin piso. Detecta huecos, no muros invisibles.

- **Guardias: tipos, rutas, ¿el Contable se rinde y abre su diálogo?**

  | Guardia | Prefab / tipo | Dónde |
  |---|---|---|
  | `Guard_Centinela_Porton` | `Enemy_Guard`, Centinela | Z1 (-4, -1.5), mira al portón |
  | `Guard_Patrulla_Porton` | `Enemy_Guard`, Patrullero | Z1: (4.2, -7) → (4.2, 3) → (-6.5, 3) → (-6.5, -7.5) |
  | `Guard_Patrulla_Barracones` | `Enemy_Guard`, Patrullero | Z2, alrededor de la fogata: (-6, 8) → (6, 8) → (6, 28) → (-6, 28) |
  | `Guard_Contable` | `Enemy_Interrogable`, Centinela, `npcId = guardia_registro` | Z2 (-4.2, 21.1), junto a su mesa con libros |
  | `Guard_Centinela_Patio` | `Enemy_Guard`, Centinela | Z3 (-3, 46.5) |
  | `Guard_Centinela_Puerta` | `Enemy_Guard`, Centinela | Z4 (3.2, 51.8), junto a la puerta |

  - **Contable:** por código sí: `canSurrender` en el prefab, `DialogueNPC` y árbol `guardia_registro` en `DialogueDatabase`. **Sin probar en Play.** Da resultado `Helpful` sin pista (D1).
  - **Tuerto (`guardia_barracon`) no se coloca:** L3 pide un solo rendible. Sus diálogos y su pista quedan sin usar, así que el juego tiene 2 guardias interrogables + Centavo, no los «3 guardias» de `CLAUDE.md`. Para L8 alcanza.
  - Refuerzos: `Anchor_Refuerzo_A` y `_B` junto a las puertas laterales (`Puerta_Lateral_A/B`), **con el GameObject apagado**, como dice la fase. N2.2 debe usar referencia serializada, no `GameObject.Find`.
  - `ValidateRoutes` reporta en consola cada ronda y si hay camino del inicio a las anclas del salón. Sin `EnemyAI` ni código de enemigo nuevo.

- **Anclas de pistas: ¿qué se puso en cada una? (CluePickup / placeholder / de Edgar intacto)**
  - `Anchor_Clue_foto_reunion_01` (mesa del barracón A) y `Anchor_Clue_carta_lobo_01` (escritorio de la oficina): si están vacías, `CluePickup` con los datos de `PHASE-3B.md` y una hoja dibujada.
  - **Lo de Edgar no se pisa:** el grupo `--- ANCLAS DE PISTAS ---` sobrevive a regenerar (el menú abre la escena, no la crea de cero). Si un ancla tiene algo que no sea `Pista_Auto_*`, no se toca ni se mueve.
  - Las dos quedan a más de 3 m del otro lado de cualquier muro: `CluePickup` no pide línea de vista y se podrían tomar a través de la pared.

- **Objetos: posición y mueble/contenedor de cada uno:**

  | Id (asset) | Tipo | Dónde | Mueble |
  |---|---|---|---|
  | `Cartuchos_Barracon` | Cartuchos | (-9.4, 25.55) | repisa de 1.05 m en el barracón A |
  | `Botiquin_Carpa` | Botiquín | (13.5, 9.3) | cajón abierto a la entrada de la carpa A |
  | `Cartuchos_Aserradero` | Cartuchos | (10.2, 40.6) | banco de trabajo junto a la sierra |
  | `Cartuchos_Pasillo` | Cartuchos | (-1.12, 56.2) | consola del pasillo |
  | `Botiquin_Oficina` | Botiquín | (10.55, 57) | repisa del estante de la oficina |
  | `Balas_Oficina` | Balas | (3, 59) | cajón abierto de la oficina |
  | `Cartuchos_Gabinete` | Cartuchos | (-8.55, 61.6) | gabinete, muro oeste del salón |
  | `Balas_Barra` | Balas | (8.6, 62.6) | repisa baja del estante detrás de la barra (hay que entrar por el extremo norte) |

  - Salen de `Data/PickupLayout_Campamento.asset`, sembrado una sola vez; después manda «Guardar posiciones de objetos».
  - El builder avisa si un objeto queda a menos de 5 cm del piso.
  - **Drops de guardias apagados** en esta escena (`GuardDropSpawner` desactivado). Ojo: «HUD → Colocar objetos» rehace el grupo y los vuelve a encender. *Generar Campamento* los apaga de nuevo.
  - **Vaivén:** N7 pide que no se muevan; eso lo cambia N1.2 en `Pickup` (Sesión A). Hasta entonces se mueven ±3 cm.

- **Inicio del nivel: línea + pensamiento:**
  - Fundido desde negro de 1.2 s (el juego queda bloqueado solo durante el fundido) → `wilbur_bosque_01` «El bosque de Don Lobo. Hasta los árboles parecen deberle plata.» con subtítulo → `RequestCustomLine` con este contexto: entró al bosque de noche, huellas de lobo y cerdo juntas, jazz a lo lejos.
  - **Sin conexión no sale pensamiento:** el guion no da línea de respaldo para la llegada y no inventé texto. Tiempo máximo: 6 s.
  - Una vez por sesión: reiniciar tras morir solo repite el fundido. `CampamentoLevelStart.ResetSession()` queda listo para N2.4.
  - Armas y munición por la regla de siempre: `WeaponSwitcher` (revólver si se obtuvo antes; munición llena al empezar el nivel) y holster listo (no hay taxi). **Depende del punto 1 de arriba.**
  - **Huellas (paso 3): saltado.** `FootprintTrail` no existe aún (N1.4). Cuando exista, hay que agregar la llamada al builder: de (0, -50) al portón (0, -10).

- **¿Se ve bien o está muy oscuro? (valores de luz/niebla)** **No se pudo ver.** Valores, más claros a propósito que `NivelPorky`:
  - Luna 0.8 (Porky 0.5). Ambiente Trilight: cielo `#4E55A8`, ecuador `#806090`, suelo `#604A3A`.
  - Niebla lineal 20..125 m, color del horizonte de `NightSkyBuilder`.
  - 29 luces cálidas (2.2–3.6 de intensidad). Las luces adicionales no proyectan sombra en el asset de URP (`m_AdditionalLightShadowsSupported: 0`): no cuestan mapas de sombra, pero atraviesan muros.
  - Si se ve oscuro, los valores están en `BuildMoonAndAmbient` y `FogStart/FogEnd`.

- **Build Settings (orden):** `MenuPrincipal` → `Intro` → `NivelPorky` → `Campamento`. `Campamento` se agregó a `BuildSceneOrder.Canonical`; `EditorBuildSettings.asset` lo escribe el menú al correr (no se editó a mano, así que **todavía no cambió**). `Final` lo agrega N2.4.

- **Menús que David debe correr (en orden):**
  1. Integrar la rama (ver *Commits*).
  2. **Plomo y Paja → Nivel 2 → Generar Campamento.**
  3. Leer el diálogo final y la consola `[Campamento]`: toda línea `✗` o `⚠` es algo que revisar. Faltan hoy: `amb_bosque`, `amb_campamento`, `wilbur_bosque_01` (no suenan; volver a correr el menú después de **Reconstruir Librería de Audio**).

- **Archivos compartidos tocados (y si estaban limpios en git status):**
  - `Scripts/Editor/BuildSceneOrder.cs`: limpio en `integracion/dia4` y sin cambios en la rama de la Sesión A.
  - `EditorBuildSettings.asset`: no se tocó; lo escribe el menú.
  - No tocados: `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `NivelPorky`, `GameStateTracker.cs`, `AICompanion`, `AudioImporter.cs`, `CharacterSpriteImporter.cs`, interrogatorio.
  - Trabajé en un worktree propio (`.claude/worktrees/n2-1`), como las subfases anteriores, para no pisar la copia compartida.

- **Qué no se pudo compilar/probar y qué debe revisar David en el Editor:**
  - **Compilación:** Roslyn de Unity 2022.3.62f3 con las referencias de los `.csproj`, sobre las fuentes del worktree. `Assembly-CSharp`: 109 fuentes, **0 errores**. `Assembly-CSharp-Editor`: 36 fuentes, **0 errores**. Avisos CS0649, CS0169, CS0414, CS0618 y CS0612 ocultos.
  - **Nada se ejecutó.** En el Editor, en este orden:
    1. La consola al generar: bordes (`✓ nadie se sale`), rondas y caminos a las anclas.
    2. Caminar los bordes: nada frena sin verse y siempre se ve algo detrás. Mirar sobre todo la baranda rota y los costados de la cerca.
    3. **Río y barranco:** que el disco lejano no los tape y que no se vea vacío entre los suelos.
    4. Oscuridad, y que las luces de la ciudad se lean a través de la niebla.
    5. Cada objeto apoyado en su mueble, con el aviso visible desde una vista normal (son las alturas exactas de `Cabinet`/`OpenCrate`/`Table`).
    6. La orientación de mallas nuevas (carpa, camioneta, gramófono, corneta): se armaron sin verlas.
    7. Que el Contable se rinda y hable; morir → recarga sin perder pistas.
    8. Batches en Stats (bosque de vista: ~50 grupos de 8 árboles). Si pesa, recortar en `BuildVista`.
  - Los `.meta` de los archivos nuevos los crea Unity al abrir; hay que subirlos.

- **Archivos creados/modificados:**
  - Creados: `Scripts/Editor/LevelBuilder_Campamento.cs` (flujo, marcadores, anclas, actores, sistemas, validación), `.Map.cs` (suelo, Z0–Z3, muebles), `.Lodge.cs` (Z4), `.Borders.cs` (bordes, vista, prueba de fugas); `Scripts/Level/Campamento/CampamentoLevelStart.cs`, `CampamentoAmbience.cs`; este audit.
  - Modificado: `Scripts/Editor/BuildSceneOrder.cs` (agrega `Campamento`).
  - **Reutilizados:** `CartoonKit`, `LevelBuilderUtils`, `TestPrefabGenerator`, `RenderingSetup`, `NightSkyBuilder`, `NoirVolumeBuilder`, `HudBuilder`, `PickupBuilder`, `IntroBookBuilder` (canvas y fundido), `InterrogationUIBuilder` (fuente), `CinematicSubtitles`, `ScreenFade`, `CinematicState`, `AICompanion`, `AudioLibrary`, `EnemyAI`/`EnemyHealth`/`DialogueNPC`, `CluePickup`, `LevelMarker`, `LightFlicker`, `RainEmitter` (viene en el Player).
  - **Lo crearán los menús:** `Assets/Scenes/Campamento.unity`, `Data/PickupLayout_Campamento.asset`, las mallas en `Art/Environment/Generated/` y `Assets/Materials/Greybox/Camp_*.mat`.

- **Commits:** en la rama `worktree-n2-1`, **sin integrar** (nada se subió):
  - `df70784` — dia4(david): N2.1 generador de Campamento: mapa, bordes, guardias, anclas, objetos e inicio del nivel
  - El de este audit: ver `git log worktree-n2-1`.
  - Para integrar, desde la copia principal: `git merge --ff-only worktree-n2-1`. Si `integracion/dia4` ya avanzó: `git cherry-pick integracion/dia4..worktree-n2-1`.
