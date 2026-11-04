# Auditoría Fase 3A8 — Los 10 cuadros del guardia de Edgar, muerte dibujada y SSAO en GPUs débiles

ESTADO: PENDIENTE DE REVISIÓN

## 1. Resumen
- Se mezcló `origin/enemies` (5 commits `dia4`: guardia, informante, efectos, Don Lobo, Porky y Max). Solo hubo un conflicto, la carpeta `guardia.meta`. El viejo `guardia_idle_01.png` se quitó.
- Con dibujo de muerte ya no hay aplastado al 15 %, solo un rebotecito (configurable). En GPUs sin soporte el SSAO se apaga solo y el shader ya no oscurece si falta el AO.
- Runtime y Editor compilan con 0 errores y 0 warnings nuevos. No se probó nada en Unity, no se commitearon los assets generados de David y no se tocó ninguna línea "ESTADO:".

## 2. Commits
| Hash | Mensaje |
|---|---|
| 9f91c25 | merge: cuadros del guardia de Edgar en integracion/dia3 |
| 26b636c | dia3a8(edgar): sin aplastado doble con dibujo de muerte |
| 7d368e0 | dia3a8(david): SSAO se apaga solo en GPUs sin soporte |
| bc32d87 | docs: PR actualizado con 3A8 |
| (este commit) | docs: auditoría 3A8 |

Push: `origin/integracion/dia3` 301288a..(este commit)

## 3. Cuadros de Edgar (guardia)
Todos miden 512×512. El pivote lo calcula `CharacterSpriteImporter` (fila más baja con alpha > 0.1, normalizada; X = 0.5) y se midió aquí sobre los PNG. **Todos los dibujos tocan el borde inferior del lienzo**, así que el pivote Y da 0 en los 10 cuadros. Como es el mismo en todos, no hay saltos entre cuadros. PPU 236 (449 px ≈ 1.9 m).

| Archivo | Anim | fps | Pivote calculado | Alto del dibujo |
|---|---|---|---|---|
| idle_01.png | idle (bucle) | 4 | (0.5, 0) | 449 px |
| idle_02.png | idle | 4 | (0.5, 0) | 455 px |
| idle_03.png | idle | 4 | (0.5, 0) | 461 px |
| idle_04.png | idle | 4 | (0.5, 0) | 455 px (idéntico a idle_02 → ciclo 1-2-3-2, respiración de 1 s) |
| walk_01.png | walk (se queda fijo) | 8 | (0.5, 0) | 449 px |
| shoot_01.png | shoot | 10 | (0.5, 0) | 449 px |
| hit_01.png | hit | 1 | (0.5, 0) | 451 px |
| surrender_01.png | surrender | 4 | (0.5, 0) | 448 px |
| talk_01.png | talk | 6 | (0.5, 0) | 452 px |
| death_01.png | death (una vez, se queda) | 8 | (0.5, 0) — **sin pivote al centro** | 264 px, tumbado, los zapatos tocan el piso |

- **Los fps** son los `DefaultFps` de `CharacterSpriteSetBuilder`. El builder conserva el fps que ya tenga el asset: `guardia.asset` ya tiene idle a 4. Con un solo cuadro, el fps no importa: el cuadro se queda fijo. idle, walk y talk están en bucle; death y hit se reproducen una vez.
- **PPU 256 y pivote a mano de Edgar:** se conservan sus `.meta` y sus GUID. `GetVersion()` del importador pasa de 3 a 4 para que Unity reimporte y aplique PPU 236 y el pivote en los pies.
- **`guardia_idle_01.png`:** se quitó con `git rm` (PNG + `.meta`). Era un dibujo más viejo y distinto (de frente, sin cola), y Edgar lo sustituye con `idle_01..04`. Además, el builder quita el prefijo `guardia_`, así que los dos habrían competido por el cuadro idle 1.
- **El prefab** usa el set `guardia` (`TestPrefabGenerator` → `GetOrCreateSet("guardia")`), y "Crear Prefabs de Prueba" reconstruye los sets antes de crear prefabs.

## 4. Conflictos del merge
| Archivo | Decisión | ¿Se perdió algo? |
|---|---|---|
| `Art/Characters/guardia.meta` (carpeta, add/add) | GUID de Edgar `3abda5e1…` | No. Nada referencia el GUID de carpeta de player `8b4c7a11…` |

## 5. Aplastado de muerte: regla nueva
- `RubberHoseAnimator` mira `BillboardSprite.HasDeathDrawing` (nuevo): es verdadero si el set tiene la anim "death" o, en el modo antiguo, si hay `deathSprite`.
- **Sin dibujo de muerte (placeholder):** el aplastado de siempre, al 15 % (`deathFlatHeight`).
- **Con dibujo de muerte:** se aplica el campo serializado `deathWithDrawing`:
  - `Bounce` (por defecto): rebotecito al caer, de alto 1 a 0.85 y de vuelta a 1 en 0.2 s (`drawnDeathBounceHeight`, `drawnDeathBounceDuration`).
  - `None`: nada.
  - `Flatten`: el comportamiento viejo (doble aplastado).
- Sigue empezando al aterrizar (`EnemyCombatFx` → `PlayDeathSquash`). El cuerpo queda en `_basePosition`: no se hunde ni flota.

## 6. SSAO
- **Causa del negro:** el SSAO pide la textura de profundidad, y en Forward URP la copia a un target **R32 SFloat** (`CopyDepthPass`). Una GPU de nivel DX10 no lo crea ("RenderTexture.Create failed: R32 SFloat"), así que el SSAO calcula con una profundidad inválida. Encima, `CartoonEnvironment` multiplicaba el ambiente por AO² y la luz directa por AO, y así la escena caía casi a negro.
- **Shader:** el AO vale 1 salvo con la keyword `_SCREEN_SPACE_OCCLUSION`, y solo entonces se lee la textura. La luz directa nunca baja de 0.6 por AO (es el mínimo normal de URP con Direct Lighting Strength 0.4, así que en una PC buena se ve igual). El AO² del ambiente de la 3A3 se mantiene, pero solo con el SSAO activo.
- **Cuándo se apaga** (`Core/GraphicsSettingsService`, `RuntimeInitializeOnLoadMethod`, antes de la escena): si `graphicsShaderLevel < 45`, si no hay RFloat o R32_SFloat como render target, si no hay R8 o si no hay texturas de profundidad. Entonces llama a `SetActive(false)` en el feature de SSAO de todos los renderers del pipeline y escribe una sola vez `[Graficos] SSAO desactivado: GPU sin soporte (<motivo>)`. Al salir de Play o del juego devuelve el estado original, y el asset no se marca como modificado.
- **Menú de opciones futuro:** `GraphicsSettingsService.SetAmbientOcclusion(bool)` devuelve el estado final; en una GPU sin soporte no lo enciende. También están `SupportsAmbientOcclusion` y `AmbientOcclusionEnabled`.
- **Cómo probarlo en una PC buena:** fuera de Play, Plomo y Paja → Gráficos → **Forzar sin SSAO (prueba)** (tiene tilde). Al entrar en Play sale `[Graficos] SSAO desactivado: modo de prueba "sin SSAO" forzado`. Para volver al SSAO normal, se pulsa el menú otra vez. En una build se usa el argumento `-sinssao`.
- **Otros shaders y ajustes para DX10:** ningún shader propio (`BackdropCard`, `BlobShadow`, `CharacterSpriteUnlit`, `CharacterPlaceholderUnlit`, `LightCone`, `SkyDome`) lee la profundidad ni la textura opaca, y ninguno pide `#pragma target` alto. El pipeline usa Forward, sin MSAA, HDR de 32 bits (R11G11B10, que DX10 sí crea), color grading LDR, 1 cascada de sombras de 2048 y sin sombras de luces adicionales. Sin la copia de profundidad del SSAO no hay formatos de riesgo. Lo único a vigilar es el rendimiento de las sombras suaves y del bucle de luces adicionales en GPUs viejas.

## 7. Menús a ejecutar (fuera de Play)
1. Abrir el proyecto y esperar la reimportación de `Art/Characters` (importador versión 4).
2. Plomo y Paja → Crear Prefabs de Prueba (reconstruye los sets: el `guardia.asset` local todavía apunta al `guardia_idle_01` borrado).
3. Plomo y Paja → Generar Nivel Porky.
4. Para la prueba 9: Plomo y Paja → Gráficos → Forzar sin SSAO (prueba), Play, y después desmarcarlo.
5. Commitear los assets generados (`chore: assets generados 3A2-3A8`).

## 8. Pruebas (las llena David)
| # | Prueba | ¿OK? | Notas |
|---|---|---|---|
| 1 | Consola sin warnings "more materials than submeshes" | |X|
| 2 | Al matar un guardia nunca se oyen 2 voces a la vez |Y| |
| 3 | Siempre suena un sonido de muerte; frase solo de vez en cuando |Y| |
| 4 | Guardias hablan poco y sin cansar (ver, patrulla, daño) |Y| |
| 5 | Nevera de los años 30 en la cocina | Y | |
| 6 | Pistas: nota (cómoda) y contrato (bajo el sofá) se ven y se recogen ||X|
| 7 | El guardia anima: idle (respira, 4 cuadros), camina, dispara, recibe golpe, se rinde, habla, muere |Y| |
| 8 | Al morir queda tirado con su dibujo de muerte, sin hacerse una rayita, y con los pies/cuerpo en el piso |Y| |
| 9 | Forzando "sin SSAO", el nivel se ve normal (no negro) |Y| |

**Errores de consola:**

No hay gamestatetracker en la escena: la pista "testimonio_puerta_01" no se pudo registrar

**Captura(s):**

## 9. Riesgos
- **Sin probar en la PC de Edgar:** la causa sale del log que él pasó y del código de URP 14.0.12. Si el nivel sigue oscuro con SSAO apagado, mandar el log completo y una captura.
- **Reflexión:** URP 14 no expone la lista de renderers, así que se lee el campo interno `m_RendererDataList`. Si URP se actualiza y cambia, sale un warning y el SSAO no se apaga (el shader igual evita el negro solo si la keyword no está activa).
- **Walk de un cuadro:** el guardia "camina" con un dibujo fijo más el rebote de `RubberHoseAnimator`. Para que de verdad camine hacen falta `walk_02..`.
- **Lo demás de Edgar ya entró:** sprites de `informante` (7), `donlobo` (4), `porky` y `max` (2 cada uno) y `fx` (10). El importador les pone pivote en los pies y PPU 250 (los `fx` quizá quieran pivote al centro). `Enemy_Interrogable` sigue usando el set `guardia`, no `informante`. Ninguno de los dos se tocó porque está fuera del alcance de esta fase.
- **Cambios locales de David:** siguen sin commitear (escena, materiales, `.meta` de audio, assets del renderer, `guardia.asset`…), igual que el `ESTADO:` de la auditoría 3A7.
