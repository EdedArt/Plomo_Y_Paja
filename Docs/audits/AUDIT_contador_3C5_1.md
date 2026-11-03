# AUDIT contador 3C.5.1: arreglos de la prueba de la 3C.5 (revólver) y pensamiento al rematar a Centavo

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1). Leídos `AUDIT_contador_3C2.md` y `AUDIT_contador_3C5.md`, el `Editor.log` de la prueba de David, los `.meta` de `Art/Viewmodel`, `Player.prefab`, `Centavo.prefab` y `NivelPorky.unity` (solo lectura) y la geometría del cuarto en `ContadorYardBuilder`.
Rama: `integracion/dia4`. **No se empezó la 3C.6.**

> **Importante:** Unity no se abrió en esta sesión. El código **compila**, pero **los menús no se ejecutaron** y **nada se probó en Play**. Las causas de abajo salen de leer los archivos y el log, no de probar.

## BUG 1: VM_Revolver salía con 0 cuadros

### Causa real
Los PNG del revólver estaban importados como **cubemap** y no como textura 2D. Por eso no tenían Sprite.
- En los `.meta`, `textureShape: 2` (Cube) **en los 6 PNG de `Art/Viewmodel/Revolver/`** y en `FX/fx_casing_revolver_01.png` y `FX/fx_round_rim_01.png`. Los de la escopeta y `fx_fogonazo_01` tienen `textureShape: 1` (2D). Es la única diferencia de importación entre los dos juegos de PNG, aparte del GUID y de campos que dependen de la forma.
  - Esos `.meta` vienen del commit del recorte del revólver (VM 1.1), no de Unity.
- Con forma Cube, Unity importa un `Cubemap`, aunque `textureType` diga Sprite. Así no hay Sprite y `AssetDatabase.FindAssets("t:Sprite")`, que usa `ViewmodelProfileBuilder.LoadNumbered`, no encuentra ninguno. Por eso el log decía `idle 0 … reload 0` y `fogonazos 1`: el fogonazo sí es 2D.
- **Lo que no es la causa:**
  - los nombres siguen el patrón `wilbur_vm_revolver_<clip>_<nn>`;
  - la carpeta es la esperada (`Assets/_Game/Art/Viewmodel/Revolver`);
  - los GUID no están repetidos;
  - los PNG son reales (1920×1080 RGBA).
- **Por qué el arreglo automático de `ViewmodelArtImporter` no lo corrigió:**
  - `NeedsFix` no miraba la forma;
  - `CheckOnLoad` buscaba con `"t:Texture2D"`, que **no encuentra los importados como Cubemap**.

### Arreglo (`Scripts/Editor/ViewmodelProfileBuilder.cs`)
- `ViewmodelArtImporter.Apply` ahora pone `textureShape = Texture2D`, y `NeedsFix` lo comprueba.
- `GetVersion` sube de 2 a **3**, así que Unity reimporta todo el arte del viewmodel al abrir.
- `CheckOnLoad` busca con `"t:Texture"`, que incluye los cubemaps.
- **Nuevo `ViewmodelArtImporter.FixFolderNow(carpeta)`:** antes de armar un perfil, `BuildProfile` reimporta **en el momento y de forma sincrónica** los PNG mal importados de la carpeta del arma y de `FX`. Lo hace aunque ya se hayan intentado en la sesión, y avisa en la consola cuántos reimportó.
- **✗ claro en la consola** (`ReportMissing`, como `Debug.LogError`). Si faltan cuadros sale `[Viewmodel] ✗ VM_Revolver: faltan cuadros:` con una línea por problema:
  - cada PNG con el nombre correcto que no dio Sprite, con el motivo (`tipo …, forma …, modo …`);
  - PNG con nombres fuera del patrón;
  - carpeta sin PNG válidos;
  - falta de `idle`.
- **Resultado esperado** al correr *Crear Prefabs de Prueba*:
  - `[Viewmodel] 8 PNG reimportados como Sprite 2D antes de armar VM_Revolver.` (solo la primera vez; si Unity ya los reimportó al abrir por la versión 3, no aparece);
  - `[Viewmodel] VM_Revolver: idle 1, fire 1, cycle 0, reload 7, aimIdle 1, aimFire 0, aimCycle 0, fogonazos 1`;
  - ningún ✗.
- **Los `.meta` no se editaron a mano.** Unity los reescribe al reimportar (`textureShape: 1`). David tiene que subir esos 8 `.meta` junto con `VM_Revolver.asset`.

### ¿El Revolver y el WeaponSwitcher están en Player.prefab y en el Player de NivelPorky?
**Sí.** Revisé los archivos que dejó el menú de David (sin commit en el árbol de trabajo):
- `Player.prefab` tiene `RevolverController` (1 referencia al GUID del script) y `WeaponSwitcher` (1).
- En `NivelPorky.unity`, el Player es una instancia de `Player.prefab` cuyas modificaciones son **solo** posición, rotación y nombre:
  - `m_RemovedComponents: []` y `m_RemovedGameObjects: []`: no hay overrides que oculten el revólver o el switcher;
  - tiene un componente agregado en la escena (`m_AddedComponents`, uno solo), que ya estaba y no es de esta fase.

Así que la escena recibe lo del prefab. El problema del Bug 1 era **solo** el perfil sin cuadros. Con el perfil vacío, al cambiar al revólver las manos desaparecen (`ViewmodelAnimator.Play` apaga la imagen si idle no tiene cuadros).

## BUG 2: no aparecía el revólver para recoger

### Revisión de la cadena
| Paso | Estado |
|---|---|
| `Revolver_Suelto` en `Centavo.prefab` | **Sí.** Existe en el prefab (no se crea en runtime): cubo, capa 2 (Ignore Raycast), `BoxCollider`, `MeshRenderer` y **`WeaponPickup`**, apagado. |
| Instancia de Centavo en NivelPorky | **Al día.** Solo modifica posición, rotación, nombre, `shed` y `yardGuards`. Sin componentes ni objetos quitados. |
| Capa / visibilidad | Ignore Raycast **sí** la dibuja la Main Camera (solo `Viewmodel` está excluida). La matriz de colisiones está toda en 1, así que choca con el piso. |
| `WeaponPickup`: E, 2.5 m, prompt, `GameplayBlocked` | El código funcionaba, pero medía la distancia en 3D desde el `transform` del Player. |
| **`DropRevolver`** | **Aquí está la causa.** |

### Causa real
- **El revólver caía detrás del banco, en una zona a la que el jugador no puede entrar.**
  - Salía de la mano de Centavo de pie (`Anchor_CentavoStand`, detrás del banco) con velocidad **"away"** (`AwayFromPlayer`, en contra del jugador) × 1.2 + 2.2 hacia arriba.
  - O sea: volaba hacia el fondo del cuarto y caía en el piso detrás del banco, cerca de la pared de atrás.
- **Esa franja la cerraron a propósito en la 3C.1** (`ContadorYardBuilder`, "Barriles y caja: cierran los lados del banco (no se pasa por detrás)").
  - Desde el frente del banco (z ≥ ~1.1 m, donde además está Centavo sentado), el revólver quedaba a ~2 m pero **tapado por un banco de 1 m**.
  - Era un cubo oscuro de 5 × 12 × 22 cm en un cuarto con poca luz: el jugador **no lo veía**.
  - Y el prompt solo salía si por casualidad quedaba a ≤ 2.5 m en 3D.
- **No hay evidencia de que atravesara el piso.** Igual se agregó la protección de abajo, porque es una pieza fina con detección discreta y cae sobre un escalón.

### Arreglo
- **`CentavoController.DropRevolver`:**
  - el revólver ahora cae **delante del banco**, a la derecha de donde Centavo queda sentado: `Anchor_CentavoSeated` + 0.6 m a la derecha (`revolverDropSide`, serializado) + 0.15 m hacia la puerta;
  - aparece justo encima del borde del banco (`BenchCover.bounds.max.y` + 0.25 m) y cae con un empujoncito hacia el jugador y un giro;
  - el `Rigidbody` va con **ContinuousDynamic** e interpolación;
  - si no hay cuarto asignado, cae hacia el jugador (no "away");
  - le pasa a `WeaponPickup` el **punto seguro** (el punto de aterrizaje).
- **`WeaponPickup`:**
  - **rescate:** si cae 1.5 m por debajo del punto seguro (atravesó el piso), vuelve ahí y queda quieto (kinemático);
  - **se ve:** look plano con contorno (`CharacterLook.ApplyPlaceholder`, como las pistas) y un brillo más fuerte (alcance 1.8 m, intensidad 0.4–1.8);
  - **rango:** 2.5 m en **horizontal** y hasta 2.2 m de diferencia de altura, en lugar de distancia 3D desde el pivote del Player;
  - busca el `WeaponSwitcher` también en los padres del objeto con tag Player;
  - **`debug`** (apagado por defecto) escribe en la consola dónde aparece, el punto seguro, dónde queda quieto y, cada 0.5 s, la distancia horizontal y vertical al jugador, si ya se puede recoger, si está bloqueado y si se ve el prompt. También avisa si no encuentra el Player con `WeaponSwitcher` y si lo rescató del piso.
- **Recordatorio de la 3C.5:** acercarse a ≤ 3 m de Centavo herido abre la escena. El revólver queda al lado de Centavo, así que lo normal es recogerlo **después** de la escena (matarlo o perdonarlo) o rematándolo antes (D4). Con Centavo herido se puede tomar si se llega a ≤ 2.5 m del revólver sin entrar a 3 m de él.

## FEATURE: pensamiento si se remata a Centavo sin escucharlo (D4)
- **Antes:** el remate (D4) llamaba `AICompanion.Speak(EnemyKilled)`, que sale solo el 50 % de las veces y con enfriamiento.
- **Ahora:** `CentavoController.Die(true)` arranca `FinishedOffThought()`:
  1. pide a la IA `AICompanion.RequestCustomLine(situación)`;
  2. espera **4 s en tiempo real** (`finishedOffTimeoutSeconds`);
  3. muestra **siempre** una línea con el nuevo `AICompanion.ShowCustomLine`: los subtítulos normales del juego (máquina de escribir), **sin bloquear al jugador ni mover la cámara**;
  4. si la IA no contesta a tiempo, sale una línea de respaldo al azar. Una respuesta tardía se ignora.
- **Situación enviada a la IA** (`InterrogationData.finishedOffSituation`): "Wilbur acaba de rematar a Centavo, herido en el piso y pidiendo que lo escuchara, sin escucharlo. Centavo era el contador de Don Lobo y sabía de la deuda de Porky. Dale a Wilbur un pensamiento en español, noir y ambiguo, de una o dos frases y como mucho 25 palabras, sin decirle qué debería haber hecho."
  - `RequestCustomLine` le suma el contexto del juego y las últimas líneas, para no repetir.
  - Las reglas generales (español, noir, ambiguo, ≤ 25 palabras) ya estaban en el prompt del sistema de AICompanion.
- **Líneas de respaldo** (exactas):
  - "Ni siquiera lo dejaste hablar. Lo que sabía se quedó en el suelo con él."
  - "Pedía que lo escucharas. Ahora ya no va a decir nada… ni a favor ni en contra de Porky."
  - "Un tiro rápido ahorra preguntas. También respuestas."
- **Dónde se agregaron:**
  - en el guion: `Docs/guiones/contador.md` §5, "Pensamiento si lo rematas sin escucharlo";
  - en los datos: `InterrogationData.finishedOffThoughts`, que llena el menú "Crear datos del interrogatorio" (`ContadorInterrogationDataBuilder`).
  - **Si David todavía no corrió ese menú,** el asset no tiene las líneas y el código usa la primera como último recurso, así que el pensamiento sale igual.
- **D4 sin cambios en el estado:**
  - `contador = Dead` y `RegisterEnemyKilled("Centavo")`;
  - `ContadorInterrogation` queda en `None`;
  - sin `registro_01` y sin interrogatorio contado.

  Solo cambia el comentario de la IA, que ya no es el genérico al azar.
- **Sin `AICompanion` en la escena** no se muestra nada: no hay dónde mostrarlo, igual que antes.

## Menús que David debe correr
1. Abrir Unity y dejar que compile. Al subir la versión del importador a 3, se reimporta el arte del viewmodel y los 8 PNG pasan a 2D.
2. **Plomo y Paja → Crear Prefabs de Prueba.** En la consola debe salir `VM_Revolver: idle 1, fire 1, … reload 7` **sin ✗**.
3. **Plomo y Paja → Contador → Crear datos del interrogatorio.** Agrega la situación y las 3 líneas de respaldo del remate.
4. **Plomo y Paja → Contador → Colocar Centavo.** Rehace el prefab con el campo nuevo `revolverDropSide`; también lo hacen "Construir patio y cuarto" y "Generar Nivel Porky".
5. **Ctrl+S** y commit de:
   - `VM_Revolver.asset` y los **8 `.meta`** reimportados (6 de `Revolver/`, `fx_casing_revolver_01` y `fx_round_rim_01`);
   - `Player.prefab`, `Centavo.prefab`, `Data/Interrogation_Contador.asset` y `NivelPorky.unity` si cambió.

   Los dos prefabs de la prueba de la 3C.5 siguen **sin commit** en el árbol de trabajo. No los subí porque cambian otra vez con estos menús.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra los módulos de `Managed/UnityEngine`, `UnityEditor.dll` y `Library/ScriptAssemblies`: **Assembly-CSharp y Assembly-CSharp-Editor con 0 errores**.
- **No probado:** la reimportación, los menús y nada en Play.
- **APIs dudosas:**
  - Que `ImportAssetOptions.ForceSynchronousImport` dentro de *Crear Prefabs de Prueba* (con el prefab del Player abierto con `LoadPrefabContents`) deje los Sprites listos en la misma llamada. Si no, correr el menú **dos veces**: la segunda ya los encuentra.
  - `Rigidbody.IsSleeping()` para el log "quedó quieto".
- **Revisar en Play:**
  1. Con 2 o la rueda (después de recoger el revólver) se ven las manos con el revólver; R muestra abrir → meter ×3 → cerrar.
  2. Darle a Centavo: el revólver cae **delante del banco, a la derecha de Centavo sentado**, se ve con contorno y brillo. Ajustar `revolverDropSide` si queda contra la caja o encima de Centavo.
  3. Después de la escena (matarlo o perdonarlo), acercarse: "[E] Recoger el revólver" → E → "Revólver de Centavo" y cambio al revólver.
  4. D4: herido, dispararle antes de los 3 m. Siempre aparece un pensamiento en los subtítulos: el de la IA si hay conexión, o una de las 3 líneas en unos 4 s. El jugador sigue moviéndose. En el `GameStateTracker`: `contadorState = Dead`, `contadorInterrogation = None`, sin `registro_01`.
  5. Con `debug` activo en el `WeaponPickup` del prefab de Centavo, la consola muestra dónde aparece y dónde queda, y la distancia al jugador cada 0.5 s.

## Archivos creados/modificados
Creados:
- `Docs/audits/AUDIT_contador_3C5_1.md` (este archivo).

Modificados:
- `Scripts/Editor/ViewmodelProfileBuilder.cs`: forma 2D en el importador (versión 3), `FixFolderNow`, búsqueda `"t:Texture"`, reporte ✗ de cuadros faltantes.
- `Scripts/Characters/Contador/CentavoController.cs`: `DropRevolver` delante del banco (`revolverDropSide`, continuo, punto seguro); D4 → `FinishedOffThought` en vez de `Speak(EnemyKilled)`.
- `Scripts/Player/WeaponPickup.cs`: `SetSafeSpot` + rescate, look con contorno, brillo más fuerte, rango horizontal, `debug`.
- `Scripts/AI/AICompanion.cs`: `ShowCustomLine` (subtítulos normales con una línea ya elegida).
- `Scripts/Characters/Contador/CentavoInterrogation.cs`: getter `Data`.
- `Scripts/Interrogation/InterrogationData.cs`: `finishedOffSituation`, `finishedOffTimeoutSeconds`, `finishedOffThoughts`.
- `Scripts/Editor/ContadorInterrogationDataBuilder.cs`: situación y 3 líneas de respaldo.
- `Docs/guiones/contador.md`: §5 "Pensamiento si lo rematas sin escucharlo".

## Commits
- `7c84a7d`: dia4(david): 3C.5.1 arreglos del revólver (sprites del viewmodel, caída delante del banco) y pensamiento al rematar a Centavo
- Commit siguiente: esta auditoría.
