# PR: player → dev — Look de cartoon años 30, combate caricaturesco y Nivel Porky renovado (fases 3A–3A4)

## Resumen
- **3A:** look Technicolor, pipeline de sprites/audio (`Art/Characters`, `Audio/Voices|SFX`), impacto y muertes caricaturescas, barks con globos de diálogo y pensamientos de la IA arriba.
- **3A2:** personajes 2D sin iluminación (shader `CharacterSpriteUnlit`, sombra blob), escenario pintado (`CartoonEnvironment` + SSAO), kit modular `CartoonKit` y cielo nocturno.
- **3A3:** primer sprite de guardia (PPU 236, ~1.9 m), interior con contraste (empapelado, piso oscuro, sombras azules), todos los menús bloquean el modo Play.
- **3A4:** pivote automático en los pies, la casa de Porky como la más pobre y saqueada (escena del crimen, vicio, goteras), lluvia que sigue al jugador, farol roto, nevera años 30.
- **3A4-fix:** raycasts de pies y lluvia robustos, parpadeo de luces independiente del FPS.

## Menús nuevos (en este orden, fuera de Play)
1. Plomo y Paja → Configurar Renderer (SSAO)
2. Plomo y Paja → Crear Prefabs de Prueba (ya crea materiales de personaje y reconstruye los sets de sprites)
3. Plomo y Paja → Reconstruir Librería de Audio (solo si hay audios nuevos)
4. Plomo y Paja → Generar Nivel Porky
Otros: Crear Materiales de Personajes, Reconstruir Sets de Sprites, Verificar URP.

## Al mezclar dev en tu rama
- **Edgar (`enemies`):** habrá conflictos conocidos en `BillboardSprite.cs`, `EnemyAI.cs`, `Art.meta` y `Art/Characters.meta`. Conserva tus poses/eventos y la parte de 3A: shader sin luz, `AlignFeetToGround`, `BlobShadow`, `accessorySet`. Frames de 512 × 512 con el personaje de ~449 px (ver `Art/Characters/README.md`); el pivote es automático.
- **Cristian:** el nivel se genera con los menús; no se edita a mano. `origin/levels` no existe todavía: crea o actualiza tu rama desde `dev`. Después de mezclar, regenera Nivel Porky y revisa NavMesh y luces.
- Nadie debe resolver conflictos en `.unity`, `.prefab` o `.asset`: regenerar con los menús.

## Lista de pruebas
- [ ] Consola limpia al importar y ningún material rosado.
- [ ] Nivel Porky: a color, de noche pero legible; interior con piletas cálidas y sombras frías.
- [ ] Guardia con sprite: sin fondo, ~1.9 m, mira a la cámara, pies sobre su sombra.
- [ ] Disparo: retroceso, onomatopeya, sangre, empujón; muerte con humo, estrellas y dientes.
- [ ] Globos de diálogo al ver, dañar, rendir y matar; pensamientos de la IA arriba.
- [ ] Casa pobre y saqueada (dentro y fuera) pero se sigue leyendo como casa de paja.
- [ ] Nevera años 30, lluvia afuera, goteras adentro, farol roto parpadeando.
- [ ] Las 2 pistas siguen en su sitio y se recogen; jugador y guardias recorren todo el nivel.
- [ ] Menús en Play muestran el aviso; `Time.timeScale` nunca queda distinto de 1.

## Nota
Los assets generados (escena, prefabs, mallas, materiales) se versionan en un commit aparte tras regenerar el nivel: `chore: assets generados 3A2-3A4`.
