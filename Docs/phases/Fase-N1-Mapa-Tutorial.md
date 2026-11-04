# PHASE N1 — Level 1 close: bigger map with see-through borders, placed pickups, tutorial, footprints to the forest

> In the repo: `Docs/phases/Fase-N1-Mapa-Tutorial.md` · content: `Docs/guiones/tutorial-huellas.md` (Guion-Tutorial-Huellas v1.0).
> **Owner:** David (Claude Code, orchestrated) · **Branch:** `integracion/dia4` · **SESSION A.** It runs in parallel with **SESSION B** (`Docs/phases/Fase-N2-Campamento.md`).
> **Deadline:** everything closes **tonight, Tue 3 Nov**; the game is delivered Wed 4 Nov.
> Paths are relative to `PLOMO_Y_PAJAV_1/Assets/_Game/` unless they start with `Docs/`, `docs/`, `Tools/` or `_entrante_*`.

## Scheduling (read first)
| Subphase | Order in Session A | Must NOT run at the same time as | Why |
|---|---|---|---|
| N1.1 Borders + bigger map | 1st | INTRO.2, HUD.3 (if still open) | same Level 1 builders |
| N1.2 Placed pickups (= HUD.3) | 2nd | HUD.3 (it replaces it), INTRO.2 | same builders / pickup system |
| N1.3 Tutorial | 3rd | INTRO.3 (if still open) | holster / draw event |
| N1.4 Footprints + exit | 4th | — | — |
| N1.5 Close | shared final playtest | — | — |

All of Session A can run **in parallel with Session B**. Both sessions share the same working copy, so:
- **Never** `git add -A` / `git add .`: stage by explicit path. Before editing a shared file run `git status`; if it has uncommitted changes you did not make, don't edit it: report what it needs. **Commit a shared file right after you edit it.**
- **Ownership tonight:**

| File / system | Owner | The other session |
|---|---|---|
| `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, `NivelPorky` | **A** | B never touches them |
| `EditorBuildSettings.asset` (adds `Campamento`, `Final`) | **B** | A loads `Campamento` by name only; if missing, report |
| `GameStateTracker.cs` | **B** | A reads only, never edits |
| `AudioImporter.cs` / `AudioLibrary` new folders (`lobo`, `porky`, `narrador` finals) | **B** | A puts new clips in existing folders (`wilbur`, SFX); if a new case is needed, report |
| `CharacterSpriteImporter.cs` | both (A: `huellas`, fondos · B: `lobo`) | git status + commit right away |
| `InterrogationData` / interrogation scripts | **B** | A never touches |
| `AICompanion` | nobody edits | both use `RequestCustomLine` / `ShowCustomLine` |

---

## 0. Rules for every subphase
Rules 1–9 of `Docs/phases/Fase-3C-Contador.md` §0 apply **verbatim**, with these substitutions:
- Audits go to `Docs/audits/AUDIT_n1_<N>.md` (in Spanish), using the subphase's template.
- Editor menus live under **Plomo y Paja → Nivel 1 → …**, are idempotent and are also run by **Generar Nivel Porky**.
- Code: borders in `Scripts/Level/Boundary/`, tutorial in `Scripts/Tutorial/`, footprints in `Scripts/Level/Footprints/`, editor tools in `Scripts/Editor/`. If recon shows a better existing place, use it and say so.
- Existing contracts still hold: `PlayerAim` owns FOV · layer 8 reserved · `IWeapon` · `RubberHoseCrosshair` (no second crosshair) · `CartoonFx` hit-stop (`timeScale` 0) · no `UIManager` · `PlayerAmmo` owns reserves · `UiHideReasons` owns HUD/hands visibility · existing sprite/audio importers and libraries · cinematic bits from INTRO (`Letterbox`, `CinematicSubtitles`, `HoldToSkip`).
- **Missing assets never block:** labelled placeholder + warning; rerunning the menu picks the real files up. The audit lists every missing id.
- **Player-facing text in Spanish, exactly as in the guion.** No per-frame GC.
- **Every N1 audit includes a top-down ASCII map of `NivelPorky`** (N up, approximate scale) marking: player start, taxi stop, Porky's house, shed, guards, pickups, border types, the forest gate. Before/after when the layout changes.

---

## 1. Decisions (do not re-discuss)
| # | Decision |
|---|---|
| N1 | **No invisible walls.** Every collider that stops the player belongs to a visible prop at the same place (fence, hedge, car, barricade, rubble, tree line, railing, gate). Only exception: a safety catch volume ≥ 10 m behind the visible border, unreachable in normal play. |
| N2 | **Borders are see-through or low.** From any walkable point the player sees scenery beyond the border (vista zone), never a blank wall at the edge of the world. A building facade only counts as a border if it has depth (porch, steps, lit or curtained windows) and its gaps show something behind. |
| N3 | **Height.** Recon `PlayerController`: if there is no jump (GDD), a border is ≥ 1.0 m and ≥ `stepOffset` + 0.4 m; if there is jump, ≥ jump apex + 0.3 m. Colliders hug the visible shape (one box per segment). Bullets hit borders. |
| N4 | **Vista.** Near vista 10–80 m beyond the borders: houses, trees, water tower, lamp posts, lit windows (emissive only, no realtime lights), static, no colliders, not on the NavMesh. Far layer 80–300 m: theater-flat backdrop cards (city skyline, hills, forest) tinted by fog, generated procedurally (seeded) when there is no art. On a far hill: **Wilbur's brick house and Max's wooden house** with lit windows (callback to the book). |
| N5 | **Variety.** No more than ~8 m of the same border piece in a row. Mix picket fence, broken fence, iron fence, hedge, parked/crashed 1930s car, Don Lobo barricade (sawhorses + planks + barrels), rubble, crates, ditch with railing, tree line. |
| N6 | **Bigger playable area** ≈ +40–60 %: street both ways, neighbours' front yards, side alley, empty lot. **No new enemies**; the new space holds atmosphere and placed pickups (N7). NavMesh rebaked; the taxi route and every guard route keep working. |
| N7 | **Pickups are placed, not loose:** they rest on furniture or inside containers that make sense (kitchen shelf, open drawer, bathroom cabinet, workbench, crate, toolbox, mailbox, porch table). Never in the middle of a floor, a yard or a street. Placed pickups don't bob (glint only). **Guard drops (H16) are OFF by default** (serialized, can be turned on). |
| N8 | **Tutorial = short and diegetic.** Hints go in the AI-thought band as **fixed lines** (no API) plus key chips at the bottom; non-blocking. The only blocking moment is the **"Pensamientos" card** (the thoughts are written live by an AI and change every playthrough), shown once while the arrival cinematic still has input locked. Each hint shows once per game session; a restart after death never repeats them. |
| N9 | **Footprints.** Wolf prints and **pig hoof prints walking side by side (no drag marks)** go from behind Centavo's shed to a **chained back gate** and into the forest. They are always visible. The scene arms only after Centavo is resolved (Dead or Pardoned, epilogue finished): short cinematic + AI thought (API with context, fallbacks in the guion), then Wilbur shoots the chain and the gate opens. Before that the gate is visibly chained (N1). |
| N10 | **Level exit:** end of the forest path → fade 1.0 s → async load `Campamento` (Session B creates it). If it's not in Build Settings: log error + the debug toast from the guion, and stay. |

---

## 2. Subphases

### N1.1 — Boundary kit + bigger Level 1
**Model:** Opus 5.5 · **Time box:** ~75 min
**Touches:** `LevelBuilder_NivelPorky`, `ContadorYardBuilder`, the INTRO.2 taxi builder (its anchors/paths must stay valid; reuse its car mesh generator for parked/crashed cars if it exists), NavMesh bake in the builders, `LightFlicker`, global Volume / fog, `PlayerController` (read only: jump, `stepOffset`), `CharacterSpriteImporter` or the texture import path for `Art/Environment/fondos/`.

0. **Recon:** current playable bounds and **how every edge is closed today** (list each invisible or blank wall), ASCII map "before", jump / `stepOffset` values, how fog and the global Volume are set.
1. **Boundary kit** — menu **Plomo y Paja → Nivel 1 → Crear kit de bordes** builds prefabs from primitives in `Prefabs/Level/Boundary/` (seeded, chunky rubber-hose, slightly bent, warm/dirty colors, reuse existing wood/metal materials):
   `Fence_Picket` (1.1 m, gaps), `Fence_PicketBroken`, `Fence_Iron` (1.6 m), `Hedge_Low`, `Barricade_Lobo`, `Car_Parked_30s`, `Car_Crashed_30s`, `Rubble_Pile`, `Crate_Stack`, `Ditch_Railing`, `TreeLine_Segment` (cartoon trees: bent trunks + layered blobs; dense trunks for the forest edge), `Lamp_Post` (emissive; a real light only inside the playable area), `Gate_Bosque` (hinged wooden gate + chain + padlock, closed collider), `Backdrop_Card` (big unlit plane, alpha silhouette, fog tint).
   - `BorderSegment` component on every piece: auto box collider hugging the shape, static flags, not walkable on top.
   - `BorderPath` (polyline of points) + editor fill: places a seeded mix of pieces along the path following N5, so borders are authored as paths, not hand-placed props.
   - `BackdropTextureGenerator` (editor): seeded silhouettes (skyline with water towers and chimneys, hills, forest) → PNG in `Art/Environment/fondos/`; if Edgar's files `fondo_ciudad_01`, `fondo_colinas_01`, `fondo_bosque_01` exist, they win.
2. **Bigger level** — menu **Plomo y Paja → Nivel 1 → Ampliar mapa** (also run by *Generar Nivel Porky*):
   - Street extended ≈ +30 m each way. One end: a Don Lobo barricade; the other: a crashed car and a broken lamp post. The street visibly continues beyond both (vista).
   - 2–3 neighbour facades with depth (porch, steps, lit/curtained windows, mailbox), front yards with low picket fences; not enterable; one neighbour porch or small open shed as a pickup spot for N1.2.
   - Side alley next to Porky's house: trash cans, crates, clothesline; it ends at an iron fence that looks onto the next street.
   - Empty lot with rubble and an empty billboard frame (no text).
   - Back of the yard: forest edge (`TreeLine_Segment` + fog) behind the yard fence, with `Gate_Bosque` and the forest path already built behind it (~30 m, ends at `Anchor_LevelExit`).
   - Vista and far backdrops per N4, including the hill with Wilbur's and Max's houses.
   - Lighting: warm street lamps; the level must read clearly (it was too dark before). Fog so the backdrops fade into the night.
   - Re-bake NavMesh. Check the taxi route (INTRO.2), the 3C.1 gate route and the 3C.4.2 guards.
3. **Safety catch** volume per N1 (respawns the player at the last safe point if ever reached).

**Audit template (N1.1):**
```
- Bordes invisibles o en blanco que había antes y cómo se cerró cada uno:
- ¿Hay salto? stepOffset y altura usada en los bordes:
- Kit de bordes: piezas creadas y cómo se mezcla:
- Mapa ASCII antes / después (área jugable aproximada en m²):
- Vista: capas, distancias, ¿fondos generados o arte de Edgar? ¿se ven las casas de Wilbur y Max?
- ¿Siguen bien la ruta del taxi, las patrullas y el NavMesh?
- Cosas que bloquean al jugador sin verse (debe ser "ninguna"):
- Menús que David debe correr (en orden):
- Qué debe mirar David en el Editor (y medir: Batches en Stats antes/después):
- Archivos compartidos tocados (y si estaban limpios en git status):
- Archivos creados/modificados:
- Commits:
```

---

### N1.2 — Pickups placed in the world (this IS HUD.3, with rule N7)
**Model:** Opus 5.5 · **Time box:** ~45 min
**Touches:** everything HUD.3 lists in `Docs/phases/Fase-HUD.md`, plus the props of N1.1.

- **If HUD.3 is not done:** implement HUD.3 §0–§4 as written (Pickup base, `MedkitPickup`, `AmmoPickup`, prompt, toast, `PickupLayout_NivelPorky`, menus *Colocar objetos* / *Guardar posiciones de objetos*), with two changes: spots follow **N7**, and **drops are off by default**.
- **If HUD.3 is already closed:** only redo the layout per N7 and turn drops off.

1. **`PickupSpot`** marker: snaps to the surface below (raycast; warning if the gap is > 0.05 m), optional container tag. The builder warns about any spot on a floor/yard/street with no furniture within 0.6 m. Pickups inside drawers/cabinets stay reachable (prompt visible from a normal standing view).
2. **Level 1 layout** (pick concrete furniture from the builders and report it):
   | Where | What |
   |---|---|
   | Porky's kitchen — shelf or counter | 1 caja de cartuchos |
   | Porky's bedroom — open nightstand drawer | 1 caja de cartuchos |
   | Porky's bathroom — cabinet or shelf | 1 botiquín |
   | Porky's living room — inside an open tin on the table | 1 caja de balas de revólver (stored before having the revolver, H14) |
   | Neighbour's porch table or open shed | 1 caja de cartuchos |
   | Side alley — crate or mailbox | 1 botiquín |
   | Centavo's shed — workbench (reward after the fight) | 1 botiquín |
   | Centavo's shed — crate | 1 caja de cartuchos |
   Totals: 4 shell boxes, 3 medkits, 1 revolver box. Nothing extra right before the gate to the yard.
3. Placed pickups: no bob, glint only (N7).

**Audit template (N1.2):**
```
- ¿Se hizo HUD.3 completo aquí o solo la distribución?
- Cómo se recoge (tecla / aviso reutilizado):
- Posiciones elegidas (mueble o contenedor de cada una) + mapa ASCII:
- ¿Algún objeto quedó suelto en el piso? (debe ser "no")
- Botiquín: ¿cura 40 %? ¿no se gasta con la vida llena?
- Munición: ¿respeta los topes? ¿la de revólver se guarda sin tener el revólver?
- Drops de guardias: ¿apagados por defecto?
- ¿Sobreviven a "Generar Nivel Porky"? (sí/no)
- Menús que David debe correr (en orden):
- Ids faltantes y placeholders:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### N1.3 — Tutorial: controls and "what are the thoughts"
**Model:** Opus 5.5 · **Time box:** ~45 min
**Touches:** `AICompanion` display path (use it; don't change its API), `IntroCinematicDirector` (INTRO.2; add an event after the fixed reveal thought only if it's missing), `PlayerWeaponHolster` (INTRO.3 draw event), `WeaponPickup` (revolver), `UiHideReasons`, `RubberHoseCrosshair` canvas sorting, pause, `InterrogationDirector.IsActive`, fonts (Special Elite).

1. **`TutorialDirector`** (`Scripts/Tutorial/`), placed by menu **Plomo y Paja → Nivel 1 → Colocar tutorial** (idempotent, run by *Generar Nivel Porky*). Steps from guion §2, each **once per game session** (same lifetime/mechanism as INTRO.2's once-per-session flag; report where it lives):
   - **T1 — Card "Pensamientos" (blocking):** right after the fixed reveal thought ends. If the arrival cinematic is off or missing: 1.5 s after control is given, with `timeScale` 0, input locked and cursor visible. Own Canvas (above HUD, below interrogation/cinematic), Special Elite, paper look like the interrogation paper. Closes with click / Space / Enter only after a 1.0 s minimum. Unscaled time. Restores everything on close.
   - **T2 — Movement:** when control returns: fixed thought line + chips (WASD / Shift / E). Each chip ticks when the player does it; all gone when done or after 10 s.
   - **T3 — Weapon:** on the shotgun draw (INTRO.3 event). If INTRO.3 doesn't exist: 3 s after control returns. Fixed thought line + chips (right click / left click / R), ticking per action; gone when done or after 12 s.
   - **T4 — Revolver:** on the revolver pickup: fixed thought line + chips (1 / 2 / wheel). If 3C.5 already shows something equivalent, keep only one and report.
2. **Chips:** small Canvas bottom-center, above the HUD, below the crosshair; paper-tag look; key glyph + Spanish label exactly as the guion. Read the real bindings from the project's input (Input Manager) and report if any key differs from the guion.
3. **Queueing:** hints never appear during interrogation, cinematic, dialogue, pause or death (`UiHideReasons` / `IsActive` checks); they wait and then show.
4. **AI tag (optional, low priority):** the first 3 **API** thoughts of a game show a tiny label "pensamiento · IA" under the band (listen to the existing AI event; don't change `AICompanion`).
5. The fixed lines go through the same display path as the reveal thought (I9), never the API.

**Audit template (N1.3):**
```
- Dónde se engancha cada paso (evento usado) y qué pasa si falta INTRO.2 / INTRO.3:
- Tarjeta "Pensamientos": ¿pausa y restaura todo? ¿mínimo 1 s antes de cerrar?
- Chips: ¿se marcan al hacer la acción? ¿teclas reales del proyecto = las del guion?
- ¿Se repite algo al morir y reiniciar? (debe ser "no") ¿dónde vive la bandera?
- ¿Aparece algo encima de un interrogatorio, cinemática o pausa? (debe ser "no")
- Etiqueta "pensamiento · IA": ¿se hizo?
- Menús que David debe correr:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### N1.4 — Footprints to the forest + Level 1 exit
**Model:** Opus 5.5 · **Time box:** ~45 min
**Touches:** `CentavoController` (read state + epilogue-finished; add an event only if missing), `GameStateTracker` (read only), `AICompanion.RequestCustomLine` / `ShowCustomLine`, `Letterbox`, `CinematicSubtitles`, `HoldToSkip`, `UiHideReasons`, the guard detection lock used by INTRO.2 (I7), `ContadorYardBuilder` / `LevelBuilder_NivelPorky`, audio library, `ScreenFade` and the async scene loading used by the book, URP decals (recon whether the renderer has the Decal feature).

1. **`FootprintTrail`** — menu **Plomo y Paja → Nivel 1 → Colocar huellas** (run by *Generar Nivel Porky*): from behind the shed, through `Gate_Bosque`, ~25 m into the forest path. Wolf + pig prints side by side, alternating L/R, ≈ 0.7 m stride, small random rotation, wet dark tint; decal quads 1 cm above ground (or URP Decal Projectors if already enabled). Art `huella_lobo_01` / `huella_cerdo_01` from `Art/Props/huellas/`, otherwise generated placeholder shapes. Reusable: Session B may call it for Level 2.
2. **`FootprintsSceneDirector`:** trigger at the start of the trail; arms only if `ContadorState` ∈ {Dead, Pardoned}, Centavo's epilogue has finished and no interrogation/cinematic is active. Sequence (guion §3), world `timeScale` stays 1, guards can't detect or damage the player (I7 mechanism), no FOV writes:
   1. Letterbox in (0.4 s), input locked, hands/HUD hidden via `UiHideReasons` (Cinematic).
   2. Camera looks down at the prints (1.2 s, ease) → glides along the trail at eye height (3 s) → stops at the gate, looking into the foggy forest with a faint warm glow far away (the camp).
   3. `wilbur_huellas_01` with subtitle.
   4. AI thought via `RequestCustomLine` with the observation context from guion §3 (timeout 5 s real time → fallback from guion §3 with its conditions). Clue/kill AI lines suppressed during the scene.
   5. 1.0 s → `wilbur_cadena_01` (optional) → shotgun blast SFX + `sfx_cadena_rota`, chain and padlock fall, gate swings open 0.6 s with `sfx_reja_abrir`.
   6. Camera back to the player's head (0.5 s, no snap), letterbox out, control back.
   - Hold-to-skip → end state (gate open). Plays once per session; after a restart the gate starts open if Centavo is resolved.
3. **Exit (N10):** `LevelExit` trigger at `Anchor_LevelExit` → fade to black 1.0 s with the ambience → `LoadSceneAsync("Campamento")`. Preload when the gate opens if memory allows (report). Missing scene → log + debug toast.

**Audit template (N1.4):**
```
- Huellas: ¿decals o quads? ¿arte de Edgar o placeholder? ¿se ve que caminan juntos?
- Cuándo se arma la escena (condiciones exactas) y qué pasa si Centavo sigue vivo/herido:
- Secuencia: tiempos reales, ¿FOV intacto? ¿control vuelve sin salto?
- Pensamiento: ejemplo real de la API + prueba sin conexión (línea de respaldo usada):
- ¿Los guardias pueden atacar durante la escena? (debe ser "no")
- Saltar: ¿deja la reja abierta y todo restaurado?
- Salida al bosque: ¿carga Campamento? ¿precarga? ¿qué pasa si no está en Build Settings?
- Menús que David debe correr:
- Archivos compartidos tocados (y si estaban limpios en git status):
- Qué no se pudo compilar/probar y qué debe revisar David en el Editor:
- Archivos creados/modificados:
- Commits:
```

---

### N1.5 — Close (merged into the shared final playtest)
**Model:** Sonnet 5.5

1. David's checklist:
   - [ ] No invisible walls anywhere; every border shows something beyond; can't get stuck or out.
   - [ ] Vista reads at night (skyline, hill with Wilbur's and Max's houses); level not too dark.
   - [ ] Taxi, guards and Centavo's yard still work after the expansion.
   - [ ] Pickups sit on furniture/in containers; prompt, toast, caps; nothing loose on the floor.
   - [ ] "Pensamientos" card once; movement, weapon and revolver hints once; nothing repeats after dying.
   - [ ] Footprints scene after Centavo (both outcomes), chain shot, gate open, forest exit loads Campamento.
   - [ ] No console errors; no GC spikes.
2. Debug flags off, dead code out.
3. Audit `Docs/audits/AUDIT_n1_5.md` ending with a **block for the Registro de fases** (Resultado, Subfases, Decisiones, Para los demás, Pendientes, Commits), in Spanish.

---

## 3. Plan B (if time runs out), in this order
1. Drop the "pensamiento · IA" tag and T4.
2. Footprints scene without the camera glide (look-down + thought + chain only).
3. Vista: far backdrops only (fewer near props).
4. Expansion: only the street ends + the side alley.
**Never drop:** no invisible walls, the "Pensamientos" card, the forest exit to Level 2.
