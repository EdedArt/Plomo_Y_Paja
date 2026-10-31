#!/usr/bin/env python3
"""
Mide el punto de mira (la punta de la mira delantera / "bead") de los PNG de apuntar del viewmodel.

USO (desde PLOMO_Y_PAJAV_1/):
    python Tools/viewmodel_sight.py                       # shotgun y revolver
    python Tools/viewmodel_sight.py ruta/a/aimidle.png    # un PNG concreto

REQUISITOS: pip install pillow numpy

Algoritmo (el mismo que usa ViewmodelProfileBuilder.MeasureSightPoint en el Editor, para que den igual):
  1. Opaco = alpha >= 128.
  2. Solo se mira la franja central del dibujo: x entre el 30 % y el 70 % del ancho.
  3. Fila superior = la fila más alta con algún píxel opaco en esa franja.
  4. Cúmulo = los píxeles opacos de las filas [superior, superior + 3) dentro de esa franja; la punta es el promedio de sus x
     (en px del dibujo, +0.5 al centro del píxel) y el borde superior de la fila más alta.
  5. Resultado en píxeles del arte de 1920x1080 con el origen ABAJO a la izquierda: (x, alto - fila_superior).
"""
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
VM = os.path.join(ROOT, "Assets", "_Game", "Art", "Viewmodel")
DEFAULTS = [
    os.path.join(VM, "Shotgun", "wilbur_vm_shotgun_aimidle_01.png"),
    os.path.join(VM, "Revolver", "wilbur_vm_revolver_aimidle_01.png"),
]
BAND = (0.30, 0.70)
CLUSTER_ROWS = 3
ALPHA = 128


def measure(path):
    a = np.asarray(Image.open(path).convert("RGBA").getchannel("A"))
    h, w = a.shape
    x0, x1 = int(w * BAND[0]), int(w * BAND[1])
    band = a[:, x0:x1] >= ALPHA
    rows = np.where(band.any(axis=1))[0]
    if len(rows) == 0:
        raise ValueError("sin píxeles opacos en la franja central: " + path)
    top = int(rows[0])
    ys, xs = np.where(band[top:top + CLUSTER_ROWS, :])
    x = float(xs.mean() + x0 + 0.5)
    return x, float(h - top), (w, h)


def main():
    paths = sys.argv[1:] or DEFAULTS
    for p in paths:
        x, y, size = measure(p)
        print("%s  %dx%d  aimSightPoint = (%.1f, %.1f)" % (os.path.basename(p), size[0], size[1], x, y))


if __name__ == "__main__":
    main()
