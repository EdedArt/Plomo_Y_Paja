# AUDIT MENU.1 — Construcción de la pantalla de inicio

ESTADO: INTEGRADA, PENDIENTE DE PRUEBA EN UNITY

**Fecha:** 3 nov 2026 · **Rama:** `worktree-menu-1` (sale de `e8bfd47`, la punta de `integracion/dia4`) · **Sin push.**

> **Unity estuvo cerrado.** El C# compila con Roslyn, pero **ningún menú se corrió y nada se probó en Play**. Donde abajo dice "por código", significa que se leyó y razonó, no que se vio funcionar.

## Lo que David debe decidir o saber primero

| # | Hallazgo | Qué se hizo | Qué falta |
|---|---|---|---|
| 1 | **M12 NO se cumple:** el botón de la pausa dice `MENU PRINCIPAL`, no "Salir al menú" (`CrearMenuPausa.cs:112` y el prefab `Resources/PauseMenu_Canvas`) | Nada, como se pidió: solo verificar y reportar | Decidir. Arreglo: cambiar ese texto y correr **Plomo y Paja → Crear menu de pausa** |
| 2 | **El arte no coincide con el encargo:** se pidieron 8 JPG. Hay 5 JPG y 3 PNG (`menu_inicio_05`, `menu_inicio_06`, `menu_rayo_02`; 8.5 MB cada uno, RGBA con alfa 255 en todo) | Se commitearon tal como llegaron, sin recomprimir. El builder acepta cualquier formato | Si los quieres en JPG, reexpórtalos y vuelve a correr el menú |
| 3 | **Hay un noveno archivo:** `menu_inicio_07.jpg`, idéntico al `03` (mismo MD5). El encargo dice 01..06 | No se hizo stage ni se borró; sigue en tu checkout. El builder salta cuadros idénticos y avisa | Borrarlo, o reemplazarlo si esperabas otro dibujo |
| 4 | **Estrellita ✦ abajo a la derecha** (parece marca de agua del generador) en `menu_inicio_05`, `menu_inicio_06` y `menu_rayo_02`. Se vio en una miniatura de 480 px; en los otros cinco no se notó | Nada | Revisar a tamaño completo; retocar si se ve en pantalla |
| 5 | **Fase-Menu v1.3 no estaba en el disco** (solo la v1.2 sin commitear) | Se redactó la v1.3 sobre la v1.2 con las decisiones de este encargo (M3, M4, M12, M13, *Touches*) | Si el orquestador tiene un texto v1.3, reemplazar el archivo |
| 6 | **Cambio fuera de *Touches*:** `GameManager.cs:40` alternaba la pausa con Esc en cualquier escena, y el objeto sobrevive al volver del nivel | Guarda: sin pausa con Esc en `MenuPrincipal` ni `Intro` (las mismas escenas que excluye `PauseMenuSpawner`). No se reconstruyó la pausa | Revisar que estés de acuerdo. Sin esto: Esc en Opciones congelaba el tiempo, un segundo Esc bloqueaba el cursor, y saltar el libro con Esc dejaba `NivelPorky` con `timeScale` 0 |

## Plantilla MENU.1

- **Menús que David debe correr (en orden):**
  1. Integrar la rama al checkout principal (ver "Pasos manuales") y abrir Unity. Esperar la importación.
  2. **Plomo y Paja → Crear menu de inicio**. Ahora abre `MenuPrincipal` y la guarda sola.
  3. Cuando llegue audio nuevo: **Plomo y Paja → Reconstruir Librería de Audio**, y otra vez el paso 2.

- **Scripts de Cristian extendidos (qué se agregó a cada uno) y archivos nuevos:**
  - `MainMenu`: estados Espera → Rayo → Menu → Cargando, prompt que late, entrada escalonada de botones, Esc para volver, Jugar async con fundido, bamboleo y bajada del logo. Default de `escenaJuego` = `Intro`. Sin campos renombrados; si la escena no se regeneró, va directo al menú como antes.
  - `MenuFrameAnimator`: segundo grupo `framesTormenta`, `CutToStorm()` (corte seco), cobertura sin deformar, parallax ±12 px, `SetExtraOffset` para la sacudida. Sin cuadros: fondo oscuro y aviso, no un rectángulo blanco.
  - `OptionsPanel`: sliders Música y Sensibilidad, `OptionsPanel.Sensibilidad`, `PlayerPrefs.Save()` al cerrar el panel.
  - `BotonCartoon`: selección compartida entre ratón y teclado, `sfx_ui_hover` / `sfx_ui_click`, `PlayIfPresent`. **También afecta a los botones del prefab de pausa** (mismo script): quedan con selección y los mismos sonidos, mudos si faltan.
  - `CrearMenuInicio`: arma todo lo anterior, sigue siendo idempotente, crea el panel de Créditos (antes el botón abría Opciones como "provisional"), textos exactos del guion, IM Fell English y lista de faltantes en consola.
  - **Nuevos:** `Cinematics/MusicPlayer.cs`, `UI/LightningFlash.cs` (con `.meta` y GUID fijo).

- **Estados (pulsa cualquier tecla → rayo → botones): ¿funcionan con ratón y teclado?** Por código, sí: `Input.anyKeyDown` incluye clics; se ignora la entrada durante el rayo; los botones entran desde 0.6 s, con 0.08 s de escalón. Teclado con el `EventSystem`: `InputManager.asset` ya tiene `Vertical` con ↑↓/WS, `Submit` con Enter y `Cancel` con Esc. **No probado en Play.**

- **Rayo: secuencia del destello, ¿corta a menu_rayo? ¿qué pasa si falta el arte o el trueno?** Alfa 0 → 0.9 (0.04 s) → 0.2 (0.06 s) → 0.8 (0.04 s) → 0 (0.35 s); corte seco en el primer pico; sacudida ±6 px durante 0.25 s, decreciente. Sin `menu_rayo_*`: destello y trueno sobre la imagen de espera, con aviso. Sin `sfx_trueno`: usa `trueno` (ya existe). Sin ninguno: solo el destello.

- **Relámpagos de ambiente: intervalo y volumen:** cada 12–20 s al azar, pico 0.35, subida 0.06 s y bajada 0.45 s. Sonido `sfx_trueno_lejos`; si falta, `sfx_trueno` o `trueno` al 40 %. Se detienen al pulsar Jugar. Hoy sonará `trueno` al 40 %.

- **Grano/ruido: ¿se reutilizó OldFilmFilter o se extrajo FilmOverlay? valores usados:** **se reutilizó `OldFilmFilter`**, sin cambiar el script: ya funciona como `Image` en un Canvas Overlay propio. Material `IntroOldFilm.mat`, canvas `UI_Pelicula` (orden 60). Grano 0.06 · parpadeo ±2 % · polvo 0.06 / fuerza 0.35 · rayas 0.06 / fuerza 0.15 · viñeta 0.2 · tinte blanco (sin sepia, para no alterar el atardecer) · sin temblor de ventanilla. **Se debe juzgar a ojo** ("se siente, no se nota"); todo queda serializado.

- **Música: ¿musica_intro sigue del menú al libro sin reiniciar? ¿se baja con el narrador? ¿se apaga al final del libro? ¿hay clic en el loop?** Por código: `MusicPlayer` es `DontDestroyOnLoad` y el libro no arranca su música si ya suena, así que no se reinicia. El libro le envía −6 dB bajo el narrador y su fundido final (`SetBookMix`) y lo apaga en `Leave`. Además, se apaga solo en cualquier escena fuera de `MenuPrincipal`/`Intro`. **Clic del loop: sin escuchar** (MENU.0 midió un salto de muestra 0.067 contra 0.004 típico). Si hace clic, poner `loopCrossfadeSeconds` en 1–2 en el objeto `MusicPlayer` (default 0, apagado). No se implementó el crujido de vinilo (opcional).

- **Abrir Intro directo en el Editor: ¿el libro sigue sonando con su propia música?** Por código, sí: sin `MusicPlayer` se conserva `StartLoop(musicSource, …)`, ahora multiplicado por el volumen de la opción Música.

- **Jugar → Intro: ¿sin tirón ni silencio?** Por código: la escena carga async desde el clic y solo se activa con la pantalla ya negra; el volumen base (0.35) es igual al del libro. **Falta medirlo en Play.** Sonido: `sfx_ui_jugar` → `sfx_escopeta_bombeo` → `escopeta_bombeo` (el que existe hoy).

- **Opciones: ¿se guardan música y sensibilidad? ¿se aplica la sensibilidad?** Sí por código: `opt_musica` y `opt_sensibilidad` en `PlayerPrefs`. La sensibilidad (0.25–2, default 1) multiplica en `PlayerController:90-91` y en la mirada del taxi (`IntroCinematicDirector:438-439`). **Falta reiniciar el juego y comprobarlo.**

- **16:9 / 16:10 / 21:9: ¿la imagen cubre la pantalla sin deformarse?** Por cálculo, sí: tamaño según el aspecto del sprite (1.792) y el padre real, recalculado si cambia, con margen de 38 px para parallax y sacudida; la respiración ya no baja de escala 1. **No se ha visto en pantalla.**

- **Pausa → Salir al menú: texto exacto, cursor, música (sin duplicar MusicPlayer):**
  - **Texto: ❌** dice `MENU PRINCIPAL` (hallazgo 1).
  - **Cursor: ✅ por código.** `PauseMenu.MenuPrincipal` reanuda y bloquea el cursor; `MainMenu.Start` lo libera.
  - **`AudioListener.pause`: ✅ por código.** La pausa nunca lo activa y `MainMenu.Start` lo deja en false.
  - **Música: ✅ por código.** En el nivel no queda `MusicPlayer`; el menú crea uno y cualquier copia extra se destruye en `Awake`.

- **Ids faltantes y placeholders:**

| Id | Estado | Placeholder |
|---|---|---|
| `sfx_trueno` | falta | `trueno_01.wav` (el del nivel) |
| `sfx_trueno_lejos` | falta | `trueno` al 40 % |
| `sfx_ui_hover`, `sfx_ui_click` | faltan | mudo |
| `sfx_ui_jugar` | falta | `escopeta_bombeo` (el guion dice `sfx_escopeta_bombeo`, pero el id real no lleva `sfx_`) |
| `amb_menu_vinilo` | falta | sin implementar (opcional) |
| `menu_logo` | falta | TMP "PLOMO Y PAJA" en IM Fell English, crema, con contorno, sombra y bamboleo |
| "·" de los créditos | no está en el atlas de IM Fell ni de Special Elite | debería caer en LiberationSans, la fuente por defecto de TMP. **Mirar que no salga □** |

- **Archivos compartidos tocados (y si estaban limpios en git status):** `StoryBookDirector.cs`, `OptionsPanel.cs`, `PlayerController.cs`, `GameManager.cs`, `IntroCinematicDirector.cs`, `MainMenu.cs`, `MenuFrameAnimator.cs`, `BotonCartoon.cs`, `CrearMenuInicio.cs`. **Todos limpios** en el `git status` del checkout principal (3 nov, 17:34: solo había documentos y arte), y trabajados en un worktree aparte. No se tocaron `IntroBookBuilder.cs` (no hizo falta), `AudioLibrary.cs`, `AudioImporter.cs`, `MenuSpriteImporter.cs`, `OldFilmFilter.cs`, `UIManager.cs`, `PauseMenu.cs` ni `CrearMenuPausa.cs`.

- **Qué no se pudo compilar/probar y qué debe revisar David en el Editor:** todo el comportamiento en Play. Compilación abajo. Revisar en especial:
  - el ciclo de 6 cuadros: son redibujos con diferencias visibles (caras, ojos), a 1.6 s por cuadro; juzgar si el fundido se ve bien;
  - la legibilidad del título y de "Pulsa cualquier tecla" sobre la imagen;
  - el grano;
  - el empalme del loop;
  - el aviso `[Menu] … es identico a …` mientras exista el `07`.

- **Archivos creados/modificados:** creados: `MusicPlayer.cs`, `LightningFlash.cs` (+ `.meta`), los 8 cuadros y esta auditoría. Modificados: los 9 scripts de "Archivos compartidos", `Fase-Menu.md`, `menu.md` y `AUDIT_menu_0.md`. Borrados: los 2 PNG viejos y sus `.meta`.

- **Commits:** ver abajo.

## Arte e importación

- **Tamaño:** los 8 miden **2752×1536** (aspecto 1.792), medidos con PIL, y también el `07`. Los 8 tienen el mismo tamaño ✅.
- **`MenuSpriteImporter`:** por lectura de código, importa los JPG **sin compresión** ✅: filtra solo por carpeta, no por extensión, y fija `TextureImporterCompression.Uncompressed`. No se vio en Unity.
- **Ojo, `maxTextureSize = 2048`:** en Unity quedan en unos 2048×1143 y ocupan unos **63 MB de RAM** los 8 (estimación). Basta para 1080p; no se cambió.
- **`MenuFrameAnimator` nunca toma `pausa_*` ni `boton_placa`:** el animador usa las listas que le da el builder, y el builder solo acepta `^menu_inicio_NN` y `^menu_rayo_NN` en orden numérico.
- **PNG viejos:** `menu_inicio_01/02.png` y sus `.meta` borrados. La copia en `_entrante_menu/actuales/` se comprobó antes (MD5 igual). Sus GUID solo los usa `MenuPrincipal.unity`, que el builder regenera.

## Compile check
Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`), con referencias, defines y LangVersion de los `.csproj` del checkout principal y fuentes del worktree. El Editor compila contra el runtime recién generado:

| Assembly | Fuentes | Resultado |
|---|---|---|
| `Assembly-CSharp` | 106 | **0 errores, 0 avisos** (CS0649, CS0169 y CS0414 ocultos) |
| `Assembly-CSharp-Editor` | 32 | **0 errores, 0 avisos** |

Además, las 34 propiedades de `FindProperty` del builder se contrastaron con los campos serializados: ninguna sin campo.

## APIs dudosas
- Fallback de TMP para "·": comportamiento recordado, no comprobado en esta versión.
- `Slider` sin handle y selección de teclado con `ColorTint`: revisar que el foco se note en Opciones.
- `Selectable` con navegación automática: revisar que el orden ↑↓ de Opciones sea el esperado.

## Pasos manuales (David)
1. **Integrar en tu checkout principal**, en `integracion/dia4`. Tus copias sin trackear chocarían con el merge, así que primero se quitan (son idénticas a las commiteadas; **no** toca `menu_inicio_07.jpg`):
   ```bash
   git restore Docs/phases/Fase-Menu.md
   cd PLOMO_Y_PAJAV_1/Assets/_Game/Art/UI/Menu
   rm menu_inicio_01.jpg menu_inicio_02.jpg menu_inicio_03.jpg menu_inicio_04.jpg menu_inicio_05.png menu_inicio_06.png menu_rayo_01.jpg menu_rayo_02.png
   cd -
   git merge --ff-only worktree-menu-1
   ```
2. Borrar `Docs/guiones/Guion-Menu.md` y `Docs/phases/Guion-Menu.md`: siguen sin trackear, y ya están copiados en `Docs/guiones/menu.md`. Decidir sobre `menu_inicio_07.jpg` (hallazgo 3).
3. Correr los menús de arriba y probar con la lista de MENU.2.
4. **Commitear lo generado:** los `.meta` de los 8 cuadros (Unity los crea al importar) junto con `Assets/_Game/Scenes/MenuPrincipal.unity`. Conviene hacerlo antes de que otra persona abra el proyecto, porque si no le saldrán otros GUID.
5. Completar el apellido de Cristian en los créditos (`CrearMenuInicio.TextoCreditos`, como pide el guion).

## Decisiones tomadas
- Se trabajó en un worktree, por eso la rama es `worktree-menu-1` y no `integracion/dia4`. Se apuntó a `e8bfd47` antes de empezar.
- Se usó el trueno y el bombeo que ya existen como placeholders, para que el rayo no quede mudo.
- Navegación con el `EventSystem` en vez de un manejo propio del teclado: así los sliders y el toggle de Opciones funcionan sin código extra.
- El título conserva el salto de línea de Cristian ("PLOMO / Y PAJA").
- `Guion-Menu.md` se copió tal cual sobre `menu.md`. Su texto todavía habla de dos cuadros; no se editó, porque es la versión del proyecto.
- Graphify: `GRAPH_REPORT.md` es del 30 oct y no cubre menú ni música (ya lo señaló MENU.0), así que la exploración siguió con búsqueda de archivos y lectura puntual de los scripts de *Touches*.

## Commits
| Hash | Mensaje |
|---|---|
| `d8c981d` | `dia4(david): MENU.0 con luz verde; Fase-Menu v1.3 y Guion-Menu v1.2 en el repo` |
| `8801486` | `dia4(david): MENU.1 arte del menu: 6 cuadros de espera y 2 de tormenta, sin los PNG viejos` |
| `b62b3ef` | `dia4(david): MENU.1 pantalla de inicio: espera, rayo, botones con teclado, musica_intro hasta el libro, grano, opciones y creditos` |
| (este) | `dia4(david): MENU.1 auditoria` |
