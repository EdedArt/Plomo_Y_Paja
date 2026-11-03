using System;
using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// Interrogatorio de Centavo (fases 3C.3 + 3C.4). Contenido en InterrogationData (Interrogation_Contador.asset,
/// textos exactos del guion §5); presentación en InterrogationDirector.
/// DÓNDE VA: raíz del prefab Centavo, junto a CentavoController. Los menús "Crear UI de interrogatorio" y
/// "Crear datos del interrogatorio" le asignan el canvas y los datos.
///
/// FLUJO: contador_int_00 → ronda 0 (opción → wilbur_int_0X → respuesta) → ronda 1 (opción → wilbur_int_1X → respuesta
/// según la confianza) → contador_int_2 → pensamiento de la IA (≤ 5 s, si no, respaldo del guion) → decisión →
///   Jalar el gatillo: wilbur_dec_matar → contador_muerte_01 cortado por el escopetazo → Exit → muerto (D11: sin línea de la IA)
///   Dejarlo ir: wilbur_dec_perdonar → contador_perdon_01 → contador_perdon_02 (solo si no dio registro_01) → Exit → huye
/// REGLAS (guion §5): confianza 0; 0A −1, 0B +1, 0C 0. Ronda 1: ≥ 0 → _v (Helpful), &lt; 0 → _m (Misleading); 1C siempre
/// (Helpful, sin cifra). Mintió = eligió 0A o dijo alguna _m. registro_01: en 1A_v o en perdon_02.
/// GameStateTracker al llegar a la decisión (D10): RegisterInterrogation (una vez) + RegisterContadorInterrogation.
///
/// EPÍLOGO (3C.4.1): al elegir se pide a la IA un último pensamiento (en paralelo con las líneas de la decisión; espera
/// máx. afterThoughtTimeoutSeconds desde ese momento, si no, respaldo del guion "Pensamiento después de la decisión").
/// Con la escena ya cerrada, el mundo corre y el jugador sigue bloqueado (sin manos, mira ni Escape):
///   Dejarlo ir: la cámara sube a la altura de los ojos siguiéndolo cojear hacia el portón → mira al cielo → Centavo
///     desaparece → pensamiento → la cámara vuelve → control.
///   Jalar el gatillo: la cámara se queda en el cuerpo y sube despacio → pensamiento (reemplaza la línea de la IA de la
///     baja, D11) → la cámara vuelve → control.
/// </summary>
[RequireComponent(typeof(CentavoController))]
public class CentavoInterrogation : MonoBehaviour
{
    [Tooltip("Prefab Prefabs/UI/InterrogationCanvas (lo asigna el menú Crear UI de interrogatorio)")]
    [SerializeField] private InterrogationDirector canvasPrefab;

    [Tooltip("Data/Interrogation_Contador.asset (lo asigna el menú Crear datos del interrogatorio)")]
    [SerializeField] private InterrogationData data;

    /// <summary>Datos del interrogatorio (CentavoController los usa para el pensamiento al rematarlo, 3C.5.1).</summary>
    public InterrogationData Data => data;

    [Tooltip("Segundos que se ve a Centavo muerto en el retrato antes de salir")]
    [SerializeField, Min(0f)] private float killHoldSeconds = 0.8f;

    [Tooltip("Segundos que se ve el aviso de la pista")]
    [SerializeField, Min(0.5f)] private float clueNoticeSeconds = 3f;

    [Tooltip("Par del retrato mientras se decide")]
    [SerializeField] private string decisionEmotion = "plead";

    [Header("Epílogo: dejarlo ir")]
    [Tooltip("La cámara sube de agachado a la altura de los ojos, siguiéndolo")]
    [SerializeField, Min(0.05f)] private float spareRiseSeconds = 0.6f;
    [Tooltip("Siguiéndolo cojear hacia el portón")]
    [SerializeField, Min(0f)] private float spareTrackSeconds = 2f;
    [Tooltip("Inclinarse hacia el cielo")]
    [SerializeField, Min(0.05f)] private float skyTiltSeconds = 1f;
    [Tooltip("Grados hacia arriba al mirar el cielo")]
    [SerializeField, Range(20f, 85f)] private float skyPitch = 65f;
    [Tooltip("Altura (m) sobre sus pies a la que mira la cámara mientras cojea")]
    [SerializeField, Min(0f)] private float spareLookHeight = 1f;

    [Header("Epílogo: jalar el gatillo")]
    [Tooltip("La cámara se queda en el cuerpo y sube despacio a la altura de los ojos")]
    [SerializeField, Min(0.05f)] private float killRiseSeconds = 1f;
    [Tooltip("Altura (m) sobre el ancla sentada a la que mira la cámara")]
    [SerializeField, Min(0f)] private float bodyLookHeight = 0.35f;

    [Header("Epílogo: pensamiento")]
    [SerializeField, Min(0f)] private float outroThoughtMinSeconds = 3f;
    [Tooltip("Los textos largos se quedan más: letras × este valor (si pasa del mínimo)")]
    [SerializeField, Min(0f)] private float outroThoughtSecondsPerCharacter = 0.055f;

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
        if (data == null)
        {
            Debug.LogWarning("[Centavo] Sin datos del interrogatorio: correr Plomo y Paja → Contador → Crear datos del interrogatorio.");
            return;
        }
        InterrogationDirector director = GetDirector();
        if (director == null) return;
        if (!_centavo.BeginInterrogation()) return;
        if (!director.Enter(_centavo, Flow, data.initialEmotion)) _centavo.ResolvePardoned(); // no debería pasar: no dejarlo colgado
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

    // ==================================================================
    // Flujo
    // ==================================================================

    private IEnumerator Flow(InterrogationDirector d)
    {
        GameStateTracker tracker = GameStateTracker.Instance;
        var run = new InterrogationRun(data, tracker != null && tracker.HasClue(data.clueId));

        // Rondas
        foreach (InterrogationRound round in data.rounds)
        {
            if (round == null || round.options == null || round.options.Length == 0) continue;
            if (!string.IsNullOrEmpty(round.openingLineId)) yield return Play(d, run, data.GetLine(round.openingLineId));

            var texts = new string[round.options.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                InterrogationLine playerLine = data.GetLine(round.options[i].playerLineId);
                texts[i] = playerLine != null ? playerLine.text : round.options[i].playerLineId;
            }
            int choice = 0;
            yield return d.Choose(texts, i => choice = i);

            InterrogationOption option = round.options[choice];
            yield return Play(d, run, data.GetLine(option.playerLineId));
            yield return Play(d, run, run.FirstAllowed(option.responseLineIds));
        }

        // Súplica: llegó a la decisión → cuenta como interrogatorio (D10)
        yield return Play(d, run, data.GetLine(data.pleaLineId));
        RegisterCompleted(run);

        // Pensamiento de la IA: nunca más de thoughtTimeoutSeconds (tiempo real); si no, respaldo del guion
        string aiLine = null;
        bool aiDone = false;
        string fallback = run.PickFallbackThought(data.fallbackThoughts);
        if (AICompanion.Instance != null)
            AICompanion.Instance.RequestCustomLine(BuildThoughtRequest(run), line => { aiLine = line; aiDone = true; });
        else aiDone = true;
        yield return d.Think(() => aiDone ? (aiLine ?? fallback) : null, data.thoughtTimeoutSeconds, () => fallback);

        // Decisión
        int decision = 1;
        yield return d.Decide(data.killLabel, data.spareLabel, i => decision = i, decisionEmotion);
        d.HideThought();
        bool killed = decision == 0;

        // Pensamiento del epílogo: se pide YA, en paralelo con las líneas de la decisión
        Func<string> afterThought = RequestAfterThought(run, killed);

        if (killed)
        {
            foreach (string id in data.killLineIds) yield return Play(d, run, data.GetLine(id));
            float until = Time.unscaledTime + killHoldSeconds;
            while (Time.unscaledTime < until) yield return null;
            _centavo.ResolveKilled();              // death_01, contador = Dead, sin línea de la IA (D11)
            d.Exit(InterrogationOutcome.Killed, dir => KillOutro(dir, afterThought));
        }
        else
        {
            foreach (string id in data.spareLineIds) yield return Play(d, run, data.GetLine(id));
            _centavo.ResolvePardoned();            // contador = Pardoned; cojea (y dice huye_01) al cerrarse la escena
            d.Exit(InterrogationOutcome.Pardoned, dir => SpareOutro(dir, afterThought));
        }
    }

    // ==================================================================
    // Epílogo
    // ==================================================================

    /// <summary>
    /// Pide el pensamiento del epílogo y devuelve con qué leerlo: null mientras espera; la línea de la IA; o el
    /// respaldo si falla o pasan afterThoughtTimeoutSeconds desde ahora (tiempo real).
    /// </summary>
    private Func<string> RequestAfterThought(InterrogationRun run, bool killed)
    {
        string fallback = run.PickFallbackThought(killed ? data.killThoughts : data.spareThoughts);
        float start = Time.unscaledTime;
        string aiLine = null;
        bool done = false;
        if (AICompanion.Instance != null)
            AICompanion.Instance.RequestCustomLine(BuildAfterRequest(run, killed), line => { aiLine = line; done = true; });
        else done = true;

        return () =>
        {
            if (done) return aiLine ?? fallback;
            return Time.unscaledTime - start >= data.afterThoughtTimeoutSeconds ? fallback : null;
        };
    }

    private IEnumerator SpareOutro(InterrogationDirector d, Func<string> thought)
    {
        Transform centavo = _centavo.transform;
        Vector3 eye = d.PlayerEyePosition;
        Func<Quaternion> lookAtHim = () => InterrogationDirector.LookFrom(eye, centavo.position + Vector3.up * (spareLookHeight * centavo.lossyScale.y)); // 3C.6: sigue la escala ×1.15 de Centavo

        // De agachado a la altura de los ojos, siguiéndolo; luego lo sigue cojear hacia el portón
        yield return d.MoveCamera(() => eye, lookAtHim, spareRiseSeconds);
        yield return d.TrackCamera(lookAtHim, spareTrackSeconds);

        // Al cielo (misma dirección, mirando hacia arriba)
        Vector3 flat = d.CameraRotation * Vector3.forward;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
        Quaternion sky = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(-skyPitch, 0f, 0f);
        yield return d.MoveCamera(() => eye, () => sky, skyTiltSeconds);

        // Ya no se lo puede seguir: desaparece mientras se mira el cielo (antes de devolver el control)
        _centavo.ForceGone();
        yield return d.OutroThought(thought, outroThoughtMinSeconds, outroThoughtSecondsPerCharacter);
    }

    private IEnumerator KillOutro(InterrogationDirector d, Func<string> thought)
    {
        Vector3 eye = d.PlayerEyePosition;
        Vector3 body = _centavo.transform.position + Vector3.up * (bodyLookHeight * _centavo.transform.lossyScale.y);
        yield return d.MoveCamera(() => eye, () => InterrogationDirector.LookFrom(eye, body), killRiseSeconds);
        yield return d.OutroThought(thought, outroThoughtMinSeconds, outroThoughtSecondsPerCharacter);
    }

    /// <summary>Dice una línea si su condición se cumple, aplica sus efectos y, al terminar, entrega la pista si toca.</summary>
    private IEnumerator Play(InterrogationDirector d, InterrogationRun run, InterrogationLine line)
    {
        if (line == null || !run.Allowed(line)) yield break;
        run.Apply(line);

        var speaker = line.speaker == InterrogationLineSpeaker.Player
            ? InterrogationDirector.Speaker.Player
            : InterrogationDirector.Speaker.Target;
        if (line.cutByShot)
            yield return d.SayCut(speaker, line.text, line.id, line.emotion, new InterrogationDirector.LineCut());
        else
            yield return d.Say(speaker, line.text, line.id, line.emotion);

        if (line.givesClue && run.MarkClueGiven()) GiveClue(d);
    }

    /// <summary>registro_01 con la misma API que CluePickup (cuenta y avisa), sin el comentario de la IA (D11).</summary>
    private void GiveClue(InterrogationDirector d)
    {
        CluePickup.Award(data.clueId, data.clueName, data.clueDescription, data.clueFullText, 5f, false, this);
        d.ShowNotice("Pista: " + data.clueName, clueNoticeSeconds);
    }

    private void RegisterCompleted(InterrogationRun run)
    {
        GameStateTracker tracker = GameStateTracker.Instance;
        if (tracker == null) return;
        ContadorInterrogation result = run.Result == InterrogationLineResult.Misleading
            ? ContadorInterrogation.Misleading
            : run.Result == InterrogationLineResult.Helpful ? ContadorInterrogation.Helpful : ContadorInterrogation.None;
        tracker.RegisterContadorInterrogation(result, run.Lied);
        string outcome = result == ContadorInterrogation.Misleading ? "mintió o intentó engañar a Wilbur" : "dio información útil";
        tracker.RegisterInterrogation(data.npcName, outcome); // no cuenta dos veces: GameStateTracker lo ignora si ya está
    }

    private string BuildAfterRequest(InterrogationRun run, bool killed)
    {
        var sb = new StringBuilder();
        sb.Append("Wilbur acaba de terminar con ").Append(data.thoughtWho).Append(' ');
        sb.Append("Lo había interrogado herido en el piso. Esto fue lo que dijo: ");
        foreach (string said in run.TargetSaid) sb.Append('«').Append(said).Append("» ");
        sb.Append(run.Lied ? "Mintió al menos una vez. " : "No se le pilló ninguna mentira. ");
        bool sheet = killed ? run.ClueGiven : true; // al perdonarlo siempre entrega la hoja (perdon_02)
        sb.Append(sheet ? "Le entregó una hoja del libro de cuentas de Don Lobo. " : "No le entregó ningún papel. ");
        sb.Append(killed
            ? "Wilbur decidió jalar el gatillo: Centavo está muerto contra el banco de trabajo. "
            : "Wilbur lo dejó ir: Centavo se aleja cojeando por el portón del patio, en la noche. ");
        sb.Append("Escribe el último pensamiento de Wilbur sobre lo que acaba de hacer: 1 o 2 oraciones, máximo 25 palabras, ");
        sb.Append("en español, noir y ambiguo. No juzgues la decisión como buena o mala ni digas qué debió hacer");
        return sb.ToString();
    }

    private string BuildThoughtRequest(InterrogationRun run)
    {
        var sb = new StringBuilder();
        sb.Append("Wilbur acaba de interrogar a ").Append(data.thoughtWho).Append(' ');
        sb.Append("Está herido de un disparo en el costado, tirado en el piso, y ahora suplica por su vida. ");
        sb.Append("Esto fue lo que dijo: ");
        foreach (string said in run.TargetSaid) sb.Append('«').Append(said).Append("» ");
        sb.Append(run.Lied ? "Mintió al menos una vez. " : "No se le pilló ninguna mentira. ");
        sb.Append("Confianza que le tiene a Wilbur: ").Append(run.Trust)
          .Append(run.Trust > 0 ? " (se calmó). " : run.Trust < 0 ? " (está aterrado). " : " (desconfía). ");
        sb.Append(run.ClueGiven ? "Le entregó una hoja del libro de cuentas de Don Lobo. " : "No le entregó ningún papel. ");
        sb.Append("Escribe el pensamiento de Wilbur antes de decidir si lo mata o lo deja ir: 1 o 2 oraciones, ");
        sb.Append("máximo 25 palabras, en español, noir y ambiguo. NUNCA le digas qué elegir ni insinúes cuál opción es mejor");
        return sb.ToString();
    }
}
