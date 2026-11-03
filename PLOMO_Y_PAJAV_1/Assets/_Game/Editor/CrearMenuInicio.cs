#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;
using TMPro;

// Arma el menu de inicio completo con un clic.
// Menu de Unity: Plomo y Paja -> Crear menu de inicio
public static class CrearMenuInicio
{
    private const string RutaMenu = "Assets/_Game/Art/UI/Menu/";

    // Colores
    private static readonly Color Crema = new Color(0.95f, 0.89f, 0.75f);
    private static readonly Color CremaClaro = new Color(1f, 0.97f, 0.88f);
    private static readonly Color Negro = new Color(0.08f, 0.06f, 0.05f);

    [MenuItem("Plomo y Paja/Crear menu de inicio")]
    public static void Crear()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Plomo y Paja",
                "Detene el modo Play antes de crear el menu.", "Ok");
            return;
        }

        // Limpia lo anterior para poder correrlo varias veces.
        var viejo = GameObject.Find("CanvasMenu");
        if (viejo != null) Object.DestroyImmediate(viejo);
        var viejoMgr = GameObject.Find("MenuManager");
        if (viejoMgr != null) Object.DestroyImmediate(viejoMgr);

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
        var f1 = Cargar<Sprite>(RutaMenu + "menu_inicio_01.png");
        var f2 = Cargar<Sprite>(RutaMenu + "menu_inicio_02.png");
        if (f1 != null && f2 != null)
        {
            var so = new SerializedObject(anim);
            var arr = so.FindProperty("frames");
            arr.arraySize = 2;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = f1;
            arr.GetArrayElementAtIndex(1).objectReferenceValue = f2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("No encontre menu_inicio_01/02 en " + RutaMenu +
                             ". Asignalos a mano en el componente MenuFrameAnimator.");
        }

        var placa = Cargar<Sprite>(RutaMenu + "boton_placa.png");

        // ---------- Panel principal ----------
        var panel = NuevoPanel("PanelPrincipal", canvasGO.transform);

        var titulo = NuevoTexto("Titulo", panel.transform, "PLOMO\nY PAJA", 110);
        Colocar(titulo.rectTransform, 600, 300, 700, 300);
        titulo.color = CremaClaro;
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.fontStyle = FontStyles.Bold;
        titulo.outlineWidth = 0.35f;
        titulo.outlineColor = Negro;

        var btnJugar    = NuevoBoton("BtnJugar",    panel.transform, "INICIAR JUEGO", placa, 600,  40);
        var btnOpciones = NuevoBoton("BtnOpciones", panel.transform, "OPCIONES",      placa, 600, -70);
        var btnCreditos = NuevoBoton("BtnCreditos", panel.transform, "CREDITOS",      placa, 600, -180);
        var btnSalir    = NuevoBoton("BtnSalir",    panel.transform, "SALIR",         placa, 600, -290);

        // ---------- Panel de opciones ----------
        var opciones = NuevoPanel("PanelOpciones", canvasGO.transform);

        var fondoOp = NuevaImagen("FondoOscuro", opciones.transform);
        Estirar(fondoOp.rectTransform);
        fondoOp.color = new Color(0f, 0f, 0f, 0.65f);

        var tituloOp = NuevoTexto("TituloOpciones", opciones.transform, "OPCIONES", 80);
        Colocar(tituloOp.rectTransform, 0, 260, 700, 120);
        tituloOp.color = CremaClaro;
        tituloOp.alignment = TextAlignmentOptions.Center;
        tituloOp.outlineWidth = 0.3f;
        tituloOp.outlineColor = Negro;

        var etqVol = NuevoTexto("EtiquetaVolumen", opciones.transform, "Volumen", 44);
        Colocar(etqVol.rectTransform, -320, 100, 420, 70);
        etqVol.color = Crema;
        etqVol.alignment = TextAlignmentOptions.Right;

        var slider = NuevoSlider("SliderVolumen", opciones.transform);
        Colocar((RectTransform)slider.transform, 160, 100, 520, 50);

        var etqPant = NuevoTexto("EtiquetaPantalla", opciones.transform, "Pantalla completa", 44);
        Colocar(etqPant.rectTransform, -320, -10, 420, 70);
        etqPant.color = Crema;
        etqPant.alignment = TextAlignmentOptions.Right;

        var toggle = NuevoToggle("TogglePantalla", opciones.transform);
        Colocar((RectTransform)toggle.transform, -40, -10, 60, 60);

        var btnVolver = NuevoBoton("BtnVolverOpciones", opciones.transform, "VOLVER", placa, 0, -220);

        var op = opciones.AddComponent<OptionsPanel>();
        var soOp = new SerializedObject(op);
        soOp.FindProperty("volumen").objectReferenceValue = slider;
        soOp.FindProperty("pantallaCompleta").objectReferenceValue = toggle;
        soOp.ApplyModifiedPropertiesWithoutUndo();

        opciones.SetActive(false);

        // ---------- Manager ----------
        var mgrGO = new GameObject("MenuManager");
        Undo.RegisterCreatedObjectUndo(mgrGO, "Crear menu");
        var mgr = mgrGO.AddComponent<MainMenu>();

        var soMgr = new SerializedObject(mgr);
        soMgr.FindProperty("panelPrincipal").objectReferenceValue = panel;
        soMgr.FindProperty("panelOpciones").objectReferenceValue = opciones;
        soMgr.ApplyModifiedPropertiesWithoutUndo();

        Conectar(btnJugar,    new UnityAction(mgr.Jugar));
        Conectar(btnOpciones, new UnityAction(mgr.AbrirOpciones));
        Conectar(btnCreditos, new UnityAction(mgr.AbrirOpciones)); // provisional
        Conectar(btnSalir,    new UnityAction(mgr.Salir));
        Conectar(btnVolver,   new UnityAction(mgr.MostrarPrincipal));

        Selection.activeGameObject = canvasGO;
        EditorUtility.SetDirty(canvasGO);

        Debug.Log("Menu de inicio creado. Revisa el campo 'Escena Juego' en MenuManager.");
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

    private static Slider NuevoSlider(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var slider = go.AddComponent<Slider>();

        var fondo = NuevaImagen("Fondo", go.transform);
        Estirar(fondo.rectTransform);
        fondo.color = new Color(0.2f, 0.18f, 0.15f);

        var zona = new GameObject("Relleno", typeof(RectTransform));
        zona.transform.SetParent(go.transform, false);
        Estirar((RectTransform)zona.transform);

        var relleno = NuevaImagen("Barra", zona.transform);
        Estirar(relleno.rectTransform);
        relleno.color = Crema;

        slider.fillRect = relleno.rectTransform;
        slider.targetGraphic = fondo;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static Toggle NuevoToggle(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var toggle = go.AddComponent<Toggle>();

        var fondo = NuevaImagen("Fondo", go.transform);
        Estirar(fondo.rectTransform);
        fondo.color = new Color(0.2f, 0.18f, 0.15f);

        var marca = NuevaImagen("Marca", go.transform);
        var r = marca.rectTransform;
        r.anchorMin = new Vector2(0.18f, 0.18f);
        r.anchorMax = new Vector2(0.82f, 0.82f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        marca.color = Crema;

        toggle.targetGraphic = fondo;
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
