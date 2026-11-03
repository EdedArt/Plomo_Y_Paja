# AUDIT INTRO.1 — El libro (escena `Intro`)

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4` (checkout principal, sin worktree) · **Alcance:** solo INTRO.1 + enmiendas A1–A5.
**Docs leídos:** `CLAUDE.md`, `Docs/phases/README.md`, `Docs/phases/Fase-Intro.md` (en el repo no existe `PHASE-INTRO.md`: las enmiendas se añadieron a `Fase-Intro.md` como §1b), `Docs/guiones/Guion-Intro.md` (= `intro.md`), §0 de `Fase-3C-Contador.md`, `Docs/audits/AUDIT_intro_0.md`.
**Puerta:** `AUDIT_intro_0.md` no tiene la línea `ESTADO: LUZ VERDE`. Se siguió porque el prompt trae las enmiendas de la revisión de INTRO.0 y pide INTRO.1 de forma explícita.

> **Importante:** Unity no se abrió en esta sesión. El código compila (ver más abajo), pero **el menú no se ejecutó y nada se probó en Play**. La escena, el prefab, el asset de datos y los materiales todavía no existen: los crea el menú. Los "sí" de abajo dicen cómo está programado.

## Reconocimiento (graphify) y clases reutilizadas
`graphify update` sobre `Assets/_Game/Scripts` (solo AST, el grafo del 2 nov estaba viejo): 1727 nodos. Hallazgos:
- **Menú principal: no existe.** No hay escena `MainMenu` ni script de menú. `GameManager` lo menciona en comentarios (índice 0) y `UIManager` no está en ninguna escena. Tampoco había ningún cargador de escenas ni fundido reutilizable.
- **Parpadeo de lámpara:** el único script es `Level/LightFlicker.cs` (el farol roto de 3A4). 3C.1 no creó otro.

| Clase | Para qué se reutilizó |
|---|---|
| `AudioLibrary` | Voces del narrador (`GetVoice`), bucles (`GetSfx`), efectos 2D (`PlaySfx2D`), volúmenes. **Se le añadió** `ContainsSfx` para que el builder liste ids faltantes. |
| `AudioLibraryBuilder` / `GameAudioImporter` (`AudioImporter.cs`) | Librería e importación existentes; regla A3 añadida (ver abajo). |
| `AudioLibraryLoader` | Registra la librería en la escena `Intro` (en los niveles lo hace el Player). |
| `CharacterSpriteImporter` (regla `IntroFolder` → `ApplyBook` de INTRO.0) | Importa las 3 texturas `libro_*` nuevas sin tocar el importador. |
| `InterrogationUIBuilder.EnsureFont` | Fuente Special Elite estática (pasó de `private` a `public`, sin otros cambios). |
| `LightFlicker` | Llama de la lámpara de aceite, afinada por `SerializedObject`: temblor 0.1, bajones suaves, **sin apagones**. |
| `CartoonKit` (`Box`, `GetMesh`, `AddLathe`, `MeshObject`, `M`) | Mesa, patas, piso, lámpara (latón + vidrio cálido emisivo), tapas y bloques del libro. |
| `LevelBuilderUtils` (`EnsureFolder`, `CreatePointLight`) | Carpetas y luz de la lámpara. |
| `NoirVolumeBuilder.GetOrCreateTechnicolorProfile` | Volume global del juego. La viñeta más fuerte va en un perfil aparte. |
| `EditorMenuGuard`, `LevelBuilder_NivelPorky.ScenePath` | Bloqueo en Play y ruta del Nivel 1. |
| `RainEmitter` (sin cambios) | El Nivel 1 arranca su lluvia sola: la del libro se le entrega encima (I11). |

No se usaron `GameManager`, `UIManager` ni `NoirPostProcess` (este último hace el pulso de daño del jugador).

## Menús que David debe correr (en orden)
1. Abrir Unity y esperar la importación. Se generan los `.meta` de `libro_*` y de los scripts nuevos, se reimportan las 8 voces recortadas, y `AudioImporter` v1 reimporta el audio (los `amb_*` quedan en estéreo).
2. **Plomo y Paja → Reconstruir Librería de Audio.** Hoy `AudioLibrary.asset` **no** tiene `sfx_libro_abrir`, `sfx_pagina`, `musica_intro`, `amb_intro_cuarto` ni las voces del taxi: los WAV de INTRO.0 nunca se importaron. Ya tiene los `narrador_intro_*` y `lluvia_loop`.
3. **Plomo y Paja → Intro → Construir escena del libro.** Pregunta si se guarda la escena abierta y crea todo. Al final, un diálogo lista los faltantes y el antes/después de Build Settings.
4. Abrir `Assets/Scenes/Intro.unity` y darle Play (ver la lista de pruebas al final).
5. Hacer commit de lo generado: `.meta`, escena, prefab, `IntroBook.asset`, materiales, `IntroVignetteProfile.asset`, `AudioLibrary.asset` y `ProjectSettings/EditorBuildSettings.asset`.

## Escena y prefab creados (rutas)
Los crea el menú, idempotente: la escena se regenera desde cero y los assets se actualizan en su sitio.
- `Assets/Scenes/Intro.unity`: está en la misma carpeta que `NivelPorky.unity`. Contiene:
  - `Cuarto`: mesa, lámpara de aceite con luz puntual cálida (sombras suaves + `LightFlicker`) y relleno frío direccional.
  - `StoryBook` (instancia del prefab), `Main Camera` (FOV 35, post-proceso on, no ve la capa UI) y `PostProceso` (Technicolor + viñeta 0.5).
  - `UI_Intro`: fundido y aviso de saltar.
  - `IntroDirector`: `StoryBookDirector`, `HoldToSkip`, `AudioLibraryLoader` y 4 `AudioSource` 2D.
- `Assets/_Game/Prefabs/Intro/StoryBook.prefab`: tapa, contratapa, lomo, 2 bloques de páginas, 7 hojas, anclas `Cam_Wide`/`Cam_Spread`/`Cam_Push` y `DoblePagina_RT` (2 rigs).
- `Assets/_Game/Data/Intro/IntroBook.asset` (textos exactos del guion §2, comprobados carácter por carácter).
- `Assets/_Game/Materials/Intro/Libro_{Cuero,Canto,Tapa,Guardas,Papel,Pagina}.mat` (URP Lit) y `Assets/_Game/Settings/IntroVignetteProfile.asset`.
- La malla de la lámpara va en `Art/Environment/Generated/IntroLamparaAceite.asset` (caché de `CartoonKit`).
- Polvo en el aire (opcional): **no se hizo**.

## Flujo de escenas antes/después (menú → intro → nivel 1)
- **Antes:** Build Settings 0 = `SampleScene` (escena de prueba), 1 = `NivelPorky`. No hay menú: el build arrancaba en `SampleScene` y nada llevaba al Nivel 1.
- **Después (al correr el menú):** 0 = `Intro`, 1 = `NivelPorky`, 2 = `SampleScene` (se mantiene, al final). El build arranca en el libro. Al terminar o al saltar, el libro carga `NivelPorky` en async. Si no estuviera en Build Settings, usa el índice siguiente y avisa.
- **Nivel 1 se sigue pudiendo abrir y jugar directo en el Editor** (no depende de `Intro`).
- Efectos laterales (sin cambiar código):
  - `GameManager.GoToMainMenu()` (índice 0) ahora llevaría al libro. Hoy nadie lo llama, porque `UIManager` no está en ninguna escena.
  - `LoadNextLevel()` desde `NivelPorky` iría a `SampleScene`. Antes ese índice no existía, y hoy no se llama.

## Fuente usada y ¿se ven ñ, ¿, ¡, tildes y …?
- **Special Elite** (`Fonts/SpecialElite SDF.asset`, estática, la de 3C.4.1). Se usa en el texto de las páginas, la portadilla, el título dorado de la tapa (TMP 3D) y el aviso de saltar.
- Revisé la tabla del asset: **están ñ, ¿, ¡, á, é, í, ó, ú, É y …** (U+2026). Por código, sí se ven. Falta verlo en pantalla.

## Ids faltantes (audio/arte) y qué placeholder se usa
- **Arte:** ninguno. Están `ilus_intro_01..06`, `libro_portada`, `libro_guardas` y `libro_papel`.
- **Audio:** los archivos existen todos. Hasta correr *Reconstruir Librería de Audio*, la librería no tiene `sfx_libro_abrir`, `sfx_pagina`, `musica_intro` ni `amb_intro_cuarto`. Mientras tanto no suenan (silencio y un aviso por id en la consola).
- **Placeholders programados por si algo falta:**
  - Ilustración: rectángulo gris con "ILUSTRACIÓN 0N".
  - Papel: crema liso. Tapa: cuero rojo liso. Guardas: ocre.
  - Voz del narrador: el texto se escribe con una duración estimada (0.055 s por carácter, mínimo 3 s).
  - Música, ambiente o lluvia: silencio.
  - El builder lista los faltantes en su diálogo.

## Saltar (mantener ESPACIO/Esc) y avanzar con clic: ¿funcionan?
Programado, sin probar:
- **Saltar:** mantener ESPACIO o Esc 1 s (`Cinematics/HoldToSkip`, tiempo real).
  - El anillo radial "Mantén ESPACIO para saltar" aparece al tocar cualquier tecla o clic y se va solo a los 2.5 s.
  - Al completarse: fundido a negro 0.5 s, la lluvia sube y se activa el Nivel 1 (se empieza a cargar si aún no).
- **Avanzar:** clic izquierdo o un toque corto de ESPACIO (< 0.25 s, se detecta al soltar).
  - Mientras se escribe: completa el texto y pasa la hoja ya, cortando la voz.
  - Después: pasa la hoja ya.
  - En la portadilla corta la espera de 2 s.
  - Un toque hecho mientras gira una hoja se descarta.
- Esc no pausa en el libro: no hay `GameManager` ni menú de pausa en esa escena.

## ¿Hay silencio o tirón al pasar al Nivel 1? (carga async)
Programado para que no lo haya:
- `LoadSceneAsync` con `allowSceneActivation = false` empieza al llegar a la página 5.
- Final: la cámara entra en la ilustración (1.5 s) y el fundido a negro dura el último 1.0 s. Mientras tanto la música y el ambiente bajan a 0 y `lluvia_loop` sube a 0.4 × volumen de efectos.
- Al activar el nivel, la fuente de lluvia pasa a `DontDestroyOnLoad` (`Cinematics/CarryOverAudio`). Ya en el nivel se apaga en 2.5 s, mientras `RainEmitter` sube la suya en ~0.25 s: hay lluvia continua.
- Si hay tirón de activación, ocurre con la pantalla en negro (el audio no se corta).
- **A comprobar en el Editor.**

## Archivos compartidos tocados (y si estaban limpios en git status)
- `Scripts/Editor/AudioImporter.cs`: **limpio** antes de editar. A3 aplicado: `amb_*` y `musica_*` en estéreo y Streaming, voces siempre mono, `GetVersion()` = 1 (antes no tenía).
- `ProjectSettings/EditorBuildSettings.asset`: **limpio**. **No lo edité**: lo reescribe el menú con `EditorBuildSettings.scenes` cuando David lo corre.
- `CharacterSpriteImporter.cs`, `GameStateTracker.cs`, `CLAUDE.md`: **no se tocaron**.
- Fuera de la lista de compartidos (los dos estaban limpios):
  - `Audio/AudioLibrary.cs`: se añadió `ContainsSfx`.
  - `Editor/InterrogationUIBuilder.cs`: `EnsureFont` pasó a ser público.

## Qué no se pudo compilar/probar y qué debe revisar David en el Editor
- **Compilado: sí.** Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`) contra los ensamblados del proyecto (referencias de los `.csproj`): **runtime (84 fuentes) y editor (27) con 0 errores y 0 avisos**. Compilé antes de cada commit con código.
- **Sin probar:** todo lo de Play y el propio menú. Revisar:
  1. **Orientación de la doble página.** La ilustración debe verse derecha, sin espejo y continua a través del lomo, en el dorso de la hoja *i* y en el frente de la *i+1*. Si saliera en espejo o invertida, el ajuste está en `IntroBookBuilder.Face` (rotación del dorso) o en el offset de `SpreadCompositor.MakeMaterial`.
  2. Que el texto del RT se vea nítido de cerca (RT 2048 con mipmaps y trilineal).
  3. **Sombras de la lámpara sobre hojas apiladas cada 0.8 mm:** posible "acné". Si aparece, poner `Cast Shadows = Off` en las caras de las hojas.
  4. El encuadre de las 3 anclas (85 % del cuadro en `Cam_Spread`) y la intensidad de la lámpara (2.2, alcance 2.6).
  5. Que la tapa al abrirse no atraviese la hoja 1 de forma visible. Su bloque izquierdo roza el lomo a mitad del giro.
  6. El tamaño del título dorado de la tapa (TMP 3D con autoajuste).
  7. Checklist de Play: libro cerrado y fundido → abre la tapa → portadilla → 6 páginas (texto exacto, al ritmo de la voz, música que baja con el narrador) → clic avanza → mantener salta → entra en la última ilustración → negro con lluvia → Nivel 1 con lluvia y sin errores en la consola.

### APIs dudosas
- **Tamaño del Canvas de los rigs.** Que un Canvas *Screen Space - Camera* tome el tamaño del `targetTexture` de su cámara. No pasa nada si no: todo está anclado en proporciones y solo los tamaños de letra asumen escala 1 a 2048 px.
- **Cámaras de los rigs.** Quedan **siempre encendidas**. No se apagan para "congelar" el RT porque un Canvas *Screen Space - Camera* con la cámara apagada puede dibujarse como Overlay. Son dos cámaras que solo pintan UI: cuesta poco.
- **RT y textura.** RenderTexture NPOT (2048×1365) con mipmaps automáticos (soportado en DX11/12). Además, `canvas.scaleFactor` está puesto a mano sin `CanvasScaler`.
- **Anillo de saltar.** Usa el sprite interno `UI/Skin/Knob.psd` para el relleno radial (sin sprite, el modo Filled no funciona).

## Diseño de doble página: tamaño del RenderTexture y cómo se parte en el lomo
- Página de 22.5 × 30 cm (proporción de `libro_papel`). La doble página es 3:2, así que **cada RT mide 2048 × 1365** (ARGB32 sRGB, profundidad 16, mipmaps automáticos, trilineal, aniso 4). Son **2 RT** que se turnan: la doble página *s* usa el rig *s* % 2. Unos 30 MB entre los dos.
- **Partición en el lomo (u = 0.5):**
  - La mitad izquierda (u 0–0.5, tiling 0.5, offset 0) va al **dorso de la hoja *i***.
  - La mitad derecha (u 0.5–1, offset 0.5) va al **frente de la hoja *i+1***.
  - Los quads de dorso giran 180° en Z respecto al frente, así que al pasar la hoja quedan derechos.
  - La hoja que gira lleva la doble página saliente en el frente y la entrante en el dorso.
  - La nueva se compone (con el texto oculto) antes de empezar a girar.
- **Maqueta (en px del RT):**
  - Papel en cada mitad, el izquierdo en espejo.
  - Ilustración 16:9 de 1505 × 846 px (62 % del alto, centrada, 5.5 % de margen arriba) con un marco de tinta de 7 px.
  - Debajo, la banda de texto: Special Elite 56 px, tinta `#2A1F1A`, centrada a lo ancho, con el efecto de máquina de escribir vía `maxVisibleCharacters` (sin basura por frame).
- **Portadilla:** misma técnica. El rig 0 muestra el título y el subtítulo en la mitad derecha. Las guardas son una textura directa en la cara interior de la tapa (recortada a 3:4).
- **Final:** con A1 la ilustración cruza las dos páginas, así que `Cam_Push` apunta al centro de la ilustración y no a la página izquierda.

## Texturas libro_* importadas (tamaños)
Copiadas (no movidas, MD5 idéntico) de `F:\Descargas\` a `Art/Intro/`. Las importa la regla `ApplyBook` de INTRO.0 (sRGB, mipmaps, máx. 2048, sin compresión).

| Textura | Tamaño | Uso |
|---|---|---|
| `libro_portada` | 1024 × 1536 (2:3) | Portada. La tapa toma su proporción: 23.7 × 35.5 cm, sin recorte. |
| `libro_guardas` | 1024 × 1536 (2:3) | Guardas, en un quad del tamaño de la página (3:4). Se recorta un 11 % vertical, centrado, sin deformar. |
| `libro_papel` | 1086 × 1448 (3:4) | Papel de las páginas (fija la proporción de la página: 22.5 × 30 cm) y fondo de los RT. |

El juego cuadrado `ilus_intro_*` de `F:\Descargas` se ignoró (A2).

## Recorte de silencio: duraciones antes/después
`PLOMO_Y_PAJAV_1/Tools/trim_voice_silence.py`:
- numpy + `wave`, umbral −45 dBFS en ventanas de 10 ms.
- Deja 0.05 s de margen y aplica un fundido de 5 ms en los bordes cortados. Un borde que ya tiene ≤ 0.1 s no se toca.
- Se corrió **solo sobre las copias del proyecto**. Una segunda pasada dice "ya cumple" en todas. Las voces de 3C no se tocaron.
- El script vive en `PLOMO_Y_PAJAV_1/Tools/`, donde están los demás (`character_key.py`…); no existe un `Tools/` en la raíz.

| Archivo | Antes (s) | Inicio → | Final → | Después (s) |
|---|---|---|---|---|
| narrador_intro_01 | 4.779 | 0.000 → 0.000 | 0.129 → 0.050 | 4.700 |
| narrador_intro_02 | 6.506 | 0.000 | 0.036 | 6.506 (ya cumplía) |
| narrador_intro_03 | 9.085 | 0.000 | 0.055 | 9.085 (ya cumplía) |
| narrador_intro_04 | 6.520 | 0.000 | 0.020 | 6.520 (ya cumplía) |
| narrador_intro_05 | 7.086 | 0.000 | 0.086 | 7.086 (ya cumplía) |
| narrador_intro_06 | 7.768 | 0.000 | 0.048 | 7.768 (ya cumplía) |
| taxista_01 | 9.680 | 0.060 | 0.330 → 0.050 | 9.400 |
| taxista_02 | 7.360 | 0.070 | 0.160 → 0.050 | 7.250 |
| taxista_03 | 5.040 | 0.100 | 0.270 → 0.050 | 4.820 |
| wilbur_taxi_01 | 5.280 | 0.060 | 0.380 → 0.050 | 4.950 |
| wilbur_taxi_02 | 1.600 | 0.160 → 0.050 | 0.320 → 0.050 | 1.220 |
| wilbur_casa_01 | 2.480 | 0.260 → 0.050 | 0.130 → 0.050 | 2.190 |
| wilbur_arma_01 | 3.280 | 0.110 → 0.050 | 0.310 → 0.050 | 2.960 |

## Decisiones tomadas
1. **Sin menú principal**, `Intro` va en el índice 0 de Build Settings y `SampleScene` pasa al final (no se borra).
2. **Estructura del libro:**
   - Tapa: más ancha que la página (+1.2 cm) y con la altura de `libro_portada`. Así la portada no se deforma y sobresale 2.7 cm arriba y abajo.
   - Hojas: 7, con 0.8 mm entre una y otra.
   - Bloques de páginas: 1.2 cm por lado.
3. **El cartel del final apunta a la ilustración** a lo ancho (A1), no a "la página izquierda".
4. **Volume:** el perfil Technicolor del juego sin tocar, más un perfil propio de viñeta 0.5 con prioridad 2.
5. **Mezcla:** música a 0.35 (−6 dB con el narrador), ambiente a 0.5, lluvia final a 0.4, todo × el volumen de efectos de la librería. Ajustables en `StoryBookDirector`.
6. **Cámara:** temblor en mano de 0.25° (≤ 0.3°). El FOV lo fija el builder (35°) y el director nunca lo toca.
7. **Reutilizable para INTRO.2** en `Scripts/Cinematics/`: `HoldToSkip`, `ScreenFade`, `CarryOverAudio`, `CinematicEase`. El código propio del libro está en `Scripts/Intro/`.
8. **A5:** ni la línea fija de la IA ni el taxi se tocaron.

## Preguntas para el orquestador
1. Cuando exista el Nivel 2, ¿se añade a Build Settings detrás de `NivelPorky`? Hoy `LoadNextLevel()` caería en `SampleScene`.
2. El polvo en el aire (opcional) no se hizo. ¿Se quiere en INTRO.4?

## Archivos creados/modificados
**Creados:**
- `Scripts/Intro/`: `IntroBookData.cs`, `StoryBook.cs`, `SpreadCompositor.cs`, `StoryBookDirector.cs`.
- `Scripts/Cinematics/`: `HoldToSkip.cs`, `ScreenFade.cs`, `CarryOverAudio.cs`, `CinematicEase.cs`.
- `Scripts/Editor/`: `IntroBookBuilder.cs`, `IntroBookBuilder.Book.cs`.
- `PLOMO_Y_PAJAV_1/Tools/trim_voice_silence.py`.
- `Art/Intro/`: `libro_portada.png`, `libro_guardas.png`, `libro_papel.png`.
- `Docs/audits/AUDIT_intro_1.md`.

**Modificados:**
- `Docs/phases/Fase-Intro.md`: §1b, enmiendas A1–A5.
- `Scripts/Editor/AudioImporter.cs`: A3.
- `Scripts/Audio/AudioLibrary.cs`: `ContainsSfx`.
- `Scripts/Editor/InterrogationUIBuilder.cs`: `EnsureFont` público.
- Voces recortadas:
  - `Audio/Voices/narrador/narrador_intro_01.wav`
  - `Audio/Voices/taxista/taxista_01..03.wav`
  - `Audio/Voices/wilbur/`: `wilbur_taxi_01`, `wilbur_taxi_02`, `wilbur_casa_01`, `wilbur_arma_01`.

Sin `.meta` nuevos: los genera Unity al importar (paso 1).

## Commits
- `dde8dc6` — dia4(david): INTRO.1 enmiendas A1–A5 tras la revisión de INTRO.0 (doble página, texturas, estéreo, silencios)
- `bfb77ac` — dia4(david): INTRO.1 texturas del libro (libro_portada, libro_guardas, libro_papel) a Art/Intro
- `156fc0f` — dia4(david): INTRO.1 AudioImporter deja amb_* y musica_* en estéreo (voces siempre mono), versión 1
- `d007d4b` — dia4(david): INTRO.1 recorte de silencios en las voces de la intro (≤ 0.1 s, -45 dBFS) + Tools/trim_voice_silence.py
- `ef31b7d` — dia4(david): INTRO.1 escena del libro: libro 3D de hojas rígidas, doble página en RenderTexture (A1), director, saltar/avanzar y menú Intro → Construir escena del libro
- (este audit) — docs: auditoría INTRO.1

No se hizo push. Se trabajó en el checkout principal sobre `integracion/dia4` (sin worktree), así que **no hace falta ningún `git merge --ff-only`**.
