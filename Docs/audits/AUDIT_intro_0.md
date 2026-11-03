# AUDIT INTRO.0 — Sincronizar con dev e importar assets de Edgar y Cristian

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4` · **Alcance:** solo el paso previo INTRO.0 (no se empezó INTRO.1).
**Docs leídos:** `CLAUDE.md`, `Docs/phases/README.md`, `Docs/phases/Fase-Intro.md`, `Docs/guiones/Guion-Intro.md`.
Nota: en el repo los archivos se llaman `Fase-Intro.md` y `Guion-Intro.md` (no `PHASE-INTRO.md` ni `intro.md`), y la fase 3C es `Fase-3C-Contador.md`. `docs/` y `Docs/` son la misma carpeta (Windows no distingue mayúsculas; git rastrea `Docs/`). Nada faltó.

## Rama y resultado del merge con dev (conflictos y cómo se resolvieron)
- `git status` inicial: **limpio** en `integracion/dia4` (nada que descartar ni preguntar).
- `git fetch origin` y `git merge origin/dev`: **avance rápido (fast-forward)** de `794714b` a `b4f15c4` (6 commits, el PR #4 `integracion/dia4` → `dev`). La rama no tenía commits propios que dev no tuviera (0 adelante / 6 atrás), así que **no hay commit de merge propio** y **no hubo conflictos**.
- Lo que llegó: `Art/Characters/intro/intro_01..06`, `Art/Characters/taxista/{conduce_01,habla_01,habla_02}` y `Art/Characters/hud/*` (con sus `.meta`).

## Ilustraciones de Edgar: de dónde a dónde, tamaños, referencias encontradas
- `git mv` (imagen + `.meta`, GUIDs intactos): `Art/Characters/intro/intro_NN.png` → `Art/Intro/ilus_intro_NN.png` (NN = 01…06, todas `.png`). Se eliminó `Art/Characters/intro.meta` y la carpeta vacía.
- **Tamaño real: las 6 miden 1920×1080 (16:9, apaisadas). El guion espera 1024×1024 (cuadradas).** Ver "Preguntas".
- **Referencias:** se buscaron los 6 GUIDs + el GUID de la carpeta y las rutas (`Characters/intro`, `intro_0N`) en todo `Assets/` y `ProjectSettings/`: **solo aparecen en sus propios `.meta`**. Ninguna escena, prefab, asset ni script las usa. No existe `Art/SpriteSets/intro.asset`. Al viajar el `.meta` con el archivo no se rompe nada.
- **Importador:** `CharacterSpriteImporter` solo trata `Art/Characters/<id>/` como personaje, así que `Art/Intro/` caía en la importación por defecto de Unity. Se añadió una regla `IntroFolder` → `ApplyBook`: textura normal (no Sprite), sRGB, mipmaps activos, Trilinear, aniso 4, Clamp, máximo 2048, **sin compresión**, sin alpha. `GetVersion` 9 → 10 para forzar reimportar. (Los `.meta` movidos aún dicen "Sprite, sin mipmaps": Unity los reescribe solo en la reimportación; no se editaron a mano.)
- El `case "intro"` viejo del importador (pivote al centro, 100 px/m) queda sin uso; no se borró para no tocar más de lo pedido.

## Archivos de Cristian: tabla archivo original → id → carpeta destino
Origen: `F:\Descargas\sonidos_final\Audio\…` (solo se **copió**, los originales no se tocaron). Todos ya traían el nombre exacto del id, **no se renombró nada**.

| Archivo original | Id | Carpeta destino |
|---|---|---|
| Voices/taxista/taxista_01.wav | taxista_01 | Audio/Voices/taxista/ (carpeta nueva) |
| Voices/taxista/taxista_02.wav | taxista_02 | Audio/Voices/taxista/ |
| Voices/taxista/taxista_03.wav | taxista_03 | Audio/Voices/taxista/ |
| Voices/wilbur/wilbur_taxi_01.wav | wilbur_taxi_01 | Audio/Voices/wilbur/ |
| Voices/wilbur/wilbur_taxi_02.wav | wilbur_taxi_02 | Audio/Voices/wilbur/ |
| Voices/wilbur/wilbur_casa_01.wav | wilbur_casa_01 | Audio/Voices/wilbur/ |
| Voices/wilbur/wilbur_arma_01.wav | wilbur_arma_01 | Audio/Voices/wilbur/ |
| SFX/sfx_libro_abrir.wav | sfx_libro_abrir | Audio/SFX/ |
| SFX/sfx_pagina_01.wav, _02, _03 | sfx_pagina (3 variantes) | Audio/SFX/ |
| SFX/amb_intro_cuarto.wav | amb_intro_cuarto | Audio/SFX/ (ahí vive `lluvia_loop`; no hay carpeta de ambientes) |
| SFX/amb_taxi_interior.wav | amb_taxi_interior | Audio/SFX/ |
| SFX/sfx_taxi_frenar.wav | sfx_taxi_frenar | Audio/SFX/ |
| SFX/sfx_limpiaparabrisas.wav | sfx_limpiaparabrisas | Audio/SFX/ |
| SFX/sfx_monedas.wav | sfx_monedas | Audio/SFX/ |
| SFX/sfx_puerta_carro.wav | sfx_puerta_carro | Audio/SFX/ |
| SFX/sfx_taxi_arranca.wav | sfx_taxi_arranca | Audio/SFX/ |
| SFX/musica_intro.wav | musica_intro | Audio/Music/ (ahí vive `musica_jazz_0N`; el constructor de la librería lee SFX y Music) |

Total: **19 archivos**. La carpeta de origen venía con los WAV dentro de `Audio/SFX` y `Audio/Voices/<personaje>`; la música estaba en `SFX/`, se puso en `Music/` por convención del proyecto.

## Archivos no importados (ambiguos / sin id / ya existían) y por qué
- **Texturas del libro (`libro_portada`, `libro_guardas`, `libro_papel`): NO estaban en `sonidos_final`** (la carpeta solo tiene `Audio/`). No se importaron. Sí existen en `F:\Descargas\` (raíz, fuera de la carpeta indicada): `libro_portada.png` 1024×1536, `libro_guardas.png` 1024×1536, `libro_papel.png` **1086×1448** (relación 0.75, no 2:3). Junto a ellas hay otro juego `ilus_intro_01..06` (1254×1254, cuadradas, la 04 es `.jpeg`). No se tocaron porque están fuera de la carpeta que se pidió y hay dos juegos de ilustraciones: decide David.
- **Duplicados exactos (MD5 idéntico) de lo que ya está en el proyecto, no se sobrescribió:** `wilbur_dec_matar`, `wilbur_dec_perdonar`, `wilbur_int_0A`, `wilbur_int_0B`, `wilbur_int_0C`, `wilbur_int_1A`, `wilbur_int_1B`, `wilbur_int_1C` (son del guion del contador y ya están en la librería).
- **`sfx_escopeta_bombeo.wav`:** idéntico byte a byte a `escopeta_bombeo_01.wav` (ya en la librería como `escopeta_bombeo`). El guion §6 dice "si ya existe el de la recarga, se usa ese", así que no se importó. En INTRO.3 usar el id `escopeta_bombeo`.
- Ningún archivo quedó sin id.

## Formato de voces: ¿todas WAV 44.1 kHz mono? ¿se convirtió algo?
- **Sí: las 15 voces de la carpeta de Cristian (7 nuevas + 8 duplicadas) son WAV PCM 16 bit, 44 100 Hz, mono.** Las del proyecto también. **No se convirtió nada** (no hay ffmpeg; no hizo falta).
- SFX y ambientes: todos WAV PCM 16 bit 44.1 kHz. Estéreo: `amb_*`, `musica_intro`, `sfx_libro_abrir`, `sfx_limpiaparabrisas`, `sfx_monedas`, `sfx_pagina_*`, `sfx_puerta_carro`, `sfx_taxi_frenar`. Mono: `sfx_taxi_arranca` (bien para 3D), `sfx_escopeta_bombeo`.
- **Duraciones:** taxista_01 9.68 s, _02 7.36 s, _03 5.04 s; wilbur_taxi_01 5.28 s, _02 1.60 s; wilbur_casa_01 2.48 s; wilbur_arma_01 3.28 s; amb_intro_cuarto y amb_taxi_interior 25.0 s; musica_intro 90.0 s; sfx_limpiaparabrisas 5.0 s.
- **Silencio (el guion pide ≤ 0.1 s al inicio y al final; medido a −40 dBFS, solo informativo, no se recortó):** colas largas en `taxista_01` (0.33 s), `taxista_03` (0.27), `wilbur_arma_01` (0.30), `wilbur_taxi_01` (0.36), `wilbur_taxi_02` (0.31 final / 0.17 inicio), `wilbur_casa_01` (0.26 inicio). Afecta poco: el libro y el taxi esperan el fin del clip + pausas.
- **Bucles:** no se puede comprobar el empalme sin escucharlos; `amb_*` y `musica_intro` miden justo 25.0 / 90.0 s y empiezan/terminan sin silencio (buena señal). Revisar de oído.
- **Aviso para INTRO.1/2:** `GameAudioImporter` solo trata como "largo" (estéreo, streaming) a `Music/` y a los nombres `lluvia_`/`musica_`. Los `amb_*` (estéreo, 25 s) se importarán **forzados a mono y Decompress On Load** (≈2.2 MB cada uno). Funciona; si se quiere estéreo, añadir el prefijo `amb_` a esa regla en INTRO.1 (esa fase ya toca `AudioImporter`). No se tocó aquí.

## Ids de la intro presentes / faltantes (guion §6 y §7)
| Tipo | Presentes | Faltan |
|---|---|---|
| Narrador | `narrador_intro_01..06` (ya en la librería) | — |
| Voces nuevas (7) | `taxista_01/02/03`, `wilbur_taxi_01/02`, `wilbur_casa_01`, `wilbur_arma_01` | — |
| SFX | `sfx_libro_abrir`, `sfx_pagina` (01–03), `sfx_taxi_frenar`, `sfx_limpiaparabrisas`, `sfx_monedas`, `sfx_puerta_carro`, `sfx_taxi_arranca`; bombeo = `escopeta_bombeo` existente | — |
| Ambientes | `amb_intro_cuarto`, `amb_taxi_interior` (lluvia: `lluvia_loop` ya existía) | — |
| Música | `musica_intro` | — |
| Ilustraciones | `ilus_intro_01..06` (1920×1080) | Ninguna falta, pero **no son 1024×1024** |
| Texturas del libro | — | **`libro_portada`, `libro_guardas`, `libro_papel`** (existen en `F:\Descargas\`, sin importar) |
| Sprites del taxista | `conduce_01`, `habla_01`, `habla_02` en `Art/Characters/taxista/` (1024×1024, llegaron con el merge) | — (falta el `case "taxista"` del importador y su set, tarea de INTRO.2) |

## Menús que David debe correr en Unity (en orden)
1. Abrir el proyecto y esperar la importación (genera los `.meta` de los 19 WAV, de `Voices/taxista/` y de `Art/Intro/`, y reimporta `ilus_intro_NN` como textura normal con la regla nueva).
2. **Plomo y Paja → Reconstruir Librería de Audio** (registra las voces del taxista, las nuevas de Wilbur y los SFX/ambientes/música).
3. **Plomo y Paja → Reconstruir Sets de Sprites** (crea el set `taxista`; todavía con la regla por defecto, INTRO.2 añade el `case`).
4. Hacer commit de los `.meta` y `AudioLibrary.asset`/set generados (no los generó este agente porque Unity no se puede correr aquí).
5. Escuchar los bucles `amb_intro_cuarto`, `amb_taxi_interior` y `musica_intro` para confirmar que empalman.

## Archivos compartidos tocados (y si estaban limpios en git status)
- `Scripts/Editor/CharacterSpriteImporter.cs` — **estaba limpio** (`git status` sin cambios antes y después del pull). Solo se añadió la regla `Art/Intro/` y se subió `GetVersion` a 10.
- `AudioImporter.cs`, `EditorBuildSettings.asset`, `GameStateTracker.cs`, `CLAUDE.md`, `AudioLibrary.asset`: **no se tocaron**.

## Preguntas para el orquestador / decisiones
1. **Ilustraciones de Edgar 1920×1080 vs 1024×1024 del guion.** Las páginas del libro son cuadradas/verticales; una imagen 16:9 se estira o se recorta. Hay otro juego cuadrado (1254×1254) en `F:\Descargas\`. ¿Cuál se usa? Sin respuesta, el constructor de INTRO.1 tendrá que recortar al centro.
2. **Texturas del libro:** ¿se importan las de `F:\Descargas\`? (`libro_papel` es 1086×1448, no 1024×1536.)
3. Decisión tomada: compresión "Uncompressed" para las texturas del libro (opción más fiel de "alta calidad / sin compresión"; ≈85 MB en memoria con las 9 texturas, aceptable en PC).
4. Decisión tomada: no importar duplicados exactos y respetar la indicación del guion para el bombeo.

## Qué no se pudo probar
- Unity no se puede abrir aquí: no se probó la importación ni la librería.
- Compile check: los 25 fuentes `Editor/**` compilan **sin errores** con el Roslyn del Editor 2022.3.62f3 contra las DLL del proyecto (`Library/ScriptAssemblies`). El ensamblado de runtime no cambió.

## Aislamiento
El trabajo de archivos se hizo en el worktree `.claude/worktrees/intro0` (rama `worktree-intro0`) y se llevó a `integracion/dia4` con avance rápido. No se hizo push.

## Commits
- (a) Merge con dev: avance rápido a `b4f15c4`; no genera commit propio.
- (b) `4aa693f` — dia4(david): INTRO.0 ilustraciones de Edgar a Art/Intro (ilus_intro_01..06)
- (b′) `1c2111f` — dia4(david): INTRO.0 regla de importación para Art/Intro (texturas del libro)
- (c) `cbfd150` — dia4(david): INTRO.0 audio de Cristian para la intro (taxista, wilbur, SFX, ambientes, música)
- (d) este audit — docs: auditoría INTRO.0
