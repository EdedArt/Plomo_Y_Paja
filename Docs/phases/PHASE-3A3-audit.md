# Auditoría Fase 3A3 — Primer sprite de guardia, contraste del interior, menús bloqueados en Play

ESTADO: PENDIENTE DE REVISIÓN

## 1. Resumen
- El primer dibujo de guardia (`guardia_idle_01.png`) queda importado (PPU 236, ≈ 1.9 m), enlazado al prefab por el set `guardia` y reproducible desde "Crear Prefabs de Prueba". El contorno del placeholder ahora es de 3 px de pantalla.
- El interior de la casa deja de ser todo naranja: papel verde grisáceo, piso marrón oscuro, cielorraso violeta oscuro, sombras azules, lámparas con menos alcance y SSAO más fuerte.
- Todos los menús "Plomo y Paja" se niegan a correr en Play. Nada se probó en Unity (no se puede ejecutar aquí).

## 2. Commits
| Hash | Mensaje |
|---|---|
| dce35a6 | dia3a3(edgar): primer sprite de guardia |
| 8aec576 | dia3a3(cristian): contraste del interior |
| (este commit, último de `git log`) | dia3a3(david): menus bloqueados en play mode |

## 3. Sprite
- Archivo usado: `Assets/_Game/Art/Characters/guardia/guardia_idle_01.png` (único candidato; estaba en una carpeta `Guardia/` con mayúscula, renombrada a `guardia/` para coincidir con la guía y con el `characterId`; el importador y el constructor de sets ahora pasan el id a minúsculas).
- Tamaño: 512 × 512 px, RGBA 8 bits. Silueta: 234 px de ancho × 449 px de alto (y 62–510), pies pegados al borde inferior.
- PPU: **236** (override `guardia` en `CharacterSpriteImporter`; el resto de personajes sigue en 250). 449 / 236 = **1.90 m** de alto, ≈ 0.99 m de ancho; lienzo completo 2.17 m. Pivote Bottom Center, Bilinear, sin compresión, Max Size 2048, Alpha Is Transparency. `GetVersion()` subido a 2 para que Unity reimporte.
- ¿Tiene transparencia real? **Sí.** 81.5 % de los píxeles con alpha 0 (esquinas incluidas); de los píxeles visibles, 90 % tienen alpha ≥ 250, 7 % entre 128 y 249 y 4 % por debajo de 128, así que el alpha clip a 0.5 recorta limpio (sin tablero de ajedrez horneado ni fondo opaco).
- Enlace: `Crear Prefabs de Prueba` ahora corre `Reconstruir Sets de Sprites` antes de crear los prefabs; el prefab del guardia referencia el set `guardia` y `BillboardSprite` oculta la cápsula cuando el set tiene frames (usa `CharacterSpriteUnlit`, alpha clip 0.5, billboard solo en Y, `BlobShadow`).

## 4. Causa del contorno invisible y arreglo
- Causa (probable, sin poder ejecutar Unity): el casco invertido se inflaba 0.025 m en espacio de mundo → ~1 px a 8 m, negro sobre un guardia gris oscuro de noche; además las aristas de los cubos se separaban al inflar por la normal.
- Arreglo: grosor en **píxeles** (3 px, `_OutlinePixels`, empuje en espacio de recorte corregido por aspecto y `w`), dirección del casco = normal + posición (sin grietas), leve sesgo de profundidad y `_Lift` 0.3 que aclara el relleno. Si sigue sin verse, subir `_OutlinePixels` en `Resources/CharacterLook/PersonajePlaceholder`.

## 5. Valores de interior cambiados
| Objeto | Antes | Después |
|---|---|---|
| Paredes interiores sobre el zócalo | núcleo de paja `#E8C35A` | `Kit_Empapelado` `#5F7A74` (verde grisáceo), sombra azul, oscurece hacia el piso (0.5 hasta 2.2 m) |
| Piso de tablas | `Madera` `#8A5A33` / `MaderaClara` `#B07A45` | `PisoTabla` `#4A3426` / `PisoTablaClara` `#5A412F`, sombra azul |
| Cielorraso (cocina y sala) | `MaderaOscura` `#5A3A22` | `Cielorraso` `#2C2A3E` |
| Rellenos de los arcos de puerta (cara interior) | paja | mismo empapelado |
| Lámpara colgante sala | 1.5 / alcance 7.5 | 1.7 / 5.5 |
| Aplique sala | 0.9 / 5 | 1.0 / 3.5 |
| Lámpara cocina A / B | 1.4 / 7 y 1.3 / 6.5 | 1.6 / 5 y 1.5 / 5 |
| Aplique dormitorio | 1.0 / 5.5 | 1.1 / 4 |
| SSAO (Renderer URP) | Intensity 1, Radius 0.3, Direct 0.25 | Intensity 2, Radius 0.45, Direct 0.4 |
| `CartoonEnvironment` (ambiente) | AO lineal | AO al cuadrado en el término ambiente |

Verificado en el repositorio: la feature SSAO está en el Renderer con Source = Depth Normals, y el pase DepthNormals de `CartoonEnvironment` existe (corregido en 3A2-fix). No se tocaron marcadores, actores, NavMesh ni posiciones de pistas.

## 6. Menús a ejecutar en orden
1. Plomo y Paja → Configurar Renderer (SSAO)
2. Plomo y Paja → Crear Prefabs de Prueba (ya reconstruye los sets de sprites y los materiales de personaje)
3. Plomo y Paja → Generar Nivel Porky

## 7. Pruebas en Unity (las llena David)
| # | Prueba | Resultado (✅/❌) | Nota |
|---|---|---|---|
| 1 | Consola limpia | | |
| 2 | Guardia con sprite, sin fondo, tamaño correcto (~1.9 m), mira siempre a la cámara | | |
| 3 | Placeholder con contorno negro visible | | |
| 4 | Interior con contraste (zonas frías y piletas cálidas) | | |
| 5 | Esquinas más oscuras | | |
| 6 | Menús en Play muestran el aviso | | |

Errores de consola:

Captura(s):

## 8. Riesgos / preguntas para el orquestador
- El contorno del placeholder es un diagnóstico probable, no medido; el shader solo se valida al importar en Unity.
- Hay un único frame (`idle`): el guardia no tiene animación de caminar/disparar, `BillboardSprite` usa idle para todo. Edgar debe entregar los demás frames con el mismo tamaño de lienzo y PPU 236.
- Sin `Crear Prefabs de Prueba` + `Generar Nivel Porky` el nivel actual no refleja nada de esta fase.
- Se versionó `Assets/_Game/Resources/CharacterLook/` (materiales de personaje; sin ellos los shaders no entran al build). El resto de assets generados por 3A2 (materiales `Kit_*`, mallas, escena, prefabs) siguen sin commitear y se regeneran con los menús.
- No se commiteó el cambio local de `Docs/phases/PHASE-3A.md`.
