# AUDIT MENU.0 — Reconocimiento e inventario de la pantalla de inicio

ESTADO: PENDIENTE DE REVISIÓN

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4` (checkout principal, sin worktree: no se tocó nada de `Assets/`) · **Alcance:** solo MENU.0 de `Docs/phases/Fase-Menu.md` (v1.1). Solo lectura + copia de 3 imágenes a una carpeta ignorada por git.

**Puerta:** `Fase-Menu.md` MENU.0 no depende de otra auditoría ("puede correr ya"). INTRO.1 sí está cerrada (ver más abajo), así que MENU.1 queda desbloqueada por ese lado.

## 1. Resumen
- La pantalla de inicio actual es la escena `MenuPrincipal` (commits de **Cristian** del 3 nov, aunque la fase la llama "de Edgar"): un fondo ilustrado **de una sola imagen** (tres cerditos + casa + huerto, sin capas ni alfa) que alterna entre 2 cuadros con fundido y "respira". No tiene **ninguna música ni sonido**, ni estado "pulsa cualquier tecla".
- El jazz de Cristian son 3 WAV de 60 s en `Audio/Music/` que **nadie reproduce hoy** y que **no son loops limpios** (0.9–2.3 s de silencio al final, uno con 1.5 s al inicio). El libro de la intro (INTRO.1, cerrada) usa su propia pista `musica_intro` (90 s), no el jazz.
- Hay 4 puntos que MENU.1 debe decidir antes de construir (sección 11): la imagen **no está "abrazada"** (están en pose de pelea), `character_key.py` está **sucio en git** por otra sesión, la **música no se puede pedir por id** (la librería elige variante al azar), y los scripts del menú actuales son de Cristian (hay que **extenderlos**, no duplicarlos).

## 2. Commits
| Hash | Mensaje |
|---|---|
| (ver sección 14) | `dia4(david): MENU.0 …` |

Push: no (CLAUDE.md: solo cuando el prompt lo pide).

## 3. Archivos creados
- `Docs/audits/AUDIT_menu_0.md` — esta auditoría.
- `PLOMO_Y_PAJAV_1/_entrante_menu/actuales/{menu_inicio_01,menu_inicio_02,boton_placa}.png` — copias sin modificar (ignoradas por git, no se suben).

## 4. Archivos modificados
- Ninguno en `Assets/` ni en código. Documentos ya presentes en el repo sin trackear: `Docs/phases/Fase-Menu.md` (v1.1) y `Docs/guiones/menu.md` (Guion-Menu v1.1, estaba como `Guion-Menu.md`; se renombró a `menu.md` como piden la fase y el guion, antes de commitear; el contenido no cambió).

## 5. Plantilla MENU.0

- **Escenas y orden en Build Settings** (`ProjectSettings/EditorBuildSettings.asset`): 0 `Assets/_Game/Scenes/MenuPrincipal.unity` · 1 `Assets/Scenes/Intro.unity` · 2 `Assets/Scenes/NivelPorky.unity`. Ya está en el orden Menú → Intro → Nivel (commit `46286e1`). `SampleScene.unity` existe en disco pero no está en Build. **La escena se llama `MenuPrincipal`, no `MainMenu`** (M1: se reutiliza, no se crea otra). `GameManager.GoToMainMenu()` la carga por ese nombre (`GameManager.cs:84`, si no, índice 0): hay que conservar el nombre.

- **Pantalla de inicio actual: escena, scripts y cómo funciona hoy.**
  - Escena `Assets/_Game/Scenes/MenuPrincipal.unity` (git: `4ed149e` arte, `3f57098` escena, `8646612` "apunta a Intro", los tres de Cristian, 3 nov). Se genera con el menú **Plomo y Paja → Crear menu de inicio** (`Assets/_Game/Editor/CrearMenuInicio.cs`, 311 líneas), que **borra y recrea** `CanvasMenu` y `MenuManager` en cada corrida.
  - `CanvasMenu`: Screen Space Overlay, Scale With Screen Size 1920×1080, match 0.5 (ya cumple M2 en eso). Hijos: `Fondo` (Image + `MenuFrameAnimator`), `PanelPrincipal` (título, botones Jugar/Opciones/Créditos/Salir), panel de Opciones (`OptionsPanel`: slider de volumen + toggle pantalla completa + Volver), panel de Créditos. `MenuManager` lleva `MainMenu`.
  - `Scripts/UI/MainMenu.cs`: `Start()` pone `timeScale=1`, `AudioListener.pause=false`, cursor libre y visible, muestra el panel principal. `Jugar()` = `SceneManager.LoadScene(escenaJuego)` **síncrono, sin fundido, sin sonido** (`escenaJuego` serializado = `Intro` en la escena; el default del código es `NivelPorky`). `Salir()` cierra (Editor: para Play). No hay estado "Pulsa cualquier tecla", ni manejo de teclado propio (↑↓/Enter/Esc), ni Esc para volver.
  - `Scripts/UI/MenuFrameAnimator.cs`: modo **Fundido** (en la escena), 1.6 s por cuadro, 0.8 s de mezcla (capa B con alfa), "respiración" por escala del `RectTransform` ±1.2 % a 0.45 Hz, tiempo sin escala. No hace parpadeos ni mirada de Porky (no hay cuadros por personaje).
  - `Scripts/UI/BotonCartoon.cs` (113 líneas): efecto cartoon de botón; **no reproduce sonido** (no hay `AudioSource`/`PlayOneShot`).
  - `Scripts/UI/OptionsPanel.cs`: guarda `opt_volumen` y `opt_pantalla_completa` en `PlayerPrefs` y los aplica `AfterSceneLoad` (`AudioListener.volume`, `Screen.fullScreen`). Compartido con el menú de pausa.
  - Fuente: la escena usa **LiberationSans SDF** (TMP por defecto), no una fuente con carácter.
  - Escena sin `AudioSource` ni `AudioListener` propio más allá del de la cámara: **silenciosa**.

- **Imágenes de la pantalla de inicio (ruta · tamaño · qué muestra · dónde se usa):**

| Archivo (en `Assets/_Game/Art/UI/Menu/`) | Tamaño · formato | Qué muestra | Dónde se usa |
|---|---|---|---|
| `menu_inicio_01.png` (2.99 MB) | 1672×941 · PNG RGB 8 bit (**sin canal alfa**) | Los **tres cerditos** al frente-izquierda, en pose de pelea (puños arriba, golpeados, vendajes; el de en medio con la escopeta al hombro; el de la derecha sudando), sobre un campo al **atardecer** naranja: gran árbol a la izquierda, casa de ladrillo con ventanas encendidas y chimenea humeando al centro, huerto de rabanitos con cerca a la derecha. A color, línea gruesa, estilo 1930s. | Escena `MenuPrincipal`, GameObject `Fondo`, `MenuFrameAnimator.frames[0]`. |
| `menu_inicio_02.png` (2.74 MB) | 1672×941 · PNG RGB (sin alfa) | Mismo dibujo, **segundo cuadro**: la composición es la misma pero con diferencias visibles (caras, humo, nubes, polvo, posiciones de manos). Está regenerado, no es un calco, por eso se usa el modo Fundido (el modo Hervido "solo se ve bien si son casi idénticos"). | Mismo componente, `frames[1]`. (No lo referencia nada más.) |
| `boton_placa.png` (2.47 MB) | 2172×724 · PNG RGBA | Placa de madera azul-verdosa con borde dorado y dos clavos, vacía (sin texto). | `Image` de los 4 botones del panel principal, del volver de Opciones/Créditos y varios fondos de panel (10 referencias en la escena). |

  - **Importación**: `MenuSpriteImporter` (`Assets/_Game/Editor/`, order 1000) fuerza en `Art/UI/Menu/`: Sprite Single, 100 PPU, **mipmaps off**, sin compresión, máx. 2048, `alphaIsTransparency` off, Bilinear, `FullRect`. Solo afecta a esa carpeta (la nueva `Art/Menu/` de M1/M2 no la cubre).
  - **Otras imágenes con aspecto de pantalla de inicio**: `Art/Intro/libro_portada.png` es la portada del libro de la intro (no es la pantalla de inicio).
  - **Sprites de cerditos por separado**: no existen. No hay `Art/Characters/<id>` de grupo; el arte nuevo del guion (`cerditos_*`, `menu_fondo`, `menu_sombra_lobo`, `menu_logo`) **todavía no existe** en el repo ni en `_entrante_menu/`.

- **¿Cerditos y fondo están en una sola imagen o en capas separadas? ¿Transparencia?:** **Una sola imagen plana por cuadro** (personajes + campo + casa fundidos). No hay capas, **ninguna transparencia** (PNG RGB, sin alfa). Solo `boton_placa.png` tiene alfa y es un elemento de UI, no del dibujo.

- **Copiadas a `_entrante_menu/actuales/`:** **sí** → `PLOMO_Y_PAJAV_1/_entrante_menu/actuales/` (junto a `_entrante_viewmodel/`): `menu_inicio_01.png`, `menu_inicio_02.png`, `boton_placa.png`. Sin modificar (copia con `cp -p`; MD5 igual al original: `54a63e34…`, `7dbd0e12…`, `607b6a4d…`). **¿Lo ignora git?: sí**, regla `.gitignore:92 **/_entrante_*/` (verificado con `git check-ignore -v` en los 3 archivos; `git status --ignored` muestra `!! _entrante_menu/`).

- **Música encontrada (ruta · duración · ¿loop limpio? · ¿cuál es la de Cristian?):**

| Pista (`Assets/_Game/Audio/Music/`) | Duración · formato | Silencio inicio / final (< −40 dBFS) | Empalme del loop | Origen |
|---|---|---|---|---|
| `musica_jazz_01.wav` | 60.000 s · 44.1 kHz · estéreo · 16 bit | 0.00 s / **1.66 s** | ❌ Arranca a pleno nivel en la muestra 0 y termina apagándose a silencio: al repetir hay hueco de ~1.7 s + salto de 0.086 (paso típico 0.016) = posible clic | commit `f789a86` (30 oct, sonidos de Cristian) |
| `musica_jazz_02.wav` | 60.000 s · idem | **1.48 s** / **2.26 s** | ❌ Silencio al inicio y al final: hueco de ~3.7 s por vuelta (sin clic) | idem |
| `musica_jazz_03.wav` | 60.000 s · idem | 0.00 s / **0.88 s** | ❌ Arranca a pleno nivel; hueco de ~0.9 s + salto 0.044 (paso 0.007) | idem |
| `musica_intro.wav` | 90.000 s · idem | 0.00 s / 0.00 s | ⚠️ Sin silencio en los bordes, nivel ambiente parecido al inicio y al final (RMS 0.003 / 0.006), pero salto de muestra 0.067 vs paso típico 0.004 en la unión: puede dar un clic leve. **No se puede confirmar sin escucharlo** (INTRO.0 ya pedía revisarlo de oído) | commit `cbfd150` (3 nov, INTRO.0, audio de Cristian) |

  - Medido con Python/numpy sobre los WAV (RMS por ventana y diferencia de muestra en la unión); eso detecta huecos y saltos, **no** si musicalmente cae en compás: eso requiere oído.
  - Los 3 jazz son **idénticos byte a byte** a los de `PLOMO_Y_PAJAV_1/_entrante_audio/Sonidos/` (MD5 igual, 30 oct 10:55). **No hay un jazz más nuevo** en el repo ni en `_entrante_audio/`; tampoco existen `musica_menu` ni `amb_menu_vinilo` (ids que pide el guion §sonidos). La pista de Cristian **más reciente** es `musica_intro` (3 nov), que es ambiente de la intro, no jazz.
  - **¿Cuál es "la de Cristian"?** Los tres jazz son suyos y de la misma fecha; no hay forma de saber cuál eligió para el menú. Propuesta: `musica_jazz_01` por defecto, confirmarlo con Cristian/David. El guion exige "máx. 0.1 s de silencio al inicio y al final" para loops: **ninguno de los tres lo cumple** (hace falta recortar/empalmar con crossfade o hacer que `MusicPlayer` haga el empalme en código).
  - **Cómo se usan hoy**: ningún script ni escena reproduce `musica_jazz_*`. Solo aparecen en `AudioLibrary.asset` (entrada `musica_jazz` con 3 variantes, creada por `AudioLibraryBuilder`) y en el importador. `AudioLibrary.GetSfx("musica_jazz")` devuelve **una variante al azar sin repetir la anterior** (`AudioLibrary.cs:290`): para fijar una pista, `MusicPlayer` debe cargar el clip concreto o la librería debe exponer una variante fija.
  - **Importación** (`GameAudioImporter`, `AudioImporter.cs:37`): todo lo de `Music/` y prefijo `musica_` se importa **Streaming, Vorbis calidad 0.6** (el `.meta` de jazz_01: `loadType: 2`, `normalize: 1`). Streaming + loop de un `AudioSource` no inserta hueco por sí mismo; el hueco está en el audio.

- **Mixer y grupos:** **no hay AudioMixer** en el proyecto (ningún `.mixer`; ningún `AudioMixer` en scripts; `InterrogationDirector.cs:18` lo confirma). Volumen global = `AudioListener.volume` (`OptionsPanel`) y `AudioLibrary.SfxVolume` (usado por el libro). M10 aplica: volumen general por `AudioListener.volume`, "Música" por el volumen del `MusicPlayer`.

- **Estado de INTRO.1 y cómo maneja la música:**
  - **INTRO.1 está cerrada**: `AUDIT_intro_1_1.md` (rediseño del libro 2D a pantalla completa) lleva `ESTADO: LUZ VERDE (orquestador, 3 nov)`. INTRO.2 e INTRO.2.1+3 (taxi, escopeta) ya existen con estado "pendiente de revisión". Escena `Assets/Scenes/Intro.unity`, directores `Scripts/Intro/StoryBookDirector.cs` (libro) e `IntroCinematicDirector.cs` (taxi).
  - **Música del libro**: `StoryBookDirector` tiene tres `AudioSource` en la escena (`musicSource`, `ambienceSource`, `rainSource`). En `Start` hace `StartLoop(musicSource, data.music)` (`StoryBookDirector.cs:101`), con `data.music = "musica_intro"` (`IntroBookData.cs:41`, `IntroBookBuilder.cs:154`): `loop=true`, **volumen 0** y `Update` lo sube a `musicVolume 0.35 × AudioLibrary.SfxVolume × duck × _musicFade` con `MoveTowards` a 0.6/s (`:323–329`). **Ducking**: −6 dB (`duckDecibels`) mientras `narratorSource.isPlaying`. **Final**: `RampAudio` (`:~295`) lleva `_musicFade` de 1 a 0 mientras sube la lluvia; la lluvia pasa a la escena siguiente por `CarryOverAudio.Carry` (`:226`, clase en `Scripts/Cinematics/CarryOverAudio.cs`, solo para esa fuente de lluvia). El libro carga la siguiente escena él mismo con `LoadSceneAsync`.
  - **Consecuencia para M7**: hoy el libro **arranca su propia música desde cero** y su pista es `musica_intro`, **no el jazz**. Para que el jazz "siga sin cortarse" hay que (a) que el `MusicPlayer` persistente tenga la fuente, (b) que `StoryBookDirector` deje de arrancar `musica_intro` (o la deje como respaldo si no hay `MusicPlayer`) y mande el ducking/fade final al `MusicPlayer`. Puntos exactos a tocar: `StoryBookDirector.cs:101` y `:323–329` (+ el campo `music` de `IntroBook.asset`/`IntroBookBuilder.cs:154`). Decisión pendiente de David: ¿el libro conserva `musica_intro` o continúa el jazz? (la fase dice jazz).
  - **Cómo llega el menú a la Intro hoy**: `MainMenu.Jugar()` → `SceneManager.LoadScene("Intro")` (valor serializado en la escena), síncrono, sin fundido ni sonido.

- **Menú de pausa:** **existe** (`UIManager`, `[Header("Menú de pausa")] pauseMenu`, `resumeButton`, `mainMenuButtonPause`), y el botón de menú ya está cableado a `GameManager.GoToMainMenu` (`UIManager.cs:106`), que pone `timeScale=1`, resetea `GameStateTracker` y carga `MenuPrincipal`. O sea, **"Salir al menú" ya existe en funcionamiento**; M13 queda en verificar la etiqueta ("Salir al menú" exacta, no comprobada: vive en un prefab/escena del nivel) y que el cursor/`AudioListener.pause` queden bien (los arregla `MainMenu.Start`). `UIManager`/`GameManager` son de David y estaban limpios en git.

- **Grano actual (Volumes) y fuentes disponibles (¿ñ y tildes?):**
  - Grano URP: `Settings/TechnicolorVolumeProfile.asset` → FilmGrain activo, tipo 2 (Medium1), intensidad **0.18**, response sin override (0.8). `Settings/NoirVolumeProfile.asset` → tipo 0 (Thin1), intensidad **0.35**, response 0.8. Además `Scripts/Intro/OldFilmFilter.cs` ya implementa **grano a ~24 cuadros/s, motas de polvo y rayas verticales** para el libro (`grainStrength 0.16`, `grainSize 1.6`, "más fuerte que el Film Grain del juego"). **MENU.1 debe evaluar reutilizarlo/extraerlo antes de crear `FilmOverlay`** (regla de CLAUDE.md: no duplicar sistemas); no se verificó cómo se aplica (shader sobre la cámara vs. overlay de UI), y el menú es un Canvas Overlay.
  - Fuentes (`Assets/_Game/Fonts/`): **IM Fell English SDF** (+ TTF Regular/Italic, licencia OFL) y **Special Elite SDF**; además **LiberationSans SDF** (TMP por defecto, la que usa el menú hoy). Las tres son atlas estáticos (`m_AtlasPopulationMode: 0`) y las tres tienen en su tabla de caracteres ñ Ñ ¿ ¡ á é í ó ú (comprobado por código Unicode 241, 209, 191, 161, 225, 233, 237, 243, 250). **No hay fuente "cartoon de título"**: si MENU.1 quiere una, hay que conseguirla; si no, Special Elite (regla M12).

- **`character_key.py`: ¿acepta un set Menu con lienzos 1536×1024?:** **No, hoy no.** Modos actuales: Contador (por defecto), `--single`, `--center [HUD]`, `Objetos`. No existe set `Menu`. El procesamiento de sets usa un lienzo **cuadrado fijo** `CANVAS = 1024` (escala `min(1024/alto, 1024/ancho)`, centrado en X y abajo). `--single` sí acepta cualquier tamaño (recorta magenta sin reescalar ni alinear), pero no agrupa un set ni alinea pares. Hay que **añadir** un set `Menu` con lienzo configurable (1536×1024) y alineación inferior que conserve el tamaño. **Ojo: el archivo está modificado y sin commitear por otra sesión** (`git diff --stat`: +31 −6, y también hay `Art/FX/`, `Art/Props/` y cambios de HUD sin commitear): regla de la fase → **MENU.1 no debe editarlo mientras siga sucio**; se reporta.

- **Sensibilidad del ratón: ¿dónde se lee?:** en **un solo lugar del jugador**: `Scripts/Player/PlayerController.cs:34–35` (`[SerializeField] private float mouseSensitivity = 2f`), usado en `:90–91` (`Input.GetAxis("Mouse X/Y") * mouseSensitivity`). Otras lecturas de ratón **no son sensibilidad del jugador**: `IntroCinematicDirector.cs:438–439` (`lookSensitivity` propio, mirada desde el asiento del taxi) y `ViewmodelMotion.cs:161` (delta crudo para el balanceo del arma). Entonces M10 cumple su condición ("un solo lugar"): se puede aplicar con un multiplicador de `PlayerPrefs` leído en `PlayerController` (archivo de David, limpio) y opcionalmente en el taxi.

- **Riesgos / archivos compartidos que MENU.1 va a tocar:** ver sección 11.

## 6. Criterios de aceptación (MENU.0)
| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1 | Graphify para scripts + búsqueda de escenas/imágenes/audio, sin modificar `Assets/` | ✅ | Se leyó `graphify-out/GRAPH_REPORT.md` (no cubre menú/música); los resultados salen de búsqueda de archivos y lectura puntual de scripts. `git status`: ningún cambio en `Assets/` por esta tarea |
| 2 | Inventario de la pantalla actual con ruta, tamaño, uso y capas/alfa | ✅ | Sección 5 |
| 3 | Copia sin modificar a `_entrante_menu/actuales/` y git la ignora | ✅ | MD5 igual; `.gitignore:92` |
| 4 | Música: ruta, duración, tasa, loop, referencias, mixer | ✅ (loop medido, **no escuchado**) | Sección 5 |
| 5 | Estado de INTRO.1 y manejo de música | ✅ | `AUDIT_intro_1_1.md:1`, `StoryBookDirector.cs` |
| 6 | Pausa, grano, fuentes, `character_key.py`, sensibilidad | ✅ | Sección 5 |
| 7 | Auditoría escrita y detenerse | ✅ | Este archivo |

## 7. Compile check
- Runtime: n/a — no se cambió código.
- Editor: n/a — no se cambió código.
- Warnings nuevos: ninguno.

## 8. Menús a ejecutar (en orden)
Ninguno (MENU.0 es solo lectura).

## 9. Pasos manuales en Unity / en el equipo (David)
1. **Escuchar** `musica_jazz_01/02/03` y decidir cuál va al menú (y si el libro conserva `musica_intro` o continúa el jazz). Pedirle a Cristian, si puede, versiones con ≤ 0.1 s de silencio y empalme limpio (guion §sonidos), o autorizar que `MusicPlayer` haga el empalme en código (cruce de 1.5–2 s entre vueltas).
2. **Rehacer el arte** con las imágenes de `PLOMO_Y_PAJAV_1/_entrante_menu/actuales/` como referencia. Ojo con la nota 1 de la sección 11 (no están abrazados).
3. Confirmar que `Tools/character_key.py` y las carpetas `Art/FX`, `Art/Props`, `Art/UI/HUD` sucias en git pertenecen a una sesión de HUD y esperar a que las commitee antes de MENU.1.
4. Coordinar con **Cristian** (dueño de escenas/UI): MENU.1 reconstruiría su escena `MenuPrincipal`.

## 10. Decisiones tomadas
- La fase pide copiar "las imágenes de la pantalla" → se copiaron las 2 ilustraciones + `boton_placa.png` (también es arte de esa escena y sirve de referencia para remakes), sin tocar el original.
- `Guion-Menu.md` → `Docs/guiones/menu.md` (la fase, el guion y el pedido lo nombran así; `contador.md` ya usa minúsculas).
- La carpeta `_entrante_menu/` se creó en `PLOMO_Y_PAJAV_1/` (donde viven `_entrante_viewmodel/`, `_entrante_audio/`, `_entrante_personajes/`), no en la raíz del repo; así coincide con las reglas del `.gitignore` y con las rutas de `character_key.py` (`ROOT/_entrante_*`).
- No se reprodujo ni modificó ningún audio; las cifras de loop son mediciones, no oído.

## 11. APIs dudosas y riesgos (para MENU.1)
1. **La imagen actual NO es "abrazados" ni de noche/lluvia.** Los tres están en pose de pelea (puños arriba, escopeta al hombro del de en medio, golpeados), de pie sobre un campo al atardecer con casa de ladrillo y huerto. El guion v1.1 §1 y los prompts de §3.1–3.2 piden "group hug", noche, lluvia, tres casitas y farola: **no coinciden con la referencia** que se va a adjuntar. Hay que decidir cuál manda (el prompt de la maestra dice "same arrangement as in the first attached image" → saldría la pose de pelea con un abrazo forzado).
2. **`Tools/character_key.py` sucio en git** (parallel session); no editar, reportar qué necesita: set `Menu` (carpetas `_entrante_menu/Cerditos/`, `/Sombra/`), lienzo 1536×1024 no cuadrado, alineado abajo, sin reescalar. Ver también `Art/UI/HUD`, `Art/FX`, `Art/Props` sin commitear.
3. **Reutilizar, no duplicar:** ya existen `MainMenu`, `MenuFrameAnimator`, `OptionsPanel`, `BotonCartoon` y el builder `CrearMenuInicio` (de Cristian). La fase propone `TitleScreenDirector`, `MenuGroupAnimator`, `GameSettings`: CLAUDE.md pide "extender scripts existentes, no duplicar sistemas". Recomendación: extender `MainMenu` (estados Attract/Main, teclado, fundido, Jugar async), `MenuFrameAnimator` (cuadros por personaje) y `OptionsPanel` (música, sensibilidad), y reutilizar/extraer `OldFilmFilter` para el grano.
4. **`CrearMenuInicio` destruye y recrea** `CanvasMenu`/`MenuManager` en cada corrida: un builder nuevo y el de Cristian se pisarían. Decidir cuál queda como fuente de verdad.
5. **Música por id es aleatoria**: `AudioLibrary.GetSfx("musica_jazz")` elige variante al azar. El `MusicPlayer` debe fijar el clip (p. ej. cargar por nombre exacto) o hay que renombrar/mover la pista elegida a `musica_menu_01`.
6. **Hand-off del libro**: `StoryBookDirector` arranca `musica_intro` por su cuenta (`:101`), duckea (`:323–329`) y hace fade final (`RampAudio`): hay que redirigir esos tres puntos al `MusicPlayer` (archivo compartido; `git status` limpio hoy, repetirlo al empezar MENU.1). Nota: el control de volumen "Música" de Opciones no existe aún; `OptionsPanel` solo maneja `AudioListener.volume`.
7. **Jugar**: hoy es `LoadScene` síncrono. M9 pide fundido 0.8 s + carga async + sonido; no hay `ScreenFade` en la escena del menú (existe `Scripts/Cinematics/ScreenFade.cs`, sin revisar su API).
8. **Escalado del fondo**: las imágenes son 16:9 (1672×941); la fase pide "scale-to-cover" para 16:9/16:10/21:9. No se comprobó cómo está anclado `Fondo` hoy (`m_PreserveAspect: 0` en varias imágenes); verificar en el Editor.
9. **Loops no limpios** (jazz) — ver tabla; sin arreglo previo el jazz sonará con hueco de 0.9–3.7 s por vuelta.
10. **`MenuSpriteImporter`** fuerza mipmaps off en `Art/UI/Menu/`; la fase pide mipmaps on en `Art/Menu/` (carpeta distinta, no choca; si se reusa la carpeta vieja sí).
11. Nombres: la fase habla de `Intro` y `Level 1`; los reales son `Intro` y `NivelPorky`. `MainMenu.escenaJuego` por defecto sigue siendo `NivelPorky` (la escena lo sobreescribe con `Intro`).

## 12. Preguntas para el orquestador
- ¿Cuál jazz va al menú (01/02/03) y el libro continúa el jazz o conserva `musica_intro`?
- ¿Se rehace la pantalla como "abrazados de noche con lluvia" (guion) o se mantiene la composición actual de pelea al atardecer (que es la que existe)?
- ¿Se construye sobre el `MainMenu` y `CrearMenuInicio` de Cristian (recomendado) o se crean los scripts nuevos de la fase?

## 13. Deuda técnica para fases siguientes
- Recortar/empalmar los jazz (o hacer el cruce en código).
- Añadir set `Menu` a `character_key.py` cuando deje de estar sucio.
- Definir fuente de título (hoy LiberationSans en el menú).
- Verificar etiqueta "Salir al menú" del pausa.

## 14. Uso de contexto
- ¿Se leyó `graphify-out/GRAPH_REPORT.md`? sí (7.8 KB; solo lista comunidades de scripts, sin nada de menú/música; la búsqueda se hizo con `Glob`/`grep` sobre `Assets/` y lecturas puntuales).
- Archivos abiertos: `Fase-Menu.md`, `phases/README.md`, `AUDIT_TEMPLATE.md`, `Guion-Menu` (inicio), `MenuPrincipal.unity` (solo greps), `MainMenu.cs`, `MenuFrameAnimator.cs`, `OptionsPanel.cs`, `CrearMenuInicio.cs`/`MenuSpriteImporter.cs` (cabeceras), `StoryBookDirector.cs`/`CarryOverAudio.cs` (tramos), `character_key.py` (cabecera + greps), `GameManager.cs` (tramo), las 3 imágenes de menú.
- Abiertos que no se modificaron y por qué: todos; MENU.0 es solo lectura.

---

## Commits
Ver `git log -1` de la rama `integracion/dia4` (commit local `dia4(david): MENU.0 …`, solo documentos; sin push).
