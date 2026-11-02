using System.Collections;
using UnityEngine;

/// <summary>
/// Conecta a Centavo herido con la escena de interrogatorio (fase 3C.3).
/// DÓNDE VA: raíz del prefab Centavo, junto a CentavoController. El menú "Crear UI de interrogatorio" le asigna
/// el prefab del canvas; se instancia la primera vez que hace falta.
///
/// 3C.3 = DEMO de presentación, solo la Ronda 0 con los clips reales (guion §5):
///   contador_int_00 → opciones 0A/0B/0C → wilbur_int_0X → contador_int_0X → pensamiento (línea de respaldo del guion)
///   → decisión "Jalar el gatillo" / "Dejarlo ir" → Exit → Centavo muerto (death_01) o cojeando por el portón.
/// La demo NO cuenta el interrogatorio, no da registro_01 ni usa la confianza: todo eso es la 3C.4, que reemplaza
/// DemoFlow por el contenido completo (InterrogationData).
/// </summary>
[RequireComponent(typeof(CentavoController))]
public class CentavoInterrogation : MonoBehaviour
{
    [Tooltip("Prefab Prefabs/UI/InterrogationCanvas (lo asigna el menú Crear UI de interrogatorio)")]
    [SerializeField] private InterrogationDirector canvasPrefab;

    [Tooltip("Segundos que se ven los puntos suspensivos antes del pensamiento de respaldo (demo)")]
    [SerializeField, Min(0f)] private float demoThinkSeconds = 1.2f;

    // Textos exactos del guion (Docs/guiones/contador.md §5, Ronda 0)
    private const string Line00 = "¡No me remate, señor! ¡Yo no soy de los que pegan! Soy el contador. Números, solo números.";
    private static readonly string[] Round0Options =
    {
        "Habla rápido o la próxima no va al costado.",
        "Tranquilo. Si cooperas, sales de aquí con vida.",
        "¿Qué hace el contador de Don Lobo escondido en el patio de mi hermano?",
    };
    private static readonly string[] Round0PlayerClips = { "wilbur_int_0A", "wilbur_int_0B", "wilbur_int_0C" };
    private static readonly string[] Round0Responses =
    {
        "¡S-sí, señor! ¡Lo secuestraron! ¡Se lo llevaron a la fuerza, pataleando y todo!",
        "G-gracias… ay… El jefe me mandó a revisar la casa. A ver si quedaba algo de valor para cobrarse.",
        "Vine por las cuentas, señor. Su hermano tiene una cuenta abierta con el jefe. Muy, muy abierta.",
    };
    private static readonly string[] Round0ResponseClips = { "contador_int_0A", "contador_int_0B", "contador_int_0C" };
    private static readonly string[] Round0ResponseEmotions = { "scared", "nervous", "talk" };

    // Líneas de respaldo del pensamiento (guion §5, sin la condicional "solo si mintió")
    private static readonly string[] FallbackThoughts =
    {
        "Un contador vivo lleva la cuenta de quién lo dejó ir. Uno muerto no le cuenta nada a nadie.",
        "Tiembla como hoja. ¿Miente por miedo… o el miedo es lo único que no está fingiendo?",
        "Porky nunca supo sumar. ¿Y si este es el único que tiene las cuentas claras?",
        "Ya está en el suelo, Wilbur. Lo difícil no es disparar… es saber a quién le estás apretando el gatillo.",
    };

    private const string DecisionKill = "Jalar el gatillo";
    private const string DecisionSpare = "Dejarlo ir";

    private CentavoController _centavo;
    private InterrogationDirector _director;

    private void Awake()
    {
        _centavo = GetComponent<CentavoController>();
    }

    private void OnEnable()
    {
        if (_centavo != null) _centavo.OnInterrogationRequested += HandleRequested;
    }

    private void OnDisable()
    {
        if (_centavo != null) _centavo.OnInterrogationRequested -= HandleRequested;
    }

    private void HandleRequested()
    {
        if (InterrogationDirector.IsActive || !_centavo.CanBeInterrogated) return;
        InterrogationDirector director = GetDirector();
        if (director == null) return;
        if (!_centavo.BeginInterrogation()) return;
        if (!director.Enter(_centavo, DemoFlow, "scared")) _centavo.ResolvePardoned(); // no debería pasar: no dejarlo colgado
    }

    private InterrogationDirector GetDirector()
    {
        if (_director != null) return _director;
        if (canvasPrefab == null)
        {
            Debug.LogWarning("[Centavo] Sin InterrogationCanvas: correr Plomo y Paja → Contador → Crear UI de interrogatorio.");
            return null;
        }
        _director = Instantiate(canvasPrefab);
        _director.name = canvasPrefab.name;
        return _director;
    }

    private IEnumerator DemoFlow(InterrogationDirector d)
    {
        yield return d.Say(InterrogationDirector.Speaker.Target, Line00, "contador_int_00", "scared");

        int choice = 0;
        yield return d.Choose(Round0Options, i => choice = i);
        yield return d.Say(InterrogationDirector.Speaker.Player, Round0Options[choice], Round0PlayerClips[choice]);
        yield return d.Say(InterrogationDirector.Speaker.Target, Round0Responses[choice], Round0ResponseClips[choice],
            Round0ResponseEmotions[choice]);

        yield return d.Think(null, demoThinkSeconds, () => FallbackThoughts[Random.Range(0, FallbackThoughts.Length)]);

        int decision = 1;
        yield return d.Decide(DecisionKill, DecisionSpare, i => decision = i);
        d.HideThought();

        if (decision == 0)
        {
            _centavo.ResolveKilled();
            d.Exit(InterrogationOutcome.Killed);
        }
        else
        {
            _centavo.ResolvePardoned();
            d.Exit(InterrogationOutcome.Pardoned);
        }
    }
}
