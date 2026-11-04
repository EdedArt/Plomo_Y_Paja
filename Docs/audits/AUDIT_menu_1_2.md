# AUDIT MENU.1.2 — Animación de espera, botones y cuadros sin marca de agua

ESTADO: PENDIENTE DE PRUEBA EN UNITY

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4`. Se escribió en un worktree y entró por fast-forward: el bloqueo de edición del checkout compartido sigue activo (`bgIsolation` sin configurar).

## Qué se cambió

**Cuadros de espera** (`MenuFrameAnimator`; tormenta sin cambios):

| Valor | Campo | Antes |
|---|---|---|
| 4 fps, rango 2–8 | `fpsEspera` | 8 fps, rango 6–12 |
| ±15 % | `variacionEspera` | igual |
| Micro-fundido 0.05 s; 0 = corte seco | `microFundidoEspera` (nuevo) | no existía |

**Botones:**
- **De dónde salía el negro:** `BotonCartoon` es `[ExecuteAlways]`. Al agregarlo, `OnEnable` creaba el hijo `Borde` con el grosor por defecto (6), antes de que el builder pusiera 0. Con grosor 0 nadie lo quitaba, y como hijo se dibuja encima de la placa: una silueta casi negra. Se confirmó en la escena: 6 `Borde` activos, color (0.08, 0.06, 0.05), `grosorBorde` 0.
- **Arreglo:** `BotonCartoon` apaga el `Borde` si el grosor es 0, y `CrearMenuInicio` lo borra al crear cada botón, así que sobrevive a volver a correr el menú.
- **Placa:** `boton_placa.png`, tipo Simple (antes Sliced), `preserveAspect`, color blanco, 330×110 (proporción 3:1 de la placa; antes 420×110).
- **Pausa: tenía el mismo defecto** (4 `Borde` en `PauseMenu_Canvas.prefab`). El cambio en `BotonCartoon` lo corrige en Play sin tocar `CrearMenuPausa` ni el prefab. Sus botones siguen en Sliced y 420 de ancho.

**Arte** (los 8 miden 2752×1536, JPEG, sin hashes repetidos):
- `menu_inicio_5.jpg` estaba **mal nombrado**: se renombró a `menu_inicio_05.jpg`.
- Borrados `menu_inicio_05.png`, `menu_inicio_06.png`, `menu_rayo_02.png` y sus `.meta`. En los recortes de la esquina inferior derecha de los 3 cuadros nuevos ya no se ve la estrellita.
- `.meta`: los 5 que creó Unity conservan su GUID. Los de `menu_inicio_05`, `menu_inicio_06` y `menu_rayo_02` aún no existían (Unity no los había importado): se crearon copiando el de `menu_inicio_01.jpg` con GUID nuevo.
- Sin compresión: los `.meta` copiados tienen `textureCompression: 0`, sin mipmaps y máximo 2048, y `MenuSpriteImporter` los fuerza al importar. No se vio en Unity.
- Referencias rotas: los GUID de los 3 PNG no aparecen en `MenuPrincipal.unity`, y el builder busca por nombre.

`Fase-Menu.md`: M3a actualizada a 4 fps y micro-fundido.

## Para revisar en Unity
- **Decisión de David:** se dejaron los `FondoOscuro` a pantalla completa de Opciones y Créditos, detrás de "Volver" y de los textos, porque sin ellos la letra pierde legibilidad sobre el dibujo. El encargo pide "sin panel oscuro detrás"; si los quieres fuera, se quitan en `CrearMenuInicio`.
- El texto sobre la placa: el área útil bajó a unos 250 px de ancho.
- `boton_placa.png` tiene transparencia y `MenuSpriteImporter` usa `alphaIsTransparency` apagado: revisar que no quede un filo oscuro en el borde.

## Compile check
Roslyn de Unity 2022.3.62f3: `Assembly-CSharp` 107 fuentes, `Assembly-CSharp-Editor` 32 fuentes, **0 errores, 0 avisos**. Nada se probó en Unity.

## Archivos
- **Modificados:** `MenuFrameAnimator.cs`, `BotonCartoon.cs`, `CrearMenuInicio.cs`, `Fase-Menu.md`.
- **Creados:** `menu_inicio_05.jpg`, `menu_inicio_06.jpg`, `menu_rayo_02.jpg`, los `.meta` de los 8 cuadros y esta auditoría.
- **Borrados:** los 3 PNG viejos.
- **Sin commitear (fuera de este encargo):** `MenuPrincipal.unity`, `PauseMenu_Canvas.prefab`, los atlas `LuckiestGuy Titulo SDF.asset` e `IMFellEnglish-Italic Titulo SDF.asset`, y varios `.meta` de otras fases.

## Commits
`dia4(david): MENU.1.2 animacion de espera mas lenta, botones con boton_placa y cuadros sin marca de agua`
