# Auditoría Viewmodel Fase 1.3 — Disparo ágil, mira y cierre de las manos de Wilbur

ESTADO: PENDIENTE DE REVISIÓN

Rama: `player`, publicada en `origin/player`. Compila con 0 errores y 0 warnings nuevos (Runtime 59, Editor 0). **No se probó nada en Unity.**
**Hay una pregunta abierta sobre `Player.prefab` y `NivelPorky.unity` (sección 4): no los commiteé.**

## 1) Causa de la demora y qué se cambió
Las tres causas aplican. La 1 y la 3 están confirmadas por lectura del código; la 2 es un cálculo a mano. Ninguna está medida en pantalla.

1. **Reloj con escala (causa principal).** En la fase 1.2 puse `ViewmodelMotion` y `ViewmodelLighting` en `Time.deltaTime` (con escala), y `ViewmodelAnimator` ya lo usaba.
   `CartoonFx` pone `Time.timeScale = 0` en cada golpe a un enemigo (hit-stop), así que el resorte del culatazo y los cuadros se congelaban en la pose de disparo mientras el mundo estaba parado.
   **Cambio:** nuevo `ViewmodelTime.Delta` = `Time.unscaledDeltaTime` recortado a 1/30 s, usado por el movimiento, el animador, el tinte, la mira y el avance de apuntar (`PlayerAim`).
   Los sub-pasos de 1/120 s del resorte se mantienen. `PlayerAim.SuppressFor` también pasó a `unscaledTime`.
2. **Corrección de bordes empujando las manos.** `KeepEdgesOffscreen` movía las manos hacia abajo en el culatazo porque el pivote (0.65, 0.1) está lejos de la esquina (una rotación de 8° mueve la esquina ~100 px).
   **Cambio:** el pivote de `ViewmodelRoot` pasó a (0.9, 0.05). Con la raíz estirada la pose en reposo no cambia al mover el pivote. Cálculo a mano en 16:9 con el culatazo máximo del perfil
   (+8°, escala 1.10/0.90, desplazamiento (40, −60)): la esquina inferior derecha queda en y ≈ −132 y el borde de pantalla está en −54, así que la corrección es **0 px** (antes ~60 px). El recorte sigue activo para el resto de casos.
3. **Tiempos de los clips y del resorte.** Los valores anteriores (disparo 0.15 s + bombeo 0.45 s + resorte de ~0.25 s) dejaban las manos 0.6 s fuera de reposo.
   **Nuevos valores** (en `ViewmodelProfile`, que ya los exponía por clip como `minDuration`):

| Campo | Antes | Ahora |
|---|---|---|
| `fire.minDuration` / `aimFire.minDuration` | 0.15 s | **0.08 s** (un cuadro "de a dos" a 12 fps = 0.083 s) |
| `cycle.minDuration` / `aimCycle.minDuration` | 0.45 s | **0.35 s** |
| `kickSpringFrequency` | 22 rad/s | **32 rad/s** |
| `kickSpringDamping` | 0.6 | **0.7** (pico en el primer cuadro, de vuelta a reposo en ~0.13 s con un 5 % de rebote) |

   **`VM_Shotgun.asset` y `VM_Revolver.asset`:** no se editan a mano (regla del proyecto). El perfil tiene `timingVersion` y `ViewmodelProfileBuilder` aplica los valores nuevos a los assets viejos
   **al abrir el Editor** (y al ejecutar los menús). Los assets commiteados siguen con los valores viejos hasta entonces: al abrir Unity sale
   `[Viewmodel] 2 perfil(es) actualizados…` y hay que volver a commitearlos.
   *Pendiente de ajuste:* el bombeo empieza 0.08 s después del disparo y dura 0.35 s, así que termina a los 0.43 s; el sonido del bombeo suena a los 0.35 s, o sea en los últimos 0.08 s del clip.
   Si se quiere que el sonido caiga justo al final del bombeo, bajar `cycle.minDuration` a 0.27 s.

**Comprobación con Debug Fire Log:** con el flag encendido el log debe mostrar, en cada disparo, `dt=0.0000 unscaledDt=0.0xxx timeScale=0.00` durante el hit-stop y la posición recuperándose a (0, 0) en ~0.15 s.
El salto de la fase 1.2 no debería volver porque la ViewmodelCamera es hermana de la principal; no lo pude verificar sin Unity.

## 2) Por qué no se veía la mira y de dónde salen los disparos
**Por qué no se veía:** `UIManager` **no existe en ningún sitio del juego**. Ni `NivelPorky.unity`, ni `SampleScene.unity`, ni ningún prefab referencian su script (busqué su GUID), y ningún script de Editor lo construye: su cabecera pide
crear el Canvas a mano y asignar `crosshair`, y eso nunca se hizo. Por eso `UIManager.Instance` es `null`, el campo `crosshair` nunca se asignó y el gancho que puse en `PlayerAim` (`SetCrosshairHiddenByAim`) no hacía nada.
De paso: tampoco hay HUD de vida ni de munición en ninguna escena (no lo toqué).
**Solución:** la mira construye su propio canvas Overlay y no depende de `UIManager`.

**De dónde salen los disparos:** `ShotgunController.ShootPellet` lanza `Physics.Raycast(new Ray(playerCamera.transform.position, forward + dispersión))`, con `playerCamera` = la cámara principal (la asigna el generador; si no, `Camera.main`).
Es el **centro de la cámara**, no la boca del cañón, y equivale a `ViewportPointToRay(0.5, 0.5)` para esta cámara (sin lens shift). El punto central de la mira es, por tanto, adonde van los perdigones. No cambié nada de la lógica de disparo.
**Hallazgo (sin tocar):** la dispersión se suma como `new Vector3(rand, rand, 0)` en ejes del **mundo** (X, Y), no en los ejes de la cámara. Mirando a lo largo del eje X del mundo, el desvío "horizontal" va en la dirección del disparo y no abre el cono hacia los lados,
y mirando arriba o abajo el cono queda deformado. Conviene corregirlo (usar `transform.right` y `transform.up`) en una fase de armas. Valores reales: cadera `spreadAngle` 5 → ±0.05 por eje (~±2.9°, unos ±47 px a 1080p con FOV 60°); apuntando ×0.6.

**La mira (`RubberHoseCrosshair`):** punto blanco con contorno negro y 4 marcas cortas redondeadas, sprites generados por código (relleno blanco, contorno negro de 1.8 px, antialias), ~28 px en total a 1080p (canvas Overlay con `match = 1`, orden 5: por encima del "¡BANG!" y por debajo del diálogo).
- **Apertura:** las marcas siguen `IWeapon.SpreadAngle` (la dispersión actual, convertida a píxeles con el FOV de la cámara y mostrada al 30 % de su tamaño real, `spreadVisualScale`) y se abren hasta +6 px caminando rápido. **Eso último es solo un adorno:** la dispersión real no cambia al caminar.
- **Al disparar:** se abren un 40 % y vuelven en 0.12 s (tiempo real).
- **Se oculta** al apuntar (`PlayerAim`), en diálogo y pausa (`GameplayBlocked`) y al morir (`PlayerHealth.OnDeath`); `UIManager` no existe, así que se reutilizó la misma lógica que usa el viewmodel.
- **Marcador de impacto: sí se hizo.** Existe `EnemyHealth.OnDamaged` por enemigo, así que la mira se suscribe a todos (y revisa cada 2 s por si aparece alguno) sin tocar `ShotgunController`: las marcas se ponen rojas 0.1 s. Se dispara por cada perdigón que acierta.

## 3) Archivos tocados
**Creados**
- `Scripts/Player/ViewmodelTime.cs` — reloj único del viewmodel (tiempo real, recortado a 1/30 s).
- `Scripts/UI/RubberHoseCrosshair.cs` — la mira.

**Modificados**
- `Scripts/Player/ViewmodelMotion.cs`, `ViewmodelAnimator.cs`, `ViewmodelLighting.cs`, `PlayerAim.cs` — reloj único.
- `Scripts/Player/FirstPersonViewmodel.cs` — pivote de la raíz (0.9, 0.05).
- `Scripts/Player/ViewmodelProfile.cs` — tiempos nuevos por defecto y `timingVersion`.
- `Scripts/Editor/ViewmodelProfileBuilder.cs` — migración de los perfiles viejos a los tiempos nuevos.
- `Scripts/Player/IWeapon.cs` y `ShotgunController.cs` — solo `float SpreadAngle { get; }` de solo lectura (`ShootPellet` no cambió).
- `Scripts/Editor/TestPrefabGenerator.cs` — añade la mira al Player.
- `Art/Viewmodel/**/*.png.meta`, `VM_Shotgun.asset`, `VM_Revolver.asset` (+ `.meta`) y `ProjectSettings/TagManager.asset` (capa `Viewmodel`, índice 8) — generados por Unity.

## 4) Commits creados y resultado del push
| Hash | Mensaje |
|---|---|
| 04742f9 | fix(viewmodel): snappy fire timing, real-time clock and corner pivot |
| c5fd7f6 | feat(hud): rubber-hose crosshair |
| 8f87374 | chore: viewmodel art import settings, profiles and Viewmodel layer |
| (este commit) | docs: viewmodel phase 1.3 audit |

Push: `origin/player` `e946f33..8f87374` (avance rápido, sin force; no se abrió PR). Como `player` se había adelantado a `integracion/dia3` en la fase 1, ese push trajo a `origin/player` todo ese historial (44 commits) además de las fases 1 a 1.3 del viewmodel.

**NO commiteado (pregunta para David):**
- **`Player.prefab`**: su diff mezcla cambios del viewmodel (ViewmodelCamera en la capa 8, pila de cámaras, máscara de la principal, `PlayerAim`, perfil en `FirstPersonViewmodel`) con cambios **ajenos**: el componente `RainEmitter`
  (lluvia, fase 3A4) y `PlayerFootsteps` (pasos, fase 3A6), que se añadieron al prefab sin commitearlos. Por la regla, los dejé fuera. **¿Lo commiteo entero (con lluvia y pasos) en un `chore: assets generados`?**
  Mientras no esté, quien haga pull tiene el prefab viejo: la ViewmodelCamera, `PlayerAim` y la mira no existen hasta ejecutar **Plomo y Paja → Crear Prefabs de Prueba**.
- **`NivelPorky.unity`** (diff de ~39 000 líneas) y `NavMesh.asset`, materiales de Greybox, `.meta` de audio y de personajes, `AudioLibrary.asset`, los dos assets de URP, `Art/SpriteSets/*` y `Art/Environment/`: cambios generados de las fases 3A–3A8, no son del viewmodel.
- `Docs/audits/PHASE-3A7-audit.md` y `PHASE-3A8-audit.md` (tus ediciones de `ESTADO:`) y `Docs/phases/PHASE-3A8.md`.
- **Auditorías del viewmodel:** en tu copia de trabajo moviste `AUDIT_viewmodel_phase1*.md` de `Docs/phases/` a `Docs/audits/` sin commitear. No toqué esa mudanza; esta auditoría quedó en `Docs/phases/` como pide el enunciado. Dime si quieres que commitee la mudanza.
- `Art/Viewmodel/FX/fx_casing_revolver.jpg` y `fx_round_rim.jpg` (con sus `.meta`): llegaron como JPG, que `Tools/viewmodel_key.py` no lee (solo PNG), y están dentro de `Assets/`. Hay que pasarlos al flujo (originales a `_entrante_viewmodel/FX/` y recortarlos) antes de usarlos.
- `_entrante_viewmodel/` nunca se commitea (está en `.gitignore`).

## 5) Cómo probar en 5 pasos
Antes: abrir Unity (debe salir `[Viewmodel] 2 perfil(es) actualizados a los tiempos de la fase 1.3…`) y ejecutar **Crear Prefabs de Prueba** (añade `PlayerAim`, la ViewmodelCamera y la mira al Player).
1. **Disparar a un enemigo (con hit-stop):** las manos salen del culatazo y vuelven a reposo mientras el mundo está congelado, sin quedarse en la pose de disparo. Opcional: activar **Debug Fire Log** y comprobar que el salto no vuelve y que `unscaledDt` avanza con `timeScale=0.00`.
2. **10 disparos seguidos:** cada disparo se ve ágil: culatazo con pico inmediato, vuelta a reposo en ~0.15 s, bombeo corto y vuelta a idle sin quedarse pegado.
3. **Apuntar y soltar** (con y sin disparar): el zoom y las manos entran y salen sin trabarse, también si hay un hit-stop en medio.
4. **Mira:**
   - en cadera: ~28 px en reposo más la apertura por dispersión;
   - caminando rápido: algo más abierta;
   - al disparar: se abre un 40 % y vuelve en 0.12 s;
   - al acertar a un enemigo: las marcas se ponen rojas 0.1 s;
   - al apuntar: desaparece.
   El punto central es donde caen los perdigones.
5. **Diálogo y pausa:** al abrir un diálogo con un guardia rendido y al pausar (Esc) desaparecen la mira y las manos, y vuelven al salir. También al morir.

## 6) Riesgos y pendientes
- **Sin probar en Unity.** Los sprites de la mira se generan con matemática (distancia con signo) y nunca los vi en pantalla; si el contorno o el tamaño no convencen, se ajustan las constantes al inicio de `RubberHoseCrosshair.cs`.
- **Los `VM_*.asset` commiteados tienen los tiempos viejos** hasta que Unity los migre al abrir; habrá que commitearlos otra vez.
- **Prefab pendiente** (sección 4): sin regenerar, el viewmodel y la mira no aparecen en el Player.
- **Sincronía bombeo / sonido:** ver el final de la sección 1.
- **Mira duplicada:** si alguien arma después un `UIManager` con su propio `CrosshairDot`, habrá dos miras; habría que quitar una.
- **La apertura de las marcas es indicativa** (30 % de la dispersión real y +6 px al caminar); el punto central es lo exacto.
- **Marcador de impacto:** un enemigo que aparezca en los 2 s antes de la siguiente revisión no mostrará el rojo en su primer golpe.
- **`IWeapon` ganó un miembro** (`SpreadAngle`): el arma de la fase 2 deberá implementarlo.
- **Dispersión en ejes del mundo** (sección 2): hallazgo sin corregir.
- **El avance de apuntar ahora corre en tiempo real:** el zoom ya no se detiene durante el hit-stop.
