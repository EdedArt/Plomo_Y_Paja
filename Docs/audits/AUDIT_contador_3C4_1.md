# AUDIT contador 3C.4.1: arreglos de la prueba de la 3C.4 (fuente, decisión, epílogo)

Fase: `Docs/phases/Fase-3C-Contador.md` (secciones 0 y 1). Leídos `AUDIT_contador_3C3.md` y `AUDIT_contador_3C4.md` (con las notas y capturas de David al final), el guion, y el código de TMP 3.0.7 (`TMP_FontAsset.TryAddCharacters`).
Rama: `integracion/dia4`.

> **Importante:** Unity no se abrió en esta sesión. El código compila, pero **nada se probó en Play** y **los menús no se ejecutaron**. La fuente, el prefab del canvas y los datos del interrogatorio cambian cuando David corra los menús (lista al final).

## Antes de empezar
- Subí aparte lo que generó la prueba de la 3C.4:
  - `9c35a8c`: datos del interrogatorio, canvas con el diseño nuevo, papel 9-slice y el estado de la fuente;
  - `77fd3fc`: las notas y capturas de David en `AUDIT_contador_3C4.md` (`image.png`, `image-1.png`).
- **Fuera de esta subfase** (David lo pidió en sus notas, pero no está en esta orden): quitar el guardia interrogable del nivel 1 y poner 3–4 guardias más antes de Centavo. **No se hizo.** Queda pendiente para una orden aparte.

## 1. Fuente: causa, arreglo y caracteres

### Causa
- **El asset no estaba vacío.**
  - `SpecialElite SDF.asset` (el del commit `2ecca7a` y el actual) tiene **106 caracteres** en `m_CharacterTable`: todas las letras, números, signos, `¡ ¿ … — – « »`, tildes, `ü` y `ñ` que pedía la lista de la 3C.3.
  - En la captura de David el texto ya sale en Special Elite.
  - Al compararlo con el ASCII imprimible completo solo faltan `< > @ \ ^ ` { | } ~`, que no estaban en la lista.
- **El aviso venía de TMP, no del asset.** En TMP 3.0.7, `TryAddCharacters(chars, out missing)` hace `missing = chars` (**todo** el texto pedido) cuando `FontEngine.LoadFontFace` falla. Lo hace antes de mirar qué caracteres ya están (`TMP_FontAsset.cs`, línea ~1634). El menú imprimía ese `missing` tal cual. La primera vez la fuente se generó bien; en la segunda corrida `LoadFontFace` falló y el menú dijo que faltaba todo.
- **Por qué falló `LoadFontFace` en la segunda corrida:** no se puede saber sin el Editor.
- **El `.meta` del TTF (escrito a mano en la 3C.3) no es la causa:**
  - es un `TrueTypeFontImporter` válido (`serializedVersion: 4`, `includeFontData: 1`, `fontNames: Special Elite`);
  - Unity lo importó sin reescribirlo (no aparece modificado en git);
  - con él, la primera generación funcionó.

### Arreglo (`InterrogationUIBuilder.EnsureFont`)
- **Juego pedido:** ASCII imprimible (32–126) + `¡ ¿ … — – « » “ ” ‘ ’ á é í ó ú ü ñ Á É Í Ó Ú Ü Ñ`. Son 120 caracteres. Se agregaron las comillas tipográficas porque la IA a veces las escribe.
- **La comprobación ya no usa el texto de TMP:** se mira la tabla del asset (`HasCharacter`) carácter por carácter. Los que el TTF no trae se averiguan preguntándole a `FontEngine` y no cuentan como faltantes (TMP los dibuja con la fuente de respaldo y el reporte los lista).
- **Si la fuente ya está completa** (hecha por este menú o a mano con el Font Asset Creator), **no se toca**: solo se pasa a **Static** y el reporte dice `✓ Fuente Special Elite completa (N caracteres), estática`.
- **Si le faltan caracteres:**
  - se completan **sobre el mismo asset**, así se conserva el GUID que usa el canvas;
  - se agregan **solo los que faltan** y nunca se vacía (ya no se llama a `ClearFontAssetData`);
  - se vuelve a comprobar con la tabla y, si está completa, queda **Static**. Una fuente Static no genera nada en el juego y TMP suelta la referencia al TTF.
- **Si `FontEngine` no puede abrir el TTF:** no se toca lo que hay (queda estática) y el reporte dice `✗` con los caracteres que faltan y que hay que usar el Font Asset Creator.
- **Si el TTF tuviera `Include Font Data` apagado,** se activa y se avisa.

### Qué debe decir el reporte del menú
- Si `FontEngine` abre el TTF: `✓ Fuente Special Elite generada (… caracteres) y estática`, o `completa` si ya lo estaba. Puede aparecer además `⚠ El TTF de Special Elite no trae «…»` si al TTF le faltan glifos (por ejemplo, las comillas tipográficas). Es informativo.
- Si no lo abre: `✗ No se pudieron generar «…»`. Con el asset actual, la línea mostraría solo `< > @ \ ^ ` { | } ~ “ ” ‘ ’`, que no son del guion. El texto en español del juego **ya se ve**.

### Si el menú no la deja completa: Font Asset Creator a mano
1. **Window → TextMeshPro → Font Asset Creator.**
2. Source Font File: `Assets/TextMesh Pro/Fonts/SpecialElite-Regular.ttf`.
3. Sampling Point Size: **Custom, 56**. Padding: **5**. Packing: Optimum. Atlas Resolution: **1024 × 1024**.
4. Character Set: **Custom Characters**, pegar exactamente:
   `` !"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~¡¿…—–«»“”‘’áéíóúüñÁÉÍÓÚÜÑ ``
   (el primer carácter es un espacio).
5. Render Mode: **SDFAA**. **Generate Font Atlas.** Revisar que "Missing characters" esté vacío o solo con comillas tipográficas.
6. **Save as…** encima de `Assets/_Game/Fonts/SpecialElite SDF.asset`, para que conserve el GUID.
7. En el Inspector del asset, Atlas Population Mode = **Static**.
8. Correr el menú otra vez: debe decir "completa … no se regenera".

## 2. Estilo de la decisión (valores)
- **Sin botones de Unity:**
  - el `Button` queda con `transition = None` y navegación apagada;
  - su `Image` es invisible pero sigue recibiendo el clic;
  - el `Outline` y la caja oscura de la 3C.3 se apagan o ya no se crean.

  `InterrogationCanvas.ApplyLayout` lo aplica al arrancar, así que también funciona con el prefab viejo; el menú lo deja guardado.
- **Escrita en el papel:** nombre **WILBUR**, y `1. Jalar el gatillo` | `2. Dejarlo ir`, cada una centrada en su mitad del papel. Entre las dos hay una **raya de tinta** (`Divisor`: 3 px de ancho, 75 % del alto, #1E1209 al 69 %).
- **Valores** (todos son campos de `InterrogationCanvas`):

| Qué | Valor |
|---|---|
| Decisión | Special Elite **52 px**, tinta **#1E1209** |
| Opciones | **34 px**, la misma tinta #1E1209 (antes #3B2414), una por renglón con "1." |
| Con el mouse encima (las dos) | tinta roja **#8B1A1A** + **subrayado** + escala **1.06** + temblor a saltos **±1.5 px a 12 Hz** (tiempo real) |
| Teclas | 1/2 para la decisión, 1/2/3 para las opciones, y clic |
| Retrato mientras se decide | **`plead`** (`CentavoInterrogation.decisionEmotion`) |

- **Mismo resaltado en opciones y decisión:** lo hace `InterrogationOptionHover`. Las opciones crecen desde el "1.", con el pivote a la izquierda.

## 3. Epílogo después de la decisión (las dos ramas, tiempos)

### Mecánica común
- **`InterrogationDirector.Exit(resultado, epílogo)`:**
  - cierra la escena (se van el retrato y el papel);
  - el desenfoque baja en 0.35 s y **el tiempo vuelve a 1**;
  - el canvas queda en modo **solo pensamiento** (banda de arriba, crema con contorno oscuro);
  - corre el epílogo, y después la cámara vuelve a la vista del jugador en **0.6 s** (`outroReturnSeconds`) y se devuelve el control.
- **Durante todo el epílogo:**
  - el jugador sigue bloqueado (`PlayerController` y escopeta apagados, cursor bloqueado e invisible);
  - `InterrogationDirector.IsActive` sigue en true, así que `PlayerAim.GameplayBlocked()` esconde manos y mira y **Escape no pausa**.
- **Dos estados distintos:**
  - `IsActive` = escena **o** epílogo;
  - `IsSceneOpen` (nuevo) = solo la escena. Ahí es donde se callan las voces del mundo (`AudioLibrary` y los barks de `CentavoController`, A3 de la 3C.4).

  Por eso `contador_huye_01` suena **al cerrarse la escena**, cuando empieza a cojear.
- **Pensamiento:**
  - se pide a la IA **en el momento de elegir**, en paralelo con las líneas de la decisión (`AICompanion.RequestCustomLine`);
  - espera como máximo **4 s de tiempo real desde ese momento** (`afterThoughtTimeoutSeconds` en los datos); si no llega, usa una línea de respaldo;
  - el pedido lleva lo que dijo Centavo, si mintió, si entregó la hoja (al perdonarlo siempre la entrega) y la decisión. Las reglas son las mismas que antes: 1–2 oraciones, ≤ 25 palabras, español, noir, ambiguo, sin juzgar;
  - se ve **max(3 s, letras × 0.055 s)** (`outroThoughtMinSeconds`, `outroThoughtSecondsPerCharacter`), con fundidos de 0.4 s.

### Dejarlo ir
1. Las líneas de la decisión, `ResolvePardoned()` y `Exit`.
2. La cámara sube de agachada a la altura de los ojos del jugador en **0.6 s** (`spareRiseSeconds`), mirándolo a 1 m sobre los pies.
3. Lo sigue cojear hacia el portón **2.0 s** (`spareTrackSeconds`). Al empezar suena `contador_huye_01`.
4. Se inclina **65°** hacia el cielo en **1.0 s** (`skyTiltSeconds`, `skyPitch`).
5. Con la cámara en el cielo, **Centavo desaparece** (`CentavoController.ForceGone()`, estado Gone).
6. Pensamiento (≥ 3 s).
7. La cámara vuelve en 0.6 s y vuelve el control. Centavo ya no está: no se lo puede seguir.

### Jalar el gatillo
1. Las líneas de la decisión (`contador_muerte_01` cortado por el escopetazo), 0.8 s con `death_01` en el retrato, `ResolveKilled()` y `Exit`.
2. La cámara se queda en el cuerpo (`death_01`, mirando a 0.35 m sobre el ancla) y sube despacio a la altura de los ojos en **1.0 s** (`killRiseSeconds`).
3. Pensamiento (≥ 3 s). Este pensamiento reemplaza la línea de la IA de la baja que D11 apaga.
4. La cámara vuelve en 0.6 s y vuelve el control.

Todos los tiempos son campos: los del epílogo en `CentavoInterrogation` (prefab de Centavo), el regreso y los fundidos en `InterrogationDirector` (prefab del canvas) y la espera de la IA en `Interrogation_Contador.asset`.

## Líneas de respaldo sin conexión
- **Agregadas al guion** (`Docs/guiones/contador.md` §5, "Pensamiento después de la decisión") y a los datos (`spareThoughts` / `killThoughts`), con el texto exacto de la orden.
- **Cómo se elige:**
  - **Dejarlo ir:** una al azar de las 3.
  - **Jalar el gatillo:** si mintió, "Murió mintiendo…" sale el **50 %** de las veces. Si dijo la verdad y no mintió (`Helpful` y sin mentiras), "Te dijo la verdad y le pagaste con plomo." sale el 50 %. Si no sale la condicionada, se elige una de las 3 generales.
- **Antes de la decisión,** "Dijo que lo colgaron de los pies…" (solo si dijo `1B_m`, 50 %) ahora usa `condition = SaidLine`. Los datos de la 3C.4, que no tenían el campo `condition`, se leen igual.
- **No se pudo probar sin conexión.** Prueba de David (key quitada o sin red): después de cada decisión sale "…" y, como mucho a los 4 s desde que eligió (sin key, al instante), una de estas líneas.

## Menús que David debe correr (en orden)
1. Abrir Unity y dejar que compile.
2. **Plomo y Paja → Contador → Crear UI de interrogatorio.** Leer la línea de la fuente (ver sección 1). Si sale `✗`, seguir los pasos del Font Asset Creator y volver a correr el menú.
3. **Plomo y Paja → Contador → Crear datos del interrogatorio.** Agrega los pensamientos del epílogo y la condición nueva. Debe decir que las 21 líneas tienen clip.
4. Commit de `Fonts/SpecialElite SDF.asset`, `Prefabs/UI/InterrogationCanvas.prefab`, `Data/Interrogation_Contador.asset` y `Prefabs/Level1/Centavo.prefab` si cambió.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 contra `Library/ScriptAssemblies` y `Managed/UnityEngine` (TMP 3.0.7 incluido): runtime y editor con **0 errores**.
- **No probado:** nada en Play, ni los menús, ni la fuente regenerada, ni la IA.
- **APIs dudosas:**
  - `FontEngine.LoadFontFace(Font, int)` y `TryGetGlyphWithUnicodeValue` desde el Editor: si `LoadFontFace` vuelve a fallar, el menú ahora lo dice en vez de mentir.
  - `atlasPopulationMode = Static`: suelta `m_SourceFontFile` y conserva `m_SourceFontFile_EditorRef`; el menú lo puede volver a dinámica para completar.
- **Revisar en Play:**
  1. La decisión se ve escrita en el papel (sin cajas), con WILBUR, la raya en el medio y Centavo en `plead`. Con el mouse encima: roja, subrayada, más grande y temblando. Teclas 1/2.
  2. Las opciones con el mismo resaltado, más chicas.
  3. **Perdonar:** la cámara sube y lo sigue cojear (con "¡Gracias, gracias, gracias…"), mira al cielo y aparece el pensamiento. Al volver la vista, Centavo ya no está. Durante todo eso: sin manos ni mira, Escape no hace nada, no se puede caminar.
  4. **Matar:** la cámara sobre el cuerpo sube despacio y aparece el pensamiento. No sale el subtítulo de la IA por la baja.
  5. Si el pensamiento tapa algo o dura poco o mucho, ajustar los campos del epílogo en el prefab de Centavo.

## Archivos creados/modificados
Creados:
- `Docs/audits/AUDIT_contador_3C4_1.md` (este archivo).

Modificados:
- `Scripts/Editor/InterrogationUIBuilder.cs`: fuente comprobada por tabla, completada sin vaciar y estática; decisión y opciones sin cajas.
- `Scripts/Interrogation/InterrogationCanvas.cs`: decisión en el papel con divisor, tinta común, modo solo pensamiento y valores nuevos.
- `Scripts/Interrogation/InterrogationOptionHover.cs`: tinta roja, subrayado, escala y temblor (opciones y decisión).
- `Scripts/Interrogation/InterrogationDirector.cs`: `Exit` con epílogo, `IsSceneOpen`, `MoveCamera`/`TrackCamera`/`ReturnCamera`/`OutroThought` y `Decide` con emoción y nombre.
- `Scripts/Interrogation/InterrogationData.cs`: condición de los pensamientos (`SaidLine`/`Lied`/`ToldTruth`), `killThoughts`/`spareThoughts` y su tiempo de espera.
- `Scripts/Characters/Contador/CentavoInterrogation.cs`: pensamiento pedido al decidir, los dos epílogos, `plead` en la decisión y tiempos como campos.
- `Scripts/Characters/Contador/CentavoController.cs`: `ForceGone()`; las voces se callan solo con la escena abierta.
- `Scripts/Audio/AudioLibrary.cs`: las voces del mundo se callan con `IsSceneOpen`.
- `Scripts/Editor/ContadorInterrogationDataBuilder.cs`: pensamientos del epílogo.
- `Docs/guiones/contador.md`: §5 "Pensamiento después de la decisión".

## Commits
- `9c35a8c`: dia4(david): 3C.4 assets generados (datos del interrogatorio, canvas con el diseño nuevo, papel 9-slice)
- `77fd3fc`: docs: notas y capturas de la prueba de David de la 3C.4
- `c8410cc`: dia4(david): 3C.4.1 fuente estática verificada, decisión escrita en el papel y epílogo con pensamiento tras la decisión
- Commit siguiente: esta auditoría.
