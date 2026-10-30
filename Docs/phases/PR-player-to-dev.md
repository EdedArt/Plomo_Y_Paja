# PR: integracion/dia3 → dev — Look de cartoon años 30, enemigos de Edgar y audio de Cristian (fases 3A–3A6)

Rama de integración creada desde `player` con `enemies` (Edgar) mezclada encima y el audio de Cristian importado.
Sustituye al antiguo PR `player → dev`: trae todo lo de player más el trabajo de Edgar ya resuelto.

## Resumen
- **3A:** look Technicolor, pipeline de sprites/audio (`Art/Characters`, `Audio/Voices|SFX`), impacto y muertes caricaturescas, barks con globos de diálogo y pensamientos de la IA arriba.
- **3A2:** personajes 2D sin iluminación (shader `CharacterSpriteUnlit`, sombra blob), escenario pintado (`CartoonEnvironment` + SSAO), kit modular `CartoonKit` y cielo nocturno.
- **3A3:** primer sprite de guardia (PPU 236, ~1.9 m), interior con contraste (empapelado, piso oscuro, sombras azules), todos los menús bloquean el modo Play.
- **3A4 (+fix):** pivote automático en los pies, la casa de Porky como la más pobre y saqueada, lluvia que sigue al jugador, farol roto, nevera años 30; raycasts de pies/lluvia robustos y parpadeo independiente del FPS.
- **3A5 — Edgar (`enemies`, 11 commits):** `EnemyAI` comentado con evento `OnStateChanged`, centinela que vuelve a su puesto, alerta al recibir daño y guardas sin NavMesh; `EnemyHealth.OnDamaged`; poses oficiales (idle/walk/shoot/hit/surrender/death/talk) y pose de ataque; `RubberHoseAnimator` (rebote, respiración, golpe, alerta, temblor, muerte); `DialogueNPC` con pose talk y registro de interrogatorios/pistas en `GameStateTracker`; Colmillo y Tuerto dan pista; sprites `wilbur_hands` (idle, fire, reload).
- **3A5 — Cristian:** 75 audios importados (barks de guardia/informante, escopeta, impactos, humo, estrellas, pista y el resto para las fases 4–6). Importación por carpeta (voces/SFX Decompress On Load + Vorbis 70; música/ambiente Streaming + Vorbis 60).
- **3A6 — Edgar:** `RubberHoseAnimator` en los guardias (en `SpriteHolder`, reemplaza a `RubberHoseMotion`); un solo aplastado de muerte (el de RubberHose, al aterrizar). `EnemyCombatFx` conserva destello, empujón, sangre, vuelo y desvanecido.
- **3A6 — Cristian:** sin `_SinUso` (todo en `Voices/`, `SFX/`, `Music/`); Wilbur se queja al recibir daño y avisa con poca vida; pasos de madera bajo techo y de barro afuera; lluvia en bucle (ahogada bajo techo) y truenos cada 25–60 s con destello. Volúmenes en `AudioLibrary.asset` → *Mix*.

## Resolución del merge con `enemies`
- `BillboardSprite.cs`: base de player (sin luz, sets de sprites, `AlignFeetToGround`, `BlobShadow`, accesorio) + lo de Edgar (`PlayAttack`, `hitPoseDuration`, poses sueltas, sin volteo por defecto, muerte que sigue mirando a la cámara, renderer en hijo `Art`, `anchorFeet` opcional).
- `EnemyAI.cs`: versión de Edgar (ya hace todas las llamadas de pose que player necesitaba).
- `Art.meta`, `Art/Characters.meta`: GUID de player.
- Sin conflictos en `.unity` / `.prefab` / `.asset` (Edgar no los tocó).

## Menús (en este orden, fuera de Play)
1. Plomo y Paja → Configurar Renderer (SSAO)
2. Plomo y Paja → Crear Prefabs de Prueba (crea materiales de personaje y reconstruye los sets de sprites, incluido `wilbur_hands`)
3. Plomo y Paja → Reconstruir Librería de Audio (**obligatorio**: debe decir "48 voces y 17 efectos")
4. Plomo y Paja → Generar Nivel Porky
Otros: Crear Materiales de Personajes, Reconstruir Sets de Sprites, Verificar URP.

## Al mezclar dev en tu rama
- **Edgar (`enemies`):** tus cambios ya están en dev. Haz `git pull origin dev` antes de seguir; `BillboardSprite` conserva tu API (`PlayIdle/Walk/Attack/Hit/Surrender/Talk/Death`). `RubberHoseAnimator` ya lo pone el generador en `SpriteHolder`; `RubberHoseMotion` ya no se añade (solo queda para prefabs viejos). Nuevo: `RubberHoseAnimator.PlayDeathSquash()` lo llama `EnemyCombatFx` al aterrizar.
- **Cristian:** el nivel se genera con los menús; no se edita a mano. `origin/levels` no existe todavía: crea tu rama desde `dev`. Todo el audio está en `Audio/Voices/<personaje>`, `Audio/SFX` y `Audio/Music`; ajusta volúmenes en `AudioLibrary.asset` → *Mix*.
- Nadie resuelve conflictos en `.unity`, `.prefab` o `.asset`: se regeneran con los menús.

## Lista de pruebas
- [ ] Consola limpia al importar, ningún material rosado y sin "Sin audio para…" de los ids que llegaron.
- [ ] Nivel Porky: a color, de noche pero legible; casa pobre y saqueada; nevera, lluvia, goteras y farol roto.
- [ ] Guardia con sprite: sin fondo, ~1.9 m, mira a la cámara, pies sobre su sombra, no se voltea.
- [ ] Guardia de Edgar: patrulla, se alerta al verte/oírte o al recibir un disparo, persigue, golpea, se rinde y muere.
- [ ] Rubber hose: rebote al caminar, aplastado al golpe, estirón de alerta, temblor al rendirse, un solo aplastado al morir.
- [ ] Voces del guardia al ver, recibir daño, rendirse, morir y patrullar; SFX de escopeta, impacto, humo y estrellas.
- [ ] Wilbur se queja al recibir daño y avisa con poca vida; pasos madera adentro / barro afuera.
- [ ] Lluvia con sonido afuera, ahogada adentro, truenos de vez en cuando.
- [ ] Interrogatorio: pose talk, la pista se registra (Colmillo y Tuerto).
- [ ] Las 2 pistas siguen en su sitio y se recogen; jugador y guardias recorren todo el nivel.
- [ ] Menús en Play muestran el aviso; `Time.timeScale` nunca queda distinto de 1.

## Nota
Los assets generados (escena, prefabs, mallas, materiales, `AudioLibrary.asset`) se versionan en un commit aparte tras regenerar en Unity: `chore: assets generados 3A2-3A6`.
