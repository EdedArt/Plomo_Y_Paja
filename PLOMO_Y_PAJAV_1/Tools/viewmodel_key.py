#!/usr/bin/env python3
"""
Recorte de fondo verde del viewmodel (manos y efectos), con matte suave.

USO (desde PLOMO_Y_PAJAV_1/):
    python Tools/viewmodel_key.py                      # procesa todo _entrante_viewmodel/
    python Tools/viewmodel_key.py Shotgun              # solo una carpeta (Shotgun, Revolver, FX)
    python Tools/viewmodel_key.py --lo 20 --hi 90 --dilate 4

REQUISITOS: pip install pillow numpy

ENTRADA : _entrante_viewmodel/<Arma|FX>/<original>.png   (RGB, fondo verde de croma; nunca los ya procesados)
SALIDA  : Assets/_Game/Art/Viewmodel/<Arma|FX>/wilbur_vm_<arma>_<clip>_<nn>.png  (manos, 1920x1080)
                                                fx_<nombre>_<nn>.png               (efectos, cuadrados)

PASOS (en este orden):
  1. Verdor g = G - max(R, B) (0..255). alpha = 1 - smoothstep(lo, hi, g): g <= lo → opaco, g >= hi → transparente.
     Entre los dos hay 1–2 px de antialias en el borde.
  2. Despill: donde g > 0, G = min(G, max(R, B)) (quita el verde que se mezcló en el contorno).
  3. Relleno de bordes: los píxeles con alpha == 0 copian el color del píxel opaco más cercano (~dilate px),
     así el filtrado bilineal / los mipmaps nunca muestran verde ni negro en el borde.
  4. DESPUÉS de recortar se escala (Lanczos) a 1920x1080; el color y el alpha se escalan por separado y se
     vuelve a rellenar. Efectos: se recortan al contenido, se centran en un cuadrado y se escalan.

NOMBRES: ver CLIP_ALIASES y fx_name(). Un arma nueva solo necesita sus PNG en _entrante_viewmodel/<Arma>/ con
nombres <arma>_<clip>[_<nn>].png.
"""
import argparse
import os
import re
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "_entrante_viewmodel")
DST = os.path.join(ROOT, "Assets", "_Game", "Art", "Viewmodel")

HANDS_SIZE = (1920, 1080)
FX_SIZE_DEFAULT = 1024
FX_SIZES = {"rim": 256, "casing": 256}   # si el nombre contiene la clave

# original (sin prefijo del arma) → (clip, número)
CLIP_ALIASES = {
    "pump": ("cycle", 1),
    "aim": ("aimidle", 1),
    "aimpump": ("aimcycle", 1),
    "reloadopen": ("reload", 1),
    "reloadinsert": ("reload", 2),
    "reloadclose": ("reload", 3),
}


def smoothstep(lo, hi, x):
    t = np.clip((x - lo) / float(hi - lo), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def key(path, lo, hi):
    """→ (rgb float32 0..255 despill, alpha float32 0..1) a la resolución original."""
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    green = g - np.maximum(r, b)
    alpha = 1.0 - smoothstep(lo, hi, green)
    spill = green > 0
    g = np.where(spill, np.minimum(g, np.maximum(r, b)), g)
    rgb = np.dstack([r, g, b])
    return rgb, alpha.astype(np.float32)


def dilate(rgb, alpha, iters):
    """Los píxeles con alpha == 0 copian (promedio de vecinos) el color opaco más cercano, hasta `iters` px."""
    original = rgb
    rgb = rgb.copy()
    valid = alpha >= 0.5
    zero = alpha < (1.0 / 255.0)
    h, w = alpha.shape
    for _ in range(iters):
        acc = np.zeros_like(rgb)
        cnt = np.zeros((h, w), np.float32)
        pv = np.pad(valid, 1)
        pc = np.pad(rgb, ((1, 1), (1, 1), (0, 0)))
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                v = pv[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
                c = pc[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
                acc += c * v[..., None]
                cnt += v
        new = (~valid) & (cnt > 0)
        rgb[new] = acc[new] / cnt[new][:, None]
        valid |= new
    # solo se escriben los píxeles totalmente transparentes; los del antialias conservan su color despill
    return np.where(zero[..., None], rgb, original)


def resize(rgb, alpha, size):
    """Escala color y alpha por separado (Lanczos). El color ya viene rellenado, sin verde ni negro en los bordes."""
    ch = [Image.fromarray(np.clip(rgb[..., i], 0, 255).astype(np.uint8)).resize(size, Image.LANCZOS) for i in range(3)]
    al = Image.fromarray(alpha, "F").resize(size, Image.LANCZOS)
    rgb2 = np.dstack([np.asarray(c).astype(np.float32) for c in ch])
    alpha2 = np.clip(np.asarray(al), 0.0, 1.0).astype(np.float32)
    return rgb2, alpha2


def to_image(rgb, alpha):
    out = np.dstack([np.clip(rgb + 0.5, 0, 255), np.clip(alpha * 255.0 + 0.5, 0, 255)]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def process_hands(src, dst, lo, hi, iters):
    rgb, alpha = key(src, lo, hi)                       # 1) recortar PRIMERO
    rgb = dilate(rgb, alpha, iters)                     # 3) relleno a la resolución original
    rgb, alpha = resize(rgb, alpha, HANDS_SIZE)         # 4) escalar DESPUÉS
    rgb = dilate(rgb, alpha, iters)                     #    y volver a rellenar
    to_image(rgb, alpha).save(dst, optimize=True)
    return stats(rgb, alpha)


def process_fx(src, dst, size, lo, hi, iters):
    rgb, alpha = key(src, lo, hi)
    ys, xs = np.where(alpha > 0.1)
    if len(xs) == 0:
        raise ValueError("el efecto quedó vacío (¿fondo no verde?): " + src)
    pad = 8
    y0, y1 = max(ys.min() - pad, 0), min(ys.max() + 1 + pad, alpha.shape[0])
    x0, x1 = max(xs.min() - pad, 0), min(xs.max() + 1 + pad, alpha.shape[1])
    rgb, alpha = rgb[y0:y1, x0:x1], alpha[y0:y1, x0:x1]
    rgb = dilate(rgb, alpha, iters)
    side = max(alpha.shape)                             # cuadrado con el estallido centrado
    sq_rgb = np.zeros((side, side, 3), np.float32)
    sq_alpha = np.zeros((side, side), np.float32)
    oy, ox = (side - alpha.shape[0]) // 2, (side - alpha.shape[1]) // 2
    sq_rgb[oy:oy + alpha.shape[0], ox:ox + alpha.shape[1]] = rgb
    sq_alpha[oy:oy + alpha.shape[0], ox:ox + alpha.shape[1]] = alpha
    sq_rgb = dilate(sq_rgb, sq_alpha, iters)
    rgb, alpha = resize(sq_rgb, sq_alpha, (size, size))
    rgb = dilate(rgb, alpha, iters)
    to_image(rgb, alpha).save(dst, optimize=True)
    return stats(rgb, alpha)


def stats(rgb, alpha):
    visible = alpha > 0.02
    soft = int(((alpha > 0.02) & (alpha < 0.98)).sum())
    spill = int(((rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2]) > 20) & visible).sum())
    return "opaco %.1f %% | píxeles semitransparentes %d | verde residual %d" % (
        100.0 * float((alpha >= 0.98).mean()), soft, spill)


def fx_name(stem):
    stem = stem.lower()
    if stem.startswith("fx_"):
        stem = stem[3:]
    if not re.search(r"_\d+$", stem):
        stem += "_01"
    return "fx_" + stem


def hands_name(weapon, stem):
    stem = stem.lower()
    if stem.startswith(weapon + "_"):
        stem = stem[len(weapon) + 1:]
    m = re.match(r"^([a-z]+)_(\d+)$", stem)
    if m:
        clip, n = m.group(1), int(m.group(2))
    else:
        clip, n = CLIP_ALIASES.get(stem, (stem, 1))
    if clip in CLIP_ALIASES and not m:
        clip, n = CLIP_ALIASES[clip]
    return "wilbur_vm_%s_%s_%02d.png" % (weapon, clip, n)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("folders", nargs="*", help="Shotgun, Revolver, FX (vacío = todas)")
    ap.add_argument("--lo", type=float, default=20.0, help="verdor por debajo del cual es opaco (default 20)")
    ap.add_argument("--hi", type=float, default=90.0, help="verdor por encima del cual es transparente (default 90)")
    ap.add_argument("--dilate", type=int, default=4, help="px de relleno de color en los bordes (default 4)")
    ap.add_argument("--src", default=SRC)
    ap.add_argument("--dst", default=DST)
    args = ap.parse_args()

    folders = args.folders or sorted(d for d in os.listdir(args.src) if os.path.isdir(os.path.join(args.src, d)))
    done = 0
    for folder in folders:
        sdir = os.path.join(args.src, folder)
        if not os.path.isdir(sdir):
            print("No existe", sdir)
            continue
        ddir = os.path.join(args.dst, folder)
        os.makedirs(ddir, exist_ok=True)
        for f in sorted(os.listdir(sdir)):
            if not f.lower().endswith(".png"):
                continue
            stem = os.path.splitext(f)[0]
            src = os.path.join(sdir, f)
            if folder.lower() == "fx":
                name = fx_name(stem) + ".png"
                size = next((s for k, s in FX_SIZES.items() if k in stem.lower()), FX_SIZE_DEFAULT)
                info = process_fx(src, os.path.join(ddir, name), size, args.lo, args.hi, args.dilate)
            else:
                name = hands_name(folder.lower(), stem)
                info = process_hands(src, os.path.join(ddir, name), args.lo, args.hi, args.dilate)
            print("%-8s %-28s -> %-42s %s" % (folder, f, name, info))
            done += 1
    print("Listo: %d PNG (lo=%g, hi=%g, dilate=%d)" % (done, args.lo, args.hi, args.dilate))
    return 0


if __name__ == "__main__":
    sys.exit(main())
