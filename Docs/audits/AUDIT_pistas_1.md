# AUDITORÍA PISTAS.1 — Pistas legibles + textos finales

Rama `pistas` (desde `origin/integracion/dia4` @ `e33f196`). Responsable: Edgar (Claude Code). Fecha: 3 nov 2026.
Nada se probó en Unity: el código compila (ver abajo) y todo lo demás queda en la lista de pruebas para David.

## Menús a ejecutar (en orden)
1. **Plomo y Paja → Contador → Crear UI de interrogatorio** (solo si `Art/UI/ui_papel_dialogo.png` no está importado como Sprite; deja el papel en 9-slice y la fuente Special Elite).
2. **Plomo y Paja → Pistas → Crear hoja de pista** → crea `Resources/Clues/ClueCard.prefab` y `Resources/Clues/ClueArt.asset`. Commitear los dos con su `.meta`.
3. Abrir `NivelPorky` → **Plomo y Paja → Pistas → Validar pistas** (no cambia la escena).

No hace falta regenerar ninguna escena.

---

- **Lista de pistas (id, dónde está, quién la coloca, cómo se obtiene):**
  | id | Dónde | Quién la coloca | Cómo se obtiene |
  |---|---|---|---|
  | `nota_porky_01` | N1, casa de Porky: marcador `Clue_nota_porky_01` (2.5, 1.1, 0.8) | `LevelBuilder_NivelPorky` pone **solo el marcador**; ahora `ClueRuntime` crea `Pista_Runtime_nota_porky_01` al cargar la escena | Se examina → hoja |
  | `contrato_01` | N1, marcador `Clue_contrato_01` (-6.5, 0.3, -2.5) | ídem | Se examina → hoja |
  | `registro_01` | N1, Centavo | `CentavoInterrogation.GiveClue` → `CluePickup.Award(…, aiComment: false)` | Interrogatorio o al perdonarlo → solo aviso |
  | `testimonio_puerta_01` | N1, Colmillo | `DialogueNPC` (nodo final de `DialogueDatabase`) → `GameStateTracker.RegisterClueCollected` | Interrogatorio → solo aviso (nuevo, por el evento `OnInterrogationCompleted`) |
  | `testimonio_socios_01` | Tuerto (`DialogueDatabase`) | ídem | ídem (no está en el guion: mismo trato que Colmillo) |
  | `foto_reunion_01` | N2, barracón, sobre la mesa | `LevelBuilder_Campamento.PlaceClueAnchors` → `Pista_Auto_foto_reunion_01` en `Anchor_Clue_foto_reunion_01` | Se examina → hoja con imagen (si existe) |
  | `carta_lobo_01` | N2, oficina, escritorio | ídem → `Pista_Auto_carta_lobo_01` | Se examina → hoja |

  **Hallazgo:** antes de esta fase **no había ningún `CluePickup` en ninguna escena ni prefab**. En el Nivel 1 las dos pistas físicas eran solo marcadores con gizmo: no se podían encontrar. La escena `Campamento` todavía no existe en `Assets/Scenes`; su builder ya coloca las dos del Nivel 2.

- **Llamadas a UIManager que había y por qué se reemplazaron:** 4, todas en `CluePickup.cs`:
  `ShowInteractPrompt` y `HideInteractPrompt` (aviso), `HideInteractPrompt` al recoger, y `ShowClueUI` en `Award` (texto).
  `UIManager` no está en ninguna escena, así que el jugador no veía nada.
  Reemplazos: `RubberHoseCrosshair.SetPrompt` (aviso), `ClueCard` (hoja) y `HudWilbur.ShowToast` (aviso "Pista: …").
  `CluePickup` ya no menciona `UIManager`.

- **Aviso [E]: ¿con línea de vista? ¿ya no se toman a través de paredes?**
  Sí. "[E] Examinar" aparece a ≤ 2.2 m en horizontal y ≤ 2.2 m de alto, con las mismas reglas que `Pickup`.
  La línea de vista va de la cámara a la pista + 0.12 m, sin triggers; la propia pista, los guardias y el jugador no tapan.
  Nunca aparece en pausa, diálogo, interrogatorio, cinemática ni con la hoja abierta.
  Antes era una esfera de 3 m sin línea de vista, a través de paredes. Con dos pistas al alcance manda la más cercana.

- **Hoja: ¿pausa y restaura todo? ¿Esc no abre la pausa?**
  - **Al abrirse:** `Time.timeScale = 0` (guarda el valor previo; si era 0 por el hitstop, restaura 1), `PlayerController.SetCanMove(false)` (sin mirar ni caminar), `CinematicState.Set(hoja, true)` y `UiHideReasons.Set(Cinematic, true)`.
  - **Qué bloquean esos dos estados:** `PlayerAim.GameplayBlocked()` (sin disparo, mira ni manos), `GameManager` (no pausa con Esc), los guardias y el daño. El HUD y las manos se ocultan.
  - **Al cerrarse:** E, clic o Esc tras 0.5 s en tiempo real. `timeScale` y el movimiento se restauran al instante, pero `CinematicState` y la razón de ocultado se sueltan **un cuadro después**. Así la misma pulsación de Esc no abre la pausa y la misma E o clic no dispara ni toma otro objeto.
  - **Si la escena cambia con la hoja abierta,** `OnDestroy` restaura todo.
  - **Por qué esos estados:** usar `CinematicState` y `Reason.Cinematic` evita tocar `PlayerAim`, `GameManager` y `UiHideReasons`, que no son míos. La solución limpia está en "Cambios que hacen falta".

- **Aviso "Pista: …" y pensamiento de la IA: ¿salen en orden?**
  Sí.
  - **Pista física:** E → sonido `pista_papel` + cuenta en el tracker → hoja → al cerrar, aviso "Pista: <título>" → a los 2 s (con el juego ya en marcha) `AICompanion.Speak(ClueFound, "título: resumen")`.
  - **`Award` (Centavo):** sonido, cuenta y aviso, sin hoja y sin IA (D11 se mantiene).
  - **Colmillo y Tuerto:** solo el aviso; el pensamiento de fin de interrogatorio sigue saliendo de donde ya salía.

- **Textos: ¿iguales al guion? ¿testimonio_puerta_01 intacto?**
  - **Copia exacta del guion §2** en `Scripts/Clues/ClueCatalog.cs`, el único lugar de los textos: títulos, cuerpos con sus saltos de línea, «», —P., —L. y "…".
  - **`registro_01`** guarda su texto aunque hoy no abre hoja (queda para un diario de pistas).
  - **`testimonio_puerta_01` está intacto:** `DialogueDatabase` no se tocó. En el catálogo solo tiene título, para el aviso.
  - **Textos viejos del builder:** `LevelBuilder_Campamento` sigue escribiendo los textos viejos en los campos de `Pista_Auto_*` ("Foto de reunión", «Socios. Mayo.», "Monsieur Wilbur…"), pero en el juego manda el catálogo. Esos campos solo valen para un id que no esté en el catálogo.

- **Cambios que hacen falta en archivos que no son míos (si hay):**
  1. **`UiHideReasons.cs` + `PlayerAim.cs` (David), recomendado:** agregar `Reason.Clue` y que `GameplayBlocked()` devuelva `true` si `ClueCard.IsOpen`; y en `GameManager.Update` (David) agregar `&& !ClueCard.IsOpen` a la condición de Esc. Con eso `ClueCard` deja de usar `CinematicState` y `Reason.Cinematic`; el cambio en `ClueCard` son dos líneas.
  2. **`LevelBuilder_NivelPorky.cs` (Cristian/David), opcional:** poner un `CluePickup` en `Clue_nota_porky_01` y `Clue_contrato_01` (hoy lo hace `ClueRuntime` al jugar). También hay que revisar las posiciones: la nota está a 1.1 m ("cajón") y el pagaré a 0.3 m ("debajo del sofá"). Si el sofá tapa la línea de vista desde los ojos, el aviso no saldrá: mover el marcador un poco hacia fuera.
  3. **`LevelBuilder_Campamento.cs` (David), opcional:** dejar de escribir los textos viejos en `PlaceClueAnchor` (el catálogo manda). No es urgente.
  4. **`Pickup.cs` (David), menor:** la regla "una tecla, un objeto" es privada de `Pickup`. Si un objeto y una pista están a la vez al alcance, una sola E usa los dos. Se resuelve con un foco compartido.
  5. **`CharacterSpriteImporter.cs`:** el caso `pistas` es de PISTAS.2 y no se agregó.

- **Qué no se pudo compilar/probar y qué debe revisar David en Unity:**
  - **Compilación:** Roslyn de Unity 2022.3.62f3: `Assembly-CSharp` 117 fuentes, 0 errores (como build y como Editor); `Assembly-CSharp-Editor` 42 fuentes, 0 errores. Nada se probó en Unity.
  - **Revisar en Unity:**
    1. En NivelPorky aparecen `Pista_Runtime_nota_porky_01` y `Pista_Runtime_contrato_01` (hoja plana con contorno y brillo, sin vaivén) apoyadas en su mueble o el piso.
    2. "[E] Examinar" sale solo viéndolas y a ≤ 2.2 m; detrás de una pared, no.
    3. E abre la hoja: el juego se congela, no se mira, no hay HUD ni manos, los guardias no actúan. Antes de 0.5 s no se cierra.
    4. Cerrar con Esc **no** abre la pausa; cerrar con clic **no** dispara.
    5. Aviso "Pista: Nota de Porky" y, unos 2 s después, el pensamiento de la IA.
    6. La pista desaparece; tras morir y reiniciar, una pista recogida antes del inicio del nivel no vuelve (lo decide el tracker).
    7. Centavo: "Pista: Hoja del libro de cuentas"; Colmillo: "Pista: Testimonio de Colmillo".
    8. Textos largos (la carta): el papel crece y no se sale de la pantalla en 16:9.
    9. Sin el prefab (antes de correr el menú) la hoja se arma sola con la fuente de TMP.

- **Archivos creados/modificados:**
  - **Nuevos:**
    - `Scripts/Clues/ClueCatalog.cs`: textos del guion.
    - `Scripts/Clues/ClueArt.cs`: dibujos por id, en `Resources`.
    - `Scripts/Clues/ClueCard.cs`: la hoja; su `Build()` lo usan el menú y el respaldo en runtime.
    - `Scripts/Clues/ClueRuntime.cs`: pistas en los marcadores y aviso de las de `DialogueNPC`.
    - `Scripts/Editor/ClueMenus.cs`: los dos menús.
    - Todos con su `.meta` y GUID fijo.
  - **Modificado:** `Scripts/Clues/CluePickup.cs`.
    - Ya no usa `UIManager`; implementa P1, P2, P3, P5 y P6.
    - Los campos `clueId`, `displayName`, `shortDescription` y `fullText` no cambian de nombre, porque los escribe `LevelBuilder_Campamento`.
    - La firma de `Award` no cambia.
    - Se quitaron los campos sin uso `interactDistance`, `interactPrompt` y `displayDuration`; ahora son constantes del P1.
  - **Lo crea el menú (no está en el commit):** `Resources/Clues/ClueCard.prefab`, `Resources/Clues/ClueArt.asset`.

- **Commits (rama pistas):**
  - `ef22112` - dia5(edgar): PISTAS.1 pistas legibles (código y menús).
  - (este archivo) - dia5(edgar): PISTAS.1 auditoría.

## Clases existentes reutilizadas
- `RubberHoseCrosshair.SetPrompt` (aviso).
- `HudWilbur.ShowToast` (aviso de pista).
- `UiHideReasons` + `CinematicState` (ocultar y bloquear).
- `PlayerController.SetCanMove`.
- `Pickup.TryFindSurface` / `Pickup.IsActor` (apoyo y línea de vista, las mismas reglas que los objetos).
- `CharacterLook.ApplyPlaceholder` / `ApplySprite` (look plano).
- `InterrogationUIBuilder.EnsureFont` + `ui_papel_dialogo.png` (papel y Special Elite).
- `LevelMarker` (marcadores de pista).
- `DialogueNPC.OnInterrogationCompleted` (solo se escucha).
- `GameStateTracker.HasClue` / `RegisterClueCollected`.

## Decisiones tomadas
1. **Pistas del Nivel 1 en runtime:** `ClueRuntime` crea un `CluePickup` en cada marcador de pista que no tenga uno. Sin esto no se podían examinar, y no podía tocar los builders.
2. **Textos en código:** `ClueCatalog` es estático, como `DialogueDatabase`, así siempre están aunque falte un asset. Los dibujos van en un asset aparte, opcional (`ClueArt`).
3. **Bloqueo con estados existentes:** la hoja usa `CinematicState` y `Reason.Cinematic` hasta que David agregue el cambio 1.
4. **Títulos de los testimonios:** "Testimonio de Colmillo" y "Testimonio de Tuerto", sacados del inicio de su texto actual. El guion dice "(el que ya tiene)", pero hoy no tenían título.
5. **La pista desaparece al examinarla:** antes quedaba visible con el collider apagado. Es coherente con P6.
6. **Resumen para la IA:** cada pista tiene uno nuevo en el catálogo; no se ve en pantalla, solo va al prompt de la IA.

## Preguntas para el orquestador
- ¿Se aprueba el cambio 1 (`Reason.Clue` + `ClueCard.IsOpen` en `GameplayBlocked` y en el Esc de `GameManager`) para dejar de usar `CinematicState` en la hoja?
- `testimonio_socios_01` (Tuerto) no está en el guion: ¿se queda con el aviso "Pista: Testimonio de Tuerto"?
