using System;
using UnityEngine;

/// <summary>
/// Todo lo que necesita el viewmodel (manos de Wilbur en primera persona) para UN arma:
/// dibujos por clip, fogonazo, anclas del cañón y parámetros de movimiento.
/// ASSETS: Assets/_Game/Art/Viewmodel/VM_&lt;arma&gt;.asset (VM_Shotgun lo crea el menú
///         "Plomo y Paja → Crear Perfil Viewmodel Escopeta").
/// ViewmodelAnimator y ViewmodelMotion solo leen de aquí; cambiar de arma = SetProfile(otro perfil).
///
/// COORDENADAS: las anclas están en píxeles del cuadro de referencia 1920x1080, con el origen ABAJO a la
/// izquierda (como el Canvas). Los movimientos están en unidades de canvas (también 1920x1080).
/// </summary>
[CreateAssetMenu(fileName = "VM_Arma", menuName = "Plomo y Paja/Viewmodel Profile")]
public class ViewmodelProfile : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public Sprite[] frames = new Sprite[0];
        [Tooltip("Cuadros por segundo; 0 = el fps por defecto del perfil")]
        public float fps;
        [Tooltip("Tiempo mínimo en pantalla (s). Sirve para clips de 1 solo cuadro (ej: el bombeo)")]
        public float minDuration;

        public bool HasFrames => frames != null && frames.Length > 0;
    }

    [Header("Arma")]
    public string weaponId = "shotgun";
    [Tooltip("Versión de los tiempos (la sube ViewmodelProfileBuilder al aplicar los valores 'snappy' de la fase 1.3 a un asset viejo)")]
    public int timingVersion;

    [Header("Clips (vacío = se usa idle)")]
    public Clip idle = new Clip();
    [Tooltip("Disparo: un cuadro \"de a dos\" (0.08 s) y sigue el bombeo")]
    public Clip fire = new Clip { minDuration = 0.08f };
    [Tooltip("Bombeo / amartillar tras el disparo (opcional): 0.35 s en total (el sonido del bombeo suena a 0.35 s del disparo)")]
    public Clip cycle = new Clip { minDuration = 0.35f };
    [Tooltip("Opcional: sin cuadros, las manos bajan durante la recarga")]
    public Clip reload = new Clip();
    public Clip aimIdle = new Clip();
    [Tooltip("Opcional: sin cuadros, al disparar apuntando se queda aimIdle + culatazo")]
    public Clip aimFire = new Clip { minDuration = 0.08f };
    [Tooltip("Opcional: sin cuadros, sale del apuntado, bombea con 'cycle' y vuelve")]
    public Clip aimCycle = new Clip { minDuration = 0.35f };

    [Header("Velocidad de animación")]
    [Tooltip("\"Animando de a dos\": 12 fps")]
    public float defaultFps = 12f;
    [Tooltip("idle con 2 o más cuadros se repite a este fps")]
    public float idleLoopFps = 8f;

    [Header("Fogonazo (PNG cuadrados con el estallido centrado)")]
    public Sprite[] flashSprites = new Sprite[0];
    [Tooltip("Boca del cañón en el cuadro de disparo desde la cadera (px 1920x1080, origen abajo-izquierda)")]
    public Vector2 hipMuzzle = new Vector2(1000f, 690f);
    [Tooltip("Boca del cañón en el cuadro de disparo apuntando (px 1920x1080, origen abajo-izquierda)")]
    public Vector2 aimMuzzle = new Vector2(960f, 550f);
    [Tooltip("Tamaño base del fogonazo (px de canvas)")]
    public float flashSize = 360f;
    public Vector2 flashScaleRange = new Vector2(0.9f, 1.2f);
    public float flashDuration = 0.07f;

    [Header("Respiración")]
    public float breathAmplitude = 6f;
    [Tooltip("0.01 = ±1 % de alto")]
    public float breathScale = 0.01f;
    [Tooltip("Respiraciones por segundo")]
    public float breathRate = 0.3f;

    [Header("Balanceo al caminar (figura de 8)")]
    [Tooltip("Pasos por segundo a la velocidad de referencia (cada paso = un rebote; el 8 completo son 2 pasos)")]
    public float bobFrequency = 1.8f;
    [Tooltip("Velocidad (m/s) a la que el balanceo va a bobFrequency")]
    public float bobReferenceSpeed = 4f;
    public Vector2 walkBobAmplitude = new Vector2(18f, 14f);
    public Vector2 runBobAmplitude = new Vector2(30f, 24f);
    [Tooltip("Al correr las manos bajan y giran")]
    public float runLower = 60f;
    public float runTilt = -6f;
    [Tooltip("Velocidad de mezcla caminar/correr/quieto (1/s)")]
    public float bobBlendSpeed = 6f;

    [Header("Inercia del mouse")]
    [Tooltip("Px por unidad de Mouse X/Y (en contra del movimiento)")]
    public float swayPerMouseUnit = 12f;
    public float swayMax = 40f;
    [Tooltip("Suavizado (más alto = sigue más rápido)")]
    public float swaySmoothing = 10f;
    [Tooltip("Grados de giro Z por cada px de inercia horizontal")]
    public float swayTiltPerPixel = 0.06f;

    [Header("Culatazo")]
    public Vector2 kickOffset = new Vector2(40f, -60f);
    public float kickRotation = 8f;
    public Vector2 kickSquash = new Vector2(1.10f, 0.90f);
    [Tooltip("Frecuencia del resorte de vuelta (rad/s)")]
    public float kickSpringFrequency = 32f;
    [Tooltip("0.7 con 32 rad/s: pico en el primer cuadro, de vuelta en reposo en ~0.15 s y solo un 5 % de rebote")]
    public float kickSpringDamping = 0.7f;

    [Header("Recarga sin cuadros (las manos bajan)")]
    public float reloadDip = 500f;
    [Tooltip("Segundos de bajada y de subida")]
    public float reloadEase = 0.4f;

    [Header("Apuntar (clic derecho)")]
    public float aimInTime = 0.15f;
    public float aimOutTime = 0.12f;
    [Tooltip("Desplazamiento de las manos al apuntar con dibujo aimIdle (px de canvas)")]
    public Vector2 aimOffset = Vector2.zero;
    [Tooltip("Sin dibujo aimIdle: las manos se mueven hacia el centro y crecen")]
    public Vector2 aimOffsetNoSprite = new Vector2(-320f, 140f);
    public float aimScaleNoSprite = 1.15f;
    [Tooltip("Golpecito de escala al cambiar al dibujo de apuntar")]
    public float aimSwapPunch = 1.03f;
    [Tooltip("Grados que baja el FOV de la cámara al apuntar")]
    public float aimFovDelta = 15f;
    [Tooltip("Multiplicadores mientras apunta")]
    public float aimBobMultiplier = 0.25f;
    public float aimSwayMultiplier = 0.3f;
    public float aimBreathMultiplier = 0.5f;
    public float aimKickMultiplier = 0.6f;
}
