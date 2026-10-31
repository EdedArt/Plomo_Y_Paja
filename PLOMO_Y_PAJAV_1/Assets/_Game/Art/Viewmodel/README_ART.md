# Viewmodel — contrato del arte (manos de Wilbur en primera persona)

## Carpetas
| Carpeta | Contenido |
|---|---|
| `Art/Viewmodel/Shotgun/` | Cuadros de las manos con la escopeta |
| `Art/Viewmodel/Revolver/` | Cuadros de las manos con el revólver (fase 2) |
| `Art/Viewmodel/FX/` | Efectos: fogonazo, etc. |
| `Art/Viewmodel/VM_<Arma>.asset` | Perfil del arma (lo crea el menú; ver abajo) |

## Manos (cuadro completo)
- PNG **1920x1080** con **fondo transparente** (alpha real, no fondo verde), dibujado **en su sitio**:
  el arma y las manos en la posición exacta en que se verán en pantalla. No recortar.
- Nombre: `wilbur_vm_<arma>_<clip>_<nn>.png`, por ejemplo `wilbur_vm_shotgun_fire_01.png`.
- `<arma>`: `shotgun`, `revolver`.
- `<clip>` (en minúsculas):

| Clip | Qué es | ¿Obligatorio? | Si falta |
|---|---|---|---|
| `idle` | En reposo (2 o más cuadros = se repite a 8 fps, "respira") | **Sí** | Sin idle no se ven manos |
| `fire` | Disparo desde la cadera | No | idle + culatazo |
| `cycle` | Bombeo / amartillar tras el disparo | No | idle |
| `reload` | Recarga (se repite mientras dura) | No | Las manos bajan y suben |
| `aimidle` | Apuntando (clic derecho) | No | Las manos solo se mueven y crecen |
| `aimfire` | Disparo apuntando | No | aimidle + culatazo |
| `aimcycle` | Bombeo apuntando | No | Sale a la cadera, bombea con `cycle` y vuelve |

- `<nn>`: 01, 02, 03… (orden de reproducción). Velocidad por defecto: 12 fps ("de a dos").

## Efectos
- PNG **cuadrado**, fondo transparente, el estallido **centrado** (el juego lo gira y lo escala al azar).
- Nombre: `fx_<nombre>_<nn>.png`, por ejemplo `fx_fogonazo_01.png`, `fx_fogonazo_02.png`.
  Con 2 o más fogonazos nunca sale el mismo dos veces seguidas.

## Importación (automática, `ViewmodelArtImporter`)
Sprite (2D and UI), Single, Bilinear, sin mipmaps, tamaño máximo 2048, compresión de alta calidad.
Si un PNG no se ve como Sprite: clic derecho en la carpeta → Reimport.

## Después de añadir o cambiar dibujos
Plomo y Paja → **Crear Perfil Viewmodel Escopeta** (o Crear Prefabs de Prueba). Solo cambia los dibujos
de `VM_Shotgun.asset`; las anclas y el movimiento ajustados a mano se conservan.

## Anclas del cañón (dónde sale el fogonazo)
En `VM_<Arma>.asset` → *Fogonazo*: `hipMuzzle` (cuadro `fire`) y `aimMuzzle` (cuadro `aimfire`/`aimidle`),
en píxeles del cuadro 1920x1080 con el **origen abajo a la izquierda**. Para medir: abrir el PNG en un editor,
leer la posición (x, y desde arriba) de la boca del cañón y usar `y = 1080 - y`.

## Originales con fondo verde
Los PNG con fondo verde se guardan fuera de Assets en `PLOMO_Y_PAJAV_1/_entrante_viewmodel/` (no van al repo).
