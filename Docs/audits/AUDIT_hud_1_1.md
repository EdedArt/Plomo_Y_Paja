# AUDIT HUD.1.1 — Conectar el arte del HUD de Edgar

**Fecha:** 3 nov 2026 · **Base:** `integracion/dia4` en `d12bace` · **Unity:** cerrado (nada se corrió ni se probó en Play).

> **Git:** igual que en INTRO.3, esta sesión no puede editar ni hacer git en el checkout principal. Trabajé en el worktree
> `.claude/worktrees/intro3` (rama `wt/intro3` = `integracion/dia4` + estos commits). Para llevar la rama local
> (avance rápido limpio): `git merge --ff-only wt/intro3` desde el checkout principal, en `integracion/dia4`.

## Archivos de Edgar encontrados y qué muestra cada uno
Estaban en `Art/Characters/hud/` (commits de Edgar `48051a3` y `bf940cc`, que llegaron desde dev). No había otro arte con pinta de HUD en `Art/` (`Art/UI/` solo tiene `ui_papel_dialogo.png`, del diálogo).

| Archivo | Tamaño | Qué muestra | Fondo |
|---|---|---|---|
| `wilbur_retrato_normal.png` | 512×512 RGBA | Medallón redondo (borde negro grueso, fondo crema): Wilbur de frente, cejas fruncidas, media sonrisa dura, camisa azul. Entero, sin golpes. | **ya transparente** (esquinas alpha 0, 0 píxeles magenta) |
| `wilbur_retrato_herido.png` | 512×512 RGBA | El mismo medallón: ojo morado, curita en el hocico, rasguños, corte que sangra, gota de sudor, dientes apretados, camisa rota. | ya transparente |
| `medallon_balas.png` | 512×512 RGBA | El mismo marco redondo crema con **tres cartuchos rojos de escopeta** con base de latón. Es un emblema de munición, no el ícono de un solo cartucho. | ya transparente |

No hizo falta `Tools/character_key.py`: ninguno tenía fondo magenta.

**Cómo se importaban:** con la regla de personaje, porque estaban en `Art/Characters/`:
- PPU 250;
- pivote propio en «los pies» (y = 0.02);
- además, «Reconstruir Sets de Sprites» los leía como personaje `hud`.

## Mapeo archivo → id del HUD (y qué id cubre más de un estado)

| Archivo de Edgar | Id del HUD | También cubre |
|---|---|---|
| `wilbur_retrato_normal` | `hud_wilbur_01` (> 75 % de vida) | **`hud_wilbur_02`** (> 50 %) |
| `wilbur_retrato_herido` | `hud_wilbur_04` (≤ 25 %) | **`hud_wilbur_03`** (> 25 %) y **`hud_wilbur_dano`** (golpe de 0.25 s) |
| `medallon_balas` | `hud_medallon_balas` (id nuevo, opcional) | — |

- **Caras:** queda normal para 01–02 y herido para 03–04 y daño.
  - Lo hace `HudBuilder`: si falta una cara, usa la más cercana que exista (02 ← 01, 03 ← 04).
  - Si falta `hud_wilbur_dano`, usa la cara más golpeada que exista **de verdad** (04).
  - El informe del menú lo dice: «ℹ Cubiertos por otro dibujo: hud_wilbur_02 ← hud_wilbur_01, hud_wilbur_03 ← hud_wilbur_04, hud_wilbur_dano ← hud_wilbur_04».
  - Cuando lleguen los dibujos propios, se usan solos.
  - **Efecto en el juego:** el golpe (`dano`) con vida alta muestra la cara herida 0.25 s, que se lee como «me dolió».
  - **Por qué en el builder:** el runtime solo cae hacia atrás (03 → 02 → 01), así que con 01 y 04 la franja 03 habría mostrado la cara normal.
- **`medallon_balas`:** es la pareja del retrato (mismo marco), pero para la munición. Usarlo como `hud_cartucho_lleno` habría dibujado seis medallones de tres cartuchos.
  - **Dónde va:** a la **izquierda de la fila de cartuchos**, 58 px, alineado con los cartuchos de 46 px. La fila y la reserva «×N» se corren 66 px.
  - **Cuándo:** solo con la escopeta; con el revólver se oculta, porque muestra cartuchos.
  - Campo nuevo `HudWilbur.ammoMedallion` → `HudAmmoRow.Init(..., shellMedallion)`. Sin el dibujo, la fila queda como antes.

## ¿Se movieron o se referencian? (GUIDs revisados)
**Se movieron** con `git mv`, cada PNG con su `.meta` (el GUID no cambia).

| Antes | Después | GUID |
|---|---|---|
| `Art/Characters/hud/wilbur_retrato_normal.png` | `Art/UI/HUD/hud_wilbur_01.png` | `61d83955561458c48b95a5249a256fbb` |
| `Art/Characters/hud/wilbur_retrato_herido.png` | `Art/UI/HUD/hud_wilbur_04.png` | `31098082b063f894db3a57f17389acff` |
| `Art/Characters/hud/medallon_balas.png` | `Art/UI/HUD/hud_medallon_balas.png` | `d5808370ca9111b41978f970a54725b5` |

- **GUIDs revisados:** grep de los 3 GUIDs en todos los `.unity`, `.prefab`, `.asset` y `.mat`, en el worktree y en el checkout principal. **Ninguna referencia**: `HudBuilder` busca por ruta (`Art/UI/HUD/<id>.png`) y el prefab del HUD se arma de nuevo.
- `Art/UI/HUD/` no existía: se creó con su `.meta` (GUID `cd0a3622d87f4df893d97500121c802d`).
- La carpeta vacía `Art/Characters/hud/` y su `.meta` se borraron.
- **`Art/SpriteSets/hud.asset` borrado:**
  - era el set vacío (`clips: []`) que «Reconstruir Sets de Sprites» creó al tomar `hud` por un personaje (entró con `d12bace`);
  - grep de su GUID `b812e94ce5f71d042b1f889fd13552bd`: nadie lo usa.

## Regla de importación nueva
`CharacterSpriteImporter` estaba limpio (comparado con el checkout principal, sin cambios locales). Ahora es la **versión 12**, así que Unity reimporta.

**Regla del HUD (`ApplyHud`, ya existía para `Art/UI/HUD/` desde HUD.1):**
- Sprite Single, Full Rect, **pivote al centro**;
- **sin mipmaps**, **sin compresión**, Bilinear, **alpha = transparencia**, máximo 1024;
- PPU 100 (en la UI no importa: `HudWilbur` fija el tamaño de cada Image).

Los tres PNG ya están en `Art/UI/HUD/`, así que la toman. Al reimportar, sus `.meta` pierden el PPU 250 y el pivote en los pies.

**Regla explícita nueva para `Art/Characters/hud/`** (por si Edgar vuelve a dejar arte del HUD ahí, como llegó desde dev): `IsHudArt(path)` la manda a la misma regla del HUD, nunca a la de personaje. Así deja de pasar el cambio de PPU y pivote de esta mañana.

**«Reconstruir Sets de Sprites» (punto 7): opción doble.**
- El arte se movió a `Art/UI/HUD/`, que el menú **nunca** escanea (solo mira las subcarpetas de `Art/Characters`).
- Además, `CharacterSpriteSetBuilder.NonCharacterFolders = { "hud" }` salta `Art/Characters/hud` si vuelve a aparecer: no crea set ni avisa.
- Resultado esperado: **ningún aviso «Nombre no válido» por archivos del HUD**, y no se vuelve a crear `SpriteSets/hud.asset`.

**«Generar Nivel Porky»:** antes solo **colocaba** el prefab del HUD que ya existía, así que no tomaba arte nuevo. Ahora primero lo rearma (`HudBuilder.BuildPrefab`) y escribe el informe en la consola. «Plomo y Paja → HUD → Crear HUD» ya lo rearmaba.

## Ids que siguen en placeholder
Para que David los genere con los prompts del guion HUD §3.1:

| Id | Placeholder | Nota |
|---|---|---|
| `hud_wilbur_02` | cara normal (01) | curita y ojo un poco hinchado |
| `hud_wilbur_03` | cara herida (04) | ojo morado, curitas, sudor |
| `hud_wilbur_dano` | cara herida (04) | mueca de dolor, ojos apretados |
| `hud_cartucho_lleno` / `hud_cartucho_vacio` | rectángulo rojo / gris | lo que se veía como «rectángulos rojos» |
| `hud_bala_llena` / `hud_bala_vacia` | rectángulo dorado / marrón | revólver |
| `hud_barra_marco` *(opcional)* | contorno grueso dibujado + relleno rojo | la «barra roja lisa» |

**Ojo con el estilo:** los retratos de Edgar son **medallones redondos con fondo crema**, no la cabeza sola sobre fondo transparente que pide el prompt del guion. Para que 02, 03 y daño combinen, conviene pedirlos editando `wilbur_retrato_normal` («Same image. Keep everything identical, only add: …»), no desde la maestra del guion.

## Menús que David debe correr
1. **Llevar la rama local:** desde el checkout principal, en `integracion/dia4`: `git merge --ff-only wt/intro3`.
2. **Abrir Unity** y esperar la reimportación (importador v12). Revisar que en `Art/UI/HUD/` los tres PNG estén con pivote **Center**, sin mipmaps y sin compresión.
3. **Plomo y Paja → Reconstruir Sets de Sprites:** sin avisos de archivos del HUD, y que no aparezca `SpriteSets/hud.asset`.
4. **Plomo y Paja → HUD → Crear HUD** (o **Generar Nivel Porky**, que ahora también rearma el HUD). El informe debe decir:
   - «Cubiertos por otro dibujo…»;
   - «✓ Medallón de munición hud_medallon_balas»;
   - la lista de los que faltan.
5. **Guardar la escena** (Ctrl+S) y probar en Play:
   - la cara normal con vida alta y la herida por debajo del 50 %;
   - el medallón a la izquierda de los cartuchos con la escopeta, oculto con el revólver;
   - que el medallón no pise «¡RECARGA!».
6. **Commitear** los `.meta` reimportados de `Art/UI/HUD/` y `Prefabs/UI/HUD_Wilbur.prefab` (+ `NivelPorky.unity` si cambió).

## Archivos / commits / push
**Movidos (`git mv`, con `.meta`):** los 3 PNG → `Art/UI/HUD/hud_wilbur_01`, `hud_wilbur_04`, `hud_medallon_balas`.

**Creados:** `Art/UI/HUD.meta`, `Docs/audits/AUDIT_hud_1_1.md`.

**Borrados:** `Art/SpriteSets/hud.asset` (+ `.meta`), `Art/Characters/hud.meta` y la carpeta vacía.

**Modificados:**
- `Scripts/Editor/CharacterSpriteImporter.cs`: v12, `IsHudArt`, `NonCharacterFolders`.
- `Scripts/Editor/HudBuilder.cs`: caras cubiertas por la más cercana, `dano`, medallón, informe.
- `Scripts/Editor/LevelBuilder_NivelPorky.cs`: rearma el prefab del HUD antes de colocarlo.
- `Scripts/UI/HUD/HudWilbur.cs`: campo `ammoMedallion`.
- `Scripts/UI/HUD/HudAmmoRow.cs`: medallón y corrimiento de la fila.

**Compilación:** Roslyn de Unity 2022.3.62f3, con las referencias de los `.csproj`.
- Runtime: 93 fuentes, **0 errores**.
- Editor: 27 fuentes, **0 errores**.
- Único aviso: el `CS0414` de `CentavoController`, que ya estaba (sin contar los `CS0649` de campos serializados).

**Commits (en `wt/intro3`):**
- `98ee568` — dia4(david): HUD.1.1 arte del HUD de Edgar conectado (…)
- (este audit) — docs: auditoría HUD.1.1

**Push:** **OK** con la credencial guardada: `18af2ab..98ee568  wt/intro3 -> integracion/dia4-intro` (avance rápido; incluye la auditoría INTRO.3 y `d12bace`).
- Equivale a `git push origin integracion/dia4:integracion/dia4-intro` después del `merge --ff-only`.
- **Este audit se commitea después del push: no está subido.**

ESTADO: pendiente de revisión
