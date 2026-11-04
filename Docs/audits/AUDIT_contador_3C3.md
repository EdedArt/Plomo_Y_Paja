# AUDIT contador 3C.3: escena de interrogatorio (presentación estilo Mouse P.I.)

Fase: `Docs/phases/Fase-3C-Contador.md`, subfase 3C.3. Leídos: `CLAUDE.md`, la fase (secciones 0 y 1), el guion `Docs/guiones/contador.md` v1.2 (§2 y §5) y las auditorías `AUDIT_contador_assets.md`, `AUDIT_contador_3C1.md` y `AUDIT_contador_3C2.md`. Graphify (`graphify update` + `explain`) para `DialogueNPC`, `PlayerAim`, `FirstPersonViewmodel` y `GameManager`. Además leí `PlayerController`, `RubberHoseCrosshair`, `ShotgunFeedback`, `ViewmodelCameraRig`, `CartoonFx` (hit-stop) y `NoirVolumeBuilder`.
Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código compila (ver más abajo), pero **nada se probó en Play** y el menú nuevo **no se ejecutó**: el prefab `InterrogationCanvas` y la fuente Special Elite todavía no existen. Las respuestas "sí/no" de abajo dicen cómo está programado; la prueba real la hace David con la lista del final.

## Antes de programar (0a / 0b)
- **0a: commit solo de documentos.** `2ad9624` docs: guiones (contador, intro) y fases 3C-Contador e Intro. Lleva `Docs/guiones/` (`contador.md`, `Guion-Intro.md`), `Docs/phases/Fase-3C-Contador.md` y `Docs/phases/Fase-Intro.md`.
- **Carpeta de originales: correcta.** Ahora se llama `PLOMO_Y_PAJAV_1/_entrante_personajes/`, sin guion bajo al final, y git la ignora (`git check-ignore` → `.gitignore:86`). Ya no aparece en `git status`.
- **0b: lo que generó el menú de la 3C.2.** Revisé antes de hacer el commit que estuviera bien guardado:
  - `NivelPorky.unity` y `Centavo.prefab` tienen la misma hora (2 nov, 11:18);
  - la escena tiene la instancia de Centavo (17 referencias al GUID del prefab, nombre `Centavo`);
  - los 22 `.meta` de los sprites de `contador` pasaron a `spritePixelsToUnits: 500`.

  Commit `f8a52bf` dia4(david): 3C.2 Centavo colocado en NivelPorky (prefab, escena, sprites a PPU 500).
- **Assets que llegaron sin commit y entraron con esta subfase** (los usa la 3C.3):
  - `Assets/TextMesh Pro/Fonts/SpecialElite-Regular.ttf` y su `LICENSE.txt`. Les escribí el `.meta` con un GUID fijo, para que el asset de fuente apunte al mismo TTF en todas las máquinas.
  - `Art/UI/ui_papel_dialogo.png` (+ `.meta` y `Art/UI.meta`).

## Cómo se ocultan manos y mira (mecanismo reutilizado)
- **El mismo que el diálogo de los guardias:** `PlayerAim.GameplayBlocked()`. Lo leen la mira (`RubberHoseCrosshair`, apaga su canvas), las manos (`FirstPersonViewmodel`, apaga su canvas) y el apuntado (`PlayerAim`, suelta el clic derecho). Solo le agregué una condición: `|| InterrogationDirector.IsActive`. No hay un segundo sistema para ocultar.
- **Bloqueo del jugador:** igual que `DialogueNPC`:
  - se apagan `PlayerController` (no camina ni gira) y `ShotgunController` (no dispara);
  - se guarda el estado del cursor y se suelta;
  - todo se devuelve en el `LateUpdate` siguiente, para que el clic de la decisión no dispare la escopeta.
- **D7 (FOV):** `PlayerAim` suelta el apuntado solo, porque `GameplayBlocked` da true, y vuelve el FOV al base en tiempo real. **El director nunca escribe `fieldOfView`.** La cámara solo cambia de posición y rotación.

## Entrada y salida: ¿se restaura todo? (timeScale, cursor, input, FOV intacto, manos, mira)
**Sí, por código** (no probado en Play). `InterrogationDirector` (`Scripts/Interrogation/`):
- **Enter(target, flow, emoción inicial):**
  1. Bloquea al jugador y suelta el cursor.
  2. Lleva la Main Camera a `Anchor_InterrogationCam` (de `ContadorShed`, vía `IInterrogationTarget.CameraAnchor`) en 0.6 s con ease in-out.
  3. Guarda `timeScale` y lo pone en 0.
  4. Desenfoca 0 → 1 en 0.35 s.
  5. Oculta el billboard (`BillboardSprite.SetVisible(false)`, D6) y muestra el retrato (aparece en 0.25 s).
  6. Corre el contenido.
- **Exit(outcome):** lo inverso, en este orden:
  1. desaparece la UI (0.25 s);
  2. vuelve el billboard, ya en su estado final (`death_01` o cojeando);
  3. el desenfoque baja a 0 y la cámara vuelve a su pose local guardada (0.4 s), a la vez;
  4. se restaura `timeScale`;
  5. en el `LateUpdate` se devuelven `PlayerController`, la escopeta y el cursor;
  6. al dejar de estar activo el director, `GameplayBlocked` vuelve a false y reaparecen manos y mira.
- **Todo en tiempo real** (`Time.unscaledTime`): cámara, UI, máquina de escribir, lip-sync, retrato y puntos suspensivos.
- **Seguro:** si el director se apaga o se destruye a mitad, `OnDisable` deja todo como estaba (tiempo, cámara, jugador, billboard y desenfoque).
- **ShotgunFeedback** sigue moviendo la cámara solo durante el render y la restaura después, así que no choca con el movimiento del director.

## ¿Funciona si entra durante un hit-stop de CartoonFx? (sí/no)
**Sí (por código).**
- El hit-stop pone `timeScale` en 0 y al terminar lo vuelve a 1, lo que rompería la escena. En `CartoonFx` se agregaron:
  - **`IsHitStopActive`:** si el interrogatorio entra durante un hit-stop, guarda 1 (y no 0) como la escala a restaurar;
  - **`TimeHeld`:** el director lo pone en true. Mientras dura, no empiezan hit-stops nuevos y el que esté en curso no devuelve la escala a 1 al terminar. En `Exit`, el director lo pone en false y restaura el valor guardado.
- Para los guardias nada cambia: `TimeHeld` es false fuera de la escena.

## Desenfoque: ¿aplica solo al mundo? (sí/no)
**Sí (por diseño; falta verlo).**
- Es un Volume global propio, creado por código (perfil en memoria, no hay asset), prioridad 100, capa Default. Su peso va de 0 a 1 en 0.35 s. Lleva:
  - Depth of Field **Gaussian** (inicio 0, fin 1 m, radio 1.5, alta calidad);
  - **viñeta +0.15** y **postExposure −0.6**, sumados a lo que ya da el volume global.

  Para sumar en vez de pisar: al entrar lee el valor actual del `VolumeManager` y pone base + 0.15 / base − 0.6. El color de la viñeta no se toca.
- El post-procesado se aplica al stack de cámaras (mundo + manos ocultas). El `InterrogationCanvas` es Screen Space Overlay, y URP lo dibuja **después** del post-procesado. Por eso el retrato, el papel y los botones quedan nítidos.
- **No se cambió el post-procesado global.**
- **API dudosa:** con camera stacking (`ViewmodelCamera` Overlay al final), el DoF puede leer la profundidad de la cámara Overlay. Si pasa, todo el mundo queda igual de borroso, que igual es lo que se busca. Revisar que se vea desenfocado y no negro.

## Lip-sync en el retrato: ¿se ve natural? umbral usado
**No probado.** Cómo está armado:
- La voz del interrogado sale por la AudioSource 2D `Voz_Interrogado` (con `ignoreListenerPause`, D5), con el mismo `LipSyncDriver` de la 3C.2.
- **Umbral:** `max(0.01, 0.35 × pico RMS del clip)`. Para cerrar baja a × 0.7 (histéresis), y cada cambio dura al menos 0.06 s.
- `LipSyncImageAdapter` (3C.2) alterna el cuadro 0 (`_01`, cerrada) y el cuadro 1 (`_02`, abierta) de la emoción actual del set `contador`.
- **Wilbur usa otra fuente** (`Voz_Wilbur`), así que no mueve la boca de Centavo.
- **Parpadeo:** `blink_01` cada 2.5–5 s durante 0.12 s, solo en `talk` y con la boca cerrada. Va con `SetOverride`, una función nueva del adaptador.
- **Retrato vivo** (`InterrogationPortrait`):
  - entrada con escala 0.92 → 1 y fundido de 0.25 s;
  - respira estirando y aplastando ±1.5 % en Y cada 1.6 s, con el pivote abajo;
  - en `scared` y `nervous` tiembla a saltos ±2 px a 12 Hz.
- **Tamaño:** abajo al centro, 78 % del alto de la pantalla, cuadrado (`AspectRatioFitter`), con `preserveAspect`.

## Fuente: ¿Special Elite? ¿se ven ñ, ¿, ¡ y tildes?
- **Special Elite: sí, cuando David corra el menú.**
  - El TTF está en `Assets/TextMesh Pro/Fonts/` y no en `Fonts/` como dice la fase. El menú busca en los dos lugares.
  - Crea `Assets/_Game/Fonts/SpecialElite SDF.asset`: TMP **dinámico**, SDFAA, muestreo 56, atlas de 1024. La textura y el material quedan dentro del mismo `.asset`.
  - Le precarga ASCII + `¡ ¿ … — « » á é í ó ú ü ñ` y sus mayúsculas. Si falta alguno, el cuadro del menú lo lista.
- **¿Se ven ñ ¿ ¡ y tildes?** Por código sí (la fuente los trae y quedan cargados). No lo vi en pantalla.
- **Sin el TTF:** se usa la fuente por defecto de TMP y el menú lo avisa.

## Menú de pausa durante la escena: comportamiento elegido
**Desactivado.**
- `GameManager.Update` ya no hace `TogglePause` con Escape mientras `InterrogationDirector.IsActive`.
- Por qué: la pausa pone `timeScale` en 1 al quitarla, cosa que rompería D5, y además no existe menú de pausa visible (`UIManager` no está en ninguna escena).
- Escape no hace nada durante la escena. Al salir, la pausa funciona normal.

## Lo que muestra la demo (solo Ronda 0)
- **Arranque:** `CentavoInterrogation` (nuevo, en el prefab de Centavo) escucha `CentavoController.OnInterrogationRequested`. Ese evento sale cuando Centavo está herido y Wilbur llega a ≤ 3 m con vista. Entonces el componente llama `BeginInterrogation()` e instancia el canvas, y `Enter` arranca con la emoción `scared`.
- **Contenido** (textos exactos del guion §5, clips reales):
  1. `contador_int_00` (`scared`).
  2. Opciones 1/2/3 (0A/0B/0C).
  3. `wilbur_int_0X`, con el nombre WILBUR en el papel.
  4. `contador_int_0X` (`scared` / `nervous` / `talk`).
  5. Banda de pensamiento: "…" animado 1.2 s y después una de las 4 líneas de respaldo del guion (sin la que es "solo si mintió").
  6. Botones "Jalar el gatillo" / "Dejarlo ir".
  7. `ResolveKilled()` o `ResolvePardoned()` (3C.2) y `Exit`.
- **Lo que la demo NO hace** (es la 3C.4):
  - no lleva la confianza;
  - no tiene la Ronda 1, ni las líneas de decisión (`wilbur_dec_*`, muerte, perdón);
  - no da `registro_01`;
  - no llama a la IA real;
  - no cuenta el interrogatorio.

  Sí registra `contador = Dead/Pardoned` (eso lo hacía ya `ResolveKilled/ResolvePardoned`), y matar no dispara la línea de la IA (D11).
- **Texto y teclas:**
  - la máquina de escribir dura 0.9 × el largo del clip;
  - **clic o Espacio** completa el texto, y un **segundo clic** corta el clip;
  - al terminar cada línea hay una pausa de 0.45 s, que también salta con un clic;
  - opciones con las teclas 1/2/3 o clic, con resaltado al pasar el mouse; la decisión, con 1/2 o clic.
- **Papel:** `ui_papel_dialogo.png` (2003 × 785, sin alpha). El menú lo importa como Sprite. El nombre (CENTAVO / WILBUR) va arriba, el texto en el medio y las opciones o la decisión abajo. Si falta el PNG: panel crema con borde oscuro.
- **Canvas:** propio (D9), Screen Space Overlay, `sortingOrder` 300 (por encima de la mira = 5 y del diálogo de los guardias = 100). No usa `UIManager` ni `DialogueUI`, salvo `DialogueUI.EnsureEventSystem()`, que ahora es público, para que los botones respondan.
- **Audio:** no hay AudioMixer, así que el ambiente sigue sonando igual. Las voces de la escena suenan con el juego congelado.

## Clases existentes reutilizadas (y por qué)
- **`PlayerAim.GameplayBlocked`:** esconde manos y mira y suelta el apuntado (D7). Es el mecanismo del diálogo.
- **El patrón `LockPlayer`/`RestorePlayer` de `DialogueNPC`:** jugador, escopeta, cursor y restauración en `LateUpdate`.
- **`DialogueUI.EnsureEventSystem`** (pasó a público).
- **`CartoonFx`:** se le agregaron `IsHitStopActive` y `TimeHeld` para convivir con el hit-stop.
- **`GameManager`:** la pausa se desactiva durante la escena.
- **De la 3C.2:**
  - `CentavoController`: `OnInterrogationRequested`, `BeginInterrogation`, `ResolveKilled`, `ResolvePardoned` y `SetWorldVisible` (que usa `BillboardSprite.SetVisible`, D6). Además ahora implementa `IInterrogationTarget` (nombre, ancla de cámara, set del retrato).
  - `LipSyncDriver` + `LipSyncImageAdapter` (al adaptador se le agregaron `SetOverride` e `IsOpen`).
  - El set `contador` (cuadro 0 = boca cerrada, 1 = abierta); se agregó el getter `BillboardSprite.SpriteSet`.
- **`ContadorShed.InterrogationCamAnchor`** (3C.1): adonde va la cámara.
- **`AudioLibrary.GetVoice` / `VoiceVolume`** (3C.2): clips de la escena.
- **`ContadorCentavoBuilder`:** el prefab de Centavo ahora incluye `CentavoInterrogation`.
- **Nuevas** (en `Scripts/Interrogation/`, reutilizables por los guardias):
  - `InterrogationDirector`, `InterrogationCanvas`, `InterrogationPortrait`;
  - `IInterrogationTarget` + `InterrogationOutcome`.

  Lo propio de Centavo va en `Scripts/Characters/Contador/CentavoInterrogation.cs`. El nombre `ContadorInterrogation` ya lo usa el enum de `GameStateTracker` (3C.2), por eso el componente se llama `CentavoInterrogation`.

## Menús que David debe correr
1. Abrir Unity, dejar que compile e importe `SpecialElite-Regular.ttf`. La consola debe quedar sin errores rojos.
2. **Plomo y Paja → Contador → Crear UI de interrogatorio.** Hace cuatro cosas:
   - crea la fuente `Assets/_Game/Fonts/SpecialElite SDF.asset`;
   - importa el papel como Sprite;
   - crea `Assets/_Game/Prefabs/UI/InterrogationCanvas.prefab`;
   - le asigna el canvas a `Centavo.prefab`.

   Todas las líneas del cuadro deben salir ✓.
3. **No hace falta tocar la escena:** el canvas se instancia solo la primera vez que se interroga. Si alguna vez se vuelve a correr *Colocar Centavo*, el prefab de Centavo vuelve a enlazar el canvas solo.
4. Commit de lo generado:
   - `Assets/_Game/Fonts/` (con su `.meta`);
   - `Assets/_Game/Prefabs/UI/` (con su `.meta`);
   - `Prefabs/Level1/Centavo.prefab`;
   - `Art/UI/ui_papel_dialogo.png.meta`, que cambia a Sprite.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra `Library/ScriptAssemblies` y `Managed/UnityEngine`, runtime y editor con **0 errores**. Los únicos avisos nuevos son CS0649 en los `[SerializeField]` que asigna el menú.
- **No probado:** nada en Play, ni el menú, ni la fuente creada.
- **APIs dudosas:**
  - `TMP_FontAsset.CreateFontAsset(...)` + `AddObjectToAsset` del atlas y del material: es la forma de guardar un dinámico. Si la fuente sale en blanco al reabrir Unity, borrar `SpecialElite SDF.asset` y correr el menú otra vez.
  - `VolumeManager.instance.stack` para leer la viñeta y la exposición actuales: si devuelve los valores por defecto, la suma da 0.15 y −0.6 absolutos, que se notaría poco.
  - El DoF con camera stacking (ver "Desenfoque").
- **Revisar en Play** (con Centavo herido, después del tiroteo de la 3C.2):
  1. Acercarse a ≤ 3 m mirándolo. La cámara baja hacia él en ~0.6 s, el mundo se congela (la lluvia queda quieta) y se desenfoca y oscurece. Centavo desaparece del mundo y aparece el retrato grande `scared`, que tiembla y respira.
  2. El papel con CENTAVO y el texto "¡No me remate, señor!…" se escribe al ritmo de la voz y la boca se mueve con la voz. Clic: completa el texto. Otro clic: corta el clip.
  3. Opciones 1/2/3 (teclado y mouse, se resaltan con el mouse). Habla WILBUR (la boca de Centavo no se mueve) y luego responde Centavo con su emoción.
  4. Arriba aparecen "…" y después una frase en cursiva. Luego, "Jalar el gatillo" / "Dejarlo ir".
  5. Al elegir:
     - el retrato se va, el mundo se aclara y la cámara vuelve a la cabeza en ~0.4 s;
     - **FOV igual que antes**, manos y mira de vuelta, cursor bloqueado, se puede caminar y disparar;
     - el clic de la decisión **no** dispara la escopeta.
     - Matar: Centavo queda con `death_01`. Perdonar: se va cojeando por el portón.
  6. Escape durante la escena: no pasa nada. Después de la escena, la pausa funciona.
  7. Letras: que se vean ñ, ¿, ¡ y las tildes con Special Elite (ej.: "¿Qué hace el contador…", "Señor").
  8. Interrogatorio durante un hit-stop: difícil de forzar. Si al salir de la escena el juego queda congelado, avisar.
  9. Ajustes posibles en el prefab `InterrogationCanvas`: posición y alto del papel (`Papel`: anclas 0.21–0.79), tamaños de letra, `linePause` y `typewriterFraction` del director.

## Archivos creados/modificados
Creados (todos con `.meta`):
- `Scripts/Interrogation/InterrogationDirector.cs`: Enter/Exit, cámara, tiempo, desenfoque, `Say`/`Choose`/`Think`/`Decide`.
- `Scripts/Interrogation/InterrogationCanvas.cs`: vista (papel, opciones, decisión, pensamiento).
- `Scripts/Interrogation/InterrogationPortrait.cs`: retrato vivo y parpadeo.
- `Scripts/Interrogation/IInterrogationTarget.cs`: interfaz y `InterrogationOutcome`.
- `Scripts/Characters/Contador/CentavoInterrogation.cs`: enlace con Centavo y demo de la Ronda 0.
- `Scripts/Editor/InterrogationUIBuilder.cs`: menú *Crear UI de interrogatorio*.
- `Assets/TextMesh Pro/Fonts/SpecialElite-Regular.ttf` y `LICENSE.txt` (llegaron sin commit; `.meta` escritos aquí).
- `Art/UI/ui_papel_dialogo.png` (+ `.meta`, `Art/UI.meta`).
- `Docs/audits/AUDIT_contador_3C3.md` (este archivo).

Modificados:
- `Scripts/Player/PlayerAim.cs`: `GameplayBlocked` también durante el interrogatorio.
- `Scripts/Core/GameManager.cs`: no pausa con Escape durante el interrogatorio.
- `Scripts/Visual/CartoonFx.cs`: `IsHitStopActive` y `TimeHeld`.
- `Scripts/Dialogue/DialogueUI.cs`: `EnsureEventSystem` público.
- `Scripts/Enemies/BillboardSprite.cs`: getter `SpriteSet`.
- `Scripts/Interrogation/LipSyncImageAdapter.cs`: `SetOverride` e `IsOpen`.
- `Scripts/Characters/Contador/CentavoController.cs`: implementa `IInterrogationTarget`.
- `Scripts/Editor/ContadorCentavoBuilder.cs`: agrega `CentavoInterrogation` al prefab y le enlaza el canvas.

## Commits
- `2ad9624`: docs: guiones (contador, intro) y fases 3C-Contador e Intro (paso 0a)
- `f8a52bf`: dia4(david): 3C.2 Centavo colocado en NivelPorky (prefab, escena, sprites a PPU 500) (paso 0b)
- `1ee5e78`: dia4(david): 3C.3 escena de interrogatorio (director, canvas, retrato, desenfoque, demo ronda 0)
- Commit siguiente: esta auditoría.
