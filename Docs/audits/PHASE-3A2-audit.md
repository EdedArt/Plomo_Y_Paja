# Auditoría Fase 3A2 — Enfoque visual "Mouse P.I.": personajes 2D sin iluminación, escenario 3D caricaturesco

ESTADO: PENDIENTE DE REVISIÓN

> El agente deja la línea de arriba así. Solo el orquestador autoriza cambiarla a
> `ESTADO: LUZ VERDE — aprobado por el orquestador`. El agente nunca escribe LUZ VERDE.

## 1. Resumen
Partes 1–4 implementadas: shaders de personaje sin iluminación + sombra blob, shader de escenario pintado + SSAO,
kit modular procedural (CartoonKit) y Nivel Porky reconstruido con el kit, haces de luz y cielo pintado.
**El gate se saltó por orden explícita de David** (`PHASE-3A-audit.md` sigue PENDIENTE). Nada se probó en Unity.

## 2. Commits
| Hash | Mensaje |
|---|---|
| 994b28d | dia3a(david): assets generados por los menus de la fase 3A |
| 8a3d928 | chore: refrescar grafo de scripts |
| 83afe31 | dia3a2(edgar): personajes 2D sin iluminacion |
| b319930 | dia3a2(david): sombreado pintado del escenario |
| 52e7a88 | dia3a2(cristian): kit modular caricaturesco |
| (último) | dia3a2(cristian): Nivel Porky con kit, luz y cielo |

Push: `origin/player` (ver `git log origin/player` tras el push final)

## 3. Archivos creados
- `Shaders/CharacterSpriteUnlit.shader` — sprite sin luz, alpha clip 0.5, `_FlashAmount`, `_Tint`, sin sombra ni niebla
- `Shaders/CharacterPlaceholderUnlit.shader` — placeholder plano con contorno negro (casco invertido)
- `Shaders/BlobShadow.shader` — elipse suave multiplicada bajo los personajes
- `Shaders/CartoonEnvironment.shader` — 3 bandas, sombra violeta, ruido pintado, UV de mundo, oscurecido de piso, SSAO, sin contorno (271 líneas)
- `Shaders/LightCone.shader`, `SkyDome.shader`, `BackdropCard.shader` — haces de luz, cúpula con estrellas, tarjetas de fondo
- `Scripts/Visual/CharacterLook.cs` — reparte los materiales de personaje (Resources/CharacterLook)
- `Scripts/Visual/BlobShadow.cs`, `PickupPresentation.cs`, `CloudDrift.cs`
- `Scripts/Editor/CharacterLookBuilder.cs` — menú "Crear Materiales de Personajes"
- `Scripts/Editor/RenderingSetup.cs` — menú "Configurar Renderer (SSAO)"
- `Scripts/Editor/CartoonKit.cs`, `CartoonKit.Structure.cs`, `CartoonKit.Props.cs` — kit modular
- `Scripts/Editor/EnvironmentModelImporter.cs` — postprocesador de `Art/Environment/Models/`
- `Scripts/Editor/NightSkyBuilder.cs` — cielo, luna, nubes, anillo de fondo, suelo lejano, niebla
- `docs/phases/PHASE-3A2.md` (archivo de fase, versionado en el primer commit)

## 4. Archivos modificados
- `Enemies/BillboardSprite.cs` — `unlitLook`: shader sin luz en sprite/accesorio, placeholder con contorno, añade BlobShadow
- `Enemies/EnemyCombatFx.cs` — destello por `_FlashAmount` (MaterialPropertyBlock); el truco de 3A queda de respaldo
- `Clues/CluePickup.cs` — añade `PickupPresentation` (sin luz + contorno + vaivén)
- `Core/NoirPostProcess.cs` — valores base: exposición 0.2, contraste 15, viñeta 0.3, grano Medium1 0.18
- `Editor/NoirVolumeBuilder.cs` — el perfil Technicolor se reescribe siempre con esos valores
- `Editor/LevelBuilderUtils.cs` — `CreateBox` = caja redondeada del kit; `WallSegments`; materiales CartoonEnvironment (convierte los URP Lit existentes)
- `Editor/TestPrefabGenerator.cs` — llama a `CharacterLookBuilder.CreateAll()`
- `Editor/LevelBuilder_NivelPorky.cs` — entorno reconstruido con el kit (marcadores, actores y NavMesh sin cambios de lógica)
- `docs/phases/PHASE-3B.md` — gate → `PHASE-3A2-audit.md`; HUD estilo medallones de los años 30
- `docs/phases/README.md` — fila de la fase 3A2

## 5. Criterios de aceptación
| # | Criterio | Estado | Evidencia |
|---|---|---|---|
| 1.1 | Guardia legible en un rincón oscuro | 🧪 | CharacterSpriteUnlit / CharacterPlaceholderUnlit no usan luces |
| 1.2 | Personajes nunca iluminados por lámparas | ✅ | shaders de personaje sin cálculo de luz; `ApplyPlaceholder` apaga sombras |
| 1.3 | El billboard no se inclina al mirar arriba/abajo | ✅ | BillboardSprite `YAxisOnly` (por defecto en los prefabs) |
| 2.1 | Sin materiales rosados; escenario sin contorno, personajes con contorno | 🧪 | CartoonEnvironment sin pase de contorno; fallback a URP Lit |
| 2.2 | Esquinas y bases más oscuras; piletas cálidas | 🧪 | SSAO (RenderingSetup) + `_FloorDarken` |
| 3.1 | Ningún cubo plano salvo escombros | 🧪 | `CreateBox` → caja redondeada; postes cilíndricos reemplazados por faroles del kit |
| 3.2 | `barril.fbx` reemplaza todos los barriles | 🧪 | `CartoonKit.TryModel` en `Barrel` |
| 4.1 | Casa de paja desde la calle; sala de los años 30 adentro | 🧪 | fardos, techo de paja, zócalo, tablas, arcos, muebles |
| 4.2 | Sin "fin del mundo"; cielo pintado | 🧪 | NightSkyBuilder (cúpula r 190, anillos 92/122/150 m, suelo lejano, niebla lineal) |
| 4.3 | Jugador y enemigos recorren todo el nivel | 🧪 | muebles fuera de puertas y del camino; rebake en el generador |

## 6. Compile check
- Runtime: 0 errores, 0 warnings nuevos
- Editor: 0 errores, 0 warnings nuevos
- Warnings nuevos (texto): ninguno. Los shaders no se pueden compilar fuera de Unity: revisar la consola al importar.

## 7. Menús a ejecutar (en orden)
1. Plomo y Paja → Crear Materiales de Personajes
2. Plomo y Paja → Configurar Renderer (SSAO)
3. Plomo y Paja → Crear Prefabs de Prueba
4. Plomo y Paja → Generar Nivel Porky (también corre 1 y 2 por su cuenta)

## 8. Pasos manuales en Unity
1. Dejar que Unity importe los shaders y compile; revisar errores de shader en la consola.
2. En el Renderer asset confirmar que aparece "Screen Space Ambient Occlusion" con Intensity 1, Radius 0.3,
   Direct Lighting Strength 0.25, Source Depth Normals, Samples Medium, Blur High, Downsample off, After Opaque off, Falloff 100.
3. En el pipeline asset confirmar Soft Shadows activo y Per Object Lights = 8.
4. Si se quiere probar 3.2: soltar `barril.fbx` en `Assets/_Game/Art/Environment/Models/` y regenerar el nivel.

## 9. Pruebas en Unity (las llena David)
| # | Prueba | Resultado (✅/❌) | Nota |
|---|---|---|---|
| 1 | Menús en orden; consola limpia; ningún material rosado | | |
| 2 | Un guardia en una esquina oscura se ve perfectamente (personaje sin iluminación, con contorno) | | |
| 3 | El escenario no tiene contornos negros; tiene sombras suaves y esquinas más oscuras | | |
| 4 | Interior de la casa: zócalo de madera, piso de tablas, marcos de puerta redondeados, muebles caricaturescos | | |
| 5 | Exterior: casa de paja reconocible, techo de paja, cerca, barriles abultados, faroles con brazo curvo | | |
| 6 | Haces de luz bajo las lámparas; cielo con luna, estrellas y nubes; sin "fin del mundo" | | |
| 7 | El jugador y los guardias recorren todo el nivel sin atascarse | | |
| 8 | Captura desde la calle y otra dentro de la cocina | | |

Errores de consola en Play (número y texto):

Captura(s):

## 10. Decisiones tomadas
- Gate saltado por orden explícita de David; la fase 3A sigue sin aprobar.
- Los materiales de personaje viven en `Resources/CharacterLook/` para que el build incluya los shaders; `BillboardSprite` los aplica en runtime, así sirven también para prefabs/escenas sin regenerar.
- El placeholder conserva su color por `MaterialPropertyBlock` (`_Tint`) con un único material compartido.
- La sombra blob es un componente (`BlobShadow`) que sigue el piso con un raycast y se oculta al terminar la muerte.
- Pistas: `PickupPresentation` mueve solo los hijos visuales; el trigger no se mueve.
- CartoonEnvironment: sin variante Forward+ ni sombras de pantalla (el proyecto usa Forward y luces adicionales por píxel).
- Piso de la casa elevado 6 cm (tablas sobre el terreno); la cómoda y la caja del contrato mantienen las pistas a 1.1 m y 0.3 m.
- Techo de paja: dos "conos" de 4 lados (cocina y fondo de la sala) con flecos; el dormitorio queda sin techo (destrozado).
- Cerca: colisión con tramos de caja invisibles; lo visible son postes, travesaños y tablas puntiagudas.
- Luces adicionales por objeto 4 → 8 (piso y paredes de la casa son mallas grandes).
- Escombros: cajas redondeadas deformadas (los únicos "cubos" permitidos).

## 11. APIs dudosas y riesgos
- `ScreenSpaceAmbientOcclusion` es `internal` en URP 14: se crea por reflexión y se configura por `SerializedObject` (`m_Settings.*`, `m_RendererFeatureMap`). Si falla, el menú lo dice y hay que añadirla a mano.
- `SpriteRenderer` + `MaterialPropertyBlock` para `_FlashAmount`: se usa Get/Set para conservar `_MainTex`; verificar que el sprite no pierde la textura al destellar.
- Horneado legacy del NavMesh usa las mallas renderizadas: haces, cielo y fondo no son estáticos; verificar que alfombras/flecos no generen islas raras.
- Muchas mallas generadas en `Art/Environment/Generated/` (una por tamaño de caja); se sobrescriben en cada generación.
- Modelos `.glb/.gltf` solo cargan si hay un importador glTF instalado; `.fbx` funciona siempre.

## 12. Preguntas para el orquestador
- ¿Se aprueba primero la 3A (sigue PENDIENTE) antes de revisar esta?

## 13. Deuda técnica para fases siguientes
- Fase 4 debe construir el Campamento con `CartoonKit` + `NightSkyBuilder` (mismo kit).
- Los materiales `Fx_DestelloMalla/Sprite` de 3A quedan solo como respaldo.
- Sin sprites finales todavía: la regla de contraste se juzga con placeholders.
- `Docs/phases/PHASE-3A.md` tiene un cambio local ajeno a la fase (no se commiteó) y `Art/Characters/` está sin seguimiento.

## 14. Uso de contexto
- ¿Se leyó `graphify-out/GRAPH_REPORT.md`? sí (refrescado al inicio)
- Archivos abiertos (lista corta): BillboardSprite, EnemyCombatFx, CluePickup, RubberHoseMotion, NoirVolumeBuilder, NoirPostProcess, LevelBuilderUtils, LevelBuilder_NivelPorky, TestPrefabGenerator, ScreenSpaceAmbientOcclusion.cs (URP)
- Archivos abiertos que no se modificaron y por qué: RubberHoseMotion (comprobar que no inclina el sprite), ScreenSpaceAmbientOcclusion.cs (nombres de campos internos)

## 15. Corrección 3A2-fix
- **Causa:** `Shadows.hlsl` (URP 14.0.12) usa `LerpWhiteTo`, definido en `render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl`. URP Lit lo recibe de forma indirecta; el pase ShadowCaster de `CartoonEnvironment` incluía `Shadows.hlsl` sin esa dependencia, así que Unity rechazaba todo el shader (escenario rosado).
- **Commit:** 9e7216a — `dia3a2(david): fix include de CommonMaterial en shaders del escenario`

| Shader | Pase | Includes | ¿Cambió? |
|---|---|---|---|
| CartoonEnvironment | ForwardCartoon | Core, CommonMaterial (HLSLINCLUDE), Lighting.hlsl | sí (CommonMaterial añadido al HLSLINCLUDE común) |
| CartoonEnvironment | ShadowCaster | Core, CommonMaterial, Shadows.hlsl (mismo orden que ShadowCasterPass.hlsl de URP) | sí |
| CartoonEnvironment | DepthOnly | Core, CommonMaterial | sí (solo por el HLSLINCLUDE común) |
| CartoonEnvironment | DepthNormals | Core, CommonMaterial | sí (solo por el HLSLINCLUDE común) |
| CharacterSpriteUnlit | Unlit | Core | no |
| CharacterSpriteUnlit | DepthOnly | Core | no |
| CharacterPlaceholderUnlit | Outline | Core | no |
| CharacterPlaceholderUnlit | Unlit | Core | no |
| CharacterPlaceholderUnlit | DepthOnly | Core | no |
| BlobShadow | Blob | Core | no |
| LightCone | Cone | Core | no |
| SkyDome | Sky | Core | no |
| BackdropCard | Card | Core | no |

- **Otros problemas encontrados en la auditoría de shaders:** ninguno. El pase DepthNormals de `CartoonEnvironment` escribe la normal igual que `DepthNormalsPass.hlsl` de URP en Forward (la variante `_GBUFFER_NORMALS_OCT` solo la activa el renderizado diferido), por lo que el SSAO con fuente Depth Normals recibe datos válidos. `ShadowCaster` mantiene `_LightDirection`/`_LightPosition` y `multi_compile_vertex _CASTING_PUNCTUAL_LIGHT_SHADOW` como en URP. Fallbacks: `CartoonEnvironment` usa `Universal Render Pipeline/Lit`; los demás `Fallback Off` a propósito (efectos solo de URP). No se tocó C#, el generador de nivel ni materiales. No se pudo recompilar el shader aquí (Unity no corre en este entorno): la verificación es el reimport.
- **Qué debe revisar David en la consola al reimportar:**
  1. Que no aparezca ningún "Shader error" para `PlomoYPaja/*` (si aparece otro, pegar el texto completo).
  2. Que `Greybox_*` y `Kit_*` ya no se vean rosados al regenerar/abrir Nivel Porky.
  3. Que el SSAO oscurezca esquinas y bases de paredes (confirma que DepthNormals funciona).
