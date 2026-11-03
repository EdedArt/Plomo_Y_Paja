#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

// Genera Assets/_Game/Resources/PauseMenu_Canvas.prefab (lo instancia PauseMenuSpawner).
// Menu de Unity: Plomo y Paja -> Crear menu de pausa. Se puede correr varias veces:
// sobrescribe el prefab (mismo GUID). Se arma en una escena de preview, no toca la escena abierta.
public static class CrearMenuPausa
{
    private const string RutaMenu = "Assets/_Game/Art/UI/Menu/";
    private const string CarpetaResources = "Assets/_Game/Resources";
    private const string RutaPrefab = CarpetaResources + "/PauseMenu_Canvas.prefab";

    // Mismos colores que CrearMenuInicio
    private static readonly Color Crema = new Color(0.95f, 0.89f, 0.75f);
    private static readonly Color CremaClaro = new Color(1f, 0.97f, 0.88f);
    private static readonly Color Negro = new Color(0.08f, 0.06f, 0.05f);
    private static readonly Color Beige = new Color(0.93f, 0.87f, 0.73f);

    [MenuItem("Plomo y Paja/Crear menu de pausa")]
    public static void Crear()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Plomo y Paja",
                "Detene el modo Play antes de crear el menu de pausa.", "Ok");
            return;
        }

        if (!AssetDatabase.IsValidFolder(CarpetaResources))
            AssetDatabase.CreateFolder("Assets/_Game", "Resources");

        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = null;
        try
        {
            root = Armar(preview);
            PrefabUtility.SaveAsPrefabAsset(root, RutaPrefab, out bool ok);
            if (ok) Debug.Log("[CrearMenuPausa] Prefab generado en " + RutaPrefab);
            else Debug.LogError("[CrearMenuPausa] No se pudo guardar " + RutaPrefab);
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
        }

        AssetDatabase.SaveAssets();
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (asset != null) EditorGUIUtility.PingObject(asset);
    }

    private static GameObject Armar(Scene preview)
    {
        // ---------- Canvas ----------
        var root = new GameObject("PauseMenu_Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, preview);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var pausa = root.AddComponent<PauseMenu>(); // agrega CanvasGroup (RequireComponent)

        // ---------- Oscurecido ----------
        var oscuro = NuevaImagen("Oscurecido", root.transform);
        Estirar(oscuro.rectTransform);
        oscuro.color = new Color(0f, 0f, 0f, 0.75f);

        // ---------- Panel ----------
        var panel = NuevaImagen("Panel", root.transform);
        Colocar(panel.rectTransform, 0, 0, 900, 700);
        var fondoPanel = Cargar<Sprite>(RutaMenu + "pausa_01.png");
        if (fondoPanel != null)
        {
            panel.sprite = fondoPanel;
            panel.color = Color.white;
        }
        else
        {
            panel.color = Beige;
            Debug.LogWarning("[CrearMenuPausa] No encontre " + RutaMenu +
                             "pausa_01.png; el panel queda beige. Volve a correr el menu cuando este.");
        }

        // ---------- Titulo ----------
        var titulo = NuevoTexto("Titulo", panel.transform, "PAUSA", 110);
        Colocar(titulo.rectTransform, 0, 245, 800, 150);
        var fuente = BuscarFuente();
        if (fuente != null) titulo.font = fuente;
        titulo.color = CremaClaro;
        titulo.fontStyle = FontStyles.Bold;
        titulo.outlineWidth = 0.35f;
        titulo.outlineColor = Negro;

        // ---------- Botones ----------
        var placa = Cargar<Sprite>(RutaMenu + "boton_placa.png");
        var btnContinuar = NuevoBoton("BtnContinuar", panel.transform, "CONTINUAR",       placa,  95);
        var btnReiniciar = NuevoBoton("BtnReiniciar", panel.transform, "REINICIAR NIVEL", placa, -20);
        var btnMenu      = NuevoBoton("BtnMenu",      panel.transform, "MENU PRINCIPAL",  placa, -135);
        var btnSalir     = NuevoBoton("BtnSalir",     panel.transform, "SALIR",           placa, -250);

        var so = new SerializedObject(pausa);
        so.FindProperty("panel").objectReferenceValue = panel.rectTransform;
        so.ApplyModifiedPropertiesWithoutUndo();

        Conectar(btnContinuar, new UnityAction(pausa.Continuar));
        Conectar(btnReiniciar, new UnityAction(pausa.Reiniciar));
        Conectar(btnMenu,      new UnityAction(pausa.MenuPrincipal));
        Conectar(btnSalir,     new UnityAction(pausa.Salir));

        return root;
    }

    // ---------- ayudantes (mismo estilo que CrearMenuInicio) ----------

    private static T Cargar<T>(string ruta) where T : Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(ruta);
    }

    private static TMP_FontAsset BuscarFuente()
    {
        foreach (var guid in AssetDatabase.FindAssets("SpecialElite t:TMP_FontAsset"))
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (f != null) return f;
        }
        Debug.LogWarning("[CrearMenuPausa] No encontre SpecialElite SDF; el titulo usa la fuente por defecto de TMP.");
        return null;
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

    private static Button NuevoBoton(string nombre, Transform padre, string txt, Sprite placa, float y)
    {
        var img = NuevaImagen(nombre, padre);
        Colocar(img.rectTransform, 0, y, 540, 105);

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
        so.FindProperty("beige").colorValue = placa != null ? Color.white : Crema;
        so.FindProperty("beigeEncima").colorValue = placa != null ? Color.white : CremaClaro;
        so.FindProperty("dorado").colorValue = CremaClaro;
        so.FindProperty("doradoEncima").colorValue = Color.white;
        so.FindProperty("grosorBorde").floatValue = placa != null ? 0f : 6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        return btn;
    }

    private static void Conectar(Button btn, UnityAction accion)
    {
        UnityEventTools.AddPersistentListener(btn.onClick, accion);
    }
}
#endif
