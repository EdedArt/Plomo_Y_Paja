# Auditoría Fase 3A — Look Technicolor, pipeline de assets, impacto de combate, barks y UI

ESTADO: PENDIENTE DE REVISIÓN

> El agente deja la línea de arriba así. Solo el orquestador autoriza cambiarla a
> `ESTADO: LUZ VERDE — aprobado por el orquestador`. El agente nunca escribe LUZ VERDE.

## 1. Resumen
Se implementaron las partes 0–4: grafo, look Technicolor, pipeline de sprites/audio, impacto caricaturesco y barks con UI dividida.
Nada se probó en Unity (no se puede ejecutar aquí); todo requiere la lista de pruebas de la sección 9.

## 2. Commits
| Hash | Mensaje |
|---|---|
| 7c8ec12 | chore: grafo de scripts y plan de fases |
| 9b51c95 | dia3a(david): look Technicolor e iluminacion |
| 0436b4f | dia3a(edgar): pipeline de sprites y audio |
| 73c7ad9 | dia3a(edgar): impacto y muertes caricaturescas |
| 487ada6 | dia3a(cristian): globos de dialogo y voces |

Push: `origin/player` (ver `git log origin/player` tras el push final)

## 3. Archivos creados
- `Scripts/Visual/CartoonFx.cs` — sangre, manchas, humo, estrellas, dientes y hit-stop con pools fijos
- `Scripts/Enemies/EnemyCombatFx.cs` — empujón, destello, muerte caricaturesca
- `Scripts/Player/ShotgunFeedback.cs` — culatazo, temblor, luz de boca y onomatopeyas
- `Scripts/Dialogue/BarkDatabase.cs` — los 22 ids con su texto
- `Scripts/Enemies/EnemyBarks.cs` — globos de diálogo + voz
- Partes 1–2 (ver commits 9b51c95 y 0436b4f): `CharacterSpriteSet`, `RubberHoseMotion`, `FirstPersonViewmodel`, `AudioLibrary`, `AudioLibraryLoader`, importadores de sprites y audio

## 4. Archivos modificados
- `Scripts/Editor/TestPrefabGenerator.cs` — añade ShotgunFeedback, CartoonFx (+materiales Fx_*), EnemyCombatFx, EnemyBarks; subtítulo de la IA arriba, cursiva, underlay
- Partes 1–2: `NoirVolumeBuilder`, `NoirPostProcess`, `LevelBuilder_NivelPorky`, `BillboardSprite`, `ShotgunController`, `CluePickup`

## 5. Criterios de aceptación
| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1.1–1.3 | Look Technicolor | 🧪 | commit 9b51c95 |
| 2.1–2.4 | Pipeline drop-in | 🧪 | commit 0436b4f |
| 3.1 | Un disparo: retroceso, popup, sangre, empujón | 🧪 | ShotgunFeedback.cs, EnemyCombatFx.cs |
| 3.2 | Muerte completa y cuerpo desaparece ~2 s | 🧪 | EnemyCombatFx.cs `DeathRoutine` |
| 3.3 | timeScale nunca queda ≠ 1 | 🧪 | CartoonFx.cs `OnDisable`/`RestoreTimeScale` |
| 3.4 | Sin asignaciones por disparo crecientes | ✅ | pools fijos en CartoonFx (384 partículas, 40 manchas, 4 popups) |
| 4.1 | Globos al ver, daño, rendirse, morir | 🧪 | EnemyBarks.cs |
| 4.2 | Pensamientos arriba, diálogo abajo | 🧪 | TestPrefabGenerator.cs `CreateAICompanionPrefab` |
| 4.3 | 22 ids exactos | ✅ | BarkDatabase.cs (conteo = 22) |

## 6. Compile check
- Runtime: 0 errores, 0 warnings nuevos
- Editor: 0 errores, 0 warnings nuevos
- Warnings nuevos (texto): ninguno (Roslyn de Unity 2022.3.62f3 contra las DLL del proyecto)

## 7. Menús a ejecutar (en orden)
1. Plomo y Paja → Crear Prefabs de Prueba (regenerar, para que los prefabs existentes reciban los componentes nuevos)
2. Plomo y Paja → Reconstruir Sets de Sprites
3. Plomo y Paja → Reconstruir Librería de Audio
4. Generar Nivel Porky

## 8. Pasos manuales en Unity
1. Dejar que Unity recompile y confirmar consola sin errores rojos.
2. Si el menú de volumen noir no creó `TechnicolorVolumeProfile.asset`, ejecutarlo antes de generar el nivel.

## 9. Pruebas en Unity (las llena David)
| # | Prueba | Resultado (✅/❌) | Nota |
|---|---|---|---|
| 1 | Ejecutar los menús en el orden del audit. Consola sin errores rojos | | |
| 2 | Generar Nivel Porky → Play: se ve a color, de noche pero legible, con faroles cálidos | | |
| 3 | Disparar a un guardia: retroceso, "¡BANG!", sangre, empujón, mancha en el piso | | |
| 4 | Matar a un guardia: muerte caricaturesca con humo, estrellas y dientes; desaparece | | |
| 5 | El guardia muestra globos al verte, al recibir daño, al rendirse y al morir | | |
| 6 | Los pensamientos de la IA salen arriba; el diálogo del interrogatorio abajo | | |
| 7 | Recibir daño: el borde de la pantalla parpadea en rojo | | |
| 8 | Con Pausa o al morir, el juego no se queda congelado ni acelerado | | |

Errores de consola en Play (número y texto):

Captura(s):

## 10. Decisiones tomadas
- La detección de "ver" se hace leyendo `EnemyAI.CurrentState` (Alert/Chase/Attack) en `EnemyBarks`, sin tocar EnemyAI.
- El globo cuelga de la raíz del enemigo (no de `Visual`) para no girar con la muerte.
- El flash de sprites usa `Sprites/Default` sobreexpuesto (no hay shader de blanco puro en el proyecto).
- El underlay del subtítulo es un material guardado como asset (`Pensamiento_Underlay.mat`) para que sobreviva en el prefab.
- `AICompanion.cs` no se modificó.

## 11. APIs dudosas y riesgos
- Material transparente de las manchas: propiedades `_Surface/_Blend/_ZWrite` del shader Unlit de URP puestas por código; verificar que se vea translúcido.
- Retroceso/temblor por `RenderPipelineManager.beginCameraRendering`: verificar que no desvía los perdigones.
- Los materiales TMP con underlay dependen de las claves `_Underlay*` del shader TMP/Distance Field.

## 12. Preguntas para el orquestador
- Ninguna.

## 13. Deuda técnica para fases siguientes
- Los prefabs de informantes (fase 3B) deben usar `voiceSet = informante` en `EnemyBarks`.
- Archivos sin seguimiento ajenos a la fase: `graphify-out/manifest.json`, `cost.json`, `.graphify_labels.json`, `StreamingAssets/LEEME.txt.meta`.

## 14. Uso de contexto
- ¿Se leyó `graphify-out/GRAPH_REPORT.md`? sí (fases 1–2); en las partes 3–4 se abrieron directamente los archivos a cambiar
- Archivos abiertos (lista corta): CartoonFx, EnemyCombatFx, ShotgunFeedback, EnemyHealth, TestPrefabGenerator, LevelBuilderUtils, README de fases
- Archivos abiertos que no se modificaron y por qué: CartoonFx/EnemyCombatFx/ShotgunFeedback (revisión de trabajo previo, sin cambios)
