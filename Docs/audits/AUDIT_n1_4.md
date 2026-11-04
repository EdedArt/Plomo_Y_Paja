# AUDIT N1.4 — Huellas al bosque + salida del Nivel 1

**Fecha:** 4 nov 2026 · **Rama:** `worktree-n1-4`, salida de `f12d251` (punta de `integracion/dia4`; **todavía no está integrada**) · **Sesión A** · **Alcance:** N1.4 + arreglos a–c. No se empezó N1.5.

> **Nada de esto se probó en Unity.** Se compiló con Roslyn y se revisó la geometría con un script propio. Las huellas, la escena y la salida todavía no existen en `NivelPorky`: las crea el menú.

**Plan B: no hizo falta.** El barrido de cámara está hecho. Portón con cadena, pensamiento de la IA y salida a `Campamento` también.

## ⚠ Lo que David debe decidir o saber primero
1. **Morir después de Centavo cierra el portón otra vez.** La fase dice "tras reiniciar, el portón empieza abierto si Centavo está resuelto", pero con la foto de N2.1.1 Centavo revive y `contadorState` vuelve a `None`. Lo que hice: el portón vuelve a tener cadena y, al resolver a Centavo de nuevo, **se abre solo y sin aviso**, sin repetir la escena. No hay bloqueo, pero puede pasar sin que el jugador lo vea. Si quieres una señal (sonido de la cadena, por ejemplo), es poco trabajo.
2. **La salida muestra "Continuará en el campamento…" hasta correr *Generar Campamento*.** `Campamento.unity` no está en el repo ni en Build Settings. Es lo que pide N10, no un error de esta subfase.
3. **Hay `.meta` por subir.** Los 4 scripts nuevos y la carpeta `Level/Footprints` no traen `.meta`. La escena guarda el GUID del script, así que hay que subirlos junto con la escena, desde la misma máquina que la generó.

## Huellas: ¿decals o quads? ¿arte de Edgar o placeholder? ¿se ve que caminan juntos?
- **Quads.** El renderer no tiene la función Decal de URP (ningún `.asset` del proyecto la nombra). Son 2 mallas, una por animal: 2 draw calls, sin collider, sin sombra, fuera del NavMesh.
- **Altura: 2.5 cm, no 1 cm.** `Camino_Bosque` va a 1.5 cm sobre el piso y habría tapado huellas a 1 cm. Cada vértice toma la altura del piso con un rayo, para seguir el terreno que ondula.
- **Placeholder generado.** Falta el arte de Edgar. El menú dibuja `gen_huella_lobo_01.png` (almohadilla, 4 dedos y garras) y `gen_huella_cerdo_01.png` (pezuña partida). Si llega `huella_lobo_01.png` o `huella_cerdo_01.png` a `Art/Props/huellas/`, gana al volver a correr el menú, y lleva un tinte húmedo oscuro.
- **¿Se ve que caminan juntos? Por diseño, sí; falta verlo en pantalla.** Mismo paso de 0.7 m, cada animal a 0.34 m del centro del rastro, izquierda y derecha alternadas (±0.09 m), giro al azar de ±7° más la apertura de ±4° de cada pie. Sin marcas de arrastre.
- **Recorrido (calculado):** 28.5 m en total, 24.1 m pasando el portón, 41 pasos por animal. Va por el mismo camino que dibuja `Nivel1MapBuilder` (`ForestTrailPoints`, ahora compartido). Queda a 1.97 m o más de las líneas de árboles.
- **Riesgo de lectura:** el shader es sin luz para que se vean siempre, pero son oscuras sobre tierra de noche. **Mirarlo en pantalla** (N9 pide "siempre visibles").
- **Para Sesión B:** `FootprintTrail.Build(nombre, puntos, grupo, reporte)` dibuja un rastro en la escena abierta; necesita piso con collider. N2.2 no la usa.

## Cuándo se arma la escena (condiciones exactas) y qué pasa si Centavo sigue vivo/herido
Se arma cuando se cumple **todo** esto:
1. El jugador está dentro de `Huellas_Escena`, que cubre todo el rincón detrás del cuarto (x -10..-2.8, z 21..25.4). No se puede rodear.
2. `ContadorState` es `Dead` o `Pardoned`.
3. Terminó el epílogo: `InterrogationDirector.IsActive` es falso **y** `CentavoController.FinishedOffThoughtPending` es falso (nuevo; es el pensamiento al rematarlo sin escucharlo, que no pasa por el interrogatorio).
4. No hay pausa, muerte, interrogatorio, cinemática ni diálogo (`PlayerAim.GameplayBlocked()` y `UiHideReasons.Death`).
5. Pasaron 3 s desde la última línea de la IA, para que se alcance a leer (decisión mía, `quietAfterLine`).

**Centavo vivo o herido:** no pasa nada. Las huellas se ven y el portón sigue con cadena y su caja de colisión (N1: lo que bloquea se ve).

## Secuencia: tiempos reales, ¿FOV intacto? ¿control vuelve sin salto?
| Paso | Tiempo |
|---|---|
| Franjas (a la vez que baja la vista) | 0.4 s |
| Baja la vista a las huellas, 1.2 m rastro adelante | 1.2 s |
| Recorre el rastro hasta 2.1 m antes del portón, a la altura de los ojos | 3 s |
| `wilbur_huellas_01` con subtítulo | largo del clip; sin clip ≈ 3.4 s |
| Pensamiento en pantalla | 3.5–8 s, según el largo |
| Pausa | 1 s |
| `wilbur_cadena_01` «Con permiso.» | largo del clip; sin clip 1.5 s |
| Disparo, cadena al barro (0.3 s), hojas giran 100° (0.6 s), la cámara sube al bosque | 0.6 s |
| Cámara de vuelta a la cabeza | 0.5 s |

**Total: ≈ 15–19 s sin voces.** Son tiempos de código, no medidos. N9 pide "corta": si se siente larga, lo primero es acortar el pensamiento.

- **FOV:** nunca se escribe. Lo sigue llevando `PlayerAim`.
- **Control sin salto (por código):** antes de volver, el cuerpo y la cabeza giran hacia el bosque; la cámara interpola hasta esa pose y recién ahí se vuelve a colgar. El jugador no se mueve de donde estaba.
- **`timeScale`** sigue en 1. Todo corre en tiempo real.

## Pensamiento: ejemplo real de la API + prueba sin conexión
**No hay ejemplo real ni prueba sin conexión: no se ejecutó nada.** Falta correrlo. Por código:
- Se pide **al empezar la escena** con el contexto del guion §3 y sus reglas. `AICompanion` agrega el resumen de pistas, interrogatorios y bajas.
- **Tope:** 5 s reales desde el pedido. Como los primeros pasos duran ≈ 7.6 s, no debería haber espera muerta. Una respuesta tardía no se muestra.
- **Respaldo:** una al azar entre las 3 generales y la condicional («Estas huellas no cojean» si fue perdonado, «A las huellas, sí» si murió). Sin key, responde al instante con respaldo.
- **Más de 30 palabras → respaldo.** El guion dice máximo 25; dejé 5 de margen (`maxThoughtWords`).
- **Lo que no se puede garantizar por código:** "nunca afirmar que Porky fue por su cuenta" se le pide a la IA, pero no se verifica la respuesta.

**Líneas de pistas y bajas durante la escena (cubierto solo en parte):** con el jugador bloqueado y los guardias quietos no se generan nuevas. Una línea **ya pedida** antes de la escena no se puede cancelar sin editar `AICompanion`: si llega antes del pensamiento, se ve un momento en la banda; si llega durante el pensamiento, este se vuelve a mostrar encima.

## ¿Los guardias pueden atacar durante la escena?
**No, por código.** Usa `CinematicState.Set`, el mismo mecanismo de I7: guardias quietos y ciegos, y `PlayerHealth` no recibe daño. La salida hace lo mismo durante su fundido. **Sin probar.**

## Saltar: ¿deja el portón abierto y todo restaurado?
**Sí, por código; sin probar.** Mantener Espacio o Esc 1 s → negro → voz y subtítulos fuera → portón abierto y cadena en el piso → vista hacia el bosque → cámara en su lugar → control → vuelve la imagen.

**Dos detalles:**
- `HoldToSkip.SkipRequested` queda en true para siempre tras saltar. Si David saltaba el taxi, este aviso ya no funcionaba: agregué `HoldToSkip.Rearm()`.
- Letterbox, subtítulos, aviso de saltar y fundido son **los del taxi**; se buscan al cargar, así que reconstruir el taxi no los deja rotos. Sin ellos, la escena corre sin eso (el menú avisa con `⚠`).

## Salida al bosque: ¿carga Campamento? ¿precarga? ¿qué pasa si no está en Build Settings?
- **Carga:** `Salida_Bosque` en `Anchor_LevelExit` → sonido y negro bajan en 1 s → activa `Campamento`. Ver arreglo (b).
- **Precarga: no al abrir el portón (decisión).** La carga empieza al entrar al trigger y se activa al terminar el fundido. Una carga a la espera de activarse detiene las demás, y morir después de abrir el portón podría colgar el reinicio de `LevelDeathRestart`. Es comportamiento conocido de Unity, no lo probé aquí.
- **Sonido:** el fundido baja `AudioListener.volume`, y se restaura solo cuando termina la carga (`load.completed`).
- **Si no está en Build Settings:** `Debug.LogError` + aviso «Continuará en el campamento…» y el jugador se queda. Al salir y volver a entrar al trigger, avisa otra vez. **El aviso sale también en una build**, no solo en el Editor.
- **Ambiente:** `Ambiente_Bosque` (3D, en el sendero) suena con `amb_bosque` desde que se abre el portón. Hoy no suena porque falta el clip.

## Arreglos a–c

### a) `NivelPorky` sin objetos
- **Causa confirmada:** `Generar Nivel Porky` colocaba los objetos antes de guardar; con `scene.name` vacío, `PickupBuilder` buscaba `PickupLayout_.asset`.
- **Arreglo:** `LevelBuilder_NivelPorky` guarda la escena en `ScenePath` justo después de `NewScene`.
- **Protección:** `PlaceInOpenScene` y *Guardar posiciones de objetos* ya no aceptan una escena sin nombre: avisan y no crean ningún asset.
- **Borrados:** `Data/PickupLayout_.asset` y su `.meta` (`git rm`).
- **Los demás menús del Nivel 1:** *Ampliar mapa* y *Colocar muebles y objetos* abren la escena por ruta; *HUD → Colocar objetos* usa la escena abierta. Todos leen `PickupLayout_NivelPorky`. El asset se crea con la distribución de N1.2 al correr el menú.
- **Efecto a tener en cuenta:** como guarda al empezar, si *Generar* falla a mitad, la escena en disco queda incompleta. Se arregla corriéndolo otra vez (ya avisaba que regenera desde cero).
- **Consecuencia de juego:** ahora sí aparecen los 8 objetos (N1.2), así que **cambia el balance del Nivel 1** respecto a lo que se haya jugado: se probó siempre sin objetos.

### b) La salida usa el camino de los managers
- **Nuevo:** `GameManager.LoadLevelAsync(nombre, activar)` y `GameManager.CanLoadLevel(nombre)`.
- **Por qué no `LoadNextLevel`:** carga por índice (+1), y el orden de Build Settings es de Sesión B. N10 pide cargar por nombre. `LoadNextLevel` se queda como estaba.
- **Foto de entrada:** es carga `Single`, y `GameStateTracker.HandleSceneLoaded` saca la foto al cambiar de escena de juego. No se saltó nada; no toqué `GameStateTracker`.
- **Además:** limpia `gameIsOver`, la pausa y `timeScale`, como `RetryCurrentLevel`. Sin `GameManager`, la salida da error y no carga por su cuenta.
- **Sin probar:** debe verse `[GameState] Foto de entrada a Campamento: …` en consola.

### c) "Jugar" borra las banderas; morir no
| Bandera | Cómo se borra al apretar Jugar |
|---|---|
| `IntroCinematicDirector._playedThisSession` y la del tracker | Ya lo hacía `ResetForNewGame` (N2.1.1). **Sin cambios**; revisado |
| `TutorialDirector._shown` | **Nuevo:** se suscribe a `GameStateTracker.NewGameStarted` |
| `FootprintsSceneDirector._playedThisSession` | **Nuevo:** igual |

- **Camino:** `MainMenu.CargarJuego` → `GameManager.StartNewGame` → `ResetForNewGame` → `NewGameStarted`. Es el único camino de "Jugar" que encontré.
- **Morir no las borra:** `RetryCurrentLevel` solo llama `RestoreLevelSnapshot`, que no las toca. Volver al menú (`ResetState`) tampoco; recién Jugar.

## Mapa ASCII de `NivelPorky`
N arriba, 1 carácter = 2 m, aproximado. El mapa no cambió respecto a N1.1; esto es **antes + lo nuevo**.

**Leyenda:** `P` inicio · `T` parada del taxi · `K` camión · `H` casa de Porky (`d` puerta, `b` boquete) · `S` cuarto de Centavo · `G` guardia/Centavo · `1` cartuchos · `2` botiquín · `3` balas · `.` caminable.
**Bordes:** `F` fachada · `-` `|` cerca de 1.6 m · `~` mezcla baja · `_` entrada · `m` mezcla · `i` reja de hierro · `B` barricada · `X` auto chocado + escombros · `t` árboles · `D` `Gate_Bosque` · `V` vecino · `c` cartel vacío.
**Nuevo en N1.4:** `"` huellas · `a` trigger de la escena (cubre todo el rincón) · `E` `Salida_Bosque`. `:` camino sin huellas.
```
  58                     tttt
  56                     t Et
  54                     t :t
  52                     t :t
  50                     t :t
  48                     tt"t
  46                      t"t
  44                      t"t
  42                      t" t
  40                      t"tt
  38                      t"t
  36                     tt"t
  34                     t""t
  32                     t" t
  30                     t""t
  28                     t "t
  26                     tDDD-
  24                     |a"a|
  22                     |a"a|
  20                     SSG.|------
  18                     S12.|.....|
  16                     |...|.....|
  14                     ----|.....|
  12                     |.G.......|
  10                     |.........|
   8                     |.........G
   6                     |H2dGH1HHH|
   4     VVVVVVVVVVVVVVii|HHHHHHHGH| VVVVVVVV
   2     VVVVVVVVVVVVVV~.|HHHHHHHHH| VVVVVVVV
   0     VVVVVVVVVVVVVV~.|HHHHGHHHH| VVVVVVVVmmmmmmmm
  -2     VVVVVVVVVVVVVV~2|HHHHHHHH1| VVVVVVVV~ccccc.m
  -4    ~VVVVVV~VVVVVVV~.|HHH3HHHGH| ~VVVVVVV~......m
  -6    ~......~.......~.|HdHHHHbHH| ~...1...~......m
  -8    ~......~.......~.G.........| ~.......~......m
 -10    ~~~_~~~~~~~_~~~P.|.........|~~~~~_~~~~~___~~m
 -12    B..............T..........KKKK.G............X
 -14    B...........................................X
 -16    Bmmmmmmmmmmmm...................mmmmmmmmmmmmX
 -18                m...................m
 -20                mFFFFFiiiFFFFiiiFFFFm
 -22                FFFFFFFFFFFFFF FFFFFF
          |         |         |         |         |
          -40       -20       0         20        40
```

## Menús que David debe correr
Desde el checkout principal:
1. **Traer la rama:** `git merge worktree-n1-4` en `integracion/dia4`.
2. **Abrir Unity** y esperar la importación. Sin `error CS`. Aparecen los `.meta` nuevos.
3. **Plomo y Paja → Generar Nivel Porky** (recomendado): trae los objetos (a) y arma huellas, escena y salida.
   - Solo huellas, sin regenerar: **Plomo y Paja → Nivel 1 → Colocar huellas**. No trae los objetos; para eso, **Nivel 1 → Colocar muebles y objetos**.
4. **Plomo y Paja → Nivel 2 → Generar Campamento**, para que la salida tenga adónde ir.
5. **Consola:** `[LevelBuilder] Huellas` sin `✗`, y `✓ 8 objetos en NivelPorky` en `[LevelBuilder] Objetos`. No debe reaparecer `PickupLayout_.asset`.
6. **Commit de lo generado** (por ruta explícita): `.meta` de `Scripts/Level/Footprints/`, `Editor/FootprintTrail.cs` y los 3 scripts; `Art/Props/huellas/`; `Materials/Greybox/Huella_*`; `Art/Environment/Generated/Huellas_Bosque_*`; `Data/PickupLayout_NivelPorky.asset`; `Scenes/NivelPorky.unity` + su NavMesh.

## Ids faltantes y placeholders
| Id | Si falta |
|---|---|
| `wilbur_huellas_01`, `wilbur_cadena_01` | Solo subtítulo (aviso en consola) |
| `sfx_cadena_rota`, `sfx_reja_abrir` | No suena; el portón se abre igual (el disparo sí suena: `escopeta_disparo` existe) |
| `amb_bosque` | No suena |
| `huella_lobo_01`, `huella_cerdo_01` | Dibujo generado |

Revisado en `Audio/` y `Art/`: **ninguno está**. Al llegar el audio: **Reconstruir Librería de Audio**.

## Archivos compartidos tocados (y si estaban limpios en git status)
**De la tabla de propiedad: ninguno.** `GameStateTracker`, `AICompanion`, `InterrogationData` y scripts de interrogatorio, `EditorBuildSettings`, `AudioImporter` y `CharacterSpriteImporter`: **no se tocaron**. `LevelBuilder_NivelPorky` es de Sesión A.

**Fuera de la tabla, cambios solo aditivos:** `GameManager`, `HoldToSkip`, `CentavoController` (propiedad de solo lectura; N1.4 lo permite), `PickupBuilder`.

**Estado en git:** la rama sale de `f12d251` con el árbol limpio; `integracion/dia4` seguía ahí al terminar el código. **Sin cruces con Sesión B:** comparé contra los commits de `worktree-n2-2` (`1fcd6dd`) y ningún archivo coincide. Lo que tenga sin commit no lo puedo ver. N2.2 sí cambia `GameStateTracker`, pero sin tocar `ResetForNewGame` ni `NewGameStarted`.

## Clases reutilizadas
`ScreenFade`, `Letterbox`, `CinematicSubtitles`, `HoldToSkip`, `CinematicState`, `CinematicEase` (cinemática) · `UiHideReasons` · `AICompanion.RequestCustomLine` / `ShowCustomLine` · `AudioLibrary` · `HudWilbur.ShowToast` · `CartoonKit.GetMesh` / `MeshObject` · shader `PlomoYPaja/BackdropCard` · `IntroBookBuilder.SetRef` · `Gate_Bosque` de N1.1, sin cambiar su prefab · `GameStateTracker.NewGameStarted`. **Ubicación:** `Scripts/Level/Footprints/` y `Scripts/Editor/`, como pide la fase.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
**Compile check:** Roslyn de Unity 2022.3.62f3 sobre `worktree-n1-4`, antes de cada commit de código, con las fuentes tomadas del disco. Resultado final: `Assembly-CSharp` 123 fuentes, sin `UNITY_EDITOR` 123 fuentes, `Assembly-CSharp-Editor` 44 fuentes: **0 errores, 0 avisos** (sin CS0169, CS0649 ni CS0414, ocultos como en auditorías anteriores).

**Revisión de geometría (script fuera de Unity, coordenadas copiadas a mano):** largo del rastro, distancia a los árboles, el trigger y el giro de las hojas hacia el bosque. No reemplaza verlo en pantalla.

**No probado:** nada en Play ni en Editor. **Revisar:**
1. **Las dos salidas de Centavo:** perdonarlo y matarlo, y además **rematarlo sin escucharlo**. La escena espera su pensamiento.
2. **Centavo sin resolver:** portón cerrado, sin escena, y no se puede pasar.
3. **La cámara:** ¿se ven las huellas al mirar abajo? ¿La cadena desde la parada? ¿Atraviesa algo al moverse? Empezar desde la punta oeste del rincón, que es el recorrido más largo. Al abrirse el portón, ¿se ve la luz lejana del campamento? (`Tarjeta_Luz_Campamento`, de N1.1, está en z 90, detrás de los árboles de z 58; no la moví.)
4. **Portón:** ¿las hojas giran bien sobre la bisagra? ¿La cadena cae al piso? Sin ver en pantalla desde N1.1.
5. **Pasar por el portón abierto** y caminar el sendero hasta la salida, buscando dónde se traba.
6. **Saltar** la escena en varios momentos, y también después de haber saltado el taxi.
7. **Morir y reiniciar** tras la escena: sin repetirla, y el portón se abre al resolver a Centavo otra vez (ver el punto 1 del inicio).
8. **Jugar desde el menú** después de haber jugado: taxi, tarjeta, tutorial y huellas salen otra vez.
9. **Salida con `Campamento` y sin él:** fundido, sonido, volumen normal al llegar, y la línea `[GameState] Foto de entrada a Campamento`. Sin la escena: error y aviso, sin quedar atrapado.
10. **API:** real, sin key y lenta, para llenar la sección del pensamiento con un ejemplo.
11. **Objetos:** los 8 en su mueble, y si la munición y los botiquines dejan bien el balance.

## Archivos creados/modificados
**Creados:**
- `Scripts/Level/Footprints/FootprintsSceneDirector.cs`, `ForestGate.cs`, `LevelExit.cs`
- `Scripts/Editor/FootprintTrail.cs`
- `Docs/audits/AUDIT_n1_4.md`

**Modificados:**
- `Scripts/Core/GameManager.cs` — `CanLoadLevel`, `LoadLevelAsync`
- `Scripts/Cinematics/HoldToSkip.cs` — `Rearm`
- `Scripts/Characters/Contador/CentavoController.cs` — `FinishedOffThoughtPending`
- `Scripts/Tutorial/TutorialDirector.cs` — `ResetSession`, enganchado a `NewGameStarted`
- `Scripts/Editor/LevelBuilder_NivelPorky.cs` — guarda la escena al empezar; llama a `FootprintTrail`
- `Scripts/Editor/Nivel1MapBuilder.cs` — `ForestTrailPoints`; *Ampliar mapa* vuelve a colocar las huellas
- `Scripts/Editor/PickupBuilder.cs` — no acepta una escena sin nombre

**Borrados:** `Data/PickupLayout_.asset` y su `.meta`.

## Commits
En la rama `worktree-n1-4`, sin integrar y sin subir:
- `57522e6` — dia4(david): N1.4 (a) NivelPorky usa PickupLayout_NivelPorky: la escena se guarda antes de construir; borrado PickupLayout_.asset
- `cf41d3d` — dia4(david): N1.4 (c) Jugar rearma el tutorial con NewGameStarted; reiniciar tras morir no
- `e9819ce` — dia4(david): N1.4 huellas, escena del porton con cadena y salida a Campamento por GameManager
- Esta auditoría: `dia4(david): N1.4 auditoria` (su propio hash no se puede escribir aquí)
