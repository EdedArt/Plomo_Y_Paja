     ESTADO: LUZ VERDE (orquestador, 3 nov)

# AUDIT INTRO.1.1 — Rediseño del libro (2D a pantalla completa, enmienda A7)

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4` (checkout principal, staging por ruta explícita) · **Alcance:** solo A7.
**Docs leídos:** `CLAUDE.md`, `Docs/phases/README.md`, `Docs/phases/Fase-Intro.md` (§1b), `Docs/guiones/Guion-Intro.md`, `Docs/audits/AUDIT_intro_1.md`.
**Puerta:** `AUDIT_intro_1.md` no tiene `ESTADO: LUZ VERDE`. Se siguió porque el prompt pide el rediseño tras la prueba de David.

> **Unity no se abrió en esta sesión.** El C# compila (ver abajo). **Los shaders no se pudieron compilar aquí**, el menú no se ejecutó y nada se probó en Play.

## Qué se borró y qué se reutilizó
**Borrado:**
- Código: `Scripts/Intro/StoryBook.cs` y `Scripts/Editor/IntroBookBuilder.Book.cs` (libro 3D, hojas, anclas de cámara, portadilla).
- Lo generado por el menú de INTRO.1 (nunca estuvo en git; se borró del disco con su `.meta`):
  - `Prefabs/Intro/StoryBook.prefab`
  - `Materials/Intro/Libro_{Cuero,Canto,Tapa,Guardas,Papel,Pagina}.mat`
  - `Art/Environment/Generated/IntroLamparaAceite.asset` + las 5 mallas `RBox_*` de la mesa, las patas, el piso y el libro
  - `Settings/IntroVignetteProfile.asset` (la viñeta ahora la hace el filtro)
  - `Assets/Scenes/Intro.unity` (la regenera el menú)
- En la escena ya no hay cuarto, mesa, lámpara, luz de relleno, Volume ni cámara en mano.

**Reutilizado:**
- `IntroBookData` / `IntroBook.asset`: mismos textos exactos e ids de audio. Se quitaron `coverTexture`, `endpaperTexture`, `titlePageTitle` y `titlePageSubtitle`, y se añadió `closedBookTexture`. `paperTexture` ahora es `hoja_papel`.
- `StoryBookDirector`: mismo flujo, mismas mezclas y la misma salida. Lo que cambió: apertura con acercamiento, pase con rizo y acercamiento final por `uvRect`.
- `SpreadCompositor` → **`PageCompositor`** (`git mv`): misma idea de 2 rigs y 2 RT que se turnan, ahora con una página por RT de 1920×1080.
- Sin cambios: `HoldToSkip`, `ScreenFade`, `CarryOverAudio`, `CinematicEase`, la carga async desde la página 5, la máquina de escribir con `maxVisibleCharacters`, los valores de mezcla (música 0.35 con −6 dB, ambiente 0.5, lluvia 0.4, entrega de 2.5 s) y el trabajo de A3/A4.
- `InterrogationUIBuilder.EnsureFont`: se generalizó con una sobrecarga `(report, rutasTTF, rutaAsset, nombre)`. La versión vieja `EnsureFont(report)` sigue igual para Special Elite.
- Texturas viejas `libro_portada`, `libro_guardas` y `libro_papel`: **no se borraron** (el prompt no lo pide). Quedan sin uso.

## Menús que David debe correr
1. Abrir Unity y esperar la importación. Se importan `libro_cerrado`, `hoja_papel`, el TTF de IM Fell, los 2 shaders y los scripts nuevos.
   - **Revisar la consola:** no debe haber errores de los shaders `PlomoYPaja/Intro/PageCurl` y `PlomoYPaja/Intro/OldFilm`.
   - La escena `Intro` que estaba abierta ya no existe. Si Unity se queja, abrir `NivelPorky`.
2. **Plomo y Paja → Reconstruir Librería de Audio**, si aún no se corrió después de INTRO.1. `AudioLibrary.asset` tiene cambios sin commit en el working copy: no los toqué.
3. **Plomo y Paja → Intro → Construir escena del libro.** El diálogo final muestra:
   - la fuente,
   - los materiales,
   - Build Settings antes/después,
   - la lista de faltantes.
4. Abrir `Assets/Scenes/Intro.unity` y darle Play (lista de pruebas al final).
5. Hacer commit de lo generado:
   - los `.meta` nuevos,
   - `Intro.unity`,
   - `IntroBook.asset`,
   - `Fonts/IMFellEnglish SDF.asset`,
   - `Materials/Intro/IntroPageCurl.mat` y `IntroOldFilm.mat`,
   - `ProjectSettings/EditorBuildSettings.asset`.

## Imágenes faltantes y placeholders
- **No falta ninguna.** `libro_cerrado.png` y `hoja_papel.png` se **copiaron** de `F:\Descargas` (MD5 idéntico; los originales siguen ahí). Las dos miden 1672×941 (≈16:9). Las 6 `ilus_intro_0N` miden 1920×1080. Las importa la regla de INTRO.0 para `Art/Intro`.
- Placeholders programados por si falta algo:
  - **Sin `libro_cerrado`:** rectángulo rojo oscuro `#4E1210` con «PLOMO Y PAJA» en dorado (TMP).
  - **Sin `hoja_papel`:** crema liso `#EEDFBF`. El dorso de la hoja que gira queda blanco con el tinte.
  - **Sin ilustración:** gris con «ILUSTRACIÓN 0N».
  - **Sin shader:** ver «Pase de página» y «Filtro» más abajo.
  - **Sin audio:** silencio, y el texto usa una duración estimada.

## Fuente usada y caracteres verificados
- **IM Fell English** (`Fonts/IMFellEnglish-Regular.ttf`, estaba en el working copy sin commit; ahora sí está en git junto con `OFL.txt`). El menú crea **`Fonts/IMFellEnglish SDF.asset` estático**, con el mismo método y el mismo juego de caracteres que Special Elite: ASCII + `¡¿…—–«»“”‘’áéíóúüñÁÉÍÓÚÜÑ`.
- Revisé la tabla `cmap` del TTF con un script: **están todos** (ñ ¿ ¡ á é í ó ú É … y el ASCII completo).
- **Respaldo:** si no está el TTF o TMP no puede generar el asset, el libro usa Special Elite (58 px) y el diálogo lo avisa. **No hizo falta.**
- `IMFellEnglish-Italic.ttf` también está en `Fonts/` sin commit. No se usa y no lo subí.

## Pase de página: ¿curl real o respaldo? cómo funciona
**Curl real, cilíndrico** (shader `Shaders/IntroPageCurl.shader`, UI sin iluminación, va en el RawImage a pantalla completa):
- **Entradas:** `_MainTex` = RT de la página actual y `_NextTex` = RT de la siguiente, ya compuesta con el texto oculto un cuadro antes. `_Progress` va de 0 a 1 en **0.8 s** con suavizado de entrada y salida (`CinematicEase.InOut`).
- **La línea del pliegue** barre de derecha a izquierda, inclinada 10°, así que la esquina de abajo a la derecha se levanta primero.
  - El papel a la derecha de la línea se enrolla en un cilindro (radio 0.13 del alto).
  - Lo que pasa de media vuelta queda plano encima, boca abajo: es el **dorso**, `hoja_papel` en espejo y aclarado con `_UnderTint` (1.08, 1.05, 0.98).
- **Iluminación:** las dos caras del cilindro se oscurecen de canto (`_FoldShade` 0.35) y llevan un **brillo** en el pliegue (`_Highlight` 0.22).
- **Sombras:**
  - Donde ya no hay papel se ve la página siguiente con una **sombra suave** junto al pliegue (`_ShadowStrength` 0.45, ancho 0.09).
  - El dorso echa una sombra leve sobre la página actual (`_FlapShadow` 0.3).
  - Las dos sombras se apagan al final del pase.
- **Comprobado con una simulación numérica** de la misma fórmula: con progreso 0 se ve solo la página actual, con 1 solo la siguiente, y en medio el cilindro y el dorso en el orden correcto.
- **Apertura:** el mismo pase lleva de `libro_cerrado` a la página 1 en 1.0 s con `sfx_libro_abrir`. Entre páginas suena `sfx_pagina` al azar (sin repetir la anterior).
- **Respaldo programado (`PageCurlView`):** si el material falta o la tarjeta no soporta el shader, la página cambia de golpe a mitad del pase y la consola avisa una vez. No se eligió de entrada: el rizo es matemática 2D por píxel, sin malla, y no depende de nada raro.
- Nota del modelo: es un «peel» clásico. A mitad del pase, el dorso claro tapa buena parte de la página que se va (como un CIPageCurl). Si se ve demasiado papel claro, bajar `_Radius` o subir `_Angle` en `Materials/Intro/IntroPageCurl.mat`.

## Filtro de película: efectos y valores por defecto
Es una `Image` a pantalla completa en su propio Canvas Overlay, **`UI_Pelicula` (orden 60)**:
- Queda encima de la página (0) y del fundido (50). Debajo solo queda el aviso de saltar (70).
- Usa el shader procedural `Shaders/IntroOldFilm.shader`, sin texturas. Mezcla **«multiplicar ×2»** (`Blend DstColor SrcColor`): oscurece y aclara la imagen sin leer la pantalla (URP no tiene GrabPass).
- **No depende del Volume del juego:** la escena `Intro` ya no tiene Volume.
- Todo es ajustable en el Inspector de `OldFilmFilter`, también en Play. Sin basura por cuadro: ids cacheados, `SetFloat`/`SetVector`/`SetColor` y `Random`.

| Efecto | Valores por defecto |
|---|---|
| Grano animado | 24 cuadros/s, fuerza 0.16 (±16 % por grano), tamaño 1.6 px, dos escalas mezcladas |
| Polvo | 8 motas posibles por cuadro, probabilidad 0.18 cada una, radio ≤ 0.006 del alto, fuerza 0.7. 75 % oscuras y 25 % claras |
| Rayas verticales | 3 posibles, probabilidad 0.22 por tramo de 9 cuadros, ancho 0.0012 del ancho, fuerza 0.35. Cortadas a lo alto, con leve vaivén |
| Gate weave | mueve la página ±1.5 px (redondeado; vertical ×0.6) a 12 cuadros/s. La página se agranda 0.37 % para que no asome el borde |
| Parpadeo cálido | exposición ±3 % por cuadro de película, suavizado (18/s). Reemplaza a la lámpara |
| Viñeta | fuerza 0.55, radio 0.85, suavidad 0.55 (algo ovalada para 16:9) |
| Tinte sepia | multiplica por (1.04, 0.97, 0.84) |

## ¿Cabe el texto más largo en 3 líneas?
**Sí, en 2.** Medí los anchos de avance reales de `IMFellEnglish-Regular.ttf` (tablas `hmtx`/`cmap`) con el mismo corte por palabras:
- **Banda de texto:** 86 % del ancho (1651 px a 1080p), entre el 3.5 % y el 31.5 % del alto (302 px).
- **Tamaño elegido: 64 px.** `narrador_intro_03` queda en **2 líneas** (1606 y 1492 px). El resto queda en 1 o 2 líneas.
- **Margen:**
  - A 68–88 px seguiría en 3 líneas.
  - 3 líneas a 64 px ocupan unos 244 px de los 302.
  - Con Special Elite (respaldo, 58 px) también serían 3 líneas.
- La medida es con las métricas del TTF, no con TMP en pantalla: **David debe confirmarlo en Play.**

## Qué no se pudo probar y qué debe revisar David
- **C# compilado: sí.** Roslyn de Unity 2022.3.62f3 (`DotNetSdkRoslyn/csc.dll`, con el `dotnet` de Unity), contra las referencias y los defines de los `.csproj`, con todos los `.cs` actuales:
  - runtime (85 fuentes): **0 errores, 0 avisos**;
  - editor (26 fuentes): **0 errores, 0 avisos**.
- **Shaders: no se compilaron** (no hay compilador de shaders sin Unity). Si alguno falla, el menú lo dice («✗ Shader … no encontrado») y el juego usa los respaldos: sin rizo (cambio a mitad del pase) y sin filtro.
- **Sin probar:** todo lo de Play y el propio menú. Revisar:
  1. **Orientación de la página en el RT** (que no salga invertida en vertical). Si sale al revés, es el `uvRect` de `PageCurlView` o el muestreo `_MainTex` en el shader.
  2. **Que el rizo se lea bien** (radio, ángulo, cuánto dorso se ve) y que no haya «dientes» en el borde del papel. Los bordes del rizo no tienen antialias.
  3. **Fuerza del filtro.** Si el grano o la viñeta tapan demasiado, bajarlos en `UI_Pelicula/Pelicula → OldFilmFilter`. Revisar también que la mezcla ×2 se vea igual en el build.
  4. **Tamaño y posición del texto** (64 px, centrado en la banda de abajo) y que se vean ñ, ¿, ¡, tildes y ….
  5. **Encuadre de la apertura:** cover-fit de `libro_cerrado` y acercamiento a 1.12. El título pintado no debe quedar cortado.
  6. **Final:** que el acercamiento ×1.45 hacia el centro de la ilustración (1.5 s) y el fundido de 1.0 s se vean bien. Si sobra papel alrededor, subir `pushZoom` (1.59 llena la pantalla con la ilustración).
  7. **Pantallas que no son 16:9:** la página queda centrada con bandas negras (`AspectRatioFitter`) y el filtro cubre toda la pantalla.
  8. **Checklist de Play:**
     - negro → libro cerrado → acercamiento → rizo a la página 1
     - narrador + máquina de escribir
     - clic completa el texto y después pasa la página
     - mantener ESPACIO/Esc 1 s salta
     - página 6 → acercamiento → negro con lluvia → Nivel 1 con lluvia y sin errores en la consola
- **APIs dudosas:**
  - Un Canvas *Screen Space - Camera* toma el tamaño del `targetTexture` de su cámara (igual que en INTRO.1).
  - `Graphic.material` con shader propio en Canvas Overlay en URP. Los shaders son CG con `UnityCG.cginc`, como `UI/Default`.
  - Los rigs quedan encendidos y separados 100 unidades en la capa UI. La cámara principal tiene `cullingMask = 0`: solo limpia a negro y lleva el `AudioListener`.
- **Decisiones tomadas:**
  - **«~68 % de alto» y «~86 % de ancho» no caben juntos en una página 16:9 con una ilustración 16:9 sin recortar.**
    - Se priorizó el alto y no recortar el dibujo: la ilustración ocupa el 63 % del alto (con márgenes, el 68 % superior) y el **63 % del ancho**.
    - El 86 % quedó como tope de ancho y como ancho del texto.
    - Se ajusta en `IntroBookBuilder` (`IllustrationHeight`, `IllustrationMaxWidth`).
  - El marco de tinta es de 6 px.
  - El cambio de Build Settings lo hace el menú: no edité `EditorBuildSettings.asset`, que ya tenía cambios sin commit de la corrida anterior del menú.

## Archivos creados/modificados/borrados
**Creados:**
- `Scripts/Intro/PageCurlView.cs`
- `Scripts/Intro/OldFilmFilter.cs`
- `Shaders/IntroPageCurl.shader`
- `Shaders/IntroOldFilm.shader`
- `Art/Intro/libro_cerrado.png`
- `Art/Intro/hoja_papel.png`
- `Fonts/IMFellEnglish-Regular.ttf` y `Fonts/OFL.txt` (los trajo David; se subieron)
- `Docs/audits/AUDIT_intro_1_1.md`

**Modificados:**
- `Docs/phases/Fase-Intro.md`: enmienda A7.
- `Scripts/Intro/IntroBookData.cs`: apertura con `closedBookTexture`; fuera la tapa, las guardas y la portadilla.
- `Scripts/Intro/StoryBookDirector.cs`: flujo 2D.
- `Scripts/Intro/SpreadCompositor.cs` → `PageCompositor.cs` (`git mv` + reescrito; el `.meta` se movió con él).
- `Scripts/Editor/IntroBookBuilder.cs`: escena 2D y Build Settings sin `SampleScene`.
- `Scripts/Editor/InterrogationUIBuilder.cs`: `EnsureFont` generalizado. Estaba limpio en `git status`.

**Borrados:**
- `Scripts/Intro/StoryBook.cs` y `Scripts/Editor/IntroBookBuilder.Book.cs`.
- Los assets generados sin commit listados arriba: prefab, 6 materiales, malla de la lámpara, 5 `RBox_*`, perfil de viñeta y `Intro.unity`.

**No se tocaron** (tenían cambios sin commit de otros): `AudioLibrary.asset`, `EditorBuildSettings.asset` y los `.meta` modificados.

## Commits
- `5410f38` — dia4(david): INTRO.1.1 enmienda A7 (libro 2D a pantalla completa, pase de página con curl y filtro de película)
- `5f34328` — dia4(david): INTRO.1.1 libro_cerrado y hoja_papel a Art/Intro (copiados de F:\Descargas) + fuente IM Fell English (OFL)
- `8aeec34` — dia4(david): INTRO.1.1 libro 2D a pantalla completa: páginas 16:9 en RT, pase con rizo (shader PageCurl), filtro de película vieja (OldFilm), IM Fell English; se borra el libro 3D
- (este audit) — docs: auditoría INTRO.1.1

No se hizo push.

ESTADO: pendiente de revisión
