# AUDIT INTRO.2 — Taxi, bajada y llegada a la casa (Nivel 1)

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4` (checkout principal, staging por ruta explícita) · **Alcance:** solo INTRO.2.
**Docs leídos:** `CLAUDE.md`, `Docs/phases/README.md`, `Docs/phases/Fase-Intro.md` (con §1b, A1–A7), `Docs/guiones/Guion-Intro.md`, §0 de `Fase-3C-Contador.md`, `AUDIT_intro_1.md` y `AUDIT_intro_1_1.md`. `REGISTRO.md` no tiene entrada de 3C.
**Puerta:** `AUDIT_intro_1_1.md` tiene `ESTADO: LUZ VERDE (orquestador, 3 nov)`.

> **Unity no se usó en esta sesión.** El C# compila (ver más abajo). El menú no se corrió y nada se probó en Play.
> Unity sí estuvo abierto un momento mientras yo trabajaba: importó los scripts nuevos, generó sus `.meta` y aplicó la regla nueva del taxista. Su `Editor.log` mostró un error en el shader nuevo `IntroRainWindow` (usaba `line`, palabra reservada de HLSL). **Ya está corregido**, pero esa corrección todavía no se volvió a importar.

## Antes de INTRO.2: commit y push de lo pendiente
- **Commit `31fb99f`:** lo que generaron Unity y el menú del libro al probar INTRO.1.1:
  - `Intro.unity`, `IntroBook.asset`, `IMFellEnglish SDF.asset`, los 2 materiales;
  - los `.meta` nuevos y los reimportados;
  - `AudioLibrary.asset` y `EditorBuildSettings.asset` (Intro y NivelPorky, ya sin SampleScene);
  - la línea de LUZ VERDE del audit 1.1.
  - Quedó fuera `IMFellEnglish-Italic.ttf`: no se usa.
- **Push: falló.** En el Administrador de credenciales la única cuenta es **`AndrxsS9`** (con una «s» más que el nombre del prompt). Intenté `git push https://AndrxsS9@github.com/EdedArt/Plomo_Y_Paja HEAD:refs/heads/integracion/dia4-intro` y GitHub respondió: `Invalid username or token. Password authentication is not supported for Git operations.` El token guardado está vencido o revocado. No reintenté ni busqué otras credenciales. **No hay nada subido.**
  - Para subir:
    1. `! git credential-manager github login` (iniciar sesión como AndrxsS9);
    2. `git push https://AndrxsS9@github.com/EdedArt/Plomo_Y_Paja integracion/dia4:integracion/dia4-intro`.

## Reconocimiento (graphify) y clases reutilizadas
`graphify update` sobre `Assets/_Game/Scripts`: 1844 nodos, 5392 aristas.

| Clase | Para qué |
|---|---|
| `PlayerAim.GameplayBlocked()` | **Mismo camino que el interrogatorio:** se le añadió `CinematicState.IsPlaying`. Con eso se ocultan manos y mira y se bloquean disparo, recarga, cambio de arma y recogida (todos los que ya lo leen). |
| `InterrogationDirector` (patrón `LockPlayer`) | Mismo bloqueo: `PlayerController` y `ShotgunController` apagados y vueltos a su estado (ShotgunController no lee `GameplayBlocked`). |
| `PlayerController` | Se añadió `SetViewAngles(yaw, pitch)`, `ViewPitch` y `CameraHolder`, para devolver el control desde la vista actual. |
| `GameStateTracker` (`DontDestroyOnLoad`) | Bandera `IntroCinematicPlayed`: una vez por sesión. |
| `GameManager` | Esc no pausa durante la cinemática (allí es «saltar»). |
| `EnemyAI` | Quietos, ciegos y sordos durante la cinemática. Nuevo `firstPatrolPoint`, que por defecto es 0: no cambia nada en otros guardias. |
| `PlayerHealth` | Sin daño durante la cinemática. |
| `AICompanion.ShowCustomLine` | Pensamiento fijo de la llegada (I9), sin API. |
| `LipSyncDriver` + `LipSyncSpriteAdapter` (3C.2) | Boca del taxista con el par habla_01/02. |
| `HoldToSkip`, `ScreenFade`, `CinematicEase`, `CarryOverAudio` (INTRO.1) | Saltar, fundidos, suavizados. La lluvia del libro sigue entrando por `CarryOverAudio`, **sin cambios**. |
| `UiHideReasons` (HUD.1) | Usa su razón `Cinematic`, que ya existía y nadie usaba: el HUD se oculta. |
| `RainEmitter` (sin cambios) | Su sonda de techo detecta el techo del taxi: lluvia ahogada y sin gotas adentro. |
| `AudioLibrary` | Voces y efectos por id (`GetVoice`, `GetSfx`, `PlaySfx2D`, `ContainsVoice`/`ContainsSfx`). |
| `CartoonKit`, `LevelBuilderUtils` | Taxi y camión con las primitivas del kit (cajas redondeadas, tornos), materiales y estáticos. |
| `LevelBuilder_NivelPorky.ExtraGuards` / `PlaceExtraGuards` / `ValidateExtraGuards` | Ronda nueva del guardia de la calle (fuente única de verdad). |
| `CharacterSpriteImporter` | Caso `taxista` nuevo. |
| `InterrogationUIBuilder.EnsureFont`, `IntroBookBuilder` (helpers de UI) | Special Elite y canvas, fundido y aviso de saltar. |

Recon del flujo de aparición: no hay respawn ni checkpoint. Morir → `GameManager.RetryCurrentLevel()` recarga la escena, y el Player aparece donde lo dejó el builder (ahora en la acera).

## Menús que David debe correr (en orden)
1. **Abrir Unity** y esperar la importación: se recompila `IntroRainWindow` corregido y aparece el `.meta` de `IntroTaxiBuilder.cs`. **Revisar la consola:** no debe quedar ningún `Shader error` ni `error CS`.
2. *(Opcional)* **Plomo y Paja → Reconstruir Librería de Audio.** `AudioLibrary.asset` ya tiene todas las voces e ids del taxi.
3. Una de las dos:
   - **Plomo y Paja → Generar Nivel Porky** (recomendado). Regenera el nivel completo: Contador, HUD y ahora también el taxi. Hornea el NavMesh **después** de poner el camión.
   - **Plomo y Paja → Intro → Construir taxi y llegada**: solo rearma `--- INTRO ---`, la ronda del guardia de la calle, el jugador en la acera y el patrullero del patio. Vuelve a hornear el NavMesh, valida las rondas y guarda la escena. Es idempotente.
4. **Play desde `Intro.unity`** (libro → taxi: prueba el empalme de la lluvia) y también **Play directo en `NivelPorky`**.
5. Hacer commit de lo generado:
   - `NivelPorky.unity`;
   - `Prefabs/Level1/Taxi_Intro.prefab`;
   - `Materials/Intro/IntroTaxiVentana.mat` y `Materials/Greybox/Kit_TaxiAmarillo`, `Kit_TaxiNegro`, `Kit_CamionVerde`;
   - las mallas `Art/Environment/Generated/Taxi_Intro_*` y `Camion_Estacionado`;
   - los `.meta` nuevos;
   - `EditorBuildSettings.asset` si cambia.

## Prefab del taxi y anclas (nombre + posición)
**`Prefabs/Level1/Taxi_Intro.prefab`** (local: +z adelante):
- **Carrocería:** amarilla, con capó largo, baúl y techo, un poco torcida (semilla fija `Taxi_Intro`). Guardabarros redondeados, estribos, parantes y paragolpes en negro. Franja a cuadros blanca y negra, parrilla y faros de cromo. Letrero «TAXI» en TMP 3D, de los dos lados.
- **Ruedas:** 4, gruesas, que giran.
- **Interior:**
  - 2 bancos; tablero con instrumentos encendidos y una luz cálida de 0.5;
  - volante;
  - 4 ventanas con lluvia (shader procedural `IntroRainWindow`);
  - **taxista** en billboard (`conduce_01`, escala 0.47 ≈ 0.96 m sentado).
- **Barrido:** spot cálida fuera a la derecha. Cruza el interior de adelante hacia atrás cada 2.5–4 s mientras anda. El dibujo del taxista se aclara con ella.
- **Techo:** collider en la capa TransparentFX (lo ve `RainEmitter`, no la vista de los guardias) y Rigidbody cinemático.
- **`Anchor_TaxiSeat`:** atrás a la derecha, (0.42, 1.2, −0.62).
- **`Motor`:** AudioSource 3D para la frenada y el arranque.

**En la escena** (grupo `--- INTRO ---`, coordenadas del mundo):

| Objeto | Posición |
|---|---|
| `Anchor_TaxiStop` | (−14.6, 0, −12.45), mirando al oeste (puerta trasera derecha del lado de la acera) |
| `Anchor_PlayerCurb` (y `PlayerSpawn` + Player) | (−14.0, 0, −10.9), mirando a lo largo de la calle (+x) |
| `Anchor_HouseLook` | (−5, 1.6, −6.2): la puerta delantera |
| `Taxi_Path` | (17.6, −15.6) → (8, −15.6) → (−4, −15.0) → (−10.5, −12.9) → parada · 33 m en 9 s |
| `Taxi_ExitPath` | parada → vuelta en U en la punta oeste → carril sur → (17.6, −18.4) · ~45 m en 7 s |
| `Camion_Estacionado` | centro (10.8, 0, −11.75), 5.6 × 2.4 m, alto 2.7: estático, collider en Default, cuenta para el NavMesh |
| `IntroCinematic` | director + `HoldToSkip` + 4 fuentes 2D. Canvas: `UI_Cine_Franjas` (orden 8), `UI_Cine_Texto` (45), `UI_Fundido` (50), `UI_Saltar` (70) |

## Lip-sync del taxista: ¿LipSyncDriver o alternativa?
**`LipSyncDriver`** (3C.2) sobre su propia voz 2D (`Voz_Taxista`) + **`LipSyncSpriteAdapter`** en su SpriteRenderer:
- En sus líneas, `TaxiInterior.SetTalking(true)` pone el par `habla_01` / `habla_02`, y la boca sigue el RMS.
- Después de `taxista_03` vuelve a `conduce_01`.
- Wilbur habla por otra fuente (`Voz_Wilbur`), así que no mueve la boca del taxista.

## ¿El taxi se va y desaparece fuera de vista? (sí/no)
**Sí** (programado):
- **Salida:** arranca despacio (`sfx_taxi_arranca` 3D), da la vuelta en U en la punta oeste y se va por el carril sur hacia el este.
- **Desaparición:** al final del camino espera hasta que su carrocería no esté en cámara (`Renderer.isVisible`) y entonces se apaga.
- **Riesgo:** si el jugador sigue mirando hacia el este, queda estacionado a ~33 m (en la niebla) hasta que mira para otro lado. No atraviesa paredes.

## Giro hacia la casa: ¿FOV intacto? ¿control vuelve sin salto?
**FOV intacto:** la cinemática nunca toca `fieldOfView`. En el taxi, la cámara del jugador se cuelga de `Anchor_TaxiSeat` (solo posición y rotación) y vuelve a su `CameraHolder` con la posición local guardada.

**Sin salto:**
- El giro hacia la casa (1.8 s, suavizado) ya mueve el cuerpo (yaw) y el `CameraHolder` (pitch) con `PlayerController.SetViewAngles`.
- Al devolver el control, `PlayerController` sigue desde esos mismos valores (`_xRotation` incluido).

**Mirada en el taxi:** ±60° / ±30°.

## Una vez por sesión: ¿dónde se guarda la bandera? ¿al morir se salta?
- **Dónde:** en `GameStateTracker.IntroCinematicPlayed`. El tracker es `DontDestroyOnLoad`, así que **sobrevive a recargar la escena**. Además hay una bandera estática de respaldo, por si el tracker no está.
- **Al morir:** sí se salta. «Reintentar» recarga NivelPorky, el director ve la bandera, apaga el taxi y no pone franjas: el jugador ya está en la acera (el builder lo dejó ahí).
- **Para probar:** `playIntroCinematic` (por defecto encendido) en `IntroCinematic` la apaga.

## ¿Los guardias detectan/dañan al jugador durante la cinemática? (sí/no + cómo)
**No.** Mientras `CinematicState.IsPlaying`:
- **Guardias (`EnemyAI.Update`):** no ven, no oyen y no atacan. Se quedan **quietos** (agente frenado) en su punto de arranque y al terminar siguen su ronda desde ahí. Si estaban esperando en un punto, no se los suelta antes de tiempo.
- **Disparos:** `EnemyAI.CanShoot` ya leía `GameplayBlocked()`.
- **Daño:** `PlayerHealth` descarta cualquier daño.
- **Por qué quietos:** así lo que se ve al devolver el control es siempre igual, y se pudo calcular (sección siguiente).

## ¿Al salir del taxi el guardia de la calle queda fuera de vista? (cómo lo calculaste)
**Sí.** Hice una simulación 2D de línea de vista con:
- las paredes de la casa a 3.5 m, con sus huecos reales: puertas, boquete, ventanas (contadas como abiertas);
- el camión;
- las cercas de 1.6 m como **transparentes**, porque los ojos van a 1.7 m (es el peor caso).

**Desde la acera** (−14, −10.9), con la vista de la llegada (yaw 62°, ±51° de cuadro):

| Guardia (quieto en su arranque) | Distancia | En cuadro | Tapado por |
|---|---|---|---|
| Patrulla_B (calle), en B_0 (18.4, −11.7) | 32.4 m | sí | camión |
| Patrulla_A (patio), en A_2 (9, 9) | 30.4 m | sí | casa (pared oeste) |
| Centinela_Sala | 17.5 m | sí | pared oeste |
| Patrulla_C (dormitorio) | 21.3 m | sí | fachada |
| Centinela_Cocina | 20.6 m | sí | pared oeste |
| Colmillo (interrogable) | 24.7 m | sí | fachada |
| Centinela_Patio | 24.8 m | sí | pared oeste |

**¿Cuándo aparece el guardia de la calle?**
- **Su nueva ronda:**
  - arranca detrás del camión y pasea a su lado (B_0 → B_1 → B_2);
  - sale a la calle (B_3 (14.8, −16.4) → B_4 (5, −16.4) → B_5 (5, −14.4));
  - vuelve.
  - Usa los valores del guardia: 2 m/s y 2 s de espera por punto.
- **Primera vez a la vista** (sin pared ni camión en medio y a ≤ 30 m, la regla I8 de INTRO.3), desde que vuelve el control:
  - acera: **12.0 s**;
  - camino al patio: **11.4 s**;
  - frente a la puerta: **12.3 s**;
  - esquina SE del patio: **18.0 s**;
  - punta oeste por la vereda de enfrente: **11.8 s**.
  - Solo si el jugador corre al centro de la calle lo ve antes: **7.4 s**.
  - O sea, ~10–20 s en el camino natural.

**Qué se movió:**
- **La parada y la acera** (antes el spawn estaba en (0, −15), al lado de la ronda vieja).
- **El guardia sí tuvo que moverse:** en esta calle abierta no hay forma de taparlo sin un obstáculo. Su ronda vieja (x 6.5–14) quedaba a la vista desde cualquier acera. Por eso hay un **camión de reparto estacionado** y la ronda pasó a la punta este: sigue siendo «el guardia de la calle» y el primer encuentro, está a más de 6 m de la ruta A y sale por el lado del camión.
- **El patrullero del patio (A)** ahora arranca en `Patrol_A_2` (esquina de atrás) con `firstPatrolPoint`. Así no aparece caminando frente a la casa en la llegada. Su ruta, con el portón de 3C.1, no cambió.
- Ninguna otra ronda cambió.

## ¿La lluvia del libro empalma con la del taxi?
**Sí** (programado):
1. El libro entrega su `lluvia_loop` por `CarryOverAudio` (**sin cambios**): la fuente sobrevive a la carga y se apaga en 2.5 s ya dentro del nivel.
2. En el primer cuadro del taxi (la pantalla todavía en negro), el director le pone a esa fuente un **paso bajo de 1100 Hz**: suena «desde adentro del auto».
3. Al mismo tiempo, el `RainEmitter` del Player (sin cambios) arranca su lluvia. Su sonda de techo, que sale de la cámara, ya sentada en el taxi, choca con el **collider del techo del taxi**: queda «bajo techo», ahogada y sin gotas adentro.
4. Cuando la lluvia del libro termina de apagarse, ya suena la del nivel.
5. Al bajarse, el filtro de la lluvia heredada se abre y `RainEmitter` detecta cielo abierto en ~0.3 s: la lluvia vuelve a sonar «afuera».

No hay silencio entre las dos. Falta oírlo.

## Saltar: ¿deja el estado final correcto?
Mantener ESPACIO o Esc 1 s (`HoldToSkip`, con el anillo de siempre). En cualquier momento, el salto:
1. funde a negro (0.4 s);
2. corta voces, subtítulos, cartel, ambiente y limpiaparabrisas;
3. apaga el taxi;
4. devuelve la cámara a la cabeza y deja al jugador **en la acera mirando la casa**;
5. quita las franjas, devuelve el control y marca la bandera de sesión;
6. vuelve la imagen (0.5 s).

El arma queda como hoy. Las corrutinas anidadas cortan con una bandera, así que no queda ningún subtítulo colgado.

## Menú de pausa durante la cinemática
**Desactivado.** `GameManager` no pausa con Esc mientras `CinematicState.IsPlaying`, porque ahí Esc mantenido es «saltar» (igual que en el interrogatorio). La pausa vuelve al devolver el control.

## Ids faltantes y placeholders
- **Ninguno.**
  - Voces: `taxista_01..03`, `wilbur_taxi_01/02`, `wilbur_casa_01`.
  - Efectos y ambientes: `amb_taxi_interior`, `sfx_limpiaparabrisas`, `sfx_taxi_frenar`, `sfx_puerta_carro`, `sfx_taxi_arranca`, `sfx_monedas`, `lluvia_loop`.
  - Sprites: `taxista/conduce_01`, `habla_01`, `habla_02`.
- **No existe un id de motor aparte.** Se usa `amb_taxi_interior` como motor e interior.
- **Placeholders programados:**
  - sin voz: subtítulo con duración estimada;
  - sin efecto: silencio;
  - sin sprite: silueta oscura;
  - sin shader de lluvia: vidrio oscuro liso.
  - El menú los lista.
- **Importador:**
  - `CharacterSpriteImporter` no tenía el caso `taxista`; los PNG salían con 250 px/m y pivote por pies.
  - Se añadió: 500 px/m, abajo al centro, mipmaps; versión **11**.
  - Unity ya reimportó los 3 `.meta` con esos valores (en el commit).

## Archivos compartidos tocados (y si estaban limpios en git status)
- `CharacterSpriteImporter.cs`: **limpio** → caso `taxista` y versión 11.
- `GameStateTracker.cs`: **limpio** → bandera de sesión.
- `EditorBuildSettings.asset`: **no lo edité**. Lo reescribe el menú: Intro, NivelPorky, y SampleScene fuera (ya estaba así, del commit `31fb99f`).
- `LevelBuilder_NivelPorky.AddToBuildSettings` ya no vuelve a meter SampleScene.
- `AudioImporter.cs` y `CLAUDE.md`: no se tocaron.
- Fuera de la lista, todos limpios antes de editar:
  - `EnemyAI.cs` (Edgar): congelado + `firstPatrolPoint`.
  - `PlayerAim.cs`, `PlayerController.cs`, `PlayerHealth.cs`, `GameManager.cs`.
  - `LevelBuilder_NivelPorky.cs`: spawn, ronda B, llamada al builder y Build Settings.
  - `IntroBookBuilder.cs`: helpers de UI pasados a `internal`.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **C# compilado: sí.** Roslyn de Unity 2022.3.62f3 contra las referencias de los `.csproj`, con todos los `.cs` actuales: **runtime 91 fuentes y editor 27 fuentes, 0 errores y 0 avisos** (antes del commit).
- **Shaders:**
  - `IntroRainWindow` tuvo un error en la importación que hizo Unity (`line` reservado). Lo corregí, pero **falta reimportarlo**.
  - `IntroPageCurl` e `IntroOldFilm` se importaron sin errores.
- **Sin probar:** el menú y todo lo de Play. Revisar:
  1. **Taxista:** escala (0.47) y posición al volante; que se lea bien visto desde atrás; tinte a oscuras y con el barrido.
  2. **Interior:**
     - que sea muy oscuro pero se lea;
     - que el barrido se vea pasar;
     - que no haya z-fighting en la franja a cuadros;
     - que las ventanas muestren la calle con los chorritos.
  3. **Lluvia:** ahogada dentro del taxi (sin gotas adentro) y abierta al bajar. Empalme desde el libro.
  4. **Recorrido:** que no atraviese el farol, el camión ni las paredes. Al empezar, la cola queda cerca del muro este (x 20.1): oculta por el negro.
  5. **Bajada:** Wilbur en la acera, junto a la cerca, mirando la calle. Giro a la puerta, línea, pensamiento de la IA **por encima de las franjas** (canvas 10 sobre 8) y cartel.
  6. **Guardias:**
     - todos quietos durante la cinemática;
     - el de la calle detrás del camión y apareciendo a los ~11–12 s;
     - el patrullero del patio arrancando detrás de la casa;
     - `ValidateExtraGuards` sin ✗ (lo imprime el menú del taxi).
  7. **Una vez por sesión:** morir → reintentar → sin taxi, en la acera.
  8. **Saltar** en el viaje, en el diálogo y en la llegada.
- **APIs dudosas:**
  - `Renderer.isVisible` (también cuenta la cámara de la escena en el Editor).
  - El techo del taxi en TransparentFX se mueve con un Rigidbody cinemático.
  - Las cercas tapan o no según su collider (en la simulación las conté transparentes).

### Decisiones tomadas
1. **Guardias congelados durante la cinemática** (no solo ciegos): así la llegada es determinista y calculable.
2. **Camión estacionado como tapa.** En la calle abierta no había otra forma de esconder al guardia sin cambiar el plano del nivel.
3. **Parada en la punta oeste** y salida con vuelta en U: las dos puntas de la calle tienen muro.
4. **Sin cartel si se salta.** Al saltar no se muestra el pensamiento de la IA.
5. **Las franjas van en el orden 8**, debajo del pensamiento de la IA (10).

## Archivos creados/modificados
**Creados:**
- `Scripts/Cinematics/`: `CinematicState.cs`, `Letterbox.cs`, `CinematicSubtitles.cs`.
- `Scripts/Intro/`: `IntroCinematicDirector.cs`, `TaxiInterior.cs`, `WaypointPath.cs`.
- `Scripts/Editor/IntroTaxiBuilder.cs`.
- `Shaders/IntroRainWindow.shader`.
- `Docs/audits/AUDIT_intro_2.md`.

**Modificados:**
- `EnemyAI.cs`, `PlayerAim.cs`, `PlayerController.cs`, `PlayerHealth.cs`.
- `GameManager.cs`, `GameStateTracker.cs`.
- `CharacterSpriteImporter.cs`, `LevelBuilder_NivelPorky.cs`, `IntroBookBuilder.cs`.
- `.meta` de `taxista/conduce_01`, `habla_01`, `habla_02` (los reimportó Unity).

## Commits
- `31fb99f` — dia4(david): INTRO.1.1 lo generado por Unity y por el menú del libro (escena Intro, IntroBook, fuente IM Fell SDF, materiales, .meta, librería de audio, Build Settings sin SampleScene)
- `8987e21` — dia4(david): INTRO.2 taxi, bajada y llegada en el Nivel 1: taxi de primitivas con interior, director de cinemática (franjas, subtítulos, saltar, una vez por sesión), guardias quietos y ciegos durante la cinemática, acera en la punta oeste y guardia de la calle tras un camión estacionado
- (este audit) — docs: auditoría INTRO.2

**Push:** no se pudo (credencial de AndrxsS9 rechazada; ver arriba). Todo queda local en `integracion/dia4`.

ESTADO: pendiente de revisión
