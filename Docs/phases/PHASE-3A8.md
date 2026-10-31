Read CLAUDE.md, graphify-out/GRAPH_REPORT.md, Docs/audits/PHASE-3A5-audit.md and Docs/audits/PHASE-3A7-audit.md first.

PHASE 3A8 — Edgar's 10 guard frames + death sprite + SSAO fallback for weak GPUs.
Branch: integracion/dia3 ONLY (push only there). Do NOT touch main, dev, player, enemies. Do NOT edit "ESTADO:" lines.
Do NOT commit David's uncommitted generated assets. No questions: decide safely, report. Unity not available.

## Part 1 — Merge Edgar's new commits (Edgar)
git fetch --all; merge origin/enemies into integracion/dia3 (expected new commit 1bb74a3 and anything after it).
Edgar added Art/Characters/guardia/: idle_01..idle_04, walk_01, shoot_01, hit_01, surrender_01, talk_01, death_01
(512×512, character 449 px, transparent). He set PPU 256 / Pivot Bottom (death_01 Center) by hand in the .meta files.
Rules:
1. CharacterSpriteImporter stays the source of truth: guardia PPU 236, automatic foot pivot. Edgar's manual .meta import
   values will be overwritten on import — that is expected. Keep his GUIDs. If a .meta conflicts, keep Edgar's GUID and
   let the importer settings win (bump GetVersion if needed so Unity reimports).
2. death_01 is drawn lying squashed on the floor: the foot-pivot rule (lowest opaque row) must put its bottom on the ground.
   Verify the importer handles it (no Center pivot, no sinking/floating).
3. Duplicates: player has guardia_idle_01.png. If Edgar's idle_01.png is the same drawing or supersedes it, remove ours
   (git rm) so the set has exactly Edgar's idle_01..04. Report the decision.
4. Make sure CharacterSpriteSetBuilder picks up all 10 frames with their anim names (idle, walk, shoot, hit, surrender,
   talk, death) and the guard prefab uses them (Crear Prefabs de Prueba). Idle with 4 frames must loop; single-frame anims
   hold. Report fps used per anim.
5. Any other conflict: same rules as 3A5 (player base for BillboardSprite, Edgar's logic for EnemyAI, never hand-merge YAML).
Commit: "merge: cuadros del guardia de Edgar en integracion/dia3"

## Part 2 — No double squash on a drawn death (Edgar)
RubberHoseAnimator / PlayDeathSquash: if the current sprite set has a "death" frame, do NOT scale to 15%. Use only a short
landing bounce (e.g. scaleY 1→0.85→1 in ~0.2 s) or nothing. Keep the 15% squash only for sets without a death frame
(placeholder). Make the behavior a serialized option documented in the audit.
Commit: "dia3a8(edgar): sin aplastado doble con dibujo de muerte"

## Part 3 — SSAO fallback (David)
Edgar's PC (DX10-level GPU) logs "RenderTexture.Create failed: R32 SFloat" and the level renders almost black.
1. Make CartoonEnvironment safe: the AO term must default to 1 (no darkening) whenever SSAO is not active — only sample
   the AO texture under the _SCREEN_SPACE_OCCLUSION keyword (as URP Lit does via GetScreenSpaceAmbientOcclusion /
   AmbientOcclusionFactor). Never let a missing/unbound AO texture darken the scene. Check the AO-squared ambient change from 3A3.
2. Runtime guard (a small bootstrap, e.g. in Core): at startup, if SystemInfo.graphicsShaderLevel < 45, or
   !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) / R32_SFloat / R8, or the platform lacks
   depth-normals support, disable the SSAO renderer feature (SetActive(false) on the ScriptableRendererFeature) and log once
   "[Graficos] SSAO desactivado: GPU sin soporte". Do NOT modify the renderer asset on disk at runtime permanently
   (restore state on exit in the Editor).
3. Add a quality toggle the future options menu can call (e.g. GraphicsSettingsService.SetAmbientOcclusion(bool)).
4. Also check the other new shaders/features for formats a DX10-level GPU can't create; report anything risky.
Commit: "dia3a8(david): SSAO se apaga solo en GPUs sin soporte"

## Part 4 — Docs
Update Docs/phases/PR-player-to-dev.md (PR integracion/dia3 -> dev) with 3A7 and 3A8. Add a section "Requisitos mínimos
(provisional)" with what is known so far (DX11 / shader model 4.5 recommended for SSAO; works without SSAO on lower).
Commit "docs: PR actualizado con 3A8". Push origin integracion/dia3.

## Audit — Docs/audits/PHASE-3A8-audit.md (Spanish), "ESTADO: PENDIENTE DE REVISIÓN", short:
1. Resumen  2. Commits
3. Cuadros de Edgar: tabla archivo | anim | fps | pivote calculado ; qué se hizo con guardia_idle_01
4. Conflictos del merge (tabla) o "ninguno"
5. Aplastado de muerte: regla nueva
6. SSAO: causa del negro, condición de apagado, cómo probarlo en una PC buena (forzar el modo sin SSAO)
7. Menús a ejecutar (fuera de Play)
8. Pruebas (las llena David) — incluye las 6 de la 3A7 más:
   7 El guardia anima: idle (respira, 4 cuadros), camina, dispara, recibe golpe, se rinde, habla, muere |
   8 Al morir queda tirado con su dibujo de muerte, sin hacerse una rayita, y con los pies/cuerpo en el piso |
   9 Forzando "sin SSAO", el nivel se ve normal (no negro)
   + "Errores de consola" + "Captura(s)"
9. Riesgos