# AUDIT contador 3C.6: prueba completa, pulido y cierre

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1), subfase 3C.6. Leídas las 10 auditorías 3C: `AUDIT_contador_assets`, `3C1`, `3C2`, `3C3`, `3C4`, `3C4_1`, `3C4_2`, `3C5`, `3C5_1` y `3C5_2`.
Rama: `integracion/dia4`. **Sin push.**
Nota de David tras la 3C.5.2: «todo está bien», sin más errores reportados. No hubo bugs que arreglar de la lista de prueba; esta subfase hizo las dos tareas fijas, apagó la depuración y limpió código.

> **Importante:** Unity no se abrió en esta sesión. El código **compila** (Roslyn de Unity 2022.3.62f3, ver abajo), pero **los menús no se ejecutaron** y **nada se probó en Play**. Los prefabs (`Centavo.prefab`, `Player.prefab`, `AICompanion.prefab`) siguen con los valores viejos hasta que David corra los menús de la sección «Menús que David debe correr». Los números de Centavo salen de **cálculo con las medidas de los PNG y de la geometría del cuarto**, no de verlo en pantalla.

## Qué pasó en el árbol mientras trabajaba (léase antes de commitear nada más)
- **El HUD.1 se commiteó a las 00:53 (`51f4e42`, `0db9d47`) mientras esta sesión estaba abierta.** Otra sesión trabaja la fase HUD en el mismo árbol. Mis ediciones empezaron antes de ese commit, así que rehice el análisis de qué archivos eran míos: al final, los diffs de los archivos de código que commiteé son **solo de la 3C.6**.
- **Dos restos de la 3C.5.2 quedaron dentro del commit del HUD** (`51f4e42`): `Scripts/Editor/CharacterSpriteImporter.cs` (carpeta `Art/Pickups/`, versión 7) y `Tools/character_key.py` (modo `--single`). Es código correcto y de la 3C; solo que el mensaje de ese commit no lo dice.
- **Reserva de la escopeta:** con el HUD.1, la reserva ya no vive en `ShotgunController.reserveAmmo` sino en **`PlayerAmmo.shotgunReserve`** (H4). Cambiar solo el campo del prefab, como pedía la orden, **no habría hecho nada**. Por eso toqué también `PlayerAmmo.cs` (una línea de valor por defecto + comentario). Esa persona/sesión del HUD debe saberlo (ver «Para los demás»).
- `Docs/phases/Fase-HUD.md` y `Docs/guiones/Guion-HUD.md` siguen sin seguimiento: no son de esta subfase y **no los toqué**.

## Checklist: estado de cada punto (OK / arreglado / pendiente)
La nota de David es general («todo está bien»), no punto por punto. Marco «OK (David)» donde su prueba lo cubre, y agrego lo que pude comprobar yo **por código**. Los puntos que tocan el tamaño de Centavo hay que **volver a mirarlos** después de correr el menú (ver «Qué mirar en Play»).

| # | Punto de la lista | Estado | Notas |
|---|---|---|---|
| 1 | Guardias bloquean el portón; Centavo espera | OK (David) | Sin cambios de lógica (D2). |
| 2 | Combate: cubrirse / asomarse / disparar se lee bien, barks sin pisarse, amenaza baja | OK (David) · **re-mirar con ×1.15** | El dibujo crece 15 %; ver tabla de tamaños. |
| 3 | Primer golpe lo tumba (escopeta y revólver); sangre; sentado alineado con el banco y del tamaño correcto | OK (David) · **re-mirar con ×1.15** | Sentado pasa de 1.06 m a 1.22 m (sección 1). |
| 4 | Herido: barks; acercarse ≤ 3 m abre la escena | OK (David) | Sin cambios. |
| 5 | Escena: agacharse, desenfoque, retrato, papel, lip-sync, opciones, pensamiento, decisión | OK (David) | Sin cambios. |
| 6 | Rama de matar y de perdonar (sale cojeando por el portón) | OK (David) · **re-mirar la cámara de los epílogos** | Los puntos de mira del epílogo ahora siguen la escala. |
| 7 | D4: dispararle herido antes de la escena | OK (David) | Sin cambios. |
| 8 | Revólver recogible en los 3 desenlaces; cambio con 1/2/rueda; recarga | OK (David) · **re-mirar la caída** | `revolverDropSide` ahora escala (sección 1). |
| 9 | Revólver preciso de lejos; escopeta fuerte de cerca y débil de lejos | OK (David) | Sin cambios de balance de daño. |
| 10 | Morir en la pelea → reinicio normal y Centavo se reinicia bien | OK (David) y **OK por código** | `GameManager.RetryCurrentLevel` hace `GameStateTracker.ResetState()` y recarga la escena; Centavo vuelve a `Dormant`. |
| 11 | Esc durante la escena | OK (David) y **OK por código** | `GameManager` no pausa mientras `InterrogationDirector.IsActive` (3C.3). |
| 12 | Todo restaurado tras la escena (mover, apuntar, FOV, manos, mira, cursor, arma actual) | OK (David) | Sin cambios. |
| 13 | Sin picos de GC por el lip-sync; sin errores en consola | OK (David) en consola · **GC sin medir** | `LipSyncDriver` usa un único búfer de 256 reservado una vez (3C.2), pero no corrí el Profiler. Compilación: 0 errores. |

**Pendiente de la lista: ninguno.** Lo único sin comprobar con herramientas es el Profiler del lip-sync.

## Bugs arreglados en esta subfase
No hubo bugs reportados. Lo que se hizo:

1. **Centavo un poco más grande** (tarea fija 1): raíz del prefab a escala **1.15**.
2. **Reserva de la escopeta 18 → 24** (tarea fija 2).
3. **Revólver soltado con tamaño equivocado** (bug que **habría salido** con el punto 1, evitado antes de que pasara): `Revolver_Suelto` es hijo de la raíz de Centavo y se suelta con `SetParent(null, true)`, que **conserva la escala del mundo**. Con la raíz a ×1.15 el revólver habría quedado 15 % más grande (0.40 m en vez de 0.35 m) y con el collider más grande, porque `WeaponPickup` calcula su tamaño suponiendo la raíz sin escalar. Arreglo: `DropRevolver` pone `localScale = 1` justo después de soltarlo.
4. **Banderas de depuración y código muerto** (secciones 3 y 4).

## 1. Centavo a escala 1.15 (tarea 1)

### Cómo quedó
- `ContadorCentavoBuilder.CentavoRootScale = 1.15f` y `root.transform.localScale = Vector3.one * CentavoRootScale` al armar el prefab. **Es el valor por defecto que escribe el builder:** «Colocar Centavo», «Construir patio y cuarto» y «Generar Nivel Porky» lo conservan al regenerar. El cuadro del menú dice `escala de la raíz ×1.15`.
- **La instancia de `NivelPorky` no tiene override de escala** (revisé sus modificaciones: solo `shed`, `yardGuards`, nombre, posición y rotación), así que hereda los 1.15 del prefab en cuanto se regenere.
- `seatedScale` queda en **0.52** y `coverSink` en **0.4**, como se pidió. Son locales a la raíz: crecen con ella (sentado 0.52 → 0.598 efectivo; hundimiento 0.4 → 0.46 m).
- **Alturas fijas del código** que estaban en metros del mundo (ojo de pie 1.6, ojo sentado 0.8, mano del disparo 1.35 + 0.35, pecho para la sangre 1.2 y 0.6, goteo, y la mira del epílogo) ahora se multiplican por `WorldScale` (= escala de la raíz). Sin esto, con el dibujo 15 % más alto, el ojo, el fogonazo y la sangre habrían quedado 15 % más abajo que el cuerpo.
- `revolverDropSide` se multiplica igual (0.6 → 0.69 m). El valor serializado sigue siendo 0.6 («a escala 1»).
- Lo que **no** cambia: `interrogationDistance` (3 m, distancia entre el jugador y Centavo), `fleeSpeed` (1.2 m/s), los anclajes del cuarto (`Anchor_*`, en el mundo) y el alcance de disparo.

### Alturas y tamaños, antes y ahora (medidos de los PNG a 500 px/m y de `CentavoController`)
| Qué | Antes (×1.00) | Ahora (×1.15) |
|---|---|---|
| Cápsula de pie / asomado / disparando (alto) | 1.80 m | **2.07 m** |
| Cápsula cubierto (alto) | 0.70 m | **0.805 m** |
| Cápsula sentado (alto) | 1.00 m | **1.15 m** |
| Radio de la cápsula | 0.30 m | 0.345 m |
| Dibujo de pie (`idle` / `peek` / `shoot`) | 2.0 m | 2.30 m |
| Dibujo `cover_01` (1.78 m) menos el hundimiento | 1.38 m | 1.59 m |
| …cuánto asoma sobre el banco de 1 m | 0.38 m | **0.59 m** |
| Dibujo sentado (`herido_01`): alto × ancho | 1.06 × 1.01 m | **1.22 × 1.16 m** |
| Dibujo muerto (`death_01`): alto × ancho | 0.92 × 1.01 m | 1.06 × 1.16 m |
| Mano del disparo (altura sobre el piso) | 1.35 m | 1.55 m |
| Ojo de pie / sentado (para la línea de vista) | 1.6 / 0.8 m | 1.84 / 0.92 m |

### ¿El banco sigue frenando los perdigones cuando está cubierto?
**Sí, por cálculo (no probado en Play).**
- **Geometría del cuarto** (`ContadorYardBuilder`): banco de 1.0 m de alto y 0.62 m de fondo; Centavo de pie a 0.44 m detrás de su cara trasera, o sea con el centro de la cápsula 1.06 m detrás de la cara delantera.
- **Método:** rayo desde el ojo del jugador que roza el borde de atrás del banco, y se comprueba si alguna parte de la cápsula cubierta queda **por encima** de ese rayo (en un plano vertical, sin dispersión de los perdigones). Probé tres alturas de ojo sobre el piso del cuarto: 1.32, 1.48 (la nominal: cámara a 1.7 m sobre el terreno y piso del cuarto 0.22 m arriba) y 1.70 (peor caso).

  | Ojo sobre el piso | Tapado desde (distancia horizontal a la cara delantera del banco): antes → ahora |
  |---|---|
  | 1.32 m | 0.30 → 0.30 m |
  | 1.48 m (nominal) | 0.30 → **0.63 m** |
  | 1.70 m (peor caso) | 0.60 → **1.20 m** |

- **Lectura:** el banco lo tapa desde ≥ 1.2 m en el peor caso, es decir a unos **2.3 m del propio Centavo** (1.2 + 1.06). La exigencia era «desde ≥ 2.5 m»: se cumple con 0.2 m de margen. En el caso nominal el margen es de más de 0.8 m.
- **Lo que se perdió:** el hueco entre el tope de la cápsula y el borde del banco bajó de **0.30 a 0.195 m**. Por eso el umbral subió (~0.6 m). La auditoría 3C.2 daba «~2.5 m» con otro método más grueso; no comparar el 2.3 de aquí con ese 2.5, comparar «antes → ahora» de la tabla.
- **No incluye** la dispersión de los perdigones: desde la cadera (±4.3° por eje) a 2.5 m un perdigón se desvía ±0.19 m, así que algún perdigón suelto puede pasar por encima del banco y pegarle al dibujo cubierto. Es lo esperado al disparar por encima de una cobertura.
- **Efecto visual a vigilar:** con el dibujo más alto y el mismo hundimiento, cubierto asoma **0.59 m** sobre el banco (antes 0.38 m), pero esa parte **no recibe golpes** (la cápsula termina 0.195 m bajo el borde). Puede parecer injusto: «le di en la cabeza». Si se nota, subir `coverSink` a **0.55** (×1.15 = 0.63 m) devuelve el asomo a ~0.42 m. No lo cambié porque la orden era dejarlo.

### ¿Sentado sigue apoyado en el banco?
**Sí.**
- El ancla sentada (`Anchor_CentavoSeated`) está a **0.29 m delante** de la cara delantera del banco y no se escala. El dibujo crece desde el centro de sus pies, así que su plano sigue a 0.29 m del banco.
- **Ancho al nivel del piso:** el cuerpo llega a 0.58 m a cada lado del centro (antes 0.505 m, medido a 0.2 m de altura). Visto de lado, el borde del dibujo empezaría a entrar en el banco desde unos **30°** de ángulo (antes 35°). Como es un billboard que gira hacia la cámara, el jugador casi siempre lo ve de frente.
- **Altura: 1.22 m, 0.22 m por encima del banco.** Antes 1.06 m (0.06 m por encima). Queda **fuera del rango de D12 (1.0–1.1 m)** por 0.12 m. Es la consecuencia directa de pedir ×1.15 sin tocar `seatedScale`. Si David quiere volver a ~1.08 m: `seatedScale` = **0.46** (0.46 × 1.15 × 2.05 m = 1.08 m). No lo cambié.

### ¿El revólver soltado no cae encima de él?
**Sí, no cae encima.**
- Punto de aterrizaje: `Anchor_CentavoSeated` + **0.69 m** a la derecha (0.6 × 1.15) + 0.15 m hacia la puerta.
- Borde del cuerpo sentado a 0.2 m de altura (lo más ancho): **0.58 m** del centro. El **centro del revólver queda 0.11 m fuera del borde** (antes 0.095 m). Como el revólver mide 0.35 m, en una vista de frente su mitad interior sigue tapando 0.06 m del contorno (antes 0.08 m), pero cae 0.15 m **delante** del plano del dibujo, no sobre él.
- Sin escalar `revolverDropSide` habría quedado a solo 0.02 m del borde, con el revólver medio encima del cuerpo: por eso escala.
- Tampoco choca con la caja (x ≥ 1.06 m, z ≤ 0.77 m del cuarto) ni con los barriles; el aterrizaje está en x = 0.69, z = 1.15.
- El tamaño del revólver se conserva en 0.35 m (ver «Bugs arreglados», punto 3).

## 2. Reserva de la escopeta 18 → 24 (tarea 2)
- **Qué hice** (todo por código/migración, sin tocar YAML):
  - `PlayerAmmo.shotgunReserve`: valor por defecto **24** (es donde vive la reserva desde el HUD.1; tope 30 sin cambios).
  - `ShotgunController.reserveAmmo`: valor por defecto **24** (el campo que pedía la orden; hoy es el respaldo si el Player no tiene `PlayerAmmo`).
  - `TestPrefabGenerator`: **«Crear Prefabs de Prueba»** migra los dos del prefab del Player de 18 a 24 **solo si siguen en 18** (`MigrateInt`): un ajuste a mano no se pisa. Sigue la misma regla que la dispersión 7.5 y el revólver 5 + 15.
  - `Docs/GDD.md`: «6 + 24».
- **Cuenta** (de `AUDIT_contador_3C5.md`, opción (a)): disponible **30** cartuchos (6 + 24; antes 24). Nivel 1: promedio ≈ 15, pesimista ≈ 22, extremo ≈ 25. Margen: promedio **15** (50 %, antes 9), pesimista **8** (27 %, antes 2), extremo **5** (antes −1: faltaba un cartucho).
- **Para recoger munición extra** (`AddAmmo`/HUD.3) queda sitio para 6 cartuchos más hasta el tope de 30.
- **Pendiente de verificar en Unity:** que tras correr el menú el Player tenga `PlayerAmmo → Shotgun Reserve = 24` y el HUD muestre `×24`.

## 3. Depuración apagada por defecto: sí
| Bandera | Valor por defecto | Valor guardado en prefab/escena | Qué se hizo |
|---|---|---|---|
| `CentavoController.debug` (teclas F9 = matar, F10 = perdonar) | false | `Centavo.prefab`: 0 | **Quitada** (campo y teclas): eran el atajo «hasta que exista la 3C.3» que pedía la fase en la 3C.2. Ya hay escena real. |
| `WeaponPickup.debug` | false | `Centavo.prefab`: 0 | Sin cambios. |
| `WeaponSwitcher.debug` (empezar con el revólver) | false | `Player.prefab`: 0 | Sin cambios. |
| `FirstPersonViewmodel.debugFireLog` / `debugSightLog` | false / false | `Player.prefab`: 0 / 0 | Sin cambios. |
| `HudWilbur.debug` (F5–F8) | false | aún no está en ningún prefab | Sin cambios (es del HUD). |
| **`AICompanion.logRequests`** | **estaba en `true`** → ahora **false** | `AICompanion.prefab`: **1** (viejo) | Cambiado en código. El prefab se **regenera** con el menú «Crear Prefabs de Prueba», que lo rehace con el valor nuevo. Hasta entonces el log de la IA sigue saliendo. |

- Comprobé que todos los `Debug.Log` fuera de `Editor/` de las otras clases de 3C están detrás de una bandera apagada (`FirstPersonViewmodel`, `WeaponPickup`).
- **No son banderas pero escriben en consola** (los dejé porque son avisos de juego, no de depuración): `GameStateTracker` (5 mensajes, uno por pista/interrogatorio/decisión/Centavo), `GameManager` («Final determinado»), `GraphicsSettingsService` (2, solo si el SSAO se desactiva) y `AudioLibrary` («Sin audio para …», una vez por id). Si David los quiere fuera, es una línea cada uno.

## 4. Código muerto y limpieza
**Quitado**
- `CentavoController`: `UpdateDebugKeys`, la bandera `debug` y su encabezado (ver arriba).
- Comentario viejo en `InterrogationDirector` («3C.3 demo, 3C.4 entero»): la demo ya se había borrado en la 3C.4; ahora el comentario dice lo que hay.

**Revisado y que ya no existía**
- **Secuencia de 7 cuadros de la recarga del revólver** (3C.5): ya se había quitado en la 3C.5.2. Solo queda `profile.reload.fps = 0` en `ViewmodelProfileBuilder.ApplyRevolverMeasurements`, que **no** es código muerto: es el reinicio que protege a los perfiles viejos guardados con 3.18 cps. `VM_Revolver.asset` ya tiene `fps: 0`.
- **Demo de la 3C.3** en `CentavoInterrogation`: borrada en la 3C.4.
- Nada de `_entrante_*` en el repo (sección 5).

**Detectado y no quitado** (API pública o hooks de otras fases; quitarlos no gana nada y rompe a quien los use después)
- Sin ningún uso hoy: `CentavoController.Billboard/LipSync/Shed`, `ContadorShed.OilLamp`, `GameStateTracker.ContadorInterrogationResult/ContadorLied`, `BillboardSprite.HasAnim`, `InterrogationCanvas.OptionCount`, `InterrogationDirector.IsBusy`, `InterrogationPortrait.Emotion`, `LipSyncDriver.Rms/SetSource`, `WeaponSwitcher.HasRevolver`, `AudioLibrary.HasVoice/PlayVoice`, `ViewmodelProfileBuilder.RevolverProfilePath`.
- `ShotgunController.AddAmmo` y `PlayerAmmo.FindOnPlayer/IsFull`: los usará el HUD.3 / los recogibles de munición.
- Avisos del compilador viejos y ajenos a 3C: `PlayerController.groundMask` y `FinaleTrigger.confrontationPoint` (campos sin uso).
- `RubberHoseCrosshair`/`Player.prefab`: el prefab todavía serializa los campos de la fila de munición provisional de la 3C.5 (`showAmmo`, `ammoCorner`, …) que el HUD.1 quitó del código. Se limpian solos al volver a correr «Crear Prefabs de Prueba».

## 5. `_entrante_*` no está en git: confirmado
- `git ls-files | grep -i entrante` → **0 archivos** seguidos.
- Ignoradas y presentes en disco: `_entrante_audio/`, `_entrante_personajes/`, `_entrante_viewmodel/`.
- **Encontré una carpeta sin proteger:** `PLOMO_Y_PAJAV_1/entrante_hud/` (25 MB de arte entrante del HUD, **sin guion bajo**, por eso la regla `_entrante_*` no la cubría). Aparecía como «sin seguimiento»: un `git add -A` la habría subido. Agregué a `.gitignore`:
  - `PLOMO_Y_PAJAV_1/entrante_hud/`;
  - una red de seguridad `**/_entrante_*/` (cubre cualquier carpeta de originales entrantes, esté donde esté).

  Comprobado con `git check-ignore`. Si la sesión del HUD quería subir esos originales, tiene que quitar la línea; me pareció lo mismo que `_entrante_viewmodel/` y `_entrante_personajes/`.

## Compile check
- **Assembly-CSharp:** 0 errores, 105 warnings (los **mismos 105 de antes** de mis cambios: 103 × CS0649 en `[SerializeField]` que asignan los menús y 2 × CS0169 ajenos). **Assembly-CSharp-Editor:** 0 errores, 0 warnings.
- Compilador: Roslyn (`csc.dll`) del Editor 2022.3.62f3, C# 9, con las referencias y los `DefineConstants` de los `.csproj` que Unity generó. Se compilaron los dos ensamblados desde las fuentes del árbol de trabajo, con el HUD.1 incluido.
- **No probado:** ningún menú, ningún Play, el Profiler.

## Menús que David debe correr (en orden)
1. Abrir Unity y dejar que compile (consola sin rojo).
2. **Plomo y Paja → Crear Prefabs de Prueba.**
   - Player: `PlayerAmmo.shotgunReserve` y `ShotgunController.reserveAmmo` 18 → 24 (solo si seguían en 18).
   - Regenera `AICompanion.prefab` con `logRequests` apagado.
   - Ojo: este menú regenera también `Enemy_Guard` y `Enemy_Interrogable` (comportamiento de siempre).
3. **Plomo y Paja → Contador → Colocar Centavo.** El cuadro debe decir `✓ Prefab … (escala de la raíz ×1.15)`.
4. **Ctrl+S** y commit de `Player.prefab`, `Centavo.prefab`, `AICompanion.prefab` y `NivelPorky.unity` si cambió.

## Qué mirar en Play (por el cambio de tamaño y de reserva)
1. Centavo en el cuarto: se ve 15 % más grande que antes, de pie mide ~2.3 m; el fogonazo sale de la mano y no del pecho.
2. **Cubierto:** los perdigones pegan en el banco desde ≥ 2.5 m; asoma más cuerpo sobre el banco (0.59 m). Si molesta que «se ve pero no recibe golpes», `coverSink` 0.55.
3. **Asomado:** un tiro lo tumba como antes (cápsula de 2.07 m).
4. **Sentado:** apoyado en el banco, de 1.22 m. Si se ve demasiado grande, `seatedScale` 0.46.
5. **Revólver:** cae a la derecha de Centavo sentado, sin quedar encima del cuerpo, y mide lo mismo que antes (0.35 m, no 0.40).
6. **Epílogos:** al perdonar, la cámara lo sigue cojear y mira el pecho de un Centavo más alto; al matar, mira el cuerpo.
7. **Escopeta:** el HUD y la mira muestran ×24 de reserva; al terminar el nivel 1 anotar cuántos cartuchos sobran.
8. Consola: sin `[AICompanion] …` por cada línea de la IA.

## Archivos creados/modificados
Creados:
- `Docs/audits/AUDIT_contador_3C6.md` (este archivo).

Modificados (código y docs):
- `Scripts/Editor/ContadorCentavoBuilder.cs`: `CentavoRootScale` 1.15 y escala de la raíz del prefab.
- `Scripts/Characters/Contador/CentavoController.cs`: `WorldScale`, alturas fijas escaladas, `revolverDropSide` escalado, revólver soltado a escala 1, sin teclas ni bandera de depuración.
- `Scripts/Characters/Contador/CentavoInterrogation.cs`: puntos de mira de los epílogos con la escala.
- `Scripts/Player/PlayerAmmo.cs`: reserva de la escopeta 24.
- `Scripts/Player/ShotgunController.cs`: `reserveAmmo` 24 (y el comentario).
- `Scripts/Editor/TestPrefabGenerator.cs`: migraciones 18 → 24.
- `Scripts/AI/AICompanion.cs`: `logRequests` apagado.
- `Scripts/Interrogation/InterrogationDirector.cs`: comentario.
- `Docs/GDD.md`: «6 + 24».
- `.gitignore`: `entrante_hud/` y `**/_entrante_*/`.

Commiteados aparte (generados por los menús de David en la 3C.5.2, sin código):
- `Art/Viewmodel/VM_Revolver.asset`, `VM_Shotgun.asset`, `Prefabs/Level1/Centavo.prefab`, `Prefabs/Player.prefab`, `Art/Pickups.meta`, `Art/Pickups/revolver_suelo.png.meta`.

## Bloque para el Registro de fases
> Propuesta. El orquestador lo pega en `Docs/phases/REGISTRO.md` (arriba de todo) al dar luz verde.

```
## 2026-11-03 — Centavo, el contador (patio, combate, interrogatorio y su revólver) · Fases 3C.0 → 3C.6 · CERRADA

**Responsable:** David (Claude Code, orquestado). **Rama:** `integracion/dia4`.
**Auditorías:** `Docs/audits/AUDIT_contador_assets.md`, `AUDIT_contador_3C1.md`, `3C2`, `3C3`, `3C4`, `3C4_1`, `3C4_2`, `3C5`, `3C5_1`, `3C5_2`, `3C6`.

### Resultado
Centavo, el contador de Don Lobo, vive en un cuarto de herramientas del patio de Porky: espera mientras los guardias del patio siguen
en pie, pelea sin moverse desde detrás de su banco, cae con el primer golpe y se le puede interrogar en una escena estilo Mouse P.I.
(retrato grande, papel, lip-sync, pensamiento de la IA y decisión). Matarlo o perdonarlo cambia el mundo, la pista `registro_01`
y los finales. Su revólver se convierte en la segunda arma de Wilbur (teclas 1/2 y rueda), y la escopeta es de corto alcance.

### Subfases
| Subfase | Qué se hizo | Por qué hizo falta |
|---|---|---|
| 3C.0 | Recorte magenta de 22 sprites (`Tools/character_key.py`), 21 voces de Centavo y 8 de Wilbur importadas. | Tener el material de Centavo listo y con el mismo flujo que los demás personajes. |
| 3C.1 | Cuarto de herramientas (`ToolShed_Contador`, banco, lámpara, anclas en `ContadorShed`), cerca y portón, ruta del patrullero cruzando el portón; `registro_01` sale solo de Centavo. | El mapa de la escena y las anclas que usan todas las subfases siguientes. |
| 3C.2 | `CentavoController` (Dormant → Alert → Combat → Hit → Wounded → Dead / Fleeing → Gone), `IDamageable`, `LipSyncDriver`, estado del contador en `GameStateTracker`, PPU 500. | Que Centavo exista y pelee con las reglas D2–D4, D12. |
| 3C.3 | `InterrogationDirector` / `InterrogationCanvas` / `InterrogationPortrait`: cámara agachada, `timeScale` 0, desenfoque, retrato vivo, Special Elite, pausa desactivada. | La presentación reutilizable de la escena. |
| 3C.4 | `InterrogationData` con las 21 líneas del guion, confianza, `registro_01`, pensamiento de la IA con respaldo, decisión (matar/perdonar), conteo para el Final C. | El contenido y las consecuencias. |
| 3C.4.1 | Fuente estática verificada, decisión escrita en el papel, epílogo con pensamiento tras la decisión (cámara sigue al cojear y mira al cielo; sobre el cuerpo al matar). | La prueba de David pidió una decisión con el estilo del juego y un cierre visual. |
| 3C.4.2 | Cuatro guardias normales en la calle y la casa antes del cuarto; Colmillo se dejó (única fuente de `testimonio_puerta_01`); cuenta de munición. | El nivel 1 necesitaba más combate antes de Centavo. |
| 3C.5 | `RevolverController`, `WeaponSwitcher` (1/2/rueda), `WeaponPickup` (E), balance (escopeta ×1.5 de dispersión y caída por distancia; revólver 24 de daño). | D13–D15: el revólver de Centavo como segunda arma. |
| 3C.5.1 | `VM_Revolver` con cuadros (los PNG eran cubemaps), revólver cae delante del banco, pensamiento al rematar a Centavo (D4). | Bugs de la primera prueba del revólver. |
| 3C.5.2 | Revólver en el piso como dibujo, mira medida (`aimSightPoint`), manos que no tapan la mira, recarga bala por bala con balas visibles (5 + 15). | Bugs de la segunda prueba. |
| 3C.6 | Centavo a escala 1.15, reserva de la escopeta 24, banderas de depuración apagadas, código muerto fuera, `entrante_hud/` ignorada. | Cierre: la nota de David («todo está bien») no dejó bugs. |

### Decisiones
- D1–D15 de `Fase-3C-Contador.md`: `registro_01` solo de Centavo; Centavo no se mueve en combate y el primer golpe lo tumba; la escena corre a `timeScale` 0 con tiempo real; el retrato es UI; lip-sync por pares `_01/_02`; D13–D15: el revólver de Centavo es la segunda arma.
- Revólver: **5 balas + 15 de reserva** (el dibujo del tambor tiene 5 huecos; no los 6 + 12 de la 3C.5), 24 de daño, un tiro por clic, recarga bala por bala.
- Escopeta: dispersión 7.5, daño al 100 % hasta 6 m, 25 % a 20 m, 0 desde 30 m. **Munición 6 + 24** (antes 6 + 18).
- Centavo: **raíz del prefab a escala 1.15** (`ContadorCentavoBuilder.CentavoRootScale`); `seatedScale` 0.52 y `coverSink` 0.4 se quedan.
- Los guardias normales pelean hasta morir; Colmillo se rinde y sigue siendo interrogable.
- Ningún menú del contador depende de `UIManager`; la mira y los avisos viven en `RubberHoseCrosshair`.

### Para los demás
- **`IDamageable`** (nuevo): la escopeta y el revólver lo buscan solo si el golpe no tiene `EnemyHealth`; los guardias no cambiaron.
- **`IWeapon`** extendida (`LoadedRounds`, `MagazineSize`, `ReserveRounds`, `CancelReload`) y `IStagedReloadWeapon`; el HUD.1 le suma `AmmoType`, `DisplayStyle` y `OnAmmoChanged`.
- **La reserva de munición vive en `PlayerAmmo`** (HUD.1). Para cambiarla hay que cambiar `PlayerAmmo` (no `ShotgunController.reserveAmmo`, que es un respaldo).
- `PlayerAim.GameplayBlocked()` incluye `InterrogationDirector.IsActive`; `GameManager` no pausa con Escape durante la escena; `CartoonFx` tiene `IsHitStopActive` y `TimeHeld`; `AudioLibrary` calla las voces del mundo con la escena abierta.
- `GameStateTracker`: `ContadorState`, `ContadorInterrogation`, `contadorLied`, `revolverObtainedInScene`. `AICompanion.ContextProvider` ahora está conectado: todas las líneas de la IA reciben el resumen de pistas, interrogatorios y bajas. `AICompanion.RequestCustomLine/ShowCustomLine` y `CluePickup.Award` son nuevos.
- Diálogos (`DialogueDatabase`): el Contable del nivel 2 ya no da `registro_01` y dice «3.200 pesos, más los intereses».
- Nivel 1: 4 guardias normales nuevos (calle, sala, dormitorio, cocina). Los coloca `LevelBuilder_NivelPorky.ExtraGuards`.
- **Menús (idempotentes):** `Contador → Construir patio y cuarto`, `Colocar Centavo`, `Crear UI de interrogatorio`, `Crear datos del interrogatorio`; `Generar Nivel Porky` los incluye. Cualquier cosa de Centavo en `NivelPorky` sobrevive a regenerar.
- Código de Centavo con alturas fijas debe multiplicar por `WorldScale` (su raíz está a ×1.15).
- `AICompanion.logRequests` quedó apagado por defecto. Las teclas F9/F10 de Centavo se quitaron.
- `.gitignore`: `entrante_hud/` y `**/_entrante_*/`.
- Cada nivel trae su propio Player: escopeta 6 + 24 y, si ya se recogió el revólver en otra escena, revólver 5 + 15.

### Pendientes que dejó
- **Colmillo:** decidir si se queda (única fuente de `testimonio_puerta_01`; ver `AUDIT_contador_3C4_2`). Sigue en el nivel.
- **Final C** no se puede probar de punta a punta hasta la Fase 4 (pide ≥ 3 pistas, ≥ 2 interrogatorios **y** Don Lobo perdonado, y Don Lobo y el Campamento todavía no existen).
- **Don Lobo / Fase 4:** con el revólver solo hacen falta 13 aciertos de 18 balas; que el jefe suelte munición o que la pelea pida las dos armas.
- **VM2:** casquillos que caen, SFX propios del revólver (hoy se reusa la escopeta con pitch 1.4), «sensación» del disparo del revólver (`ShotgunFeedback`).
- **HUD.2 / HUD.3** (sesión del HUD): recogibles de munición y botiquín.
- Afinar a ojo: `coverSink` 0.55 si molesta el asomo, `seatedScale` 0.46 si el sentado se ve demasiado alto, `handsScale` del revólver (0.85–0.9 si las manos se ven grandes).
- Profiler del lip-sync (sin medir).
- API pública sin uso (lista en la sección 4 de `AUDIT_contador_3C6.md`).

### Commits principales
- 3C.0: `8c18846` · 3C.1: `68b8b53`, `64d3cc3` · 3C.2: `580d8de`, `f8a52bf` · 3C.3: `1ee5e78`, `2ecca7a`
- 3C.4: `6686e85`, `41a8dc0`, `9c35a8c` · 3C.4.1: `c8410cc` · 3C.4.2: `d0741ef`, `e5f0ed2` (assets 4.1 + 4.2)
- 3C.5: `aef044b` · 3C.5.1: `7c84a7d`, `74d6974` · 3C.5.2: `d1223f7`, `03dbabf` (assets)
- 3C.6: `84b9e25` (+ la auditoría, commit siguiente)
```

## Commits
- `03dbabf`: dia4(david): 3C.5.2 assets generados (VM_Revolver y VM_Shotgun medidos, prefabs de Player y Centavo con el revólver como dibujo, .meta de Art/Pickups)
- `84b9e25`: dia4(david): 3C.6 Centavo a escala 1.15, reserva de la escopeta 24, banderas de depuración apagadas y código muerto fuera
- Commit siguiente: esta auditoría.
