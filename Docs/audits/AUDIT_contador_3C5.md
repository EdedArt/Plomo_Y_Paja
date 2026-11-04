# AUDIT contador 3C.5: el revólver de Centavo (recogida, segunda arma, balance de armas)

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1, sobre todo D3, D13, D14 y D15), subfase 3C.5.
Leídos: `CLAUDE.md`, `docs/phases/README.md`, `docs/phases/PHASE-VM2.md`, `PHASE-4.md` (vida de Don Lobo), `AUDIT_contador_3C2.md`, `AUDIT_contador_3C4_1.md` y `AUDIT_contador_3C4_2.md`.
Graphify (`graphify-out/graph.json`): quién usa `ShotgunController`, `IWeapon`, `PlayerAim`, `RubberHoseCrosshair`, `FirstPersonViewmodel`, `CluePickup` y `PickupPresentation`. Como el grafo tenía pocas aristas hacia `IWeapon` y `PlayerAim`, se completó con búsquedas puntuales.
Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código **compila** (Roslyn de Unity 2022.3.62f3, ver abajo), pero **los menús no se ejecutaron** y **nada se probó en Play**. `VM_Revolver.asset` hoy tiene los cuadros vacíos: se llenan al correr el menú. Los "sí" de abajo describen cómo quedó programado; la prueba real es la lista del final.

## Antes de empezar
- **Fase actualizada:** `Fase-3C-Contador.md` (D13–D15, 3C.5 = revólver, 3C.6 = prueba completa) en su propio commit de documentación: `0af81c3`.
- **Respuestas de David que mandan sobre VM2** (la orden de esta sesión):
  - revólver con 6 cargadas + 12 de reserva y 24 de daño (no los 35 de VM2);
  - la escopeta cae al 100 % hasta 6 m, al 25 % a 20 m y a 0 desde 30 m, con dispersión × 1.5;
  - el revólver no está al principio: solo se consigue con el que suelta Centavo, en cualquier desenlace.

## Qué se comprobó de lo que ya se sabía (verificado, no supuesto)
| Dato | Resultado |
|---|---|
| Guardias con 60 de vida (`EnemyHealth.maxHealth`) | **Sí** |
| Escopeta: 8 perdigones × 15, "5°", sin caída por distancia | **Sí.** Ojo: el "5" **no son grados**. `ShootPellet` suma `rand(−5, 5) · 0.01` a cada eje del forward, o sea ±0.05 por eje, **≈ ±2.9°**. |
| Munición 6 + 18, sin recogibles, `AddAmmo` sin usar | **Sí** |
| Colmillo se rinde al 30 % (≤ 18 de vida) | **Sí.** Además tiene 0.4 s de gracia tras rendirse. |
| Centavo recibe daño por `IDamageable` (la escopeta mira primero `EnemyHealth`) | **Sí.** El revólver usa los **mismos dos caminos, en el mismo orden**. |
| El revólver de Centavo ya cae con física al recibir el golpe (3C.2) | **Sí:** `Revolver_Suelto` (cubo "Metal", capa Ignore Raycast) con `Rigidbody` que se agrega al caer. **Ese objeto** es ahora el recogible. |
| En el interrogatorio y su epílogo no se dispara ni se cambia de arma | **Sí,** por `PlayerAim.GameplayBlocked()`, que ya incluye `InterrogationDirector.IsActive` (escena + epílogo), el diálogo, la pausa y el game over. El revólver, el cambio de armas y la recogida lo consultan. |

## Qué parte de VM2 se hizo y qué quedó pendiente
**Hecho (el mínimo para jugar):**
- **`IWeapon` extendida** (no se creó otra interfaz): `LoadedRounds`, `MagazineSize`, `ReserveRounds`, `CancelReload()`. `ShotgunController` las implementa con sus campos de siempre.
- **`WeaponSwitcher`**: teclas 1/2, rueda del mouse, bajada y subida de manos, cancela la recarga. Solo el arma activa tiene el componente encendido.
- **`RevolverController : IWeapon`**: hitscan de una bala, semiautomático, con el mismo camino de daño que la escopeta.
- **Recarga:** completa en 2.2 s, con los 3 cuadros del arte (abrir, meter, cerrar) en la secuencia abrir → meter ×3 → cerrar. La arma `ViewmodelProfileBuilder.GetOrCreateRevolverProfile`.
- **Mira, `PlayerAim` y viewmodel** siguen al arma activa (`FirstPersonViewmodel.SetWeapon/SetProfile`, `PlayerAim.SetWeapon`, `RubberHoseCrosshair.SetWeapon`).

**Pendiente de VM2 (no se hizo):**
- **Recarga bala por bala con las balas visibles** (`ChamberOverlay`, 5 aros, casquillos que caen). **Por qué no:**
  - el arte `reload_01` tiene **5 huecos** y el tambor de Centavo es de **6** (D13: "sabe contar hasta seis");
  - además, los 5 aros tiene que acomodarlos David a mano en el Inspector.

  Se reemplazó por la recarga completa (2.2 s), como permitía la orden.
- **Eventos `OnReloadOpened`, `OnRoundLoaded` y `OnReloadClosed`**, y "disparar durante la recarga termina la bala y dispara".
- **`IWeapon.Profile`:** el perfil lo guarda `WeaponSwitcher` (`shotgunProfile` / `revolverProfile`), así que no hizo falta.
- **Contador de onomatopeyas compartido y "sensación" del disparo del revólver:** `ShotgunFeedback` (culatazo de cámara, temblor, luz y "¡BANG!") exige `ShotgunController`, así que el revólver no la tiene. El revólver sí tiene el culatazo y el fogonazo del viewmodel y el destello de `ViewmodelLighting`.
- **SFX propios del revólver:** no hay en la librería. Se usan los de la escopeta: disparo con pitch 1.4 (como Centavo), `escopeta_vacia` y `escopeta_cargar`. Hay ranuras `AudioClip` opcionales en el Inspector.

## Cambio de armas: teclas, tiempo, ¿cancela la recarga?
- **Teclas:**
  - **1** (o 1 del teclado numérico) = escopeta;
  - **2** = revólver;
  - **rueda del mouse** = la otra.

  Sin revólver, la 2 y la rueda **no hacen nada**.
- **Tiempo: 0.35 s en total** (`switchTime`, tiempo real):
  1. las manos bajan en 0.175 s, con la misma bajada que la recarga sin cuadros (`ViewmodelMotion.StartReloadDip`, vía `FirstPersonViewmodel.PlaySwitchDip`);
  2. a la mitad cambian el perfil y el arma;
  3. suben en 0.175 s.
- **Durante el cambio:** las dos armas están apagadas (no se dispara) y `PlayerAim` no deja apuntar (`WeaponSwitcher.IsSwitching`).
- **¿Cancela la recarga? Sí.** `CancelReload()` corta la corrutina sin cargar nada. La escopeta, si quedó vacía, recarga al volver a ella (apretando disparar o R).
- **Bloqueado** en pausa, diálogo, interrogatorio + epílogo y muerto.
- **Robustez:** fuera de esos bloqueos, `WeaponSwitcher` vuelve a imponer cada frame que solo el arma activa esté encendida. Así no quedan las dos encendidas si `DialogueNPC` o `PlayerHealth.ResetHealth` encienden la escopeta con el revólver en la mano.
- **Depuración:** `WeaponSwitcher.debug` (apagado) = empezar con el revólver.

## Recogida: cómo se recoge, ¿funciona en los 3 desenlaces?
- **Cómo:** `WeaponPickup` en `Revolver_Suelto`. Es **la misma interacción que las pistas** (`CluePickup`): acercarse (2.5 m) y apretar **E**.
  - **Prompt:** "[E] Recoger el revólver" bajo la mira. El prompt de `UIManager` no existe en las escenas, así que se puso en el canvas de la mira.
  - **Al recogerlo:**
    - suena `escopeta_cargar` y sale el aviso **"Revólver de Centavo"** 2.5 s;
    - da **6 + 12** balas;
    - **se cambia solo al revólver la primera vez**;
    - el objeto se apaga.
  - **No es una pista:** no llama a `RegisterClueCollected` ni a la IA.
- **Por qué el aviso no es el de las pistas:** `CluePickup.Award` muestra el texto con `UIManager.ShowClueUI`, y `UIManager` **no existe en ninguna escena** (regla 4). El aviso va en el canvas de la mira (`RubberHoseCrosshair.ShowNotice`).
- **Brillo sutil:** una luz puntual chica (1.2 m) que late. No se usó `PickupPresentation` porque mueve el objeto y este tiene `Rigidbody`.
- **¿Cuándo se puede recoger?** En cuanto aparece y pasan 0.4 s (lo que tarda en caer). Se puede tomar con Centavo herido, antes de la escena.
- **Los 3 desenlaces (por código):** el revólver cae en `EnterHit`, que es **el primer golpe en todos los caminos**. `DropRevolver` lo saca del prefab (`SetParent(null)`), así que no depende de lo que pase después con Centavo:

  | Desenlace | ¿Está en el piso? |
  |---|---|
  | Matado en la escena | sí: la escena (`ResolveKilled`) no lo toca |
  | Perdonado | sí: Centavo se va cojeando y lo deja |
  | Rematado antes de la escena (D4) | sí: ya estaba en el piso desde el golpe |

  Durante la escena no se puede recoger (bloqueado); al terminar, sí.
- **Cambio de nivel:** cada escena trae su propio Player. Para que D13 valga en el Campamento:
  - `GameStateTracker` (que sobrevive entre escenas) guarda **en qué escena** se recogió (`revolverObtainedInScene`);
  - en **otra** escena, `WeaponSwitcher` lo da desde el principio con 6 + 12 (cada nivel empieza con la munición llena, como la escopeta);
  - si se **reinicia la misma escena**, no se da: hay que volver a recogerlo del piso de Centavo;
  - `ResetState()` lo borra.
- **D3 con el revólver:** `RevolverController` busca primero `EnemyHealth` y después `IDamageable`, igual que la escopeta. Un tiro a Centavo llama a `TakeDamage(24)` → Hit. Herido, otro tiro → D4.

## Tabla de balance antes/después (escopeta y revólver)
Todos los números son campos serializados (Inspector).

**Escopeta** (`ShotgunController`):

| | Antes | Después |
|---|---|---|
| Perdigones × daño | 8 × 15 | 8 × 15 (igual) |
| Dispersión, cadera | 5 (≈ ±2.9° por eje) | **7.5** (≈ ±4.3°) |
| Dispersión, apuntando | ×0.6 = 3 (≈ ±1.7°) | ×0.6 = **4.5** (≈ ±2.6°) |
| Caída del daño | ninguna | **100 % hasta 6 m → 25 % a 20 m → 0 a 30 m** (`DamageMultiplierAt`) |
| Alcance | 30 m | 30 m |
| Cadencia | 1.2 s | 1.2 s |
| Recarga | 2.5 s | 2.5 s |
| Munición | 6 + 18 | 6 + 18 |

Otros cambios de la escopeta:
- **Sin efecto con 0 de daño:** un perdigón que llega con 0 (desde 30 m) no llama a `TakeDamage`.
- **Recarga:** si se cambió de arma antes de la recarga automática, no recarga escondida. Al volver, disparar con el arma vacía y reserva recarga (antes: clic en vacío).
- **El prefab ya tiene 5 guardado:** el menú lo pasa a 7.5 **solo si sigue en 5**. Un ajuste a mano no se pisa.

**Revólver** (`RevolverController`, nuevo):

| Daño | Dispersión cadera | Dispersión apuntando | Alcance | Caída | Cadencia | Tambor + reserva | Recarga |
|---|---|---|---|---|---|---|---|
| **24** (40 % de 60, D15) | 0.5° | 0° | 80 m | ninguna | 0.45 s, un tiro por clic | **6 + 12** | completa, 2.2 s |

**Daño de la escopeta por disparo contra un guardia** (simulación de 20 000 disparos, cápsula de 0.8 × 2 m, apuntando al centro):

| Distancia | Antes | Ahora, cadera | Ahora, apuntando |
|---|---|---|---|
| 3–5 m | 120 (mata 100 %) | 120 (100 %) | 120 (100 %) |
| 6 m | 120 (100 %) | 107 (100 %) | 120 (100 %) |
| 8 m | 120 (100 %) | 71 (74 %) | 107 (100 %) |
| 10 m | 96 (99 %) | 50 (18 %) | 84 (95 %) |
| 12 m | 80 (91 %) | 36 (8 %) | 61 (66 %) |
| 15 m | 64 (71 %) | 20 (0 %) | 37 (2 %) |
| 20 m | 48 (40 %) | 5 | 13 |

- **A quemarropa pega igual que antes:** hasta 6 m siguen entrando los 8 perdigones (120).
- **De lejos:** desde la cadera ya no mata de un tiro pasados ~8 m; apuntando, hasta ~10 m.

## ¿Los guardias se siguen rindiendo con ambas armas? ¿Don Lobo es vencible con cada una? (estimaciones)
**Guardias (60 de vida, se rinden a ≤ 18):**
- **Revólver:** 60 → 36 → 12.
  - Guardia normal: muere al **3.er** tiro.
  - Colmillo: **se rinde al 2.º** (12 ≤ 18). Un 3.er tiro lo remata (cuenta como muerto, como siempre).
  - No hay un solo tiro que lo salte: 24 < 60 − 18.
- **Escopeta:** cada perdigón quita como mucho 15, menos que la ventana de 18, así que **siempre** pasa por la rendición. A quemarropa, 60 → 45 → 30 → 15 se rinde al 3.er perdigón, y los 0.4 s de gracia anulan el resto. De lejos, con perdigones de menos daño, tarda más tiros pero también se rinde.
- **Ojo con el orden del nivel:** Colmillo está en la cocina, **antes** del patio. Centavo solo se activa con los guardias del patio fuera de combate (D2). En la ruta normal, el revólver llega **después** de Colmillo. Si el jugador deja a Colmillo para después, con el revólver también se rinde (2 tiros).

**Don Lobo** (Fase 4, `BossAI` **todavía no existe**). Según `PHASE-4.md`: 400 de vida y se rinde a ≤ 25 % con el daño frenado en el umbral, o sea hay que quitar **300**. Lo que sigue son estimaciones (tiempo puro de disparo, sin coberturas ni esquivar).

| Arma | Distancia | Tiros | Tiempo |
|---|---|---|---|
| Escopeta | ≤ 6 m | 3 | ~2.5 s |
| Escopeta | ~8 m, desde la cadera | 4–5 | ~5–6 s |
| Escopeta | 10–12 m, desde la cadera | 6–9 | ~6–13 s (con una recarga) |
| Revólver | cualquiera (sin caída) | 13 aciertos (300 / 24); con 75 % de puntería, ~17 balas | ~12 s (17 × 0.45 s + 2 recargas × 2.2 s) |

- **Escopeta:** vencible. Lo es más si se pelea de cerca, que es lo que pide D14.
- **Revólver solo:** vencible, pero **justo**. Hay 18 balas en total (6 + 12) y hacen falta 13 aciertos (72 %).
- **Con las dos:** cómodo.
- **Recomendación para la Fase 4:** que el jefe deje recoger munición del revólver, o que la pelea pida usar las dos armas. Con el revólver solo, una mala puntería deja al jugador sin balas.

## Munición del nivel 1: la cuenta nueva (con la caída y la dispersión nueva)
- **Base:** la de `AUDIT_contador_3C4_2.md`. Los guardias pegan cuerpo a cuerpo y corren a 5 m/s; el primer tiro suele salir a 8–12 m y el segundo a quemarropa.
- **Con la caída,** el disparo a 8–12 m ya no mata desde la cadera (18–74 %). Subí los tiros por guardia: promedio 1.5 → **1.75**, pesimista 2 → **2.5**.

**Solo escopeta:**

| Blanco | Promedio | Pesimista |
|---|---|---|
| 6 guardias que pelean hasta morir | 6 × 1.75 = 10.5 | 6 × 2.5 = 15 |
| Colmillo (siempre se rinde; de lejos puede necesitar 2) | 1.5 | 2 |
| Centavo (cualquier perdigón lo tumba, D3; la dispersión más ancha ayuda a darle asomado) | 3 | 4 |
| Rematar a Centavo herido (D4, opcional) | — | 1 |
| **Total** | **≈ 15** | **≈ 22** |

- **Disponible:** 24.
- **Margen:** ≈ 9 en el promedio (37 %) y **≈ 2 en el pesimista (8 %)**. Antes era 10 y 5.
- **Si el jugador dispara mucho desde la cadera a más de 10 m** (3 tiros por guardia), el pesimista llega a **≈ 25: falta 1 cartucho**.

**Escopeta + revólver:**
- El revólver solo aparece al **primer golpe a Centavo**. Para entonces los 6 guardias y Colmillo ya están fuera de combate (D2 obliga a despejar el patio, y la casa está antes).
- En el nivel 1 solo puede reemplazar el remate opcional (D4): **pesimista ≈ 21 cartuchos**.
- Las 18 balas del revólver quedan casi enteras para lo que venga. **No arreglan la munición de la escopeta en el nivel 1.**

**¿Alcanzan los 24 cartuchos?** **Sí en el promedio, muy justo en el pesimista.** Apuntar (clic derecho) mantiene el tiro letal hasta ~10 m.
- **Si la prueba muestra que faltan, lo más chico es:**
  - **(a)** subir la reserva de la escopeta de 18 a 24 (campo `reserveAmmo` del prefab);
  - **(b)** 2–3 cajas de cartuchos con `ShotgunController.AddAmmo`, que ya existe y nadie llama.
- No lo cambié: la orden es reportar.

## Mira: ¿cambia según el arma?
**Sí.** `RubberHoseCrosshair` lee `IWeapon.SpreadAngle` del **arma activa** (`SetWeapon` lo llama `WeaponSwitcher`).
- **Escopeta:** 7.5 desde la cadera, **más abierta que antes** (5).
- **Revólver:** el ángulo se pasa a las mismas unidades (`tan(0.5°) · 100 ≈ 0.87`), así que las marcas quedan **casi cerradas**.
- El marcador rojo de impacto (`EnemyHealth.OnDamaged`) funciona igual con el revólver.
- Al apuntar la mira se sigue ocultando (sin cambios).

## Cómo sabe el jugador cuántas balas tiene
- **Propuesta más chica (hecha):** una fila de **puntos chicos debajo de la mira**, un punto por bala del cargador del arma activa:
  - lleno = cargada;
  - tenue = vacía;
  - al lado, **"+N"** con la reserva.
- **Cómo está hecha:**
  - usa el mismo sprite del punto central de la mira y el mismo canvas Overlay;
  - sigue visible al apuntar y se oculta en pausa, diálogo, interrogatorio y muerte;
  - son ~60 líneas en `RubberHoseCrosshair`.
- **Para las dos armas:** la escopeta también tiene 6 y no costaba nada. Se puede apagar con `showAmmo` en el Inspector del Player.
- **Fuente:** el "+N" y los avisos usan la fuente por defecto de TMP (como el "¡BANG!" de `ShotgunFeedback`).
- No reemplaza el HUD de munición de la 3B (parte 4), que sigue pendiente.
- **Lo que no se hizo:** las balas visibles en el tambor de VM2 (ver arriba).

## Menús que David debe correr
1. Abrir Unity y dejar que compile.
2. **Plomo y Paja → Crear Prefabs de Prueba.** En `Player.prefab`:
   - agrega el GameObject `Revolver` bajo la cámara (`RevolverController` apagado + `AudioSource`);
   - agrega `WeaponSwitcher` con los dos perfiles;
   - pasa la dispersión de la escopeta de 5 a 7.5;
   - **rearma `VM_Revolver.asset`** (cuadros idle, fire, aimidle y la secuencia de recarga de 7 cuadros a 3.18 cps). En la consola debe salir `[Viewmodel] VM_Revolver: idle 1, fire 1, … reload 7`.

   El Player de NivelPorky es instancia del prefab, así que recibe los cambios.
3. **Plomo y Paja → Contador → Colocar Centavo.** El prefab de Centavo se rehace con `WeaponPickup` en `Revolver_Suelto`. "Construir patio y cuarto" y **"Generar Nivel Porky"** también lo hacen, así que el recogible no se pierde.
4. **Ctrl+S** y commit de `Player.prefab`, `Centavo.prefab`, `VM_Revolver.asset` y `NivelPorky.unity`.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`) contra los módulos de `Managed/UnityEngine`, `UnityEditor.dll` y `Library/ScriptAssemblies` (TMP incluido): **Assembly-CSharp y Assembly-CSharp-Editor con 0 errores**.
- **No probado:** los menús, `VM_Revolver` con cuadros, y nada en Play.
- **APIs dudosas:**
  - `Input.mouseScrollDelta` con el Input Manager viejo: debería andar en cualquier mouse.
  - Que el `Rigidbody` del revólver quede quieto en el piso del cuarto, que tiene un escalón.
  - Que la fuente por defecto de TMP tenga la "ó" de "Revólver": LiberationSans SDF trae Latin-1.
- **Detalle conocido:** si se cambia de arma **durante** la recarga de la escopeta (que baja las manos), la bajada del cambio arranca desde arriba, así que las manos dan un salto corto. Es cosmético.
- **Revisar en Play:**
  1. Al empezar el nivel: solo escopeta; la 2 y la rueda no hacen nada. Debajo de la mira, 6 puntos y "+18".
  2. Escopeta: a quemarropa un guardia muere de un tiro como antes; a ~12 m desde la cadera ya no. Apuntando llega más lejos. La mira se ve más abierta.
  3. Colmillo con la escopeta: se rinde (no muere de un tiro).
  4. Centavo: un tiro (escopeta o revólver) lo tumba y suelta el revólver con brillo. Acercarse: "[E] Recoger el revólver"; con E aparece "Revólver de Centavo", las manos bajan y suben con el revólver, y la fila de la mira muestra 6 puntos y "+12".
  5. Repetirlo en los 3 desenlaces: recoger antes de la escena; no recogerlo y matarlo en la escena; perdonarlo; rematarlo herido (D4). En todos el revólver tiene que seguir en el piso y poder recogerse.
  6. Durante la escena y el epílogo: E, 1, 2, rueda y clic no hacen nada.
  7. Revólver: un tiro por clic; la mira se cierra; los puntos se apagan de a uno. Al vaciarlo recarga solo (abrir, meter, meter, meter, cerrar) en ~2.2 s. R recarga. Sin balas: clic en vacío.
  8. Cambiar de arma a mitad de recarga: la corta sin errores, no se carga nada. Volver a la escopeta vacía y disparar: recarga.
  9. Si Colmillo sigue vivo después de Centavo: con el revólver se rinde al 2.º tiro.
  10. Morir con el revólver en la mano y reiniciar: se empieza solo con escopeta y el revólver vuelve a caer de Centavo.
  11. Terminar el nivel y anotar cuántos cartuchos de escopeta sobran (ver la cuenta).

## Clases existentes reutilizadas (y por qué)
- **`IWeapon`:** extendida, no duplicada (regla 4). Ya la leían el viewmodel, `PlayerAim` y la mira.
- **`FirstPersonViewmodel.SetWeapon/SetProfile`, `PlayerAim.SetWeapon`:** se hicieron para la "fase 2" del cambio de arma.
- **`ViewmodelMotion.StartReloadDip`:** la bajada y subida del cambio de arma.
- **`ViewmodelAnimator`:** el clip de recarga del perfil (sin cambios de código).
- **`ViewmodelProfileBuilder.BuildProfile`:** `VM_Revolver` con los PNG de VM 1.1–1.4.
- **`EnemyHealth.TakeDamage` / `IDamageable`:** el mismo camino de daño que la escopeta (D3, D15).
- **`PlayerAim.GameplayBlocked`:** un solo bloqueo para pausa, diálogo, interrogatorio + epílogo y game over.
- **`RubberHoseCrosshair`:** la mira única (regla 4). Muestra la dispersión, la munición y los avisos.
- **Interacción de `CluePickup`** (E + distancia): copiada en `WeaponPickup`, que **no** es una pista.
- **`GameStateTracker`** (sobrevive entre escenas): para llevar el revólver al nivel 2.
- **`AudioLibrary`:** SFX de la escopeta (no hay de revólver).
- **`TestPrefabGenerator.CompletePlayerPrefab` y `ContadorCentavoBuilder`:** los menús de siempre (idempotentes).

## Archivos creados/modificados
Creados (con `.meta`):
- `Scripts/Player/RevolverController.cs`: el revólver (`IWeapon`).
- `Scripts/Player/WeaponSwitcher.cs`: inventario y cambio de armas.
- `Scripts/Player/WeaponPickup.cs`: recogible del revólver de Centavo.
- `Docs/audits/AUDIT_contador_3C5.md` (este archivo).

Modificados:
- `Scripts/Player/IWeapon.cs`: munición y `CancelReload`.
- `Scripts/Player/ShotgunController.cs`: caída por distancia, dispersión 7.5, miembros de `IWeapon`, cancelar la recarga.
- `Scripts/Player/FirstPersonViewmodel.cs`: `PlaySwitchDip`.
- `Scripts/Player/PlayerAim.cs`: no apunta durante el cambio de arma.
- `Scripts/UI/RubberHoseCrosshair.cs`: `SetWeapon`, fila de munición, `ShowNotice`, `SetPrompt`; la mira se oculta al apuntar, pero la munición no.
- `Scripts/Core/GameStateTracker.cs`: `revolverObtainedInScene`, `RegisterRevolverObtained`, `HasRevolverFromEarlierScene`, reset.
- `Scripts/Editor/TestPrefabGenerator.cs`: `EnsureRevolverAndSwitcher`.
- `Scripts/Editor/ViewmodelProfileBuilder.cs`: secuencia de recarga del revólver (`RevolverReloadSeconds`).
- `Scripts/Editor/ContadorCentavoBuilder.cs`: `WeaponPickup` en `Revolver_Suelto`.
- `Docs/phases/Fase-3C-Contador.md`: fase actualizada por David (commit aparte).

Sin cambios: `EnemyHealth`, `EnemyAI`, `CentavoController`, `InterrogationDirector`, `DialogueNPC` y `PlayerHealth`. El bloqueo se resuelve con `GameplayBlocked` y con que `WeaponSwitcher` imponga qué arma está encendida.

## Commits
- `0af81c3`: docs: fase 3C actualizada (D13–D15, 3C.5 revólver de Centavo, 3C.6 prueba completa)
- `aef044b`: dia4(david): 3C.5 revólver de Centavo (recogida, segunda arma, cambio de armas y balance)
- Commit siguiente: esta auditoría.
