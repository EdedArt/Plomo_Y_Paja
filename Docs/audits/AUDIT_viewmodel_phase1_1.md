# Auditoría Viewmodel Fase 1.1 — Calidad de imagen e integración con la escena

ESTADO: LUZ VERDE — aprobado por el orquestador

Rama: `player` (local, **sin push**). No se probó nada en Unity: lo que dice "verificado" abajo es por lectura de código y de
la documentación de URP, y está marcado así. Compila con 0 errores y 0 warnings nuevos (Runtime 59, uno menos que antes; Editor 0).

## 1) Qué causaba cada problema
Sin ver el Game view no puedo aislar cuál de estas causas pesaba más; lo que sigue es lo que **sí está en el código de la fase 1**
y cada punto se ataca por separado.
1. **Contornos con escalones.**
   - El alpha de la fase 1 era una rampa lineal estrecha (g ≈ 30–115 de 255) y no se despillaba el verde del contorno.
   - Los píxeles transparentes quedaban con color **negro** (`out[alpha < 0.02, :3] = 0`). Un shader de UI con alpha normal (no premultiplicado)
     mezcla ese negro en el borde al filtrar, y el contorno se ve sucio o dentado.
   - Ahora el matte es `1 − smoothstep(20, 90, g)`, hay despill y los píxeles transparentes copian el color opaco vecino (dilatación de 4 px).
2. **Se ve peor que el PNG original.**
   - La importación era `CompressedHQ` (compresión con pérdida) y sin mipmaps, y el filtro era Bilinear. Eso ensucia los trazos finos y oscuros.
   - Además el dibujo se escalaba de 1672x941 a 1920x1080 una sola vez y con el color de los transparentes en negro (punto anterior).
   - Ahora: Compression None, mipmaps Kaiser, Trilinear, y el recorte se hace **antes** de escalar, con color y alpha escalados por separado.
3. **Las manos parecen pegadas.** Esto sí es seguro: el `ViewmodelCanvas` era Screen Space - **Overlay**, y los canvases Overlay se dibujan
   **después** del post-procesado, así que no recibían grano, viñeta, color ni bloom. Además se dibujaban a brillo completo, sin tinte. Ahora el
   canvas es Screen Space - Camera (entra en el post-procesado) y las manos llevan un tinte cálido que se ilumina un instante al disparar.

## 2) Archivos tocados
**Creados**
- `Tools/viewmodel_key.py` — herramienta de recorte reutilizable (Python, Pillow + numpy). Sirve para el revólver y para todo lo que venga.
- `Scripts/Player/IWeapon.cs` — interfaz mínima de arma: `OnFired`, `OnReloadStarted`, `OnReloadFinished`, `IsReloading`, `ReloadDuration`.
- `Scripts/Player/ViewmodelLighting.cs` — tinte base de las manos y flash en blanco al disparar.
- `Scripts/Player/ViewmodelLightingOverride.cs` — componente opcional por nivel que reemplaza el tinte.
- `Art/Viewmodel/Revolver/wilbur_vm_revolver_{idle,fire,aimidle}_01.png` y `reload_01..03.png` (+ `.meta`) — revólver ya recortado.
- `Docs/phases/viewmodel_1_1_before_after.png` — recorte al 400 % de un contorno, antes y después.

**Modificados**
- `Art/Viewmodel/Shotgun/*.png` (5) y `FX/fx_fogonazo_01.png` — vueltos a recortar desde los originales con el matte suave (mismos nombres y GUID).
- `Art/Viewmodel/README_ART.md` — herramienta, importación nueva, nombres de los originales.
- `Scripts/Editor/ViewmodelProfileBuilder.cs` — importación nueva (ver abajo), comprobación automática de los PNG, `GetOrCreateRevolverProfile()` y el menú "Crear Perfil Viewmodel Revólver".
- `Scripts/Player/FirstPersonViewmodel.cs` — canvas Screen Space - Camera, regla del plano cercano, material con ZTest Always, `ViewmodelLighting` y conexión por `IWeapon` (`SetWeapon`).
- `Scripts/Player/PlayerAim.cs` — usa `IWeapon` en vez de `ShotgunController` y tiene `SetWeapon`.
- `Scripts/Player/ShotgunController.cs` — **una sola línea**: `: IWeapon` en la declaración de la clase (ya tenía todos los miembros).

**Importación (Art/Viewmodel):** Sprite (2D and UI), Single, Compression None, Generate Mip Maps ON (Kaiser), Filter Trilinear,
Max Size 2048, Alpha Is Transparency ON. `GetVersion()` sube a 2 (reimporta todo) y además `ViewmodelArtImporter` comprueba los PNG
al abrir el Editor y cada vez que llegan archivos nuevos: si alguno no cumple, lo reimporta solo (una vez por sesión, para no
entrar en bucle). Eso cubre el caso de Unity abierto cuando se copiaron los archivos.

**Fuera de lo pedido, avisado:** el texto pedía "sin cambios en ShotgunController", pero también "IWeapon reemplaza el cableado
directo". La única línea de `ShotgunController` que cambió es la declaración de la interfaz. "Mover la lectura de la dispersión
al arma con `PlayerAim.IsAiming`" ya estaba así desde la fase 1 (`ShootPellet` lee `_aim.IsAiming`), así que no hizo falta tocarla.

## 3) Parámetros del recorte usados
| Parámetro | Valor | Qué hace |
|---|---|---|
| `lo` | 20 | Verdor `g = G − max(R,B)` por debajo del cual el píxel es opaco |
| `hi` | 90 | Verdor por encima del cual el píxel es transparente (en medio: `1 − smoothstep(lo, hi, g)`) |
| despill | `G = min(G, max(R,B))` donde `g > 0` | Quita el verde mezclado en el contorno |
| dilatación | 4 px | Los píxeles con alpha 0 copian el color opaco más cercano (a la resolución original y otra vez a la final) |
| orden | recortar → rellenar → escalar (Lanczos) → rellenar | Color y alpha se escalan por separado |
| manos | 1920x1080 | Fogonazo: recorte al contenido, centrado, 1024x1024 |

Resultado en los 12 PNG: 0 píxeles de verde residual en las manos y 23 en el fogonazo (tonos amarillo-verdosos legítimos, no del fondo).
Se probó `lo=20 / hi=90` tal cual venía en el pedido y se dejó: el contorno negro queda sólido y el borde conserva 1–2 px de antialias
(ver `Docs/phases/viewmodel_1_1_before_after.png`). Si algún borde se ve fino, subir `hi` a ~120; para otros fondos de croma, cambiar `--lo/--hi`.

**Revólver:** los 6 PNG verdes estaban dentro de `Assets/` y sin respaldo; se copiaron a `_entrante_viewmodel/Revolver/` (igual que
la escopeta) y se quitaron de `Assets/` junto con sus `.meta`. `revolver_aim` → `aimidle`, `reloadopen/insert/close` → `reload_01/02/03`.
`VM_Revolver` se crea al ejecutar el menú (ver sección 4); sus anclas son estimadas: `hipMuzzle` (1185, 690), `aimMuzzle` (960, 545).

**FX `fx_round_rim` y `fx_casing_revolver`: no existen en el repo ni en `_entrante_viewmodel/`**, así que no se pudieron procesar.
La herramienta ya los trata (recorte, centrado y 256x256 por el nombre): copiar los originales verdes a
`_entrante_viewmodel/FX/` y correr `python Tools/viewmodel_key.py FX`.

## 4) Cómo probar en 5 pasos
Antes: esperar la compilación. Plomo y Paja → **Crear Perfil Viewmodel Revólver** (crea `VM_Revolver.asset`, consola:
`[Viewmodel] VM_Revolver: idle 1, fire 1, cycle 0, reload 3, aimIdle 1…`) y **Crear Prefabs de Prueba**. Si algún PNG no se reimportó,
sale `[Viewmodel] N PNG reimportados…`.
1. **Game view 1920x1080, escala 1x, Play en NivelPorky.** Los contornos negros ya no tienen escalones y el dibujo se ve tan
   nítido como el PNG. En la consola puede salir `[Viewmodel] nearClipPlane 0.3 → 0.03` (ver riesgos).
2. **Pegado a una pared** (caminar contra ella con la cámara casi tocándola y mirándola de frente, y también en una esquina):
   la pared nunca tapa las manos ni el fogonazo.
3. **Apuntando (clic derecho):** el FOV baja 15° y las manos **no cambian de tamaño** por el zoom (solo por su animación de apuntar).
4. **Disparando:** las manos pasan un instante a blanco puro (~0.06 s) y vuelven al tinte cálido en ~0.25 s; sale el fogonazo (sin tinte).
   Lo que no cambió: el "¡BANG!" y la luz de `ShotgunFeedback`.
5. **NivelPorky oscuro:** las manos ya no brillan más que el cuarto. Deben verse con grano, viñeta y color como el resto de la imagen; el
   HUD (munición, vida, mira) debe verse **sin** grano ni viñeta. Opcional: poner un `ViewmodelLightingOverride` en la escena con otro tinte.

## 5) Riesgos
- **Nada de esto se vio en pantalla.** Lo verificado por código:
  - *Post-procesado:* en URP un canvas Screen Space - Camera se dibuja con los objetos transparentes de la cámara, antes del post-procesado, así que recibe
    el volumen del juego (ColorAdjustments, Vignette, FilmGrain, Bloom, Tonemapping; los pone `NoirVolumeBuilder`). La cámara del Player ya tiene
    `renderPostProcessing = true`. El HUD y el diálogo siguen en Overlay y no lo reciben.
  - *Zoom del FOV:* un canvas Screen Space - Camera se redimensiona solo al frustum a `planeDistance`; el tamaño de las manos lo decide el
    `CanvasScaler` (por la pantalla), no el FOV. Por eso **no se añadió compensación**. Si en el paso 3 cambiaran de tamaño, avisar.
  - *Paredes sobre las manos:* no se pudo comprobar, así que se aplicó la opción segura desde el principio: `HandsImage` y `MuzzleFlashImage`
    usan un material de UI con ZTest Always (`unity_GUIZTestMode`). Es un material nuevo por instancia (se destruye al cerrar).
- **`nearClipPlane` baja a 0.03 (afecta al mundo):** si el near clip de la cámara pasa de 0.05, se baja a 0.03 en runtime y se escribe en la
  consola. Un near clip tan chico reduce la precisión del buffer de profundidad (posible z-fighting a lo lejos, y SSAO/sombras usan esa profundidad).
  Si se nota, subir `nearClipPlaneWhenLowered` en el inspector del viewmodel o dejar el canvas en Overlay. La cámara del prefab no se tocó.
- **Mipmaps en UI:** a 1x se usa el nivel 0 (idéntico al PNG); en un Game view más chico entra Trilinear y las manos se ven más suaves en vez de
  centellear. Cuesta algo más de memoria (+33 %) por 1920x1080 sin comprimir (≈8 MB por cuadro y ≈11 MB con mipmaps).
- **Revólver sin arma:** existe el perfil, pero no hay un `RevolverController` ni cambio de arma (fase 2). Su reload de 3 cuadros se reproduce
  en bucle a 12 fps, que sería demasiado rápido: en la fase 2 conviene repartir los 3 cuadros a lo largo de `ReloadDuration` y dejar el último.
- **Anclas del revólver y valores propios** (fogonazo 280 px, culatazo más suave) son estimados; se ponen al **crear** el asset, no al actualizarlo.
- **`ViewmodelLighting` usa `unscaledDeltaTime`:** el flash no se queda congelado en el hitstop (`timeScale` 0) de `CartoonFx`.
- **Fuera de alcance, sin tocar:** `ShotgunFeedback`, la mira (HUD), pausa/diálogo. `VM_Shotgun.asset` (de la fase 1) sigue sin commitear
  porque lo genera un menú; se commitea junto con los assets generados.
- **Metas:** los `.meta` de los PNG de la escopeta que Unity ya había reescrito (con los ajustes de la fase 1) no se commitearon; Unity los
  actualiza solo con los ajustes nuevos al reimportar y conviene commitearlos después de abrir el proyecto.
