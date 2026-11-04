# AUDIT contador_assets — Centavo (sprites + voces)

Guion: `Docs/guiones/Guion-Contador.md` v1.2 (el archivo se llama así en el repo, no `contador.md`; secciones 3, 6 y 7 leídas).

## 1. Archivos creados
- `PLOMO_Y_PAJAV_1/Tools/character_key.py` — recorte magenta (#FF00FF) hermano de `viewmodel_key.py`; importa `smoothstep/dilate/resize/to_image` de este.
- `PLOMO_Y_PAJAV_1/Assets/_Game/Art/Characters/contador/*.png` — 22 PNG 1024x1024 RGBA (sin `.meta`: los crea Unity al importar).
- Este informe.

## 2. Archivos modificados
- `Tools/viewmodel_key.py` — **NO modificado** (la salida del viewmodel no cambia).
- `.gitignore` — añade `PLOMO_Y_PAJAV_1/_entrante_personajes/`. (La carpeta real de entrada actual, `_entrante_viewmodel/_entrante_personajes_/contador/`, ya queda ignorada por la regla de `_entrante_viewmodel/`.)
- `Assets/_Game/Scripts/Editor/CharacterSpriteImporter.cs` — caso `contador`: Bottom Center (0.5, 0), mipmaps ON; resto igual (Sprite Single, Uncompressed, Bilinear). `GetVersion` 4→5 para forzar reimportación.
- `Assets/_Game/Scripts/Editor/AudioImporter.cs` — `preloadAudioData = !isLong` (voces/SFX: Preload ON). Ya tenía mono, Decompress On Load y Vorbis 0.7.
- No se tocó ningún script de gameplay.

## 3. Recorte (resultado)
- Entrada: **`_entrante_viewmodel/_entrante_personajes_/contador/`** (ahí está la carpeta real, 22 PNG de 1254x1254), no `_entrante_personajes/Contador/`. El script prefiere `_entrante_personajes/Contador/` y usa la otra como respaldo.
- Faltan: ninguno. Sobran: ninguno (22/22, sin inventar cuadros).
- Escala única 0.81920 (alto máx 1250 px, ancho máx 1188 px) para todos los cuadros.
- Despill solo en la franja de borde (≤3 px de zona transparente): orejas salmón, mancha roja y boca rojo oscuro quedan intactas (revisado visualmente en herido_01, scared_01, idle_01, limp_01). Magenta residual en bordes: 0–2 px por imagen.
- Alineación de pares (dy, dx en px del lienzo 1024):

| Cuadro → referencia | dy | dx |
|---|---|---|
| herido_02 → herido_01 | 0 | 0 |
| talk_02 → talk_01 | 0 | 0 |
| scared_02 → scared_01 | 0 | 0 |
| nervous_02 → nervous_01 | +1 | 0 |
| plead_02 → plead_01 | 0 | +1 |
| relief_02 → relief_01 | 0 | 0 |
| blink_01 → talk_01 | 0 | +1 |
| idle_02 → idle_01 | 0 | −1 |

  limp_01 / limp_02: sin alinear entre sí, solo abajo + centrado en X.
- Los 22 cuadros tienen su píxel opaco más bajo en la fila 1023. Nota: nervous_02 perdió 1 fila de píxeles inferior por el desplazamiento +1 (despreciable).
- Los desplazamientos son diminutos: los pares ya venían casi alineados.

## 4. Audio
Contador (21/21): coinciden exactamente con la sección 6. Wilbur (8/8): `wilbur_int_0A/0B/0C/1A/1B/1C`, `wilbur_dec_matar/perdonar` presentes y bien nombrados.
- Faltantes: ninguno. Mal nombrados: ninguno. No se renombró nada.
- Extras en `Voices/wilbur/` (no son del guion, ya existían): `wilbur_dano_01/02/03`, `wilbur_pocavida_01`.
- Formato verificado: los 29 son WAV mono 44 100 Hz, 1.4–10 s. No medí el silencio inicial/final (máx. 0.1 s del guion).
- Los 21 de contador y 7 de wilbur aún no tienen `.meta` (Unity no los ha importado).

## 5. Menús / pasos manuales (Unity no se ejecutó aquí)
1. Abrir Unity y dejar que importe: el postprocessor aplica Sprite Single, sin compresión, mipmaps ON, Bottom Center, Bilinear a `Art/Characters/contador/`; y mono, Decompress On Load, Preload ON, Vorbis 70 a las voces. Verificar en el Inspector de un sprite y un WAV.
2. Menú `Plomo y Paja/Reconstruir Sets de Sprites` y `Reconstruir Librería de Audio`.
3. Puntos de revisión: PPU 250 (heredado del default) hace el lienzo de 1024 px = 4.1 m; el tamaño en escena hay que ajustar al escalar el billboard. Con `FullRect` y mipmaps ON, comprobar que no sangra magenta/negro en el borde a distancia.

## 6. Compile check
No ejecutado (sin Editor). Los cambios C# son dos líneas/un `case` con APIs ya usadas en esos archivos.

## 7. Commits
Ninguno (no se pidió). `_entrante_personajes` no se agregó a git.
