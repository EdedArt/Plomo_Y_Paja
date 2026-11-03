"""Recorta el silencio del inicio y del final de las voces de la intro (fase INTRO.1, enmienda A4).

Uso (desde cualquier carpeta; las rutas salen de la ubicación del script):
    python PLOMO_Y_PAJAV_1/Tools/trim_voice_silence.py            # recorta las voces de la intro (en sitio, copias del proyecto)
    python PLOMO_Y_PAJAV_1/Tools/trim_voice_silence.py --dry-run  # solo mide, no escribe nada

- Solo WAV PCM 16 bit (mono o estéreo). Umbral -45 dBFS medido en ventanas de 10 ms (RMS).
- Deja 0.05 s de margen antes de la primera ventana con voz y después de la última (queda <= 0.1 s de silencio).
- Fundido de 5 ms en los bordes cortados para que no haya clic. Si un borde ya tiene <= 0.1 s, no se toca.
- No toca las voces de la fase 3C ni nada fuera de la lista INTRO_VOICES.
"""
import argparse
import os
import sys
import wave

import numpy as np

VOICES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game", "Audio", "Voices")
INTRO_VOICES = (
    [os.path.join("narrador", f"narrador_intro_{i:02d}.wav") for i in range(1, 7)]
    + [os.path.join("taxista", f"taxista_{i:02d}.wav") for i in range(1, 4)]
    + [os.path.join("wilbur", n + ".wav") for n in ("wilbur_taxi_01", "wilbur_taxi_02", "wilbur_casa_01", "wilbur_arma_01")]
)

THRESHOLD_DBFS = -45.0
WINDOW_S = 0.010
PAD_S = 0.05
MAX_SILENCE_S = 0.10
FADE_S = 0.005


def read(path):
    with wave.open(path, "rb") as w:
        params = w.getparams()
        if params.sampwidth != 2 or params.comptype != "NONE":
            raise ValueError(f"{path}: solo WAV PCM 16 bit (sampwidth={params.sampwidth}, comp={params.comptype})")
        data = np.frombuffer(w.readframes(params.nframes), dtype="<i2").reshape(-1, params.nchannels)
    return params, data


def voiced_range(data, rate):
    """(primer, último+1) frame de las ventanas por encima del umbral; None si todo es silencio."""
    x = data.astype(np.float64) / 32768.0
    mono = np.sqrt(np.mean(x * x, axis=1))  # energía por frame (todos los canales)
    win = max(1, int(round(WINDOW_S * rate)))
    count = len(mono) // win
    if count == 0:
        return None
    rms = np.sqrt(np.mean((mono[: count * win] ** 2).reshape(count, win), axis=1))
    db = 20.0 * np.log10(np.maximum(rms, 1e-10))
    loud = np.nonzero(db > THRESHOLD_DBFS)[0]
    if len(loud) == 0:
        return None
    return loud[0] * win, min(len(mono), (loud[-1] + 1) * win)


def process(path, dry_run):
    params, data = read(path)
    rate = params.framerate
    n = len(data)
    rng = voiced_range(data, rate)
    if rng is None:
        return n / rate, 0.0, 0.0, n / rate, 0.0, 0.0, "todo silencio: no se toca"
    first, last = rng
    lead, trail = first / rate, (n - last) / rate

    pad = int(round(PAD_S * rate))
    start = max(0, first - pad) if lead > MAX_SILENCE_S else 0
    end = min(n, last + pad) if trail > MAX_SILENCE_S else n
    if start == 0 and end == n:
        return n / rate, lead, trail, n / rate, lead, trail, "ya cumple"

    out = data[start:end].astype(np.float64)
    fade = int(round(FADE_S * rate))
    if start > 0 and fade > 0:
        out[:fade] *= np.linspace(0.0, 1.0, fade)[:, None]
    if end < n and fade > 0:
        out[-fade:] *= np.linspace(1.0, 0.0, fade)[:, None]
    out = np.clip(np.round(out), -32768, 32767).astype("<i2")

    new_n = len(out)
    new_lead = (first - start) / rate
    new_trail = (end - last) / rate
    if not dry_run:
        with wave.open(path, "wb") as w:
            w.setnchannels(params.nchannels)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes(out.tobytes())
    return n / rate, lead, trail, new_n / rate, new_lead, new_trail, "recortado" + (" (simulado)" if dry_run else "")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dry-run", action="store_true", help="solo medir, no escribir")
    args = parser.parse_args()

    print(f"{'archivo':<24} {'antes':>7} {'ini':>6} {'fin':>6}   {'después':>7} {'ini':>6} {'fin':>6}  estado")
    missing = 0
    for rel in INTRO_VOICES:
        path = os.path.join(VOICES, rel)
        name = os.path.basename(rel)
        if not os.path.exists(path):
            print(f"{name:<24} FALTA")
            missing += 1
            continue
        d0, l0, t0, d1, l1, t1, status = process(path, args.dry_run)
        print(f"{name:<24} {d0:7.3f} {l0:6.3f} {t0:6.3f}   {d1:7.3f} {l1:6.3f} {t1:6.3f}  {status}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
