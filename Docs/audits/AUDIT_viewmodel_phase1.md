# Auditoría Viewmodel Fase 1 — Manos rubber-hose, apuntar (ADS) y fogonazo (solo escopeta)

ESTADO: PENDIENTE DE REVISIÓN

Rama: `player` (local, **sin push**). Antes de empezar, `player` local se adelantó (fast-forward) hasta
`integracion/dia3` (d55e7ce): `player` estaba 33 commits atrás, sin commits propios, y sin eso faltaba
todo lo de Edgar, Cristian y la 3A8. `origin/player` no se tocó. No se probó nada en Unity. Compila con
0 errores y 0 warnings nuevos (Runtime 59, uno menos que antes; Editor 0).

## 1) Cómo estaban montadas las manos antes
- **Dibujo:** `Player/FirstPersonViewmodel.cs`, en la cámara del Player (lo añadía "Crear Prefabs de Prueba").
  Creaba en runtime un canvas Screen Space - Overlay `Viewmodel_Canvas` (sortingOrder −10, Scale With Screen Size
  1920x1080, match 0.5) con una `Image` "Hands" de 640x640 anclada abajo a la derecha. Usaba el
  `CharacterSpriteSet` "wilbur_hands" (idle/fire/reload de `Art/Characters/wilbur_hands`). No había SpriteRenderer
  en el mundo, así que no hubo nada que migrar ni apagar.
- **Movimiento viejo:** balanceo seno con `PlayerController.IsMoving`, culatazo lineal de 70 px y bajada de recarga
  fija de 160 px a velocidad constante (sin sincronizar con la recarga).
- **Fogonazo de antes:** `ShotgunController.muzzleFlash` (ParticleSystem; el generador no lo asigna, así que no salía
  nada) y `ShotgunFeedback` (luz puntual de 0.05 s en `muzzleLocalPosition` + "¡BANG!" en un canvas con sortingOrder −5).
  No había fogonazo dibujado. Las dos cosas siguen igual.
- **ShotgunController:**
  - Disparo: corrutina `Fire()`. `OnFired` ya existía y se lanza después de los perdigones y de restar munición.
  - Recarga: corrutina `Reload()`. `OnReloadStarted` al empezar y `OnReloadFinished` al terminar ya existían.
    Dura `reloadTime` = 2.5 s. Hay auto-recarga 0.3 s después de vaciar el cargador.
  - Dispersión: `ShootPellet()`, `Random.Range(-spreadAngle, spreadAngle) * 0.01f` en X e Y.
- **PlayerController:** no expone la velocidad. Sí expone `IsMoving` (velocidad del CharacterController > 0.1) e
  `IsRunning` (Shift y `_canMove`). La velocidad se lee directo del `CharacterController`, sin cambiar PlayerController.
- **Estados que ya existían:**
  - Mira: `UIManager.crosshair`; `ShowDialogue` la oculta.
  - Pausa: `GameManager.IsGamePaused` (`timeScale` 0).
  - Diálogo e interrogatorio: `DialogueUI.Instance.IsOpen`.
  - Muerte: el evento `PlayerHealth.OnDeath` (no hay getter) y `GameManager.IsGameOver`.

## 2) Archivos creados / modificados
**Creados**
- `Scripts/Player/ViewmodelProfile.cs` — ScriptableObject por arma: clips, fogonazo, anclas y todos los parámetros.
- `Scripts/Player/ViewmodelAnimator.cs` — cuadros (disparo → bombeo, idle en bucle, cambio a apuntar) y fogonazo.
- `Scripts/Player/ViewmodelMotion.cs` — respiración, balanceo en 8, correr, inercia del mouse, culatazo con resorte, bajada de recarga y apuntar.
- `Scripts/Player/PlayerAim.cs` — clic derecho: `IsAiming`, `OnAimChanged`, `AimBlend`, FOV, bloqueos y oculta la mira.
- `Scripts/Editor/ViewmodelProfileBuilder.cs` — importador de `Art/Viewmodel` y menú "Crear Perfil Viewmodel Escopeta" (VM_Shotgun.asset).
- `Art/Viewmodel/README_ART.md` — contrato del arte.
- `Art/Viewmodel/Shotgun/wilbur_vm_shotgun_{idle,fire,cycle,aimidle,aimcycle}_01.png` y `Art/Viewmodel/FX/fx_fogonazo_01.png` (+ `.meta`) — el arte, sin fondo y con el nombre del contrato.

**Modificados**
- `Scripts/Player/FirstPersonViewmodel.cs` — reescrito como anfitrión: crea `ViewmodelCanvas/ViewmodelRoot/HandsImage+MuzzleFlashImage`, conecta la escopeta, se oculta por estado y tiene `SetProfile`. Mantiene la clase y el GUID, así que el prefab sigue apuntando a él.
- `Scripts/Player/ShotgunController.cs` — multiplicador de dispersión al apuntar y `ReloadDuration` (ver 3).
- `Scripts/Player/PlayerController.cs` — velocidad ×0.6 y sin correr al apuntar (ver 3).
- `Scripts/UI/UIManager.cs` — gancho para ocultar la mira (ver 3).
- `Scripts/Editor/TestPrefabGenerator.cs` — el Player recibe `PlayerAim` y el viewmodel usa `VM_Shotgun` en vez del set `wilbur_hands`.
- `.gitignore` — `PLOMO_Y_PAJAV_1/_entrante_viewmodel/` (originales con fondo verde).

**Escenas:** no se tocó ninguna. El Player de las escenas es una instancia del prefab, así que recibe `PlayerAim` y el
perfil al ejecutar "Crear Prefabs de Prueba".

**Arte (decisión fuera del guion):** los PNG que estaban en `Shotgun/` y `FX/` eran RGB **sin alpha, con fondo verde**,
de 1672x941, y el fogonazo no era cuadrado (1312x1199). Tal cual, habrían tapado la pantalla de verde. Se hicieron estos
cambios:
- Se quitó el verde (alpha por "cuánto más verde que rojo/azul" y se limpió el halo verde de los bordes).
- Las manos se escalaron a 1920x1080 (misma proporción 16:9).
- El fogonazo se recortó, se centró en un cuadrado y se pasó a 1024x1024.
- Se renombraron según el contrato: `shotgun_aim` → `aimidle`, `shotgun_aimpump` → `aimcycle`, `shotgun_pump` → `cycle`.

Los originales, intactos, están en `PLOMO_Y_PAJAV_1/_entrante_viewmodel/` (ignorado por git). Los de `Revolver/`
(también con fondo verde) no se tocaron ni se commitearon: son de la fase 2.

## 3) Cambios fuera del viewmodel, línea por línea
- **ShotgunController.cs**
  - `+ [SerializeField] float aimSpreadMultiplier = 0.6f;`
  - `+ private PlayerAim _aim;` y en `Awake`: `_aim = GetComponentInParent<PlayerAim>();`
  - En `ShootPellet`: `float spread = spreadAngle * (apuntando ? aimSpreadMultiplier : 1f);`, y los dos `Random.Range` usan `spread` (era `spreadAngle`).
  - `+ public float ReloadDuration => reloadTime;` (solo lectura).
  - `OnFired`, `OnReloadStarted` y `OnReloadFinished` ya existían en los puntos correctos: no se tocaron. Daño, munición, cadencia y recarga quedaron iguales.
- **PlayerController.cs**
  - `+ [SerializeField] float aimSpeedMultiplier = 0.6f;`
  - `+ private PlayerAim _aim;` y en `Awake`: `TryGetComponent(out _aim);`
  - `IsRunning` añade `&& !IsAimingNow` (apuntando no se corre), con un nuevo `private bool IsAimingNow`.
  - En `HandleMovement`: `if (IsAimingNow) speed *= aimSpeedMultiplier;`
- **UIManager.cs**
  - `+ public void SetCrosshairHiddenByAim(bool hidden)`: oculta o muestra la mira, pero con el panel de diálogo abierto la deja oculta. Lo llama `PlayerAim` en `OnAimChanged`.
- **Cámara:** `PlayerAim` cambia `fieldOfView` (base leída en `Start`, base − 15 al apuntar) y la devuelve a la base en `OnDisable`.

## 4) Valores por defecto y anclas del cañón
Todo está en `Art/Viewmodel/VM_Shotgun.asset` (inspector).

| Grupo | Valores |
|---|---|
| Clips | fps 12 por defecto; idle en bucle a 8 si tiene 2+ cuadros; `fire` ≥ 0.15 s; `cycle` ≥ 0.45 s (el bombeo suena a 0.35 s); `aimFire` ≥ 0.15 s; `aimCycle` ≥ 0.45 s |
| Respiración | ±6 px en Y, ±1 % de alto, 0.3 respiraciones por segundo |
| Balanceo | 1.8 pasos por segundo a 4 m/s (escala con la velocidad). Caminar: 18 × 14 px; correr: 30 × 24 px, manos 60 px más abajo y −6° |
| Inercia | 12 px por unidad de mouse, en contra, límite ±40 px, suavizado 10, giro 0.06° por px |
| Culatazo | (+40, −60) px, +8°, escala (1.10, 0.90). Resorte de 22 rad/s con amortiguación 0.6: vuelve en ~0.25 s con un pequeño rebote |
| Recarga sin cuadros | −500 px. Baja en 0.4 s, espera y sube en los últimos 0.4 s de `ReloadDuration` (2.5 s) |
| Apuntar | Entrar 0.15 s, salir 0.12 s, FOV −15°. Cambia el dibujo al 50 % con un golpecito de escala de 1.03. Mientras apunta: balanceo ×0.25, inercia ×0.3, respiración ×0.5, culatazo ×0.6. Sin aimidle: (−320, +140) px y ×1.15 |
| Fogonazo | 360 px × (0.9–1.2), giro de 0–360°, 0.07 s |
| Fuera del perfil | Dispersión al apuntar ×0.6 (ShotgunController) y velocidad al apuntar ×0.6 (PlayerController) |

**Anclas (estimadas mirando los dibujos):** px del cuadro 1920x1080, con el origen abajo a la izquierda.
- `hipMuzzle` = (1000, 690): boca del cañón en `fire` (el arma levantada por el culatazo).
- `aimMuzzle` = (960, 550): boca del cañón en `aimidle`.

**Cómo ajustarlas:**
1. Seleccionar `VM_Shotgun.asset`.
2. En *Fogonazo*, editar `hipMuzzle` o `aimMuzzle`. Se puede hacer en Play: el próximo disparo ya usa el valor nuevo.
3. Para medir en un editor de imágenes: x igual, y = 1080 − y_desde_arriba.

Los cambios hechos en Play se pierden al parar. Hay que anotarlos y escribirlos fuera de Play, porque es un asset.

## 5) Cómo probar en 8 pasos
1. Con Unity abierto, esperar la compilación. Si los PNG de `Art/Viewmodel` no salen como Sprite (2D and UI): clic derecho en `Art/Viewmodel` → Reimport.
2. Plomo y Paja → **Crear Prefabs de Prueba**. Crea `VM_Shotgun.asset`, añade `PlayerAim` y asigna el perfil. En consola: `[Viewmodel] VM_Shotgun: idle 1, fire 1, cycle 1, … fogonazos 1`.
3. Play en NivelPorky. Quieto: se ve la escopeta abajo y al centro-derecha, sin fondo verde, y respira. Al caminar: balanceo en 8. Con Shift: balanceo grande, manos abajo y giradas. Al mover el mouse, las manos se atrasan un poco.
4. Clic izquierdo: dibujo de disparo, fogonazo en la boca del cañón, culatazo con rebote y después el dibujo de bombeo.
5. Mantener clic derecho: el zoom (FOV) y las manos van al centro, el dibujo cambia a mitad de camino, la mira desaparece, se camina más lento y Shift no corre. Al soltar, todo vuelve.
6. Disparar apuntando: aimidle + culatazo más chico + fogonazo en `aimMuzzle`, y después el bombeo apuntando (aimcycle).
7. Recargar (R, o vaciar el cargador): las manos bajan, esperan y suben justo cuando termina la recarga. Si se estaba apuntando, se sale. No se puede apuntar durante la recarga.
8. Pausa (Esc), hablar con un guardia rendido y morir: las manos desaparecen y vuelven al salir de la pausa o del diálogo. Probar también a 4:3 y 21:9 en Game view: las manos quedan apoyadas abajo.

## 6) Riesgos y pendientes
- **Arte procesado por mí:** el recorte del verde puede dejar un borde fino en zonas antialias muy verdes. Lo ideal es que el artista exporte PNG con alpha real (contrato en README_ART.md).
- **El escalado** 1672 → 1920 suaviza un poco el trazo.
- **Anclas estimadas a ojo:** confirmarlas con el paso 4–6 y ajustarlas en el asset.
- **Dibujos de 1 solo cuadro:** el arma no se ve "animada", solo se mueve por código. Para animar de verdad hacen falta `idle_02..`, `fire_02..`, etc.
- **Sin cuadros de recarga ni `aimfire`:** se usan la bajada de manos y aimidle + culatazo.
- **Unity estaba abierto** mientras se trabajaba: los PNG se importaron antes de que compilara el importador (salen como Default). Ver el paso 1.
- **Escenas:** `NivelPorky.unity` y `Player.prefab` tienen cambios locales sin commitear de David. "Crear Prefabs de Prueba" vuelve a guardar el prefab.
- **El set viejo `wilbur_hands`** ya no lo usa nadie (sigue existiendo en Art/Characters y en el builder de sets).
- **`ShotgunFeedback`** sigue poniendo su luz en una posición 3D fija y el "¡BANG!" en (0.7, 0.3) de la pantalla. No sigue el ancla del cañón: queda para otra fase.
- **`UIManager.ShowDialogue`** (panel de diálogo viejo de UIManager) no oculta las manos: solo se detecta `DialogueUI`, que es el que usa `DialogueNPC`.
- **Hitstop:** `CartoonFx` pone `timeScale` en 0 un instante en cada golpe. Por eso la pausa se detecta con `GameManager.IsGamePaused`, no con `timeScale`.
- **El FOV** se escribe cada frame desde `PlayerAim`. Si en el futuro otro script cambia el FOV, van a pelear.
- **`player` local adelantado:** cuando se haga push, `origin/player` pasará a incluir todo `integracion/dia3`.

## 7) Gancho para el sistema de armas de la Fase 2
- **Perfil del revólver:** `ViewmodelProfileBuilder.BuildProfile("revolver", "Revolver", "VM_Revolver", "fx_fogonazo")` crea `VM_Revolver.asset` desde `Art/Viewmodel/Revolver/wilbur_vm_revolver_<clip>_<nn>.png`. Los PNG actuales del revólver hay que pasarlos antes a alpha y al nombre del contrato.
- **Cambiar de arma:** `FirstPersonViewmodel.SetProfile(perfil)` cambia cuadros, fogonazo, anclas, movimiento y tiempos de apuntado (`PlayerAim.SetTiming`) sin recrear el canvas.
- **Eventos del arma nueva:** `FirstPersonViewmodel.HandleFired()`, `HandleReloadStarted()` y `HandleReloadFinished()` son públicos para llamarlos desde el arma nueva. Hoy se conectan solos a `ShotgunController`. Falta una interfaz común de arma (eventos + `ReloadDuration`) para no depender de `ShotgunController`.
- **Apuntar:** `PlayerAim` es del jugador, no del arma. La dispersión ×0.6 está hoy solo en `ShotgunController`; el revólver tendrá que leer `PlayerAim.IsAiming` igual.
