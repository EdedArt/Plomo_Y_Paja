# Phase 3A2 — "Mouse P.I." visual approach: unlit 2D characters, rich 3D cartoon environment

- **Day:** Fri 30 Oct (morning, before 3B)
- **Model:** Opus 5.5
- **Commit tag:** `dia3a2`
- **Gate:** `docs/audits/PHASE-3A-audit.md` must start with `ESTADO: LUZ VERDE`. If not, stop.
- **Audit file:** `docs/audits/PHASE-3A2-audit.md`
- **First action (include in the first commit):**
  1. In `docs/phases/PHASE-3B.md`, change the Gate line to require `docs/audits/PHASE-3A2-audit.md`.
  2. In `docs/phases/PHASE-3B.md` Part 4, replace the HUD bullet's style with: "HUD in the style of
     1930s cartoon badges: health = round portrait medallion bottom-left with a number on a ribbon;
     ammo = round medallion bottom-right with loaded/reserve numbers; objective text top-left in a
     rounded cartoon font; no bars".
  3. Add this phase to the table in `docs/phases/README.md`, between 3A and 3B.

## The reference and what we copy from it
Reference game: MOUSE: P.I. For Hire (Fumi Games, Unity + URP). Facts from their public breakdown:
- Characters are hand-drawn 2D sprites placed in 3D, and **sprites receive no scene lighting**, so
  they always look the same and pop out of the background.
- The weapon is rendered as a **UI element**, not a 3D object (we already do this:
  `FirstPersonViewmodel`).
- Environments are **fully modeled 3D, built from many small modular meshes** (walls, corners,
  trims, props), shaded softly and **without outlines**. Lighting does most of the work: dark
  rooms with pools of light, volumetric light, a procedural sky with hand-drawn clouds.
- Characters carry a consistent ink outline baked into the drawings.

**Our differences:** full Technicolor color instead of black and white; characters drawn **front
view only** (they always face the camera, no 8 angles) to save art time.

**The core readability rule:** environment = soft, painterly, slightly dark, no outlines.
Characters, clues and pickups = unlit, crisp outlines, bright. That contrast is why enemies are
always visible even in dark rooms.

## Context
Relevant: `Enemies/BillboardSprite.cs`, `Enemies/RubberHoseMotion.cs`,
`Player/FirstPersonViewmodel.cs`, `Editor/LevelBuilder_NivelPorky.cs`,
`Editor/LevelBuilderUtils.cs`, `Editor/NoirVolumeBuilder.cs`, `Core/NoirPostProcess.cs`, the
URP renderer asset, URP 14 / Unity 2022.3.

## Out of scope
Level 2 layout (Phase 4 must reuse this kit), gameplay, final sprites.

---

## Part 1 — Character presentation (unlit billboards)
Commit: `dia3a2(edgar): personajes 2D sin iluminacion`

1. `Assets/_Game/Shaders/CharacterSpriteUnlit.shader` (URP, HLSL): unlit, alpha-clip (cutoff
   0.5, no sorting problems), casts a soft blob shadow only (see 3), ignores fog, supports a
   `_FlashAmount` (white hit flash, replaces the 3A `Sprites/Default` trick) and `_Tint`.
2. `BillboardSprite`: cylindrical billboard (rotate around Y only, always facing the camera) using
   this shader for the sprite and the accessory overlay. Placeholders also get a flat unlit look
   with a black outline (so the contrast rule can be judged before final art).
3. Blob shadow: a dark soft ellipse decal under each character (one quad, unlit, multiplied).
4. Clues and pickups use the same unlit treatment + a gentle bob, so they read as "interactable".

**Acceptance**
- 1.1 A guard standing in a dark corner is fully readable.
- 1.2 Characters never get lit/shaded by lamps; the environment does.
- 1.3 The billboard never tilts when the player looks up or down.

## Part 2 — Environment shading (soft, painted, no outlines)
Commit: `dia3a2(david): sombreado pintado del escenario`

1. `Assets/_Game/Shaders/CartoonEnvironment.shader` (URP, HLSL, < 300 lines, Spanish comments):
   - Soft 3-band lighting (main + additional lights; point lights are essential), shadows
     received, `ShadowCaster` and `DepthOnly` passes.
   - Shadow color shifted toward violet (`_ShadowColor`), not black.
   - Procedural "painted" variation: low-frequency color noise + paper grain (`_PaintNoise`),
     so untextured surfaces don't look like plastic.
   - Optional `_BaseMap` with world-space UVs (`_UseWorldUV`, axis by normal, 1 tile ≈ 2 m).
   - Edge darkening by curvature/AO-like gradient near the floor (`_FloorDarken`, height based)
     to fake the grounded look of the reference.
   - **No outline pass.**
2. URP renderer: enable the **Screen Space Ambient Occlusion** renderer feature (intensity ~1,
   radius ~0.3) via an editor script; record the exact settings.
3. `LevelBuilderUtils.GetOrCreateMaterial` creates CartoonEnvironment materials.
4. Technicolor volume adjustments: vignette 0.3, film grain 0.18 (Medium), contrast 15,
   bloom kept for lamps. Darker overall than 3A, **but** the player path always has a light pool
   at least every ~8 m.

**Acceptance**
- 2.1 No pink materials; environment has no black outlines; characters do.
- 2.2 Corners and wall bases are visibly darker (AO); lamps create warm pools.

## Part 3 — Modular cartoon kit (geometry)
Commit: `dia3a2(cristian): kit modular caricaturesco`

New `Editor/CartoonKit.cs` with procedural mesh builders (meshes saved as assets under
`Assets/_Game/Art/Environment/Generated/` so scenes don't hold huge inline meshes). Everything
chunky, rounded and slightly irregular, with a deterministic seed:
1. `RoundedBox(size, bevel, segments)` — beveled box; base for everything.
2. Walls with **wainscoting**: lower panel (darker wood, 1 m), chair rail trim, baseboard,
   upper wall; optional raised panels.
3. Door frames with a **rounded top**, chunky trim, a door leaf with panels and a big round knob.
4. Window frames with thick rounded trim and an emissive warm pane behind.
5. Plank floor: individual planks with tiny random height/rotation offsets and gaps.
6. Props: **bulgy barrel** (lathe mesh with bulge + 2–3 metal hoops + uneven top planks),
   crate (rounded, plank seams), round table, chair with a curved back, wall lamp with a curved
   arm, hanging ceiling lamp (bowl), rug (rounded rectangle with a border), potted plant
   (rounded pot + leaf cards).
7. Straw-house specifics for Porky: walls of straw bundles (stacked rounded cylinders), a
   thatched roof (cone + overlapping jagged fringe cards), rope-tied bundles, broken straw debris.
8. External models: `Assets/_Game/Art/Environment/Models/<name>.fbx|.glb` — if a model with a
   kit name exists (`barril`, `caja`, `mesa`, `silla`, `lampara_pared`, `lampara_techo`,
   `maceta`, `cerca`, `farol`), the builder uses it instead of the procedural one and assigns
   CartoonEnvironment materials keeping each submesh's main color. (Teammates may drop CC0 packs
   or AI-generated models there.) Import scale normalized by a postprocessor.

**Acceptance**
- 3.1 No visible plain cube in the level except intentional debris.
- 3.2 Dropping `barril.fbx` and regenerating replaces every procedural barrel.

## Part 4 — Rebuild Nivel Porky with the kit + lighting and sky
Commit: `dia3a2(cristian): Nivel Porky con kit, luz y cielo`

1. Porky's house interior rebuilt with the kit: wainscoted walls, plank floor, rounded door
   frames, rug, table + chairs in the kitchen, sofa (rounded boxes) in the sala, dresser in the
   bedroom (clue markers stay on top of the same furniture), hanging lamps, a staircase is NOT
   needed. Exterior: straw walls, thatched roof, fence, barrels, street lamps with curved arms,
   uneven terrain, dirt path, cobblestone street.
2. Lighting like the reference: interior mostly dark with 1–2 warm lamps per room; fake
   volumetric light cones (additive, soft-edged cone meshes, unlit, faint) under every lamp;
   moonlight through the broken wall. Exterior: cold moon + warm street lamps.
3. Sky: procedural gradient dome (top `#1B1F5C`, horizon `#6B4A7A`), stars, big cream moon with
   halo, 4–6 slow **hand-drawn-looking cloud cards** (procedural soft blobs with an ink-wash edge).
   Use `Backdrops/cielo_noche.png` if present.
4. Backdrop ring (unlit cards, fog-tinted): hills near, hills far, Porcópolis skyline with lit
   windows. Procedural silhouettes if no images; `Backdrops/*.png` if present.
5. Light distance fog toward the horizon color.
6. Rebake NavMesh; furniture must not block doors or the main path; enemies still patrol.

**Acceptance**
- 4.1 From the street the house reads as a straw house; inside it reads as a 1930s cartoon room.
- 4.2 No "end of the world" visible; the sky reads as a painted night.
- 4.3 Player and all enemies navigate the whole level.

---

## Unity test checklist for David (copy into audit section 9, in Spanish)
1. Menús en orden; consola limpia; ningún material rosado.
2. Un guardia en una esquina oscura se ve perfectamente (personaje sin iluminación, con contorno).
3. El escenario no tiene contornos negros; tiene sombras suaves y esquinas más oscuras.
4. Interior de la casa: zócalo de madera, piso de tablas, marcos de puerta redondeados, muebles caricaturescos.
5. Exterior: casa de paja reconocible, techo de paja, cerca, barriles abultados, faroles con brazo curvo.
6. Haces de luz bajo las lámparas; cielo con luna, estrellas y nubes; sin "fin del mundo".
7. El jugador y los guardias recorren todo el nivel sin atascarse.
8. Captura desde la calle y otra dentro de la cocina.

## Finish
Write `docs/audits/PHASE-3A2-audit.md` (Spanish), commit with the last part, `git push origin player`.
Stop. Do not start Phase 3B.
