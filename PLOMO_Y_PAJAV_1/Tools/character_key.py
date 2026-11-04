#!/usr/bin/env python3
"""
Recorte de fondo MAGENTA (#FF00FF) de personajes (Centavo, el contador: gorra verde, por eso no se usa croma verde).
Reusa smoothstep/dilate/resize/to_image de viewmodel_key.py (no se modifica; el viewmodel sigue igual).

USO (desde PLOMO_Y_PAJAV_1/):
    python Tools/character_key.py                    # procesa Contador
    python Tools/character_key.py --lo 20 --hi 90 --dilate 4
    python Tools/character_key.py --single ENTRADA.png --out SALIDA.png [--crop 4]   # un solo archivo (UI)
    python Tools/character_key.py --center [HUD] [--size 256]   # iconos del HUD, centrados (fase HUD.1)
    python Tools/character_key.py Lobo                           # Don Lobo, mismo recorte que Contador (fase N2.2)
    python Tools/character_key.py Objetos [--size 512]          # objetos del mapa, alineados abajo (fase HUD.3)

ENTRADA : _entrante_personajes/Contador/*.png   (si no existe, usa _entrante_viewmodel/_entrante_personajes_/contador/)
SALIDA  : Assets/_Game/Art/Characters/contador/<nombre>.png  (1024x1024 RGBA)

MODO --center (fase HUD.1, iconos y caras del HUD): carpeta _entrante_hud/<NOMBRE>/ (por defecto HUD) -> --dst (por
defecto Assets/_Game/Art/UI/HUD/). Cada PNG: mismo recorte magenta + despill + dilate, recorte a lo opaco con 4 px de margen,
lienzo CUADRADO con el dibujo CENTRADO (no se alinea abajo) y escala a --size x --size (por defecto 256). Conserva el nombre.
Los pickup_* que estén en esa carpeta se saltan: son objetos del mapa, no del HUD. La carpeta real es entrante_hud/ (sin guion bajo).

MODO Objetos (fase HUD.3, objetos del mapa): carpeta <entrante_hud>/Objetos/ -> Assets/_Game/Art/Props/pickups/. Mismo recorte, pero
el dibujo queda ALINEADO ABAJO y centrado en X (apoya en el piso; pivote abajo al centro en el importador). --size 512 por defecto.

MODO --single (fase 3C.4, ej. el papel del diálogo Art/UI/ui_papel_dialogo.png): mismo recorte magenta + despill de borde
+ dilate, sin escalar ni alinear. --crop N recorta a lo opaco dejando N px de margen (sin --crop conserva el tamaño).
--out puede ser el mismo archivo de entrada (Unity conserva el GUID del .meta).

PASOS:
  1. Magenta m = min(R, B) - G. alpha = 1 - smoothstep(lo, hi, m).
  2. Despill SOLO en la franja de borde (<= EDGE px de zona transparente) y donde m > 0: R y B bajan m/2, así las orejas
     rosa salmón, la mancha roja y el interior rojo oscuro de la boca (lejos del borde) no se tocan.
  3. Relleno de color de bordes (dilate), recorte al contenido.
  4. UNA escala para todos los cuadros: s = min(1024/alto_max, 1024/ancho_max). Centrado en X y alineado abajo.
  5. Pares (traslación pura, correlación de fase sobre alpha): *_02 -> *_01, blink_01 -> talk_01, idle_02 -> idle_01.
     limp_01/02 no se alinean entre sí. Cada desplazamiento se imprime. Después cada grupo se baja en bloque
     para que su píxel opaco más bajo toque el borde inferior.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from viewmodel_key import smoothstep, dilate, resize, to_image  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "_entrante_personajes", "Contador")
SRC_FALLBACK = os.path.join(ROOT, "_entrante_viewmodel", "_entrante_personajes_", "contador")
DST = os.path.join(ROOT, "Assets", "_Game", "Art", "Characters", "contador")
PROPS_DST = os.path.join(ROOT, "Assets", "_Game", "Art", "Props", "pickups")
CANVAS = 1024
EDGE = 3
OPAQUE = 0.5

EXPECTED = ["herido_01", "herido_02", "talk_01", "talk_02", "scared_01", "scared_02", "nervous_01", "nervous_02",
            "plead_01", "plead_02", "relief_01", "relief_02", "blink_01", "death_01", "idle_01", "idle_02",
            "peek_01", "cover_01", "shoot_01", "hit_01", "limp_01", "limp_02"]
PAIRS = {  # frame -> referencia
    "herido_02": "herido_01", "talk_02": "talk_01", "scared_02": "scared_01", "nervous_02": "nervous_01",
    "plead_02": "plead_01", "relief_02": "relief_01", "blink_01": "talk_01", "idle_02": "idle_01",
}

# Don Lobo (fase N2.2, guion nivel2 §3): 16 cuadros. Los _02 de boca salen de su _01
LOBO_SRC = os.path.join(ROOT, "_entrante_personajes", "Lobo")
LOBO_DST = os.path.join(ROOT, "Assets", "_Game", "Art", "Characters", "lobo")
LOBO_EXPECTED = ["idle_01", "idle_02", "peek_01", "cover_01", "shoot_01", "call_01", "hit_01", "rodillas_01", "rodillas_02",
                 "talk_01", "talk_02", "angry_01", "angry_02", "laugh_01", "laugh_02", "death_01"]
LOBO_PAIRS = {"idle_02": "idle_01", "rodillas_02": "rodillas_01", "talk_02": "talk_01", "angry_02": "angry_01", "laugh_02": "laugh_01"}


def key_magenta(path, lo, hi, despill_all=False, edge_strength=0.5):
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = np.minimum(r, b) - g
    alpha = (1.0 - smoothstep(lo, hi, m)).astype(np.float32)
    # franja de borde: píxeles a <= EDGE px de una zona (casi) transparente
    near = alpha < 0.5
    h, w = alpha.shape
    for _ in range(EDGE):
        p = np.pad(near, 1)
        near = near | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    spill = (m > 0) & near
    d = np.where(spill, m * edge_strength, 0.0)
    if despill_all:  # humo translúcido (fx_impacto_02): el magenta se transparenta en todo el dibujo; R y B bajan hasta G
        d = np.where(m > 0, m, 0.0)
    return np.dstack([r - d, g, b - d]), alpha


def shift(arr, dy, dx):
    out = np.zeros_like(arr)
    h, w = arr.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = arr[ys, xs]
    return out


def phase_shift(ref, mov):
    """(dy, dx) enteros que hay que aplicar a `mov` para que coincida con `ref`."""
    F = np.fft.fft2(ref) * np.conj(np.fft.fft2(mov))
    F /= np.maximum(np.abs(F), 1e-9)
    c = np.fft.ifft2(F).real
    y, x = np.unravel_index(np.argmax(c), c.shape)
    h, w = c.shape
    return (y - h if y > h // 2 else y), (x - w if x > w // 2 else x)


def bbox(alpha):
    ys, xs = np.where(alpha > OPAQUE)
    return ys.min(), ys.max() + 1, xs.min(), xs.max() + 1


def key_single(args):
    rgb, alpha = key_magenta(args.single, args.lo, args.hi, edge_strength=1.0 if args.defringe else 0.5)
    rgb = dilate(rgb, alpha, args.dilate)
    h, w = alpha.shape
    if args.crop >= 0:
        y0, y1, x0, x1 = bbox(alpha)
        y0, x0 = max(0, y0 - args.crop), max(0, x0 - args.crop)
        y1, x1 = min(h, y1 + args.crop), min(w, x1 + args.crop)
        rgb, alpha = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
    out = args.out or args.single
    to_image(rgb, alpha).save(out, optimize=True)
    clear = float((alpha < 0.05).mean()) * 100
    print("OK %s -> %s  %dx%d  transparente %.1f %%" % (args.single, out, alpha.shape[1], alpha.shape[0], clear))
    return 0


def incoming_hud_folder(folder):
    """La carpeta de originales del HUD: entrante_hud/ (la real) o _entrante_hud/ (la del guion)."""
    for name in ("entrante_hud", "_entrante_hud"):
        path = os.path.join(ROOT, name, folder)
        if os.path.isdir(path):
            return path
    return os.path.join(ROOT, "entrante_hud", folder)


def key_center(args, bottom=False):
    folder = args.center if isinstance(args.center, str) else "HUD"
    src = args.src or incoming_hud_folder(folder)
    default_dst = PROPS_DST if bottom else os.path.join(ROOT, "Assets", "_Game", "Art", "UI", "HUD")
    dst = args.dst if args.dst != DST else default_dst
    if not os.path.isdir(src):
        sys.exit("No existe la carpeta de entrada: %s" % src)
    os.makedirs(dst, exist_ok=True)
    files = sorted(f for f in os.listdir(src) if f.lower().endswith(".png"))
    if not bottom:
        files = [f for f in files if not f.lower().startswith("pickup_")]  # copias de los objetos del mapa
    if not files:
        sys.exit("No hay PNG en %s" % src)
    for f in files:
        rgb, alpha = key_magenta(os.path.join(src, f), args.lo, args.hi, args.despill_all)
        rgb = dilate(rgb, alpha, args.dilate)
        y0, y1, x0, x1 = bbox(alpha)
        h, w = alpha.shape
        y0, x0 = max(0, y0 - 4), max(0, x0 - 4)
        y1, x1 = min(h, y1 + 4), min(w, x1 + 4)
        rgb, alpha = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
        ch, cw = alpha.shape
        side = max(ch, cw)
        sq_rgb = np.zeros((side, side, 3), np.float32)
        sq_a = np.zeros((side, side), np.float32)
        oy, ox = (side - ch if bottom else (side - ch) // 2), (side - cw) // 2
        sq_rgb[oy:oy + ch, ox:ox + cw] = rgb
        sq_a[oy:oy + ch, ox:ox + cw] = alpha
        sq_rgb = dilate(sq_rgb, sq_a, args.dilate)
        out_rgb, out_a = resize(sq_rgb, sq_a, (args.size, args.size))
        out_rgb = dilate(out_rgb, out_a, args.dilate)
        out = os.path.join(dst, os.path.splitext(f)[0].lower() + ".png")
        to_image(out_rgb, out_a).save(out, optimize=True)
        print("OK %s -> %s  %dx%d (%s)" % (f, out, args.size, args.size, "abajo" if bottom else "centrado"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--lo", type=float, default=20.0)
    ap.add_argument("--hi", type=float, default=90.0)
    ap.add_argument("--dilate", type=int, default=4)
    ap.add_argument("--src", default=None)
    ap.add_argument("--dst", default=DST)
    ap.add_argument("--single", default=None, help="un solo PNG (sin escalar ni alinear)")
    ap.add_argument("--out", default=None, help="salida de --single (por defecto, sobrescribe la entrada)")
    ap.add_argument("--defringe", action="store_true", help="--single: despill COMPLETO en la franja de borde (sin halo rosado en bordes crema)")
    ap.add_argument("--crop", type=int, default=-1, help="--single: recortar a lo opaco con este margen en px")
    ap.add_argument("--center", nargs="?", const="HUD", default=None,
                    help="iconos centrados: carpeta _entrante_hud/<NOMBRE> (por defecto HUD) -> Art/UI/HUD")
    ap.add_argument("--size", type=int, default=None, help="lado del lienzo cuadrado de salida (--center 256, Objetos 512)")
    ap.add_argument("--despill-all", action="store_true", help="--center/Objetos: quita el magenta de todo el dibujo (humo translúcido)")
    ap.add_argument("objects", nargs="?", default=None, help="carpeta de objetos del mapa, alineados abajo (Objetos)")
    args = ap.parse_args()
    expected, pairs, default_src = EXPECTED, PAIRS, (SRC if os.path.isdir(SRC) else SRC_FALLBACK)
    if args.objects and args.objects.lower() == "lobo":
        expected, pairs, default_src = LOBO_EXPECTED, LOBO_PAIRS, LOBO_SRC
        if args.dst == DST:
            args.dst = LOBO_DST
        args.objects = None
    if args.objects:
        args.center = args.objects
        args.size = args.size or 512
        key_center(args, bottom=True)
        return
    args.size = args.size or 256
    if args.center:
        key_center(args)
        return
    if args.single:
        return key_single(args)

    src = args.src or default_src
    if not os.path.isdir(src):
        print("No existe la carpeta de entrada:", src)
        return 1
    print("Entrada:", src)
    found = {os.path.splitext(f)[0]: f for f in os.listdir(src) if f.lower().endswith(".png")}
    print("Faltan:", sorted(set(expected) - set(found)) or "ninguno")
    print("Sobran:", sorted(set(found) - set(expected)) or "ninguno")
    names = [n for n in expected if n in found]

    crops = {}
    for n in names:
        rgb, alpha = key_magenta(os.path.join(src, found[n]), args.lo, args.hi)
        rgb = dilate(rgb, alpha, args.dilate)
        y0, y1, x0, x1 = bbox(alpha)
        crops[n] = (rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1])
    maxh = max(c[1].shape[0] for c in crops.values())
    maxw = max(c[1].shape[1] for c in crops.values())
    s = min(CANVAS / maxh, CANVAS / maxw)
    print("Escala única: %.5f (alto máx %d, ancho máx %d)" % (s, maxh, maxw))

    frames = {}  # nombre -> (rgb, alpha) en lienzo 1024, centrado y abajo
    for n, (rgb, alpha) in crops.items():
        h, w = alpha.shape
        size = (max(1, round(w * s)), max(1, round(h * s)))
        rgb2, al2 = resize(rgb, alpha, size)
        rgb2 = dilate(rgb2, al2, args.dilate)
        cr = np.zeros((CANVAS, CANVAS, 3), np.float32)
        ca = np.zeros((CANVAS, CANVAS), np.float32)
        ox, oy = (CANVAS - size[0]) // 2, CANVAS - size[1]
        cr[oy:oy + size[1], ox:ox + size[0]] = rgb2
        ca[oy:oy + size[1], ox:ox + size[0]] = al2
        frames[n] = (cr, ca)

    for n, ref in pairs.items():
        if n not in frames or ref not in frames:
            print("PAR OMITIDO %s -> %s (falta archivo)" % (n, ref))
            continue
        dy, dx = phase_shift(frames[ref][1], frames[n][1])
        flag = "  ¡REVISAR: desplazamiento grande!" if max(abs(dy), abs(dx)) > 40 else ""
        print("ALIGN %-11s -> %-10s dy=%+d dx=%+d%s" % (n, ref, dy, dx, flag))
        frames[n] = (shift(frames[n][0], dy, dx), shift(frames[n][1], dy, dx))
    if expected is EXPECTED:
        print("limp_01 / limp_02: sin alinear entre sí (solo abajo + centrado)")

    # grupos: referencia + sus pares; se bajan en bloque
    groups = {}
    for n in frames:
        groups.setdefault(pairs.get(n, n), []).append(n)
        # blink_01 cuelga de talk_01: ya está en el grupo de talk
    os.makedirs(args.dst, exist_ok=True)
    for ref, members in groups.items():
        low = max(np.where(frames[m][1] > OPAQUE)[0].max() for m in members)
        down = CANVAS - 1 - low
        for m in members:
            cr, ca = frames[m]
            cr, ca = shift(cr, down, 0), shift(ca, down, 0)
            cr = dilate(cr, ca, args.dilate)
            own = np.where(ca > OPAQUE)[0].max()
            to_image(cr, ca).save(os.path.join(args.dst, m + ".png"), optimize=True)
            print("OK %-11s bajado %+d px, fila baja %d%s" % (m, down, own, "" if own == CANVAS - 1 else "  (grupo: %d px sobre el borde)" % (CANVAS - 1 - own)))
    print("Listo: %d PNG en %s" % (len(frames), args.dst))
    return 0


if __name__ == "__main__":
    sys.exit(main())
