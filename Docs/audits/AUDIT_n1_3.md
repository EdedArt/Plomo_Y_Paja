# AUDIT N1.3 — Tutorial: controles y "qué son los pensamientos"

**Fecha:** 3 nov 2026 · **Rama:** `worktree-n1-3`, salida de `37b15d7` (**todavía no está en `integracion/dia4`**) · **Sesión A** · **Alcance:** N1.3 + el arreglo del taxi pedido en la revisión de N1.1. No se empezó N1.4.

> **Nada de esto se probó en Unity.** Se compiló con Roslyn y el encuadre del taxi se calculó con un script. El tutorial todavía no existe en la escena: lo crea el menú.

## Dónde se engancha cada paso (evento usado) y qué pasa si falta INTRO.2 / INTRO.3

| Paso | Evento | Si falta o no aplica |
|---|---|---|
| **T1** Tarjeta | `IntroCinematicDirector.RevealThoughtEnded` (**nuevo**: no existía). La cinemática espera con `SetHoldAfterReveal` hasta que se cierra | Sin cinemática, o si se saltó: 1.5 s después de recibir el control |
| **T2** Moverse | Control devuelto (`GameplayBlocked` en false), siempre después de T1 | Igual |
| **T3** Escopeta | `PlayerWeaponHolster.OnStateChanged` → `Ready` (cuando el arma ya responde, no al empezar a subir) | Sin holster, o con el arma ya lista: 3 s después de recibir el control |
| **T4** Revólver | `WeaponSwitcher.HasRevolver` pasa a true (leído cada frame; `WeaponPickup` no tiene evento y no lo cambié) | Sin `WeaponSwitcher`: no aparece |

- **Orden en la llegada:** pensamiento fijo → T1 → cartel "PLOMO Y PAJA" → control → T2.
- **Si se salta la cinemática** (mantener Espacio/Esc), `RevealThoughtEnded` no se dispara y T1 sale 1.5 s después. Mientras la tarjeta está abierta no se puede saltar.
- **T4 frente a 3C.5:** `WeaponPickup` solo muestra el nombre («Revólver de Centavo»), no los controles. No es equivalente, así que **quedan los dos**: el aviso y T4.
- **Sin AICompanion:** los pensamientos no salen (aviso en consola), pero las etiquetas sí. **Sin tarjeta en la escena:** T1 se salta con un aviso y no congela nada.

## Tarjeta "Pensamientos": ¿pausa y restaura todo? ¿mínimo 1 s antes de cerrar?
**Por código, sí a las dos. Sin probar en Play.**
- **Al abrir:** `timeScale` 0, cursor visible y libre, `PlayerController` apagado. Sin cinemática, además toma `CinematicState` (sin armas ni daño, guardias quietos, Esc no pausa). Con cinemática, ese estado ya es del director y no lo toca.
- **Hit-stop:** pone `CartoonFx.TimeHeld = true`, el mismo mecanismo del interrogatorio: un hit-stop en curso no le devuelve el tiempo a 1.
- **Al cerrar:** `timeScale` 1, `TimeHeld` false, cursor bloqueado y oculto, el jugador como estaba, suelta `CinematicState` y la espera de la cinemática. También lo hace si la escena se destruye con la tarjeta abierta.
- **Cierre:** clic, Espacio o Enter, solo después de 1.0 s en tiempo real. «Clic para continuar» aparece recién entonces.
- **El clic que cierra no dispara:** se atiende en `LateUpdate`, cuando las armas ya corrieron su `Update` con la entrada bloqueada.
- **Canvas 40:** encima del HUD (4) y debajo de cine (45), diálogo (100) e interrogatorio (300).

## Chips: ¿se marcan al hacer la acción? ¿teclas reales del proyecto = las del guion?
**Por código, sí:** cada etiqueta se tacha al hacer la acción (`<s>`, como dice el guion §1). Se van al completar las tres (0.7 s después) o a los 10 s (T2), 12 s (T3) y 10 s (T4; la fase no fijaba tiempo para T4).

**Teclas: todas coinciden con el guion; no hubo que cambiar ninguna.** Revisadas en `InputManager.asset` y en el código:

| Guion | Real |
|---|---|
| WASD | Ejes `Horizontal` / `Vertical` (también flechas) |
| Shift | `KeyCode.LeftShift` (**solo el izquierdo**) |
| E | `KeyCode.E` |
| Clic derecho | `GetMouseButton(1)` |
| Clic izquierdo | `Fire1`: `mouse 0` (también `left ctrl`) |
| R | `KeyCode.R` |
| 1 / 2 / Rueda | `Alpha1`, `Alpha2` (también teclado numérico), `mouseScrollDelta` |

**Dos matices:**
- **"Shift" se marca al correr de verdad:** Shift + moverse. Solo apretarlo no cuenta.
- **"E" se marca con apretarla**, aunque no haya nada que examinar.

**Canvas de las etiquetas: la fase pide "encima del HUD, debajo de la mira", pero HUD = 4 y mira = 5: no cabe un entero.** Las puse en 5, igual que la mira. Están abajo al centro y no se pisan con ella.

## ¿Se repite algo al morir y reiniciar? ¿Dónde vive la bandera?
**No, por código; sin probar.** Cada paso se marca **al empezar**, así que morir con el paso en pantalla tampoco lo repite.

**Dónde vive:** `TutorialDirector._shown`, estática, que dura toda la ejecución. Es el mismo mecanismo que `IntroCinematicDirector._playedThisSession`. **No usé `GameStateTracker`** porque es de Sesión B y aquí solo se lee.

**Efecto a tener en cuenta (L12):** volver al menú y apretar Jugar **no** vuelve a mostrar el tutorial hasta cerrar el juego. A la cinemática del taxi le pasa lo mismo hoy: `ResetState` no toca sus banderas. Si se quiere un "juego nuevo" completo, hay que limpiar las dos.

## ¿Aparece algo encima de un interrogatorio, cinemática o pausa?
**No, por código; sin probar.** Mientras `PlayerAim.GameplayBlocked()` (pausa, game over, interrogatorio, cinemática, diálogo) o `UiHideReasons.Death` estén activos, ningún paso empieza; el que está en pantalla se oculta y su tiempo no corre. Vuelve al terminar.

**Excepción a propósito:** T1 sale con la cinemática abierta, es lo que pide N8. Durante la tarjeta quedan bloqueados pausa y salto.

**Cola:** si hay otro paso esperando, el que está en pantalla termina a los 4 s. Así T3 no espera los 10 s de T2 si aparece un guardia.

## Etiqueta "pensamiento · IA": ¿se hizo?
**No (opcional, baja prioridad).** `AICompanion.OnLineSpoken` se dispara para **todas** las líneas: API, respaldo y fijas. Sin cambiar `AICompanion` (nadie lo edita) no se distinguen, y sin internet la etiqueta diría "IA" sobre líneas guardadas. **Lo que haría falta:** un evento o un dato de origen en `AICompanion`. Entonces se agrega en pocas líneas.

## Arreglo del taxi (revisión de N1.1)
**Elegí apagarlo antes de x -40.** La otra opción no cumple: al girar y salir al este, al final de la llegada la cámara mira a yaw 62° y el taxi entra en cuadro.

- **`Taxi_ExitPath`:** parada → x -20 → x -30 → **x -37**, en z -12.45. La trompa llega a x -39.4: el auto entero queda antes de x -40, a 4.6 m de la barricada (x -44).
- **Sin mover nada más:** ni la parada, ni `Anchor_PlayerCurb`, ni `Anchor_HouseLook`.
- **Tiempo:** `exitDriveSeconds` de 7 a **4.5 s**, porque el camino pasó de 45 a 22 m.
- **Sonido (efecto nuevo):** antes el taxi se apagaba más allá del alcance del motor. Ahora se apaga a 23 m, donde se oye, y se habría cortado de golpe. El motor baja a cero en los últimos 1.5 s (`exitEngineFadeSeconds`).
- **Fuera de cuadro (calculado, FOV vertical 60°, cuatro esquinas del taxi durante todo el recorrido):**

| Pantalla | Margen mínimo fuera del borde |
|---|---|
| 16:9 | 41.6° |
| 21:9 | 33.9° |
| 32:9 | 23.3° |

  El mínimo cae en t = 0, cuando la imagen todavía está en negro. El cálculo es sobre un modelo simple, no sobre la cámara real. El mismo modelo con el camino viejo da 33.9° en 21:9 (la auditoría de INTRO.2.1 midió ≥ 31°).
- **Saltar:** por código, `SkipToEnd` llama a `HideTaxi` y `Release` lo vuelve a llamar, así que no queda taxi al saltar en ningún momento; no hizo falta cambiarlo. **Sin probar.**
- **La salida sigue yendo al oeste**, hacia el territorio de Don Lobo, aunque ya no llega a la barricada ni se ve. Solo se oye alejarse. Si la línea del taxista sigue sonando rara, lo único que queda es no moverlo: que se apague en la parada durante el negro.

## Menús que David debe correr
Desde el checkout principal:
1. **Traer la rama:** `git merge worktree-n1-3` en `integracion/dia4`, o el método que uses.
2. **Abrir Unity** y esperar la importación. Sin `error CS`.
3. **Plomo y Paja → Generar Nivel Porky** (recomendado): rehace el taxi, el camino de salida y el tutorial.
   - Sin regenerar, los dos: **Plomo y Paja → Intro → Construir taxi y llegada** (camino nuevo) y **Plomo y Paja → Nivel 1 → Colocar tutorial**.
4. **Commit de lo generado** (por ruta explícita): `.meta` de los 2 scripts nuevos y de `Scripts/Tutorial/`, `Scenes/NivelPorky.unity`.

## Archivos compartidos tocados (y si estaban limpios en git status)
**De la tabla de propiedad: ninguno.** `GameStateTracker`, `AICompanion`, `InterrogationData` y los scripts de interrogatorio, `EditorBuildSettings`, `AudioImporter` y `CharacterSpriteImporter`: **no se tocaron**.
- `InterrogationUIBuilder`: solo llamo a `EnsureFont`. El papel lo cargo directo de `Art/UI/ui_papel_dialogo.png`, porque `EnsurePaperSprite` es privado y ese archivo es de Sesión B.
- `LevelBuilder_NivelPorky.cs` es de Sesión A.
- `IntroCinematicDirector.cs` e `IntroTaxiBuilder.cs` no están en la tabla. Cambios mínimos y aditivos, aparte del camino de salida.

**Estado en git:** esta rama sale de la punta de `integracion/dia4` (`37b15d7`) con el árbol limpio, y la punta seguía ahí al terminar el código. Sesión B trabaja en otro worktree; no pude ver qué tiene sin mezclar.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
**Compile check:** Roslyn de Unity 2022.3.62f3 sobre `worktree-n1-3`: `Assembly-CSharp` 112 fuentes, `Assembly-CSharp-Editor` 38 fuentes, **0 errores, 0 avisos** (sin CS0169, CS0649 ni CS0414). Una primera corrida apuntó por error al worktree de N1.1; la detecté por el conteo de fuentes y no cuenta.

**No probado:** nada en Play ni en Editor. **Revisar:**
1. **Secuencia completa desde `Intro.unity`:** taxi → llegada → pensamiento → tarjeta → cartel → T2. ¿Se siente bien la pausa? ¿El taxi ya no se ve ni se oye cortarse?
2. **Saltar en distintos momentos:** en el taxi, durante la llegada, y justo antes de la tarjeta. Debe quedar sin taxi, con la tarjeta 1.5 s después.
3. **Espacio para cerrar la tarjeta:** si se mantiene, cuenta para saltar el cartel; un toque corto puede dejar marcado el "toque" de `HoldToSkip` (no vi que tenga efecto en el taxi, pero no está probado).
4. **Papel de las etiquetas:** el 9-slice de un PNG grande a escala 1/5 puede verse mal. Sin el PNG, usa un panel crema con borde.
5. **Ancho de las etiquetas:** ¿tapan el HUD abajo a la izquierda?
6. **Fuente:** que Special Elite tenga todos los caracteres de la tarjeta (`ú`, `ó`, `í`).
7. **Morir y reiniciar** con T2 o T3 en pantalla: no debe repetirse.
8. **Pausa, interrogatorio y diálogo** con una etiqueta en pantalla: se oculta y vuelve con el tiempo que le quedaba.

## Archivos creados/modificados
**Creados:**
- `Scripts/Tutorial/TutorialDirector.cs`
- `Scripts/Editor/TutorialBuilder.cs`
- `Docs/audits/AUDIT_n1_3.md`

**Modificados:**
- `Scripts/Intro/IntroCinematicDirector.cs` — `RevealThoughtEnded`, `SetHoldAfterReveal`, salida de 4.5 s, el motor baja antes de apagarse
- `Scripts/Editor/IntroTaxiBuilder.cs` — `Taxi_ExitPath` termina en x -37
- `Scripts/Editor/LevelBuilder_NivelPorky.cs` — llama a `TutorialBuilder`

## Commits
En la rama `worktree-n1-3`:
- `f19ac80` — dia4(david): N1.3 tutorial y tarjeta Pensamientos; el taxi se va sin cruzar la barricada
- Esta auditoría: `dia4(david): N1.3 auditoria` (su propio hash no se puede escribir aquí)
