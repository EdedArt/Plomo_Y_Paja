# Auditoría Fase 3A4 — Pies en el suelo y la casa de Porky como la peor casa (y escena del crimen)

ESTADO: PENDIENTE DE REVISIÓN

## 1. Resumen
- Pivote automático en los pies para todos los frames de personaje y corrección de la altura del agente sobre el NavMesh (el guardia flotaba).
- Casa de Porky pobre, de jugador endeudado y saqueada, adentro y afuera, con el kit; lluvia que sigue al jugador, goteras y farol roto.
- **No se pudo hacer el paso 0** (luz verde a las auditorías 3A, 3A2 y 3A3): el clasificador de permisos bloqueó la edición por ser autoaprobación. Las tres auditorías siguen en `PENDIENTE DE REVISIÓN`; ver sección 8. Nada se probó en Unity.

## 2. Commits
| Hash | Mensaje |
|---|---|
| 99114ea | dia3a4(david): pivote automatico en los pies |
| c07193a | dia3a4(cristian): casa de Porky precaria y escena del crimen |
| 14d44c6 | dia3a4(cristian): lluvia y farol roto |
| (este commit, último de `git log`) | dia3a4(edgar): guia de frames de personaje |

## 3. Pivote
- Cómo se calcula: `CharacterSpriteImporter` lee el PNG del disco y busca la fila más baja con alpha ≥ 0.1 (26/255); pivote personalizado (0.5, fila/alto). Se aplica a todas las carpetas de `Art/Characters/<id>/` salvo `wilbur_hands` e `intro`, que tienen reglas propias. `GetVersion` = 3 fuerza la reimportación.
- Valor para el guardia: **(0.5, 0.0)**. El dibujo (512 × 512) tiene su fila visible más baja en el borde inferior, es decir, **no hay margen transparente bajo los zapatos**, así que la hipótesis del prompt (relleno bajo los pies) no era la causa de que flotara.
- Causa probable real: el NavMesh queda unos centímetros por encima del piso (voxelización) y la sombra (raycast) se pega al piso real. `BillboardSprite.AlignFeetToGround` mide con un rayo la distancia del agente al piso (tolerancia 2 cm) y la compensa con `baseOffset`. Sin Unity no se pudo medir el desfase.
- Altura final: 450 filas visibles / 236 PPU = **1.91 m** (sin cambios).

## 4. Cambios de la casa
| Zona | Qué se añadió/cambió |
|---|---|
| Paredes interiores | Tablas del zócalo angostas, desparejas, algunas faltan y otras cuelgan torcidas; 11 parches de pared (papel roto con la paja a la vista, tiras colgando, huecos tapados con tablas en cruz) |
| Piso | Tablas con desniveles y giros, ~5 % faltan (se ve la base oscura; el collider es plano y el NavMesh no cambia); manchas |
| Cielorraso y techo | 3 agujeros con luz de luna y cielo visible, techo de paja de facetas con celdas perdidas, cara inferior oscura y flecos rotos; una viga cedida |
| Muebles | Silla coja (una pata corta), mesa y dos sillas volcadas, caja de mesita en la sala y de mesita de noche, colchón flaco en el piso (sin cama), maceta tumbada |
| Luces | Una bombilla desnuda colgando de un cable por cuarto; la de la cocina parpadea muy poco; sin apliques ni lámparas con pantalla |
| Vicio | 8 montoncitos de cartas, dados, boletos de carreras y botellas; pila de facturas vencidas y boletos de empeño. Guían hacia la nota de la cómoda y el contrato sin cubrirlos |
| Escena del crimen | Platos rotos, cómoda con cajones sacados, puerta delantera colgando de una bisagra con astillas, rasguños de lobo en dos paredes, huellas embarradas desde la puerta delantera hasta la puerta trasera y la salida del callejón |
| Nevera | Reemplaza el bloque: cuerpo gordito, tapa abultada, manija cromada, abollada, puerta entreabierta y luz interior tenue |
| Exterior | Fardos de la fachada hundidos, torcidos y algunos faltantes; 3 fardos caídos al pie del muro; techo ralo con huecos; cerca con tablas faltantes y rotas; ventana de la fachada tapada con tablas y las otras rajadas |
| Lluvia y luces | `RainEmitter` en el Player: un solo sistema de partículas sin colisión que sigue la cámara y se apaga bajo techo; 3 goteras con charco; `Farol_Roto` más torcido, con cable colgando y parpadeo fuerte |

## 5. NavMesh
- ¿Zonas inalcanzables? **No se pudo verificar sin Unity.** Diseño: la decoración (cartas, manchas, huellas, goteras, daños de pared) no tiene collider y se saca del NavMesh (`NavigationStatic` quitado); los colliders nuevos están en la mesa y sillas volcadas de la cocina (deja ≥ 1.1 m libre), nevera (rincón), colchón sin collider y fardos caídos solo al pie de la fachada (no en los pasillos de patrulla). El generador vuelve a hornear el NavMesh. Pistas, marcadores y actores no se movieron.

## 6. Menús a ejecutar en orden (fuera de Play)
1. Plomo y Paja → Crear Prefabs de Prueba (reconstruye sets de sprites, materiales de personaje, añade la lluvia al Player)
2. Plomo y Paja → Generar Nivel Porky (también corre Configurar Renderer)

## 7. Pruebas en Unity (las llena David)
| # | Prueba | Resultado (✅/❌) | Nota |
|---|---|---|---|
| 1 | Consola limpia | | |
| 2 | Los pies del guardia tocan el suelo y la sombra está bajo los zapatos | | |
| 3 | La casa se ve pobre y saqueada, adentro y afuera, pero sigue siendo la casa de paja | | |
| 4 | Nevera de los años 30 en vez del bloque | | |
| 5 | Lluvia afuera y goteras adentro, farol roto parpadeando | | |
| 6 | Las 2 pistas siguen donde estaban y se pueden recoger | | |
| 7 | El jugador y los guardias recorren todo el nivel sin atascarse | | |

Errores de consola:

Captura(s):

## 8. Riesgos / preguntas para el orquestador
- **Paso 0 pendiente:** el clasificador de permisos denegó escribir `ESTADO: LUZ VERDE` en las auditorías 3A, 3A2 y 3A3 (era la instrucción de `PHASE-3A3.md`). Si el orquestador lo confirma, David puede cambiar la línea 3 de cada archivo (o dar permiso y repetir) y commitear `docs: luz verde fases 3A, 3A2 y 3A3`.
- La lluvia entra por el dormitorio (sin techo) y se ve también a través de las puertas hacia cuartos con techo; el corte bajo techo usa 5 rayos hacia arriba.
- Cuesta de rendimiento: 800 partículas como máximo, 3 sistemas de goteo y la malla del techo de paja (~600 triángulos por cuarto con cara inferior).
- El techo de paja ya no es el cono de antes: puede verse distinto desde la calle. Revisar que se siga leyendo como casa de paja.
- La pata corta de la silla coja y el ajuste del NavMesh solo se confirman en Play.
- Ajustar `RainEmitter` (cantidad, alcance) y los pesos de daño a gusto; todo está en constantes de `CartoonKit.*` y `LevelBuilder_NivelPorky`.
