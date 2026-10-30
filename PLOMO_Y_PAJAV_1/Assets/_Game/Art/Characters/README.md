# Guía de frames de personaje (para Edgar)

Carpeta: `Assets/_Game/Art/Characters/<characterId>/<anim>_<NN>.png` (también vale `<characterId>_<anim>_<NN>.png`).
`characterId` y carpeta en **minúsculas**: `guardia`, `informante`, `donlobo`, `porky`, `max`, `wilbur_hands`, `fx`, `intro`.
Animaciones: `idle walk shoot hit surrender death talk fire reload`. Numeración: `01`, `02`, ...

## Reglas para cada frame de un guardia (y de los demás personajes de pie)
1. **Lienzo de 512 × 512 px**, siempre el mismo en todos los frames del personaje.
2. **El personaje mide ~449 px de alto** (cabeza a pies). Con 236 píxeles por metro eso son ~1.9 m en el juego.
3. **Fondo transparente real** (PNG RGBA). Nada de tablero de ajedrez dibujado ni de fondo de color.
4. **Los pies cerca del borde de abajo.** Da igual si queda un poco de margen: el pivote se calcula solo.
5. Contorno de tinta negro y limpio (el shader recorta el alpha a 0.5: evitar bordes semitransparentes anchos).
6. Centrado en horizontal; el personaje siempre se dibuja de frente (el juego lo gira hacia la cámara).

## Qué hace Unity solo
- Sprite Single, Bilinear, sin compresión, Max Size 2048.
- **Pivote automático:** X = centro del lienzo, Y = la fila más baja con alpha > 0.1 (los pies). Así cada frame queda pegado al suelo aunque el margen de abajo cambie entre frames.
- 236 px/m para `guardia`; 250 px/m para el resto (`wilbur_hands` y `intro` tienen sus propias reglas).

## Después de soltar o cambiar PNGs
Menú **Plomo y Paja → Reconstruir Sets de Sprites** (o **Crear Prefabs de Prueba**, que ya lo hace). Salir de Play antes.
