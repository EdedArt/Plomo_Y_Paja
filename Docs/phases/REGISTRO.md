# Registro de trabajo cerrado — Plomo y Paja

Una entrada por cada bloque de trabajo que el orquestador cierra (luz verde final). **Lo más nuevo va arriba.**
Aquí va el resumen para el equipo; el detalle de cada subfase está en su auditoría (`docs/audits/`).

**Cómo agregar una entrada** (copiar el bloque de abajo y llenarlo):

```
## AAAA-MM-DD — <Bloque> · Fases <X → Y> · CERRADA
Responsable · Rama · Auditorías
### Resultado        (2–3 líneas)
### Subfases         (tabla: qué se hizo y por qué hizo falta)
### Decisiones       (lo que ya no se vuelve a discutir)
### Para los demás   (lo que cambió en el proyecto y afecta a otros)
### Pendientes que dejó
### Commits principales
```

---

## 2026-10-31 — Viewmodel de Wilbur (manos en primera persona) · Fases VM 1 → 1.4 · CERRADA

**Responsable:** David (Claude Code, orquestado). **Rama:** `player` → PR a `dev`.
**Auditorías:** `docs/audits/AUDIT_viewmodel_phase1*.md` (la 1.3 quedó en `docs/phases/`).

### Resultado
Manos estilo rubber-hose de los años 30 (referencia: Mouse P.I. For Hire) con la escopeta: dibujos 2D a pantalla
completa animados por código, apuntar con clic derecho con la mirilla en el centro exacto, fogonazo dibujado, mira
propia y onomatopeya ocasional.

### Subfases
| Subfase | Qué se hizo | Por qué hizo falta |
|---|---|---|
| 1 | Sistema base: `ViewmodelProfile` (ScriptableObject por arma), `ViewmodelAnimator` (cuadros a 12 fps), `ViewmodelMotion` (respiración, balanceo en 8, inercia del mouse, culatazo con resorte, bajada de recarga), `PlayerAim` (clic derecho: FOV −15°, dispersión ×0.6, velocidad ×0.6, sin correr), fogonazo como sprite; las manos se ocultan en pausa, diálogo y muerte. | Las manos viejas eran un dibujo de perfil, pequeño y sin vida. |
| 1.1 | Herramienta de recorte `Tools/viewmodel_key.py` (croma suave, sin halo verde), importación sin compresión y con mipmaps, `ViewmodelLighting` (tinte y destello al disparar), interfaz `IWeapon` mínima, arte del revólver recortado y `VM_Revolver`. | El verde se quitó con un corte duro: bordes en escalera y menos calidad; las manos no recibían el post-procesado. |
| 1.2 | Manos ancladas abajo a la derecha según el alto de pantalla, con 8 % de margen (overscan); `ViewmodelCamera` (Overlay, capa `Viewmodel`) en el stack de URP; resorte con sub-pasos. | Se despegaban de la esquina en pantallas anchas; la lluvia y la sangre se dibujaban encima; salto raro al disparar. |
| 1.3 | Reloj propio en tiempo real (`ViewmodelTime`) para que el hit-stop no congele las manos; pivote (0.9, 0.05); tiempos ágiles (disparo 0.08 s, bombeo 0.35 s, resorte 32/0.7); mira `RubberHoseCrosshair` con canvas propio y marcador de impacto rojo. | Las manos se quedaban pegadas tras disparar; no había mira (`UIManager` no existe en ninguna escena). |
| 1.4 | Mirilla centrada exacta al apuntar en cualquier formato (`aimSightPoint` medido del PNG); mira de gotas estilo Mouse (44 px); onomatopeya cada 15–20 disparos con palabra al azar; dispersión de la escopeta en ejes de la cámara; FX del revólver; limpieza de Git (prefab y assets de 3A–3A8). | La mirilla quedaba corrida en pantallas que no son 16:9; el ¡BANG! en cada disparo cansaba; la dispersión se deformaba según hacia dónde se mirara. |

### Decisiones
- **Armas:** escopeta (hecha) y revólver de detective (pendiente, VM2). Se descartaron MP40 y Thompson.
- **Arte por IA:** PNG con fondo verde puro `#00FF00`, generado desde una imagen maestra por arma; cada frame se edita **siempre desde la maestra**, nunca encadenando ediciones.
- **Contrato del arte** (`Art/Viewmodel/README_ART.md`): manos 1920×1080 dibujadas en su sitio; FX cuadrados y centrados; nombres `wilbur_vm_<arma>_<clip>_<nn>.png` y `fx_<nombre>_<nn>.png`.
- **Revólver:** se sostiene con una sola mano; la izquierda solo aparece al recargar. Las balas del tambor las coloca el código (la IA no cuenta bien).
- La mira no depende de `UIManager`.

### Cómo agregar o cambiar arte de las manos
1. Original con fondo verde en `_entrante_viewmodel/<Arma>/` (esa carpeta no se sube a git).
2. `python Tools/viewmodel_key.py <Arma>` → deja los PNG listos en `Art/Viewmodel/<Arma>/`.
3. Unity: **Plomo y Paja → Crear Perfil Viewmodel <Arma>** y luego **Crear Prefabs de Prueba**.
4. Ajustes finos (tiempos, anclas del cañón, movimiento) en `Art/Viewmodel/VM_<Arma>.asset`.

### Controles y depuración
- Clic izquierdo: disparar · clic derecho (mantener): apuntar · R: recargar.
- Main Camera → *First Person Viewmodel*: **Debug Fire Log** (culatazo) y **Debug Sight Log** (centrado de la mirilla).

### Para los demás (lo que cambió en el proyecto)
- Capa nueva **`Viewmodel`** (índice 8) en `TagManager.asset`. No usar ese índice para otra cosa.
- El Player tiene una `ViewmodelCamera` hermana de la Main Camera; la Main Camera no ve la capa `Viewmodel`.
- **`PlayerAim` escribe el FOV de la cámara principal cada frame:** ningún otro script debe cambiar `fieldOfView` directo; hay que hacerlo a través de `PlayerAim`.
- **`IWeapon`** (`Scripts/Player/IWeapon.cs`): toda arma nueva la implementa; el HUD y la mira leen el arma por `IWeapon`.
- El hit-stop de `CartoonFx` (`timeScale` 0) no congela las manos: usan tiempo real.
- La dispersión de la escopeta ahora va en los ejes de la cámara; puede cambiar un poco el balance de daño.
- Ya existe una mira (`UI/RubberHoseCrosshair.cs`): no crear otra en el HUD.

### Pendientes que dejó
- **VM2:** revólver jugable, cambio de armas y recarga con balas visibles → `docs/phases/PHASE-VM2.md`.
- **HUD de vida y munición:** no existe en ninguna escena (Fase 3B, Parte 4).
- `ShotgunFeedback`: la luz del disparo y la onomatopeya no siguen la boca del cañón.
- El SSAO también corre para la `ViewmodelCamera` (costo extra en PCs débiles).
- Opcional: bocanada de humo después del disparo.

### Commits principales
- 1.3: `04742f9`, `c5fd7f6`, `8f87374`
- 1.4: `fb0ab45`, `231a0be`, `06139dd`, `a73815a`, `502394d`, `0a44ff7`, `bb1f0ef`, `339f2ab`
