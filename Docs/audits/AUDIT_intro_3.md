# AUDIT INTRO.2.1 + INTRO.3 — Arreglos del playtest del taxi · Arranque sin arma y sacar la escopeta

**Fecha:** 3 nov 2026 · **Base:** `integracion/dia4` en `f811846` · **Alcance:** PARTE A = INTRO.2.1, PARTE B = INTRO.3.
**Docs leídos:** `CLAUDE.md`, `Docs/phases/README.md`, `Docs/phases/Fase-Intro.md` (§1b, A1–A7), `Docs/guiones/Guion-Intro.md`, §0 de `Fase-3C-Contador.md`, `Docs/audits/AUDIT_intro_2.md`.

> **Unity estuvo cerrado.** El C# compila con Roslyn (ver más abajo). Ningún menú se corrió y nada se probó en Play.

> **Cómo se trabajó con git (importante):** esta sesión corre en segundo plano y el entorno **no deja editar ni hacer git
> en el checkout principal** (lo bloquea para no pisar el trabajo de otras sesiones). Por eso:
> - trabajé en un worktree (`.claude/worktrees/intro3`, rama local `wt/intro3`), que sale de `integracion/dia4` en `f811846`;
> - los 3 commits están en `wt/intro3`, que es `integracion/dia4` + estos 3 commits (avance rápido limpio);
> - **la rama local `integracion/dia4` del checkout principal todavía no avanzó.** Para llevarla (sin conflictos: los commits solo tocan scripts y docs, nada de lo que David tiene sin commitear):
>   `git merge --ff-only wt/intro3` (desde el checkout principal, en `integracion/dia4`).
> - Staging siempre por ruta explícita. No toqué lo que estaba sin commitear en el checkout principal (`NivelPorky.unity`, `NavMesh.asset`, `Centavo.prefab`, el taxi generado, materiales, etc.): eso sigue siendo el paso 5 de la auditoría INTRO.2.

---

## Sección INTRO.2.1

### Altura del asiento antes/después y ventanas
Medidas en espacio local del taxi (y = altura sobre el piso):

| | Antes | Después |
|---|---|---|
| `Anchor_TaxiSeat` (ojos) | (0.42, **1.20**, −0.62) | (0.42, **1.42**, −0.62) |
| Alféizar (borde de arriba de las puertas) | 1.23, **0.03 m por encima de los ojos** (por eso casi no se veía nada) | **1.15**, 0.27 m por debajo de los ojos |
| Ventanas laterales (quad) | 1.23 – 1.73 (alto 0.50) | 1.15 – 1.73 (alto 0.58) |
| Vidrio visible (hasta el techo, 1.695) | 1.23 – 1.695 | 1.15 – 1.695 → **centro 1.42 = los ojos** |
| Parantes | 1.22 – 1.74 | 1.14 – 1.74 (bajan con el alféizar) |
| Techo | 1.74 | **sin cambios** (no se subió) |

- Las puertas ahora van de 0.81 a 1.15 (antes 0.81 a 1.23). La franja a cuadros (0.95 – 1.05) queda igual, debajo del alféizar.
- Parabrisas y luneta no cambiaron.
- **Taxista:** desde 1.42 la línea de la vista a su cabeza (~1.5 m) pasa a ~1.47 m sobre el respaldo del banco delantero (borde a ~1.30 m). Se le ve cabeza y hombros por encima del respaldo; antes quedaba más tapado. Su billboard no cambió (escala 0.47, en (−0.42, 0.8, 0.24)).
- El techo sigue 0.27 m por encima de los ojos: la sonda de `RainEmitter` sigue viéndolo (lluvia ahogada adentro).
- Todo está en `IntroTaxiBuilder` (constantes `SeatEyeHeight`, `WindowSill`, `WindowTop`). Hay que correr el menú del taxi para rearmar el prefab.

### Rutas de entrada de armas bloqueadas (clase por clase)
**Causa del bug:** `ShotgunController` **nunca** leía `PlayerAim.GameplayBlocked()`; dependía de que alguien le apagara el componente. La cinemática lo apagaba en su `Start`, pero `WeaponSwitcher.Start → EnforceEnabled()` lo volvía a encender si su `Start` corría después (Unity no garantiza el orden). Con eso, el clic izquierdo disparaba en el taxi. Además, las corrutinas siguen corriendo con el componente apagado: una recarga empezada antes seguía sonando.

| Clase | Antes | Ahora |
|---|---|---|
| `ShotgunController` | sin bloqueo propio | `Update` no lee entrada si está bloqueada y corta una recarga en curso. `OnDisable` corta la recarga. La auto-recarga tras vaciar y el bombeo diferido (0.35 s) no suenan si se bloqueó mientras tanto. En la parte B el bloqueo es `PlayerAim.WeaponInputBlocked()` (= `GameplayBlocked()` + arma enfundada). |
| `RevolverController` | ya leía `GameplayBlocked()`, pero una recarga empezada seguía (el interrogatorio y la cinemática no apagan este componente) | bloqueado: corta la recarga (las balas ya metidas se quedan) y descarta el disparo en cola. Parte B: `WeaponInputBlocked()`. |
| `WeaponSwitcher` | `Start` volvía a encender la escopeta aunque hubiera bloqueo (**el bug**) | `Start` solo impone el arma activa si no hay bloqueo; `Update` lo hace al terminar. Cambio con 1/2/rueda: ya estaba bloqueado. Parte B: tampoco con el arma enfundada. |
| `PlayerAim` | ya bloqueaba el apuntado con `GameplayBlocked()` | igual. Parte B: `WeaponInputBlocked()`, tampoco se apunta enfundada. No hay ningún sonido de «apuntar» en el código: lo que se oía en el taxi era el disparo, el clic de vacía y el bombeo de la escopeta. |
| `ShotgunFeedback` | solo reacciona a `OnFired` (no lee entrada) | sin cambios: sin disparo no hay luz, sacudida ni onomatopeya. |
| `WeaponPickup` | ya leía `GameplayBlocked()` (sin prompt ni «E» bloqueado) | sin cambios. |
| `FirstPersonViewmodel` / `ViewmodelAnimator` / `ViewmodelMotion` | solo eventos del arma (no leen entrada) | sin cambios para la parte A. |

**El interrogatorio no cambia de camino:** sigue apagando `PlayerController` y `ShotgunController` y los restaura, y `GameplayBlocked()` sigue siendo la misma función. Lo único nuevo: si empieza a mitad de una recarga, la recarga se corta y no carga (se vuelve a recargar después).

### Punto blanco: cuándo se ve y cuándo no
Es un **modo de `RubberHoseCrosshair`** (no es otra mira):
- **Modos:** `Hidden` / `Dot` / `Full`.
- **Punto chico:** 5 px de blanco con 1 px de contorno casi negro (sprite procedural, como la mira).
- **Mira completa (punto grande + 4 gotas):** está en su propio grupo. Al salir del modo punto entra con un fundido de 0.15 s (`fullFadeInSeconds`).

| Momento | Qué se ve |
|---|---|
| Taxi: fundido de entrada (0.8 s) | nada |
| Taxi andando y diálogo (se puede mirar con el mouse) | **punto** (el director lo enciende con `CinematicState.SetCenterDot` al terminar el fundido) |
| Puerta, fundido a negro, bajada | nada (se apaga con el `sfx_puerta_carro`) |
| Llegada: giro, línea de Wilbur, pensamiento de la IA, cartel «PLOMO Y PAJA» | nada (no se puede mirar; el punto sobre el cartel quedaba mal) |
| Saltar (cualquier momento) | nada |
| Control devuelto, arma enfundada (parte B) | **punto** |
| Sacando el arma (0.5 s) | **punto**; al llegar a Ready → mira completa con fundido |
| Pausa, diálogo, interrogatorio, muerte | nada (igual que antes) |
| Apuntando | nada (igual que antes) |

- **Franjas:** el punto sí se ve con las franjas puestas en el taxi (el centro de la pantalla queda libre). Durante la animación de las franjas al salir (la llegada) está apagado.
- **Avisos y prompts** (`ShowNotice` / `SetPrompt`): se siguen viendo en modo punto (por ejemplo, el prompt de una pista con el arma enfundada).

### Taxi: ¿fuera de cuadro en toda la llegada? ¿apagado antes de devolver el control?
**Sí y sí** (calculado; falta verlo en Play).

**Qué cambió:**
- **Salida nueva** (`Taxi_ExitPath`): recto al oeste desde la parada, hacia donde ya mira el taxi: (−14.6, −12.45) → (−20) → (−30) → (−45) → (−60), en el carril z −12.45, 45.4 m en 7 s.
  - Antes daba una vuelta en U y se iba al este por el carril sur, **cruzando el cuadro**.
  - La calle es una caja cerrada (muros de 3 m en x ±20.1, fachadas al sur), así que no había salida al este fuera de cuadro.
  - El taxi atraviesa el muro oeste: pasa a ~1.5 s, **a espaldas de la cámara**, y del otro lado lo tapa el muro.
- **Bajada:** Wilbur se baja mirando **yaw 20°**: la cerca de la acera, del lado por donde baja; `curbStandYaw`, lo pone el builder.
  - Antes miraba 90° (a lo largo de la calle) y la cola del taxi quedaba **dentro del cuadro** al volver la imagen.
- **Todo a la vez:** vuelve la imagen (0.5 s), la cámara gira a la derecha hacia la puerta (20° → 62.4°, 1.8 s) y el taxi arranca.

**Comprobación** (script en Python, puntos de la carrocería ±1.1 × ±2.4 × 0–2.05 m contra el cuadro de la cámara en cada instante, cada 0.02 s):
- **Cámara:** ojos a 1.7 m, FOV vertical 60° (`Player.prefab`).
- **Medio FOV horizontal:** 45.8° en 16:9 y 53.8° en 21:9.
- **Margen mínimo** del taxi al borde del cuadro en toda la llegada: **38.5° en 16:9 y 31.5° en 21:9**.
- El peor instante es el primer cuadro: el taxi todavía está en la parada, a la derecha y atrás.
- Con la vista vieja (90°), el taxi se veía salvo que la pantalla se quedara en negro 1 s más.

**Apagado:**
- Al final del camino (7 s), `HideTaxi()` lo apaga. Ya no se espera a `Renderer.isVisible`.
- `Release()` (devolver el control) y el salto lo apagan **sí o sí**.
- La llegada dura ~13 s (giro + línea + pensamiento 5.5 s + cartel 3.2 s): el taxi se apaga ~6 s antes de devolver el control.

**Sonido:**
- `sfx_taxi_arranca` sigue en el `Motor` (3D, lineal de 3 a 45 m).
- Al apagarse, el motor está a 47 m del jugador: ya no se oye, así que no hay corte.

---

## Sección INTRO.3

### Cómo se ocultan manos/mira (razones de ocultado) y quién más las usa
Cada sistema **compone** sus razones en cada frame; nadie apaga la razón de otro (I12):
- **Manos (`FirstPersonViewmodel.LateUpdate`):** `oculto = muerto || GameplayBlocked() || PlayerWeaponHolster.HidesHands || ViewmodelMotion.RaisePending`.
  - `GameplayBlocked()` = pausa, game over, diálogo, interrogatorio o cinemática.
  - `HidesHands` solo vale en `Holstered`.
  - `RaisePending` evita que se vea un frame de las manos en reposo antes de subir.
- **Mira (`RubberHoseCrosshair.ComputeMode`):** muerto → nada; cinemática → punto o nada; `GameplayBlocked()` → nada; `PlayerWeaponHolster.BlocksWeapons` (`Holstered` o `Drawing`) → punto; si no → completa.
- **HUD:** `UiHideReasons.Holstered` (ya existía desde HUD.1, nadie la usaba). Oculta solo la munición; la vida sigue visible. Se apaga en `Ready`.
- **Entrada:** `PlayerAim.WeaponInputBlocked()` = `GameplayBlocked() || PlayerWeaponHolster.BlocksWeapons`.
  - **La leen:** `ShotgunController`, `RevolverController`, `PlayerAim` (apuntado) y `WeaponSwitcher` (cambio de arma).
  - **Los guardias no:** `EnemyAI` sigue con `GameplayBlocked()` y le puede disparar a un Wilbur desarmado.
- **Quién más usa estas razones:**
  - el interrogatorio y el diálogo (vía `GameplayBlocked`);
  - la pausa;
  - la muerte (`_dead` en la mira, el viewmodel y `PlayerAim`; `UiHideReasons.Death` en el HUD);
  - la cinemática (`CinematicState` + `UiHideReasons.Cinematic`).
- La munición no se toca; caminar y correr siguen libres.

### Disparadores implementados y cuál gana primero
Gana el primero que llega; `RequestDraw` solo hace algo en `Holstered`. Ninguno cuenta mientras `GameplayBlocked()` (cinemática, pausa, diálogo).
1. **Guardia a la vista**, cada 0.1 s en tiempo real, sin basura: lista de `EnemyAI` guardada al empezar, `Plane[6]` preasignado.
   - Vivo, a **≤ 30 m**.
   - Su caja (0.6 × 1.8 × 0.6) **dentro del frustum** de la cámara principal (`GeometryUtility.TestPlanesAABB`).
   - **Línea de vista**, el mismo modelo que INTRO.2: rayo a la capa Default (paredes, camión; los triggers no cuentan) hacia el pecho (+1 m) **o** la cabeza (+1.6 m).
   - **Cercas:** son cajas de 1.6 m. El rayo a la cabeza desde ojos a 1.7 m pasa por encima de una cerca, así que, como en la simulación de INTRO.2, no tapan.
   - **Con el plano de INTRO.2,** esto lo dispara el guardia de la calle a los **~11–12 s** de devuelto el control (acera 12.0 s, camino al patio 11.4 s, frente a la puerta 12.3 s).
2. **Daño recibido** (`PlayerHealth.OnDamaged`; en la cinemática no hay daño).
3. **Un guardia pasa a `Alert`, `Chase` o `Attack`** (`EnemyAI.OnStateChanged`).
4. **Trigger de la puerta** `WeaponDraw_Puerta` (`WeaponDrawTrigger`): caja de 2.4 × 2 × 2 m centrada en (−5, 1, −7.2), frente a la puerta delantera de Porky. Capa Ignore Raycast. Lo pone el menú del taxi.

En el camino natural gana el 1 (el guardia de la calle). El 4 es el respaldo si el jugador va directo a la puerta sin mirar hacia el camión. La razón queda en `PlayerWeaponHolster.DrawReason` (y en la consola con `debug`).

### Animación de sacar el arma: ¿reutiliza ViewmodelMotion? duración:
**Sí.**
- `ViewmodelMotion.StartRaise(0.5)` usa el mismo canal de la bajada de recarga (`_dip` × `reloadDip` del perfil), al revés.
- Empieza desde más abajo (× 1.8 → 900 px de 1080) para que el dibujo salga de abajo de la pantalla, y sube con `SmoothStep`.
- **Duración: 0.5 s** (`drawSeconds`), con `ViewmodelTime` (tiempo real).
- **Sonidos:** `wilbur_arma_01` (voz 2D) al empezar; `escopeta_bombeo` a los 0.3 s (`pumpAt`). Según el guion §6 se reusa el bombeo de la recarga; `sfx_escopeta_bombeo` no existe como id.
- **Al llegar a Ready:** la mira completa entra con fundido de 0.15 s y se desbloquea la entrada.
- **Sin la voz:** solo el bombeo (plan B del guion §8).

### ¿Funciona durante un hit-stop? (sí/no)
**Sí** (programado): la corrutina cuenta con `Time.unscaledDeltaTime`, `ViewmodelMotion` usa `ViewmodelTime` (tiempo real) y el audio no depende de `timeScale`.

### Reinicio tras morir: ¿empieza armado? ¿dónde se guarda weaponDrawn?
**Sí, empieza armado.**
- **Dónde se guarda:** `GameStateTracker.WeaponDrawn` (`MarkWeaponDrawn()`), junto a `IntroCinematicPlayed`.
  - El tracker es `DontDestroyOnLoad`, y `ResetState()` (Reintentar) no borra ninguna de las dos banderas.
  - Hay además una bandera estática de respaldo, como la del taxi.
- Se marca **al empezar** a sacarla: si muere a mitad de la subida, el reintento empieza armado.
- **Regla de arranque:** enfundada **solo** si `IntroCinematicDirector.PlaysThisLoad` (la cinemática corre en esta carga) y el arma no se sacó antes.
  - Al reintentar, la cinemática ya se vio, así que empieza `Ready`.
  - Si se salta la cinemática, sigue enfundada (I5).

### Con playIntroCinematic apagado: ¿empieza armado?
**Sí.** Sin cinemática en esta carga, `PlaysThisLoad` es false → `Ready` desde el primer frame. Lo mismo en otras escenas (sin director) y si el Player no tiene `PlayerWeaponHolster`: el arma funciona como antes.

### Rutas revisadas para no romper (Centavo, interrogatorio, pausa, muerte)
Revisadas leyendo el código, no en Play:
- **Centavo (combate y herido, 3C.2):**
  - Se activa solo con los guardias del patio caídos o rendidos (D2), y eso exige haber disparado: el arma ya está fuera.
  - Su daño al jugador pasa por `PlayerHealth.TakeDamage` → `OnDamaged`; si por algo siguiera enfundada, la saca.
  - Los perdigones le llegan por `IDamageable`, sin cambios.
  - `CentavoController` no lee `PlayerAim` ni el arma: sin cambios.
- **Interrogatorio (3C.3):**
  - Mismo camino: apaga `PlayerController` y `ShotgunController` y los restaura; manos y mira se ocultan por `InterrogationDirector.IsActive` dentro de `GameplayBlocked()`.
  - Al cerrar vuelven solas, porque las razones se componen.
  - Novedad: una recarga a medias se corta al empezar (escopeta por `OnDisable`, revólver en su `Update`).
- **Diálogo con guardias rendidos (`DialogueNPC`):** igual que el interrogatorio. Solo hay rendidos después de disparar.
- **Pausa:**
  - Con Esc, `GameplayBlocked()` → manos y mira ocultas, y los disparadores no cuentan.
  - Si la pausa cae justo a mitad de la subida, la subida termina en tiempo real detrás del menú.
  - Durante la cinemática la pausa sigue desactivada (INTRO.2).
- **Muerte y reintento:**
  - `PlayerHealth.Die` apaga la escopeta (ahora además corta la recarga).
  - `_dead` oculta todo.
  - `RetryCurrentLevel` recarga la escena → sin cinemática → `Ready` (ver arriba). `ResetHealth` vuelve a encender la escopeta, como antes.
- **Recoger el revólver (`WeaponPickup` → `WeaponSwitcher.GiveRevolver`):** sin cambios con el arma lista.
  - Si se recogiera estando enfundada (no debería poder pasar: hace falta neutralizar a Centavo), queda como arma activa sin animación y sale al sacarla.
  - En ese caso raro sonaría el bombeo de la escopeta.

---

## Menús que David debe correr (en orden)
1. **Llevar la rama local:** desde el checkout principal, en `integracion/dia4`: `git merge --ff-only wt/intro3`.
2. **Abrir Unity** y esperar la importación. Entran `PlayerWeaponHolster.cs` y `WeaponDrawTrigger.cs` con sus `.meta` ya commiteados (GUID fijo para todo el equipo). **Consola:** sin `error CS`.
3. Una de las dos:
   - **Plomo y Paja → Generar Nivel Porky** (recomendado).
   - **Plomo y Paja → Intro → Construir taxi y llegada**: solo rearma `--- INTRO ---`.

   Las dos:
   - rearman `Taxi_Intro.prefab` con el asiento y las ventanas nuevas y la salida al oeste;
   - ponen `WeaponDraw_Puerta`;
   - añaden `PlayerWeaponHolster` a `Player.prefab`;
   - escriben `curbStandYaw = 20` en el director.

   El informe del menú debe decir «✓ PlayerWeaponHolster añadido a …/Player.prefab» y «✓ Trigger para sacar el arma…».
4. **Play desde `Intro.unity`** y **Play directo en `NivelPorky`**.
5. **Commitear lo generado:** `NivelPorky.unity`, `Taxi_Intro.prefab`, `Player.prefab` y las mallas `Taxi_Intro_*` regeneradas. También lo pendiente de INTRO.2: materiales, `Camion_Estacionado`, `.meta` nuevos y `IntroTaxiBuilder.cs.meta`.

## Qué no se pudo probar (y qué mirar en el Editor)
- **Nada se probó en Play.** C# compilado; menús sin correr.
- **Taxi por dentro:**
  - desde 1.42 m: que se vea la calle por la ventana derecha sin que el techo moleste;
  - que el taxista se lea por encima del respaldo;
  - que no asome el borde de la puerta nueva ni quede un hueco entre el alféizar (1.15) y la ventana.
- **Punto blanco:**
  - se ve en el taxi después del fundido y desaparece con la puerta;
  - no aparece en la llegada ni sobre el cartel;
  - tamaño y contorno sobre el cielo y la calle.
- **Llegada:**
  - Wilbur se baja mirando la cerca y gira a la derecha hacia la puerta;
  - el taxi no se ve nunca (tampoco en 21:9);
  - el arranque se oye detrás a la izquierda y se apaga;
  - al recuperar el control y girar, no hay taxi.
- **Sin arma:**
  - sin manos, punto blanco, HUD sin munición;
  - clic izquierdo, clic derecho, R, 1/2 y rueda no hacen nada ni suenan.
- **Sacar:**
  - el guardia de la calle a los ~11–12 s la dispara;
  - probar también recibir daño y entrar al trigger de la puerta;
  - la subida desde abajo en 0.5 s, la línea, el bombeo y luego la mira completa con fundido;
  - durante un hit-stop.
- **Rutas:** morir → reintentar (armado, sin taxi); `playIntroCinematic` apagado (armado); interrogatorio de Centavo (manos y mira vuelven); pausa.
- **APIs dudosas:**
  - `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])` (sobrecarga sin basura, existe desde 2017).
  - `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` con la escena abierta (ya se usa en `TestPrefabGenerator`).
  - `OnTriggerEnter` con `CharacterController` contra un trigger estático: Unity lo dispara, pero conviene comprobarlo.
- **Decisiones tomadas:**
  - **El taxi atraviesa el muro oeste** fuera de cuadro: la calle no tiene salida.
  - **El punto se apaga en toda la llegada** (no solo en los fundidos), porque ahí no se mira con el mouse.
  - **`weaponDrawn` se marca al empezar a sacarla.**

## Archivos
**Creados:**
- `Scripts/Player/PlayerWeaponHolster.cs` (+ `.meta`).
- `Scripts/Intro/WeaponDrawTrigger.cs` (+ `.meta`).
- `Docs/audits/AUDIT_intro_3.md`.

**Modificados:**
- **Parte A:**
  - `ShotgunController.cs`, `RevolverController.cs`, `WeaponSwitcher.cs`: bloqueo y recargas cortadas.
  - `CinematicState.cs`: `CenterDot`.
  - `RubberHoseCrosshair.cs`: modos y punto chico.
  - `IntroCinematicDirector.cs`: punto, `curbStandYaw`, salida a la vez que el giro, `HideTaxi`, sin `isVisible`.
  - `IntroTaxiBuilder.cs`: asiento, alféizar, ventanas, salida al oeste.
- **Parte B:**
  - `PlayerAim.cs`: `WeaponInputBlocked`.
  - `GameStateTracker.cs`: `weaponDrawn`. Estaba limpio en `git status` antes de editarlo.
  - `ShotgunController.cs`, `RevolverController.cs`, `WeaponSwitcher.cs`: enfundada.
  - `ViewmodelMotion.cs`: `StartRaise`.
  - `FirstPersonViewmodel.cs`: `PlayDraw` + razón enfundada.
  - `RubberHoseCrosshair.cs`: punto con el arma enfundada.
  - `UiHideReasons.cs`: solo comentario.
  - `IntroCinematicDirector.cs`: `PlaysThisLoad`.
  - `IntroTaxiBuilder.cs`: trigger de la puerta y holster en el prefab.
  - `TestPrefabGenerator.cs`: holster en el prefab.

## Compilación
Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`), con las referencias, defines y LangVersion de los `.csproj` del checkout principal y las fuentes del worktree:

| Momento | Runtime | Editor |
|---|---|---|
| Parte A (antes del commit) | 91 fuentes, **0 errores** | 27 fuentes, **0 errores** |
| Parte B (antes del commit) | 93 fuentes (+2 nuevas), **0 errores** | 27 fuentes, **0 errores** |

- **Único aviso:** `CS0414 CentavoController.flashSeconds`, que ya estaba antes (archivo no tocado).
- Se omitieron los `CS0649` de campos `[SerializeField]`, que Unity suprime.

## Commits (en `wt/intro3` = `integracion/dia4` + estos)
- `6e70bbf` — dia4(david): INTRO.2.1 arreglos del playtest: asiento del taxi más alto con alféizar más bajo, armas sin disparo ni sonido durante la cinemática, punto blanco en lugar de la mira en el taxi, el taxi se va al oeste fuera de cuadro y se apaga antes de devolver el control
- `18af2ab` — dia4(david): INTRO.3 arranque sin arma y sacar la escopeta: PlayerWeaponHolster (enfundada → sacando → lista; guardia a la vista ≤ 30 m con frustum y línea de vista, daño, alerta o trigger de la puerta), subida de 0.5 s en tiempo real con wilbur_arma_01 y bombeo, punto blanco mientras está enfundada, weaponDrawn en GameStateTracker
- (este audit) — docs: auditoría INTRO.2.1 + INTRO.3

## Resultado del push
**OK** con la credencial guardada en el Administrador de credenciales (no se escribió ni se mostró ninguna):
- Se empujó `wt/intro3` → **`origin/integracion/dia4-intro`** (rama nueva): `* [new branch] wt/intro3 -> integracion/dia4-intro`.
- **Contenido:** idéntico a lo que daría `git push origin integracion/dia4:integracion/dia4-intro` después del avance rápido: `f811846` + `6e70bbf` + `18af2ab`.
- **Este audit no está subido** (se commitea después del push).
- Para subirlo también: `git push origin wt/intro3:integracion/dia4-intro` (o, tras el `merge --ff-only`, `git push origin integracion/dia4:integracion/dia4-intro`).

ESTADO: pendiente de revisión
