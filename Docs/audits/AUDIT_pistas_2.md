# AUDITORÍA PISTAS.2 — Arte y cierre

Rama `pistas`. Responsable: Edgar (Claude Code). Fecha: 3 nov 2026. Sigue a `AUDIT_pistas_1.md`.
**Unity no se puede abrir aquí:** el paso 3 de la fase (correr *Validar pistas* y jugar las pistas) lo hace David con la lista de abajo.

## Menús a ejecutar (en orden)
1. Volver a Unity y dejar que importe. El importador sube a v14 y reimporta `Art/Props/pistas/`.
2. **Plomo y Paja → Pistas → Crear hoja de pista** → crea o actualiza `Resources/Clues/ClueCard.prefab` y `Resources/Clues/ClueArt.asset`. Este último toma los 4 dibujos y la foto grande. **Commitear los dos con su `.meta`**, más los `.meta` de `Art/Props/pistas/*.png`, que Unity completa al reimportar.
3. Con `NivelPorky` abierta: **Plomo y Paja → Pistas → Validar pistas**. Con `Campamento` abierta, otra vez.

## Lo que hice
1. **Recorte:** `python Tools/character_key.py --single <entrada> --out Assets/_Game/Art/Props/pistas/<nombre>.png --crop 4` sobre las 4 imágenes de `_entrante_personajes/Pistas/`. Esa carpeta está en `.gitignore`: los originales no se commitean.
   - **Por qué `--crop 4`:** sin él el lienzo seguía de 1024×1024 y el dibujo quedaba entre 160 y 260 px por encima del borde inferior. Con el pivote abajo, la pista habría flotado unos 5–8 cm. Recortado a lo opaco, el borde de abajo del PNG es el del dibujo.
   - **Resultado:**
     | Archivo | Tamaño | Transparente |
     |---|---|---|
     | `pista_nota_01` | 950×523 | 47 % |
     | `pista_contrato_01` | 940×525 | 39 % |
     | `pista_foto_01` | 1012×614 | 47 % |
     | `pista_carta_01` | 888×719 | 32 % |
   - **Nombre corregido:** la entrada del pagaré se llamaba `pista_contrato_01_.png`; la salida usa el nombre del guion, `pista_contrato_01.png`.
2. **Foto grande:** `pista_foto_reunion_grande.png` (1024×1024, RGB, sin magenta) se importa como Sprite de UI: pivote al centro, sin mipmaps, sin compresión, máximo 2048.
3. **Importador (`CharacterSpriteImporter.cs`, v13 → v14), caso `Art/Props/pistas/`:**
   - **Dibujos:** pivote abajo al centro, mipmaps y Trilinear, sin compresión, 3333 px por metro. Con el lado mayor de unos 1000 px, el dibujo mide ≈ 0.3 m; `CluePickup` además lo deja en 0.3 m exactos.
   - **`*_grande`:** la regla del HUD (Sprite de UI), con máximo 2048.
   - El importador se commiteó solo y enseguida (regla de la fase), con git status limpio antes.
4. **`.meta` de los 4 PNG nuevos:** como Unity todavía no los había importado, los creé mínimos (solo `fileFormatVersion` y `guid`) para que el GUID sea el mismo en todas las máquinas. Unity completa el resto al importar.

## Resultado de "Validar pistas" (esperado; no se pudo correr aquí)
Después de *Crear hoja de pista*, en `NivelPorky`, el menú debería mostrar:
```
✓ Hoja: Assets/_Game/Resources/Clues/ClueCard.prefab
✓ Dibujos: Assets/_Game/Resources/Clues/ClueArt.asset

CATÁLOGO (textos del guion):
- nota_porky_01 «Nota de Porky»: hoja de … caracteres, dibujo ✓ pista_nota_01
- contrato_01 «Pagaré»: hoja de … caracteres, dibujo ✓ pista_contrato_01
- registro_01 «Hoja del libro de cuentas»: hoja de … caracteres
- testimonio_puerta_01 «Testimonio de Colmillo»: solo aviso
- testimonio_socios_01 «Testimonio de Tuerto»: solo aviso
- foto_reunion_01 «Foto de la reunión»: hoja de … caracteres, dibujo ✓ pista_foto_01, imagen ✓ pista_foto_reunion_grande
- carta_lobo_01 «Carta de Don Lobo»: hoja de … caracteres, dibujo ✓ pista_carta_01

ESCENA NivelPorky:
- nota_porky_01: marcador …/Clue_nota_porky_01 (2.5, 1.1, 0.8) → su pista se crea al jugar (ClueRuntime)
- contrato_01: marcador …/Clue_contrato_01 (-6.5, 0.3, -2.5) → su pista se crea al jugar (ClueRuntime)
```
Cómo leer el resultado:
- **"placeholder (falta … como Sprite)"** en un dibujo: Unity no lo importó como Sprite. Revisar que el importador v14 haya corrido (Reimport de la carpeta).
- **"⚠ Falta … ClueCard.prefab"**: no se corrió *Crear hoja de pista*. Aun así, en el juego la hoja se arma por código.
- **En `Campamento`:** deberían salir `foto_reunion_01` y `carta_lobo_01` como `Pista_Auto_*` bajo sus anclas.

## Pruebas manuales que faltan (David, en el Editor)
1. **Importación:** en el Inspector, `pista_nota_01` debe ser Sprite, PPU 3333, pivote Bottom Center, mipmaps sí. `pista_foto_reunion_grande` debe ser Sprite, sin mipmaps, compresión None.
2. **NivelPorky, Play:** se ven el papel doblado (nota) y el pagaré con cinta, de unos 0.3 m, **apoyados** (sin flotar ni hundirse), que giran solo en Y hacia la cámara, con brillo cada 2–3 s y sin vaivén. Si el marcador del pagaré queda debajo del sofá, revisar que se vea y que salga el aviso (ver PISTAS.1, cambio 2).
3. **Aviso "[E] Examinar"** solo viéndolas y a ≤ 2.2 m.
4. **Hoja:** abre con E; no se cierra antes de 0.5 s; Esc no abre la pausa; el clic no dispara.
5. **Aviso y orden:** "Pista: Nota de Porky" y, a los ~2 s, la IA.
6. **Campamento, si se juega:** la foto abre la hoja **con la imagen grande** (380 px de alto, proporción conservada) y el texto debajo, sin salirse del papel; la carta abre una hoja larga que no se sale de la pantalla en 16:9.
7. **Reinicio:** una pista tomada antes del inicio del nivel no reaparece; una tomada después sí reaparece al reiniciar (lo decide el tracker).

## Cambios de Unity en otras carpetas (no tocados ni commiteados)
Unity, abierto en esta máquina, regeneró unas 500 entradas fuera de mi alcance:
- `Assets/Materials/Greybox/*.mat` modificados y `Camp_*.mat` nuevos.
- `Assets/Scenes/Campamento.unity` (nueva, sin rastrear) y su carpeta.
- `Art/Environment/Generated/*.asset`.
- `.meta` de los scripts `LevelBuilder_Campamento*` y `Level/Campamento/*`.
- `ProjectSettings/.../codecoverage/Settings.json`.

Son de otros (Campamento, NivelPorky) o regeneración del Editor: se dejan para quien corresponda. `Art/Intro/nota_pagare.png` tampoco se tocó.

## Bloque para el Registro de fases
- **Resultado:**
  - Las pistas se leen sin `UIManager`: "[E] Examinar" con línea de vista, hoja de papel con pausa, aviso "Pista: título" y luego la IA.
  - Textos exactos del guion en `ClueCatalog`.
  - Dibujos en el mundo para nota, pagaré, foto y carta, e imagen grande en la hoja de la foto.
  - Las pistas del Nivel 1, que solo eran marcadores, ahora existen en runtime (`ClueRuntime`).
- **Decisiones:**
  - **Bloqueo de la hoja:** usa `CinematicState` + `UiHideReasons.Reason.Cinematic` para no tocar archivos ajenos.
  - **Recorte:** `--crop 4` para que el pivote abajo sea el del dibujo.
  - **Tamaño:** PPU 3333 (≈ 0.3 m).
  - **`.meta`:** mínimos, con GUID fijo, para los PNG nuevos.
  - **Títulos de los testimonios:** "Testimonio de Colmillo" y "Testimonio de Tuerto".
- **Para los demás:**
  - **David:** `Reason.Clue` + `ClueCard.IsOpen` en `PlayerAim.GameplayBlocked()` y en el Esc de `GameManager`; foco compartido entre `Pickup` y `CluePickup`.
  - **Cristian/David:** revisar el marcador `Clue_contrato_01` (debajo del sofá); opcionalmente colocar los `CluePickup` desde `LevelBuilder_NivelPorky`.
  - **Campamento:** su builder puede dejar de escribir los textos viejos.
- **Pendientes:**
  - Correr los menús de arriba y commitear `ClueCard.prefab`, `ClueArt.asset` y los `.meta` completos.
  - Las pruebas manuales 1–7.
- **Commits (rama pistas):**
  - `ef22112` - PISTAS.1 código.
  - `72d104c` - PISTAS.1 auditoría.
  - `8cdcc09` - importador v14 (caso pistas).
  - `9403fcf` - dibujos y foto grande.
  - (este archivo) - PISTAS.2 auditoría.
