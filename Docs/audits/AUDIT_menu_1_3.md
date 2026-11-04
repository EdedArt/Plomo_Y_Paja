# AUDIT MENU.1.3 — Espera con fundido lento y movimiento de cámara

ESTADO: LUZ VERDE (orquestador, 3 nov)

**Fecha:** 3 nov 2026 · **Rama:** `integracion/dia4`. Se escribió en un worktree y entró por fast-forward: el bloqueo de edición del checkout compartido sigue activo (`bgIsolation` sin configurar).

## Qué se cambió (`MenuFrameAnimator`)

| Grupo de espera | Campo | Valor |
|---|---|---|
| Modo | `modoEspera` | Fundido |
| Cuadro mantenido | `mantenerEspera` | 2.0 s |
| Mezcla con el siguiente | `fundidoEspera` | 1.2 s |
| Respiración | `respirarEspera` | activa, con la amplitud y velocidad de siempre |

- **Ciclo:** 3.2 s por cuadro, 01→06 en bucle: 19.2 s la vuelta.
- **Micro-fundido:** eliminado.
- **Libro animado:** el código y `fpsEspera` / `variacionEspera` siguen ahí, sin usar; solo actúan con `modoEspera` = Hervido.
- **Tormenta:** su fundido no cambió (`segundosPorFrame` 1.6, `duracionFundido` 0.8).

| Movimiento de cámara (espera y tormenta) | Campo | Valor |
|---|---|---|
| Activo | `moverCamara` | sí |
| Zoom | `zoomCamara` | 1.00 → 1.04 |
| Desplazamiento | `recorridoX` / `recorridoY` | ±1.5 % del ancho / ±1 % del alto de pantalla |
| Ida y vuelta, ease in-out | `periodoCamara` | 20 s |

- **Sin salto con el rayo:** el movimiento depende solo del tiempo sin escala, no del grupo, y la respiración usa la misma fórmula en los dos. `CutToStorm` no los reinicia.
- **Sin GC:** solo operaciones numéricas por cuadro. No se midió en el Profiler.
- **Inicio del ciclo:** arranca en un extremo (zoom 1.00, −1.5 % / −1 %) y al llegar a 1.04 está en el extremo contrario.
- **Fuera del encargo:** M3a y el checklist de MENU.2 en `Fase-Menu.md`, para que no contradigan esto.

## Cobertura
El recorrido de cámara se suma al margen de cobertura en cada eje; zoom y respiración solo agrandan. Se calculó el peor caso: zoom 1.00, máximo desplazamiento, parallax ±12 y sacudida ±6 juntos. Es un cálculo, no se ha visto en pantalla.

| Pantalla | Lienzo | Holgura X | Holgura Y |
|---|---|---|---|
| 16:9 (1920×1080) | 1920×1080 | 14 px | 1 px |
| 16:10 (1920×1200) | 1821×1138 | 118 px | 1 px |
| 21:9 (2560×1080) | 2217×935 | 1 px | 153 px |
| 21:9 (3440×1440) | 2226×932 | 1 px | 157 px |

El tope de 1 px es el margen de seguridad de la fórmula.

## Para revisar en Unity
- Que el fundido se vea bien: durante 1.2 s de cada 3.2 s se mezclan dos dibujos distintos, y puede verse doble contorno.
- Que el título siga legible con el fondo en movimiento.

## Compile check
Roslyn de Unity 2022.3.62f3: `Assembly-CSharp` 107 fuentes, `Assembly-CSharp-Editor` 32 fuentes, **0 errores, 0 avisos**. Nada se probó en Unity.

## Archivos
- **Modificados:** `Scripts/UI/MenuFrameAnimator.cs`, `Docs/phases/Fase-Menu.md`.
- **Creados:** esta auditoría.

## Commits
`dia4(david): MENU.1.3 espera con fundido lento y movimiento de camara`
