# AUDIT contador 3C.4: contenido, lógica, pensamiento de la IA y consecuencias

Fase: `Docs/phases/Fase-3C-Contador.md`, subfase 3C.4 (+ Parte A con los arreglos de la prueba de David de la 3C.3).
Leídos:
- `CLAUDE.md`, la fase (secciones 0 y 1) y el guion `Docs/guiones/contador.md` v1.2 (§5 y §6);
- las auditorías `AUDIT_contador_assets.md` y `AUDIT_contador_3C1/3C2/3C3.md`;
- Graphify y el código de `GameStateTracker`, `AICompanion`, `CluePickup`, `FinaleTrigger`/`GameManager` (finales), `AudioLibrary` y `Tools/character_key.py`.

Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código compila (ver más abajo), pero **nada se probó en Play** y **los menús no se ejecutaron**. Los datos `Interrogation_Contador.asset` todavía no existen, y el prefab `InterrogationCanvas` todavía tiene el diseño de la 3C.3: el diseño nuevo se aplica solo al arrancar (ver Parte A). Las respuestas de abajo describen cómo está programado; la prueba real la hace David con la lista del final.

## Antes de empezar
Estaba sin commit lo que generó el menú de la 3C.3: `InterrogationCanvas.prefab`, `Fonts/SpecialElite SDF.asset`, el `.meta` del papel como Sprite y `Centavo.prefab` con el canvas enlazado. Lo subí aparte: `2ecca7a`.

## Parte A: arreglos de la prueba de la 3C.3 (commit `6686e85`)

### A1: papel con alpha real
- **Herramienta:** `Tools/character_key.py` tiene un modo nuevo `--single ENTRADA --out SALIDA [--crop N]`: mismo recorte magenta y mismo despill de borde, sin escalar ni alinear. El modo de los personajes no cambió.
- **El fondo no era magenta puro:** era aprox. (245, 23, 241). El recorte usa `min(R, B) − G`, así que igual da 218 ≫ 90 y queda totalmente transparente.
- **Resultado:**
  - `Art/UI/ui_papel_dialogo.png` pasó de 2003 × 785 RGB a **1960 × 682 RGBA**, recortado a lo opaco con 4 px de margen;
  - 9.9 % del área es transparente (las esquinas rotas);
  - lo revisé sobre un fondo verde: sin borde magenta.
- **Original guardado** en `_entrante_personajes/UI/ui_papel_dialogo_magenta.png`. Está ignorado por git.
- **Se sobrescribió el mismo archivo**, así que el GUID del `.meta` no cambia.
- **Importación** (`InterrogationUIBuilder`): sigue siendo Sprite, ahora con `alphaIsTransparency` y bordes de **9-slice (150, 110, 150, 110 px)**, para que las orillas rotas no se estiren al dibujarse en una tira más ancha (≈ 4.8:1 frente a 2.9:1 del PNG).

### A2: diseño estilo Mouse P.I. (valores finales)
- **Todo es un campo de `InterrogationCanvas`.** Se aplica en `Awake` y, en Play, cada vez que se cambia en el Inspector (`OnValidate`), así que David lo puede ajustar en vivo.
- **El prefab que ya existe** toma el diseño nuevo al arrancar, sin volver a correr el menú. El menú lo deja guardado también en el prefab (`ApplyLayout()`).

| Qué | Valor |
|---|---|
| CanvasScaler | Scale With Screen Size, 1920 × 1080, **match 0.5** (en la 3C.3 era 1: corregido) |
| Papel | anclas x **0.12–0.88**, y **0.02–0.30**; 9-slice (`paperSliceScale` = 2); el AspectRatioFitter de la 3C.3 queda apagado |
| Margen interior del papel | **6 %** izq/der, **12 %** arriba/abajo |
| Retrato | **95 %** del alto, abajo al centro desde **y = 0.02** (llega a 0.97; con la respiración, 0.984); cuadrado; **detrás del papel** (primer hijo). Respiración y temblor de la 3C.3 sin cambios |
| Nombre | **44 px**, negrita, **#3B2414**, renglón de arriba del papel |
| Texto | **38 px** (baja hasta 26 si es muy largo), **#1E1209** |
| Opciones | **34 px**, cada una en su renglón con "1.", "2.", "3."; con el mouse encima, **más oscura (#0E0703) y subrayada** (`InterrogationOptionHover`, nuevo). Ya no tienen recuadro |
| Decisión | **44 px** |
| Pensamiento | **36 px cursiva**, crema **#F2E6C8** con **contorno oscuro** #140C08 (ancho 0.22), sin panel (alpha 0), anclas x 0.08–0.92, y 0.88–0.99 |
| Aviso de pista (nuevo) | 34 px, sobre el papel (y 0.31–0.38) |

- **Para que quepa en la tira:** al aparecer las opciones o la decisión, el texto de la línea se oculta, porque comparten el lugar debajo del nombre. Sobre las opciones el nombre pasa a ser **WILBUR**.
- **Ojo:** el pensamiento queda arriba, sobre la cabeza del retrato. Si tapa la cara, mover `thoughtAnchorMin/Max` o subir `thoughtPanelAlpha`.

### A3: audio durante la escena (qué encontré)
- **Centavo:**
  - En la 3C.2, `BeginInterrogation()` ya cortaba su voz y en el estado `Interrogation` no corre ningún temporizador de barks (`herido_01/02` solo en `Wounded`). **Esto estaba bien.**
  - **Sí había un caso:** `ResolvePardoned()` arrancaba la huida y decía `contador_huye_01` **antes** de cerrarse la escena, encima de ella.
  - **Arreglo:** `CentavoController.Say` ya no suena mientras `InterrogationDirector.IsActive`, y `contador_huye_01` queda pendiente hasta que la escena se cierra.
- **Guardias:** sus voces pasan por `AudioLibrary.TryPlayEnemyVoice` (un canal). No había nada que lo callara en la escena (aunque con `timeScale` 0 sus temporizadores no avanzan). **Arreglo:**
  - `TryPlayEnemyVoice`, `PlayVoice` y `PlayVoice2D` devuelven false mientras la escena está activa;
  - al entrar, el director corta lo que esté sonando en ese canal (`CutEnemyVoiceBelow(int.MaxValue)`).
- **Las voces de la escena no se pisan:** ya en la 3C.3, cada `Say` corta las dos fuentes (`Voz_Interrogado` y `Voz_Wilbur`) antes de empezar. Lo comprobé en el código; no cambió.
- **Lo que sigue sonando:** la lluvia, el ambiente y el escopetazo del final de la escena (son efectos, no voces). No hay AudioMixer.

## Parte B: la subfase (commit `41a8dc0`)

### Qué se hizo
- **`InterrogationData` + `InterrogationRun`** (`Scripts/Interrogation/InterrogationData.cs`, reutilizables). Cada línea tiene id (= clip), quién habla, texto, emoción, condición y efectos (confianza, resultado, mentira, entrega la pista, cortada por disparo). También hay rondas con opciones y respuestas candidatas, súplica, decisión con sus líneas, y pensamientos de respaldo condicionados.
- **Menú nuevo Plomo y Paja → Contador → Crear datos del interrogatorio** (`ContadorInterrogationDataBuilder`):
  - crea `Assets/_Game/Data/Interrogation_Contador.asset` con las **21 líneas** del guion;
  - comprobé con un script que los 26 textos (21 líneas + 5 pensamientos) aparecen **idénticos** en el guion;
  - lista los ids sin clip;
  - le asigna los datos a `CentavoInterrogation` en `Centavo.prefab`.

  `ContadorCentavoBuilder` también los enlaza al reconstruir el prefab.
- **`CentavoInterrogation`:** se **borró la demo** de la 3C.3 (textos fijos, 1.2 s de "…", decisión sin líneas). Ahora el flujo sale de los datos:
  1. `contador_int_00`;
  2. ronda 0 (opción → `wilbur_int_0X` → respuesta);
  3. ronda 1 (opción → `wilbur_int_1X` → respuesta según la confianza);
  4. `contador_int_2`;
  5. pensamiento de la IA;
  6. decisión y sus líneas;
  7. `Exit`.
- **Wilbur** siempre aparece como **WILBUR** y suena por `Voz_Wilbur`, así que la boca de Centavo no se mueve. Eso incluye `wilbur_dec_matar` y `wilbur_dec_perdonar`.
- **Matar:** `wilbur_dec_matar` → `contador_muerte_01` (`scared`). Este clip se **corta 0.2 s antes del final con `escopeta_disparo`**, y el retrato pasa a `death_01`. Se ve 0.8 s, luego `ResolveKilled()` (`death_01` en el mundo, `contador = Dead`, sin línea de la IA, D11) y `Exit`.
- **Perdonar:** `wilbur_dec_perdonar` → `contador_perdon_01` (`relief`) → `contador_perdon_02` (`talk`, **solo si todavía no dio** `registro_01`, y lo entrega) → `ResolvePardoned()` → `Exit`. Al cerrarse la escena dice `contador_huye_01` y se va cojeando por el portón.
- **`registro_01`:**
  - Nueva `CluePickup.Award(...)` (estática). Es lo mismo que hacía `CluePickup` al recogerse: sonido `pista_papel`, `GameStateTracker.RegisterClueCollected` (cuenta para los finales), `UIManager.ShowClueUI` si existiera, y comentario de la IA opcional. `CluePickup` ahora la usa también, con el mismo comportamiento de antes.
  - Centavo la llama con el comentario de la IA **apagado** (D11) y muestra en el canvas el aviso "Pista: Registro de deudas" durante 3 s, porque `UIManager` no está en ninguna escena.
  - **Textos de la pista:** el guion no los trae, así que los escribí yo a partir de `1A_v`:
    - nombre: "Registro de deudas" (el del GDD);
    - descripción: "Hoja del libro de Centavo: Porky sacó 3.200 pesos de la caja fuerte de Don Lobo";
    - texto completo: dos frases sobre la hoja.

    Están en el asset y se pueden cambiar ahí.
- **Pensamiento de la IA:**
  - `AICompanion.RequestCustomLine(situación, callback)` es nuevo: pide la línea con el mismo prompt de sistema, el contexto del juego (`GetStatusSummary`, conectado en la 3C.2) y las últimas líneas, y **no** la muestra en los subtítulos. `Speak` y lo demás de `AICompanion` no cambiaron.
  - El pedido dice:
    - quién es Centavo y que está herido;
    - **todo lo que dijo** (sus textos);
    - si mintió, y la confianza;
    - si entregó la hoja;
    - "1 o 2 oraciones, máximo 25 palabras, en español, noir y ambiguo, NUNCA le digas qué elegir".
  - **Espera como máximo 5 s de tiempo real** (`thoughtTimeoutSeconds`) mientras animan los "…". Si no hay respuesta, si hay error o si no hay key, usa la línea de respaldo **elegida antes de pedir**.
  - La línea de "colgaron de los pies" solo puede salir si dijo `1B_m`, y entonces el 50 % de las veces. Si no, sale una de las 4 generales.
  - Nada bloquea la entrada: la banda no recibe clics y la decisión aparece al terminar el pensamiento.

### Clips faltantes según el menú de validación
- **El menú no se ejecutó.** Comprobé lo mismo a mano contra `Audio/AudioLibrary.asset`: los **21 ids tienen clip** (13 de `contador_int_*` / `muerte` / `perdon` + 8 de `wilbur_int_*` / `wilbur_dec_*`). Faltantes: **ninguno**.
- Al correr el menú, el cuadro debe decir "✓ Las 21 líneas tienen clip".

### Tabla de los 9 caminos (ronda 0 × ronda 1): respuesta, Helpful/Misleading, ¿mintió?, ¿registro_01?
La confianza empieza en 0: **0A = −1, 0B = +1, 0C = 0**.

| Ronda 0 | Confianza | Ronda 1 | Respuesta | Resultado | ¿Mintió? | ¿registro_01 en el diálogo? |
|---|---|---|---|---|---|---|
| 0A | −1 | 1A | `contador_int_1A_m` | Misleading | sí (0A + _m) | no |
| 0A | −1 | 1B | `contador_int_1B_m` | Misleading | sí (0A + _m) | no |
| 0A | −1 | 1C | `contador_int_1C` | Helpful (sin cifra) | **sí (0A)** | no |
| 0B | +1 | 1A | `contador_int_1A_v` | Helpful | no | **sí** |
| 0B | +1 | 1B | `contador_int_1B_v` | Helpful | no | no |
| 0B | +1 | 1C | `contador_int_1C` | Helpful (sin cifra) | no | no |
| 0C | 0 | 1A | `contador_int_1A_v` | Helpful | no | **sí** |
| 0C | 0 | 1B | `contador_int_1B_v` | Helpful | no | no |
| 0C | 0 | 1C | `contador_int_1C` | Helpful (sin cifra) | no | no |

- "Mintió" se marca en la respuesta `contador_int_0A` (la que el guion marca como mentira, y solo sale si se elige 0A) y en `1A_m` y `1B_m`.

### Matar / perdonar: ¿registro_01 sale según el guion en todos los casos?
**Sí:**
- **Perdonar:** siempre lo da, por `1A_v` o, si no, por `perdon_02`, que solo se dice cuando la pista todavía no se dio (condición `ClueNotGiven`). Nunca se entrega dos veces: `MarkClueGiven` + `GameStateTracker`, que ignora duplicados.
- **Matar:** solo si ya la había dado `1A_v` (los caminos 0B-1A y 0C-1A).

### IA: ejemplo de 2 pensamientos reales + prueba sin conexión (línea de respaldo usada)
- **No se pudo probar:** no hay Unity ni se usa la key aquí. Nunca se lee `openai_key.txt`.
- **Prueba de David:**
  - con key: anotar 2 pensamientos reales; el log dice `[AICompanion] línea propia (Xs): …`;
  - sin conexión (cable de red o key quitada): a los 5 s como máximo (sin key, al instante) debe salir una de las líneas de respaldo del guion.

### GameStateTracker al final (los dos caminos)
- **Al llegar a la decisión (D10), una vez:**
  - `RegisterContadorInterrogation(Helpful | Misleading, mintió)`;
  - `RegisterInterrogation("Centavo", "dio información útil" | "mintió o intentó engañar a Wilbur")`. Suma 1 a `InterrogationsCompleted`, y el tracker no cuenta dos veces el mismo nombre.
- **Matar:** `contador = Dead`, `RegisterEnemyKilled("Centavo")` (cuenta la baja), sin línea de la IA (D11). `registro_01` solo si salió `1A_v`.
- **Perdonar:** `contador = Pardoned`, y `registro_01` siempre.
- **D4 (dispararle herido antes de la escena, 3C.2):** `contador = Dead`, interrogatorio `None`, sin pista y sin interrogatorio contado. Sin cambios.
- **El contexto de la IA** (`GetStatusSummary`) incluye a Centavo desde la 3C.2.

### ¿Siguen bien las condiciones de los 3 finales?
**Sí, no se tocaron.**
- `GameStateTracker.DetermineEnding()`:
  - **C:** ≥ 3 pistas, ≥ 2 interrogatorios y lobo perdonado;
  - **B:** ≥ 2 pistas y lobo muerto;
  - **A:** el resto.
- `GameManager` es el único que la llama. `FinaleTrigger` solo lee `CluesFound` para elegir un comentario.
- Centavo suma como mucho **1 pista** (`registro_01`) y **1 interrogatorio**.
- Ejemplo: en el nivel 1, con nota + contrato + `registro_01` (perdonándolo) y Colmillo + Centavo interrogados, ya se cumplen las pistas e interrogatorios del final C antes del nivel 2.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra `Library/ScriptAssemblies` y `Managed/UnityEngine`: runtime y editor con **0 errores**. Los únicos avisos nuevos son CS0649 en los `[SerializeField]` que asignan los menús.
- **No probado:** nada en Play, ni los menús, ni la IA.
- **APIs dudosas:**
  - `Image.pixelsPerUnitMultiplier` con 9-slice.
  - Contorno de TMP (`outlineWidth`), que crea una instancia del material en Play.
  - `AudioSource.time` para cortar `contador_muerte_01` 0.2 s antes del final.
- **Menús que David debe correr (en orden):**
  1. Abrir Unity y dejar que reimporte `ui_papel_dialogo.png` (ahora con alpha).
  2. **Plomo y Paja → Contador → Crear UI de interrogatorio**: importa el papel con alpha y 9-slice, y rehace el prefab con el diseño nuevo y el aviso de pista.
  3. **Plomo y Paja → Contador → Crear datos del interrogatorio**: crea el asset, debe decir que las 21 líneas tienen clip, y lo enlaza en `Centavo.prefab`.
  4. Commit de:
     - `Assets/_Game/Data/` (con su `.meta`);
     - `Prefabs/UI/InterrogationCanvas.prefab`;
     - `Prefabs/Level1/Centavo.prefab`;
     - `Art/UI/ui_papel_dialogo.png.meta`.
- **Revisar en Play:**
  1. **Diseño:**
     - el retrato grande con la cara, el torso y las manos **por encima** del papel;
     - la cabeza nunca cortada arriba;
     - el papel como tira baja sin magenta y con las orillas sin estirar;
     - tamaños y colores como en la tabla de A2;
     - ajustar en vivo en el Inspector de `InterrogationCanvas` y copiar los valores al prefab.
  2. **Opciones:** en renglones con "1.", "2.", "3." y el nombre WILBUR encima; con el mouse se oscurecen y subrayan; teclas 1/2/3.
  3. **Recorrer al menos 3 de los 9 caminos** (por ejemplo 0A-1B, 0B-1A, 0C-1C) y comprobar en el Inspector de `GameStateTracker`: `contadorInterrogation`, `contadorLied`, las pistas y `InterrogationsCompleted`.
  4. **Matar:** el escopetazo corta "…no he cuadrado el…!", el retrato pasa a `death_01`, y en el mundo queda `death_01`. No sale subtítulo de la IA por la baja.
  5. **Perdonar sin haber dado la hoja:** suena `perdon_02` y aparece "Pista: Registro de deudas". Al cerrar, `contador_huye_01` suena **después** de la escena y se va cojeando.
  6. **Audio:** durante la escena no suena ninguna voz del mundo (ni de Centavo ni de los guardias), y las voces de la escena nunca se pisan (probar clics rápidos).
  7. **IA:** con y sin conexión (ver arriba).

## Archivos creados/modificados
Creados (todos con `.meta`):
- `Scripts/Interrogation/InterrogationData.cs`: datos genéricos + estado de una partida (`InterrogationRun`).
- `Scripts/Interrogation/InterrogationOptionHover.cs`: resaltado de opciones.
- `Scripts/Editor/ContadorInterrogationDataBuilder.cs`: menú *Crear datos del interrogatorio* con las 21 líneas del guion.
- `Docs/audits/AUDIT_contador_3C4.md` (este archivo).

Modificados:
- `Tools/character_key.py`: modo `--single`.
- `Assets/_Game/Art/UI/ui_papel_dialogo.png`: con alpha, recortado.
- `Scripts/Interrogation/InterrogationCanvas.cs`: diseño ajustable, opciones en el lugar del texto, aviso de pista.
- `Scripts/Interrogation/InterrogationDirector.cs`: `SayCut` (corte por disparo), `ShowNotice`, WILBUR sobre las opciones y corte de las voces del mundo al entrar.
- `Scripts/Editor/InterrogationUIBuilder.cs`: papel con alpha y 9-slice, aviso de pista, `ApplyLayout` en el prefab.
- `Scripts/Audio/AudioLibrary.cs`: voces del mundo calladas en la escena y `ContainsVoice`.
- `Scripts/Characters/Contador/CentavoController.cs`: sin barks durante la escena; `huye_01` al cerrarse.
- `Scripts/Characters/Contador/CentavoInterrogation.cs`: flujo completo desde los datos (la demo se borró).
- `Scripts/AI/AICompanion.cs`: `RequestCustomLine` (sin subtítulos).
- `Scripts/Clues/CluePickup.cs`: `Award` estático compartido.
- `Scripts/Core/GameStateTracker.cs`: `HasClue`.
- `Scripts/Editor/ContadorCentavoBuilder.cs`: enlaza los datos del interrogatorio.

## Commits
- `2ecca7a`: dia4(david): 3C.3 UI de interrogatorio generada (canvas, fuente Special Elite, papel como Sprite)
- `6686e85`: dia4(david): 3C.4 parte A, arreglos de la prueba de 3C.3 (papel con alpha, diseño Mouse P.I. ajustable, voces del mundo calladas en la escena)
- `41a8dc0`: dia4(david): 3C.4 interrogatorio completo de Centavo (datos del guion, confianza, registro_01, pensamiento de la IA, decisión)
- Commit siguiente: esta auditoría.
