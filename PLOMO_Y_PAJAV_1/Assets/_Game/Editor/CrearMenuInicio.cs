#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using TMPro;
using Object = UnityEngine.Object;

// Arma el menu de inicio completo con un clic (es la unica fuente de la escena MenuPrincipal).
// Menu de Unity: Plomo y Paja -> Crear menu de inicio
// Se puede correr varias veces: borra lo anterior y vuelve a tomar el arte y el audio que haya.
public static class CrearMenuInicio
{
    private const string RutaMenu = "Assets/_Game/Art/UI/Menu/";
    private const string RutaEscena = "Assets/_Game/Scenes/MenuPrincipal.unity";
    private const string RutaFuente = "Assets/_Game/Fonts/IMFellEnglish SDF.asset";
    private const string RutaFuenteRespaldo = "Assets/_Game/Fonts/SpecialElite SDF.asset";
    private const string RutaMusica = "Assets/_Game/Audio/Music/musica_intro.wav";
    private const string RutaLibreria = "Assets/_Game/Audio/AudioLibrary.asset";

    private const string TextoCreditos =
        "<size=64>PLOMO Y PAJA</size>\n\n" +
        "Programación y dirección — David Cabezas\n" +
        "Arte y personajes — Edgar Arteaga\n" +
        "Voces, música y sonido — Cristian\n\n" +
        "Universidad Cooperativa de Colombia · Pasto · 2026\n" +
        "Hecho con Unity";

    // Lo que el menu puede usar y que aun puede no estar: se lista al final de cada corrida.
    private static readonly string[] SonidosEsperados =
        { "sfx_trueno", "sfx_trueno_lejos", "sfx_ui_hover", "sfx_ui_click", "sfx_ui_jugar" };

    // Colores
    private static readonly Color Crema = new Color(0.95f, 0.89f, 0.75f);
    private static readonly Color CremaClaro = new Color(1f, 0.97f, 0.88f);
    private static readonly Color Negro = new Color(0.08f, 0.06f, 0.05f);

    private static TMP_FontAsset fuente;

    [MenuItem("Plomo y Paja/Crear menu de inicio")]
    public static void Crear()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Plomo y Paja",
                "Detene el modo Play antes de crear el menu.", "Ok");
            return;
        }

        if (!AbrirEscena()) return;

        // Limpia lo anterior para poder correrlo varias veces.
        foreach (string nombre in new[] { "CanvasMenu", "MenuManager", "UI_Pelicula", "UI_Fundido", "MusicPlayer" })
        {
            var viejo = GameObject.Find(nombre);
            if (viejo != null) Object.DestroyImmediate(viejo);
        }

        var faltantes = new List<string>();

        fuente = Cargar<TMP_FontAsset>(RutaFuente);
        if (fuente == null) fuente = Cargar<TMP_FontAsset>(RutaFuenteRespaldo);
        if (fuente == null) faltantes.Add("fuente IMFellEnglish / SpecialElite (se usa la de TMP)");

        // ---------- Canvas ----------
        var canvasGO = new GameObject("CanvasMenu",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Crear menu");

        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // ---------- Fondo animado ----------
        var fondo = NuevaImagen("Fondo", canvasGO.transform);
        Estirar(fondo.rectTransform);
        fondo.color = Color.white;
        fondo.raycastTarget = false;

        var anim = fondo.gameObject.AddComponent<MenuFrameAnimator>();
        Vector2 tamano = Vector2.zero;
        Sprite[] espera = BuscarCuadros("menu_inicio", ref tamano);
        Sprite[] tormenta = BuscarCuadros("menu_rayo", ref tamano);
        if (espera.Length == 0)
        {
            faltantes.Add("menu_inicio_NN (fondo oscuro hasta que lleguen)");
            Debug.LogWarning("No encontre menu_inicio_NN en " + RutaMenu +
                             ". Asignalos a mano en el componente MenuFrameAnimator.");
        }
        if (tormenta.Length == 0) faltantes.Add("menu_rayo_NN (el rayo cae sobre la imagen de espera)");

        var soAnim = new SerializedObject(anim);
        AsignarLista(soAnim.FindProperty("frames"), espera);
        AsignarLista(soAnim.FindProperty("framesTormenta"), tormenta);
        soAnim.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Destello del rayo (encima del fondo, debajo de la UI) ----------
        var destello = NuevaImagen("Destello", canvasGO.transform);
        Estirar(destello.rectTransform);
        destello.color = new Color(1f, 1f, 1f, 0f);
        destello.raycastTarget = false;

        var rayo = destello.gameObject.AddComponent<LightningFlash>();
        var soRayo = new SerializedObject(rayo);
        soRayo.FindProperty("flash").objectReferenceValue = destello;
        soRayo.FindProperty("background").objectReferenceValue = anim;
        soRayo.ApplyModifiedPropertiesWithoutUndo();

        var placa = Cargar<Sprite>(RutaMenu + "boton_placa.png");

        // ---------- Panel principal ----------
        var panel = NuevoPanel("PanelPrincipal", canvasGO.transform);

        var logo = CrearLogo(panel.transform, faltantes);

        var btnJugar    = NuevoBoton("BtnJugar",    panel.transform, "Jugar",    placa, 600,  40);
        var btnOpciones = NuevoBoton("BtnOpciones", panel.transform, "Opciones", placa, 600, -70);
        var btnCreditos = NuevoBoton("BtnCreditos", panel.transform, "Créditos", placa, 600, -180);
        var btnSalir    = NuevoBoton("BtnSalir",    panel.transform, "Salir",    placa, 600, -290);

        // Jugar suena con su propio bombeo (MainMenu), no con el clic.
        var soJugar = new SerializedObject(btnJugar.GetComponent<BotonCartoon>());
        soJugar.FindProperty("sonidoClic").stringValue = "";
        soJugar.ApplyModifiedPropertiesWithoutUndo();

        var botones = new[] { btnJugar, btnOpciones, btnCreditos, btnSalir };
        foreach (var b in botones) b.gameObject.AddComponent<CanvasGroup>();

        // ---------- "Pulsa cualquier tecla" ----------
        var prompt = NuevoTexto("PromptEspera", canvasGO.transform, "Pulsa cualquier tecla", 56);
        Colocar(prompt.rectTransform, 0, -450, 1200, 110);
        prompt.color = CremaClaro;
        prompt.outlineWidth = 0.3f;
        prompt.outlineColor = Negro;
        var promptGrupo = prompt.gameObject.AddComponent<CanvasGroup>();
        promptGrupo.blocksRaycasts = false;
        promptGrupo.interactable = false;

        // ---------- Panel de opciones ----------
        var opciones = NuevoPanel("PanelOpciones", canvasGO.transform);

        var fondoOp = NuevaImagen("FondoOscuro", opciones.transform);
        Estirar(fondoOp.rectTransform);
        fondoOp.color = new Color(0f, 0f, 0f, 0.65f);

        var tituloOp = NuevoTexto("TituloOpciones", opciones.transform, "Opciones", 80);
        Colocar(tituloOp.rectTransform, 0, 320, 700, 120);
        tituloOp.color = CremaClaro;
        tituloOp.alignment = TextAlignmentOptions.Center;
        tituloOp.outlineWidth = 0.3f;
        tituloOp.outlineColor = Negro;

        NuevaEtiqueta("EtiquetaVolumen", opciones.transform, "Volumen general", 170);
        var slider = NuevoSlider("SliderVolumen", opciones.transform, 0f, 1f, 1f);
        Colocar((RectTransform)slider.transform, 240, 170, 520, 50);

        NuevaEtiqueta("EtiquetaMusica", opciones.transform, "Música", 70);
        var sliderMusica = NuevoSlider("SliderMusica", opciones.transform, 0f, 1f, 1f);
        Colocar((RectTransform)sliderMusica.transform, 240, 70, 520, 50);

        NuevaEtiqueta("EtiquetaSensibilidad", opciones.transform, "Sensibilidad del ratón", -30);
        var sliderSens = NuevoSlider("SliderSensibilidad", opciones.transform, 0.25f, 2f, 1f);
        Colocar((RectTransform)sliderSens.transform, 240, -30, 520, 50);

        NuevaEtiqueta("EtiquetaPantalla", opciones.transform, "Pantalla completa", -130);
        var toggle = NuevoToggle("TogglePantalla", opciones.transform);
        Colocar((RectTransform)toggle.transform, 10, -130, 60, 60);

        var btnVolver = NuevoBoton("BtnVolverOpciones", opciones.transform, "Volver", placa, 0, -300);

        var op = opciones.AddComponent<OptionsPanel>();
        var soOp = new SerializedObject(op);
        soOp.FindProperty("volumen").objectReferenceValue = slider;
        soOp.FindProperty("pantallaCompleta").objectReferenceValue = toggle;
        soOp.FindProperty("musica").objectReferenceValue = sliderMusica;
        soOp.FindProperty("sensibilidadRaton").objectReferenceValue = sliderSens;
        soOp.ApplyModifiedPropertiesWithoutUndo();

        opciones.SetActive(false);

        // ---------- Panel de creditos ----------
        var creditos = NuevoPanel("PanelCreditos", canvasGO.transform);

        var fondoCr = NuevaImagen("FondoOscuro", creditos.transform);
        Estirar(fondoCr.rectTransform);
        fondoCr.color = new Color(0f, 0f, 0f, 0.75f);

        var textoCr = NuevoTexto("TextoCreditos", creditos.transform, TextoCreditos, 42);
        Colocar(textoCr.rectTransform, 0, 90, 1400, 640);
        textoCr.color = CremaClaro;
        textoCr.outlineWidth = 0.2f;
        textoCr.outlineColor = Negro;

        var btnVolverCr = NuevoBoton("BtnVolverCreditos", creditos.transform, "Volver", placa, 0, -340);

        creditos.SetActive(false);

        // ---------- Fundido y pelicula (canvas propios, encima de todo) ----------
        var fundido = IntroBookBuilder.BuildFade();
        Undo.RegisterCreatedObjectUndo(fundido.transform.root.gameObject, "Crear menu");
        fundido.GetComponent<CanvasGroup>().alpha = 0f;

        CrearPelicula(faltantes);

        // ---------- Manager ----------
        var mgrGO = new GameObject("MenuManager");
        Undo.RegisterCreatedObjectUndo(mgrGO, "Crear menu");

        // El menu es la primera escena: sin esto no habria AudioLibrary activa ni truenos.
        var libreria = Cargar<AudioLibrary>(RutaLibreria);
        if (libreria != null)
        {
            var soLoader = new SerializedObject(mgrGO.AddComponent<AudioLibraryLoader>());
            soLoader.FindProperty("library").objectReferenceValue = libreria;
            soLoader.ApplyModifiedPropertiesWithoutUndo();
            foreach (string id in SonidosEsperados)
            {
                if (!libreria.ContainsSfx(id)) faltantes.Add(id);
            }
        }
        else
        {
            faltantes.Add("AudioLibrary.asset (menu sin sonido)");
        }

        var mgr = mgrGO.AddComponent<MainMenu>();

        var soMgr = new SerializedObject(mgr);
        soMgr.FindProperty("escenaJuego").stringValue = "Intro";
        soMgr.FindProperty("panelPrincipal").objectReferenceValue = panel;
        soMgr.FindProperty("panelOpciones").objectReferenceValue = opciones;
        soMgr.FindProperty("panelCreditos").objectReferenceValue = creditos;
        soMgr.FindProperty("promptEspera").objectReferenceValue = promptGrupo;
        soMgr.FindProperty("rayo").objectReferenceValue = rayo;
        soMgr.FindProperty("logo").objectReferenceValue = logo;
        soMgr.FindProperty("fundido").objectReferenceValue = fundido;
        var listaBotones = soMgr.FindProperty("botones");
        listaBotones.arraySize = botones.Length;
        for (int i = 0; i < botones.Length; i++)
            listaBotones.GetArrayElementAtIndex(i).objectReferenceValue = botones[i];
        soMgr.ApplyModifiedPropertiesWithoutUndo();

        Conectar(btnJugar,    new UnityAction(mgr.Jugar));
        Conectar(btnOpciones, new UnityAction(mgr.AbrirOpciones));
        Conectar(btnCreditos, new UnityAction(mgr.AbrirCreditos));
        Conectar(btnSalir,    new UnityAction(mgr.Salir));
        Conectar(btnVolver,   new UnityAction(mgr.MostrarPrincipal));
        Conectar(btnVolverCr, new UnityAction(mgr.MostrarPrincipal));

        // ---------- Musica ----------
        var musicaGO = new GameObject("MusicPlayer");
        Undo.RegisterCreatedObjectUndo(musicaGO, "Crear menu");
        var soMusica = new SerializedObject(musicaGO.AddComponent<MusicPlayer>());
        var clip = Cargar<AudioClip>(RutaMusica);
        if (clip == null) faltantes.Add("musica_intro.wav (se busca en la AudioLibrary)");
        soMusica.FindProperty("clip").objectReferenceValue = clip;
        soMusica.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = canvasGO;
        EditorUtility.SetDirty(canvasGO);

        var escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("Menu de inicio creado y guardado en " + escena.path + ". Cuadros: " + espera.Length +
                  " de espera, " + tormenta.Length + " de tormenta.");
        if (faltantes.Count > 0)
        {
            Debug.LogWarning("[Menu] Faltan (quedan placeholders; volve a correr el menu cuando lleguen): " +
                             string.Join(", ", faltantes));
        }
    }

    // ---------- piezas del menu ----------

    // Trabaja siempre sobre MenuPrincipal, sin perder lo que haya sin guardar en otra escena.
    private static bool AbrirEscena()
    {
        if (SceneManager.GetActiveScene().path == RutaEscena) return true;
        if (!File.Exists(RutaEscena))
        {
            Debug.LogWarning("No existe " + RutaEscena + ": el menu se arma en la escena abierta.");
            return true;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(RutaEscena);
        return true;
    }

    // Todos los cuadros del grupo, en orden numerico y con cualquier formato. El nombre exacto
    // deja fuera pausa_* y boton_placa. Cuadros identicos se saltan; tamanos distintos se avisan.
    private static Sprite[] BuscarCuadros(string prefijo, ref Vector2 tamano)
    {
        var patron = new Regex("^" + prefijo + @"_(\d+)\.(jpg|jpeg|png)$", RegexOptions.IgnoreCase);
        var porNumero = new SortedDictionary<int, string>();
        foreach (string archivo in Directory.GetFiles(RutaMenu))
        {
            Match m = patron.Match(Path.GetFileName(archivo));
            if (!m.Success) continue;

            int numero = int.Parse(m.Groups[1].Value);
            string ruta = archivo.Replace('\\', '/');
            if (porNumero.TryGetValue(numero, out string otra))
            {
                if (File.GetLastWriteTimeUtc(ruta) <= File.GetLastWriteTimeUtc(otra))
                {
                    Debug.LogWarning("[Menu] Dos archivos para el cuadro " + numero + ": se usa " + otra + " y se ignora " + ruta + ".");
                    continue;
                }
                Debug.LogWarning("[Menu] Dos archivos para el cuadro " + numero + ": se usa " + ruta + " y se ignora " + otra + ".");
            }
            porNumero[numero] = ruta;
        }

        var cuadros = new List<Sprite>();
        var vistos = new Dictionary<string, string>();
        using (MD5 md5 = MD5.Create())
        {
            foreach (string ruta in porNumero.Values)
            {
                string huella = BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(ruta)));
                if (vistos.TryGetValue(huella, out string igual))
                {
                    Debug.LogWarning("[Menu] " + ruta + " es identico a " + igual + ": no se repite en el ciclo.");
                    continue;
                }
                vistos[huella] = ruta;

                var sprite = Cargar<Sprite>(ruta);
                if (sprite == null)
                {
                    Debug.LogWarning("[Menu] " + ruta + " no se importo como Sprite (revisa MenuSpriteImporter).");
                    continue;
                }

                Vector2 medida = sprite.rect.size;
                if (tamano == Vector2.zero) tamano = medida;
                else if (medida != tamano)
                {
                    Debug.LogWarning("[Menu] " + ruta + " mide " + medida + " y los demas " + tamano +
                                     ": igual cubre la pantalla, pero el fundido puede saltar.");
                }
                cuadros.Add(sprite);
            }
        }
        return cuadros.ToArray();
    }

    private static void AsignarLista(SerializedProperty lista, Sprite[] sprites)
    {
        lista.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    // menu_logo.png si existe; si no, el titulo en TMP con contorno y sombra.
    private static RectTransform CrearLogo(Transform padre, List<string> faltantes)
    {
        var go = new GameObject("Logo", typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var rt = (RectTransform)go.transform;
        Colocar(rt, 600, 300, 700, 300);

        var sprite = Cargar<Sprite>(RutaMenu + "menu_logo.png");
        if (sprite != null)
        {
            var img = NuevaImagen("LogoImagen", rt);
            Estirar(img.rectTransform);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return rt;
        }

        faltantes.Add("menu_logo (se usa el titulo en TMP)");

        var sombra = NuevoTexto("TituloSombra", rt, "PLOMO\nY PAJA", 110);
        Estirar(sombra.rectTransform);
        sombra.rectTransform.anchoredPosition = new Vector2(7, -7);
        sombra.color = new Color(Negro.r, Negro.g, Negro.b, 0.7f);
        sombra.fontStyle = FontStyles.Bold;

        var titulo = NuevoTexto("Titulo", rt, "PLOMO\nY PAJA", 110);
        Estirar(titulo.rectTransform);
        titulo.color = CremaClaro;
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.fontStyle = FontStyles.Bold;
        titulo.outlineWidth = 0.35f;
        titulo.outlineColor = Negro;
        return rt;
    }

    // Reutiliza el filtro del libro (OldFilmFilter) en su propio Canvas, mas debil: se siente, no se nota.
    private static void CrearPelicula(List<string> faltantes)
    {
        var material = Cargar<Material>(IntroBookBuilder.FilmMaterialPath);
        if (material == null)
        {
            faltantes.Add("IntroOldFilm.mat (menu sin grano; corre Plomo y Paja -> Intro -> Construir escena del libro)");
            return;
        }

        Canvas canvas = IntroBookBuilder.OverlayCanvas("UI_Pelicula", 60);
        Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Crear menu");

        var img = NuevaImagen("Pelicula", canvas.transform);
        Estirar(img.rectTransform);
        img.color = Color.white;
        img.raycastTarget = false;
        img.material = material;

        var so = new SerializedObject(img.gameObject.AddComponent<OldFilmFilter>());
        so.FindProperty("graphic").objectReferenceValue = img;
        so.FindProperty("grainStrength").floatValue = 0.06f;
        so.FindProperty("dustChance").floatValue = 0.06f;
        so.FindProperty("dustStrength").floatValue = 0.35f;
        so.FindProperty("scratchChance").floatValue = 0.06f;
        so.FindProperty("scratchStrength").floatValue = 0.15f;
        so.FindProperty("flickerAmount").floatValue = 0.02f;
        so.FindProperty("vignetteStrength").floatValue = 0.2f;
        so.FindProperty("tint").colorValue = Color.white; // sin sepia: el atardecer conserva su color
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void NuevaEtiqueta(string nombre, Transform padre, string txt, float y)
    {
        var t = NuevoTexto(nombre, padre, txt, 40);
        Colocar(t.rectTransform, -300, y, 520, 70);
        t.color = Crema;
        t.alignment = TextAlignmentOptions.Right;
    }

    // El borde cambia de color al tener el foco del teclado.
    private static void ResaltarFoco(Selectable s, Image fondo)
    {
        fondo.color = Color.white;
        s.targetGraphic = fondo;
        s.transition = Selectable.Transition.ColorTint;
        var c = ColorBlock.defaultColorBlock;
        c.normalColor = new Color(0.2f, 0.18f, 0.15f);
        c.highlightedColor = new Color(0.35f, 0.3f, 0.2f);
        c.selectedColor = new Color(0.55f, 0.45f, 0.25f);
        c.pressedColor = new Color(0.55f, 0.45f, 0.25f);
        c.disabledColor = new Color(0.2f, 0.18f, 0.15f, 0.5f);
        s.colors = c;
    }

    // ---------- ayudantes ----------

    private static T Cargar<T>(string ruta) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(ruta);
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Colocar(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static GameObject NuevoPanel(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        Estirar((RectTransform)go.transform);
        return go;
    }

    private static Image NuevaImagen(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(padre, false);
        return go.GetComponent<Image>();
    }

    private static TMP_Text NuevoTexto(string nombre, Transform padre, string txt, float tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = txt;
        t.fontSize = tam;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static Button NuevoBoton(string nombre, Transform padre, string txt,
                                     Sprite placa, float x, float y)
    {
        var img = NuevaImagen(nombre, padre);
        Colocar(img.rectTransform, x, y, 420, 110);

        if (placa != null)
        {
            img.sprite = placa;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = Crema;
        }

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None; // lo maneja BotonCartoon

        var t = NuevoTexto("Texto", img.transform, txt, 44);
        Estirar(t.rectTransform);
        t.color = CremaClaro;
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.28f;
        t.outlineColor = Negro;
        t.margin = new Vector4(40, 10, 40, 10);

        var estilo = img.gameObject.AddComponent<BotonCartoon>();
        var so = new SerializedObject(estilo);
        // Con placa, los colores no deben tenir la madera.
        so.FindProperty("beige").colorValue = placa != null ? Color.white : Crema;
        so.FindProperty("beigeEncima").colorValue = placa != null
            ? new Color(1f, 1f, 1f, 1f) : CremaClaro;
        so.FindProperty("dorado").colorValue = CremaClaro;
        so.FindProperty("doradoEncima").colorValue = Color.white;
        so.FindProperty("grosorBorde").floatValue = placa != null ? 0f : 6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        return btn;
    }

    private static Slider NuevoSlider(string nombre, Transform padre, float min, float max, float valor)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var slider = go.AddComponent<Slider>();

        var fondo = NuevaImagen("Fondo", go.transform);
        Estirar(fondo.rectTransform);
        ResaltarFoco(slider, fondo);

        var zona = new GameObject("Relleno", typeof(RectTransform));
        zona.transform.SetParent(go.transform, false);
        Estirar((RectTransform)zona.transform);

        var relleno = NuevaImagen("Barra", zona.transform);
        Estirar(relleno.rectTransform);
        relleno.color = Crema;

        slider.fillRect = relleno.rectTransform;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = valor;
        return slider;
    }

    private static Toggle NuevoToggle(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var toggle = go.AddComponent<Toggle>();

        var fondo = NuevaImagen("Fondo", go.transform);
        Estirar(fondo.rectTransform);
        ResaltarFoco(toggle, fondo);

        var marca = NuevaImagen("Marca", go.transform);
        var r = marca.rectTransform;
        r.anchorMin = new Vector2(0.18f, 0.18f);
        r.anchorMax = new Vector2(0.82f, 0.82f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        marca.color = Crema;

        toggle.graphic = marca;
        toggle.isOn = Screen.fullScreen;
        return toggle;
    }

    private static void Conectar(Button btn, UnityAction accion)
    {
        UnityEventTools.AddPersistentListener(btn.onClick, accion);
        EditorUtility.SetDirty(btn);
    }
}
#endif
