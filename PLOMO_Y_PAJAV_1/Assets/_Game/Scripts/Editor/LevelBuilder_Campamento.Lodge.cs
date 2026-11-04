using UnityEngine;
using KitMesh = CartoonKit.KitMesh;
using WallOpening = LevelBuilderUtils.WallOpening;

/// <summary>
/// Z4, la guarida: cabaña de troncos. Plano interior (m):
///   Pasillo  x -1.5..1.5  z 54..60   entra por el marco abierto de la fachada (z 54), sigue al salón (z 60)
///   Oficina  x 1.5..11    z 54..60   puerta en el pasillo (z 57); escritorio con Anchor_Clue_carta_lobo_01
///   Salón    x -9..9      z 60..74   18 × 14: escritorio, 4 columnas, barra, billar, sofá, dos puertas laterales
///   Atrás    x -3..3      z 74..78   Door_CuartoAtras cerrada con candado (solo se ve en los finales)
/// El escritorio, las columnas y la barra son macizos: tapan balas en los dos sentidos.
/// </summary>
public static partial class LevelBuilder_Campamento
{
    private const float DeskTopOffice = 0.95f;
    private const float FrontH = 3.5f, HallH = 4.5f, LodgeT = 0.3f;

    private static void BuildLodge(Transform env, Transform lighting, Transform deco)
    {
        Transform z4 = LevelBuilderUtils.CreateGroup("Z4_Guarida", env);
        BuildLodgeShell(z4, lighting, deco);
        BuildPorch(z4, lighting, deco);
        BuildHallway(z4, lighting);
        BuildOffice(z4, lighting);
        BuildHall(z4, lighting, deco);
    }

    private static void BuildLodgeShell(Transform parent, Transform lighting, Transform deco)
    {
        const CartoonKit.WallFace In = CartoonKit.WallFace.Wainscot, Out = CartoonKit.WallFace.Plain, None = CartoonKit.WallFace.None;
        Material wood = K("Madera"), dark = K("MaderaOscura");
        Transform walls = LevelBuilderUtils.CreateGroup("Muros", parent);

        // Bloque de adelante: fachada (marco abierto en x = 0), costados y el muro que da al salón
        CartoonKit.Wall("Guarida_Fachada", new Vector3(-11f, 0f, 54f), new Vector3(11f, 0f, 54f), FrontH, LodgeT, dark, None, In, walls, new WallOpening(11f, 2.2f, 2.5f));
        CartoonKit.Wall("Guarida_Costado_O", new Vector3(-11f, 0f, 54f), new Vector3(-11f, 0f, 60f), FrontH, LodgeT, dark, In, None, walls);
        CartoonKit.Wall("Guarida_Costado_E", new Vector3(11f, 0f, 60f), new Vector3(11f, 0f, 54f), FrontH, LodgeT, dark, In, None, walls);
        CartoonKit.Wall("Guarida_Entrada_Salon", new Vector3(-11f, 0f, 60f), new Vector3(11f, 0f, 60f), HallH, LodgeT, dark, In, In, walls, new WallOpening(11f, 2.4f, 2.6f));

        // Pasillo
        CartoonKit.Wall("Pasillo_O", new Vector3(-1.5f, 0f, 54f), new Vector3(-1.5f, 0f, 60f), FrontH, 0.2f, wood, In, In, walls);
        CartoonKit.Wall("Pasillo_E", new Vector3(1.5f, 0f, 54f), new Vector3(1.5f, 0f, 60f), FrontH, 0.2f, wood, In, In, walls, new WallOpening(3f, 2f, 2.4f));

        // Salón
        CartoonKit.Wall("Salon_O", new Vector3(-9f, 0f, 60f), new Vector3(-9f, 0f, 74f), HallH, LodgeT, dark, In, Out, walls);
        CartoonKit.Wall("Salon_E", new Vector3(9f, 0f, 74f), new Vector3(9f, 0f, 60f), HallH, LodgeT, dark, In, Out, walls);
        CartoonKit.Wall("Salon_N", new Vector3(9f, 0f, 74f), new Vector3(-9f, 0f, 74f), HallH, LodgeT, dark, Out, In, walls);

        // Cuarto de atrás: se ve solo en los finales, por fuera cierra la cabaña
        CartoonKit.Wall("Atras_O", new Vector3(-3f, 0f, 74f), new Vector3(-3f, 0f, 78f), 3f, 0.25f, dark, Out, Out, walls);
        CartoonKit.Wall("Atras_E", new Vector3(3f, 0f, 74f), new Vector3(3f, 0f, 78f), 3f, 0.25f, dark, Out, Out, walls);
        CartoonKit.Wall("Atras_N", new Vector3(3f, 0f, 78f), new Vector3(-3f, 0f, 78f), 3f, 0.25f, dark, Out, Out, walls);

        CartoonKit.PlankFloor("Piso_Delante", new Vector3(0f, 0.02f, 57f), new Vector2(21.8f, 5.8f), parent, 0.6f);
        CartoonKit.PlankFloor("Piso_Salon", new Vector3(0f, 0.02f, 67f), new Vector2(17.8f, 13.8f), parent, 0.25f);

        Ceiling("Cielorraso_Delante", new Vector3(0f, FrontH + 0.1f, 57f), new Vector2(22.3f, 6.3f), parent);
        Ceiling("Cielorraso_Salon", new Vector3(0f, HallH + 0.1f, 67f), new Vector2(18.3f, 14.3f), parent);
        Ceiling("Cielorraso_Atras", new Vector3(0f, 3.1f, 76f), new Vector2(6.3f, 4.3f), parent);
        PitchedRoof("Techo_Delante", new Vector3(0f, FrontH + 0.2f, 57f), 22f, 6f, 1.7f, deco);
        PitchedRoof("Techo_Salon", new Vector3(0f, HallH + 0.2f, 67f), 18f, 14f, 3f, deco);
        PitchedRoof("Techo_Atras", new Vector3(0f, 3.2f, 76f), 6f, 4f, 1f, deco);

        // Por fuera se lee "cabaña de troncos"
        LogSkin("Troncos_Fachada_O", new Vector3(-11f, 0f, 53.84f), 9.9f, FrontH, 0f, deco);
        LogSkin("Troncos_Fachada_E", new Vector3(1.1f, 0f, 53.84f), 9.9f, FrontH, 0f, deco);
        LogSkin("Troncos_Costado_O", new Vector3(-11.16f, 0f, 54f), 6f, FrontH, -90f, deco);
        LogSkin("Troncos_Costado_E", new Vector3(11.16f, 0f, 54f), 6f, FrontH, -90f, deco);

        // Ventanas encendidas: la fachada tiene fondo (N2) aunque no se entre por ahí
        foreach (float x in new[] { -7.6f, -4f, 4f, 7.6f })
            CartoonKit.WindowFrame("Ventana_Fachada_" + x, new Vector3(x, 1.8f, 53.62f), Quaternion.LookRotation(Vector3.back), 1.3f, 1.1f, deco, true);
    }

    /// <summary>Troncos horizontales pegados a un muro (solo dibujo). start = extremo; el muro corre por +X local girado por yaw.</summary>
    private static void LogSkin(string name, Vector3 start, float length, float height, float yaw, Transform parent)
    {
        string key = $"Camp_PielTroncos_{Cm(length)}x{Cm(height)}";
        MeshGo(name, key, b =>
        {
            const float r = 0.17f;
            for (float y = r; y < height; y += r * 1.85f)
                AddLog(b, new Vector3(length * 0.5f, y, 0f), length + 0.25f, r);
        }, parent, start, yaw, K("Madera"));
    }

    private static void BuildPorch(Transform parent, Transform lighting, Transform deco)
    {
        Transform porch = LevelBuilderUtils.CreateGroup("Porche", parent);
        CartoonKit.Box("Porche_Piso", new Vector3(0f, 0.03f, 52.7f), new Vector3(5.4f, 0.06f, 2.6f), K("Madera"), deco, default, 0.01f, false);
        CartoonKit.Box("Porche_Techo", new Vector3(0f, 3.05f, 52.6f), new Vector3(5.6f, 0.16f, 3f), K("TechoEdificio"), porch, new Vector3(-6f, 0f, 0f));
        foreach (int s in new[] { -1, 1 })
        {
            CartoonKit.Box("Porche_Poste_" + s, new Vector3(s * 2.45f, 1.5f, 51.4f), new Vector3(0.22f, 3f, 0.22f), K("MaderaOscura"), porch);
            CartoonKit.WallLamp("Aplique_Puerta_" + s, new Vector3(s * 1.65f, 2.55f, 53.72f), 180f, porch, lighting, WarmLamp, 2.4f, 10f);
        }
        CartoonKit.StreetLamp("Farol_Guarida_O", new Vector3(-7.5f, 0f, 51.3f), 90f, parent, lighting, WarmLamp, 2.6f, 14f);
        CartoonKit.StreetLamp("Farol_Guarida_E", new Vector3(7.5f, 0f, 51.3f), -90f, parent, lighting, WarmLamp, 2.6f, 14f);
    }

    private static void BuildHallway(Transform parent, Transform lighting)
    {
        Transform hallway = LevelBuilderUtils.CreateGroup("Pasillo", parent);
        Table("Consola_Pasillo", new Vector3(-1.12f, 0f, 56.2f), 0.4f, 1.2f, 0.85f, 0f, hallway);
        CartoonKit.HangingLamp("Pasillo_Lampara", new Vector3(0f, FrontH, 57f), 0.5f, hallway, lighting, WarmLamp, 2.4f, 9f);
    }

    private static void BuildOffice(Transform parent, Transform lighting)
    {
        Transform office = LevelBuilderUtils.CreateGroup("Oficina", parent);
        SolidDesk("Escritorio_Oficina", new Vector3(7.4f, 0f, 57f), 2.4f, 1.1f, DeskTopOffice, 0f, office);
        CartoonKit.Chair("Silla_Oficina", new Vector3(7.4f, 0f, 58.2f), 180f, office);
        Cabinet("Estante_Oficina", new Vector3(10.55f, 0f, 57f), 1.6f, 0.5f, 1.9f, -90f, office, 0.45f, 1.05f);
        OpenCrate("Cajon_Oficina", new Vector3(3f, 0f, 59f), 0.7f, -12f, office);
        CartoonKit.Rug("Alfombra_Oficina", new Vector3(7f, 0.02f, 56.8f), new Vector2(4.4f, 3f), 0f, office);
        CartoonKit.HangingLamp("Oficina_Lampara", new Vector3(6.4f, FrontH, 57f), 0.6f, office, lighting, WarmLamp, 3f, 11f);
    }

    private static void BuildHall(Transform parent, Transform lighting, Transform deco)
    {
        Transform hall = LevelBuilderUtils.CreateGroup("Salon", parent);

        // Escritorio grande del Lobo (Anchor_LoboStart detrás) y la cubierta principal
        SolidDesk("Escritorio_Lobo", new Vector3(0f, 0f, 70.4f), 4.2f, 1.2f, 1.1f, 0f, hall);

        // 4 columnas
        foreach (float x in new[] { -5f, 5f })
        foreach (float z in new[] { 64.5f, 69.5f })
            CartoonKit.Box($"Columna_{x:0}_{z:0}", new Vector3(x, HallH * 0.5f, z), new Vector3(0.8f, HallH, 0.8f), K("MaderaOscura"), hall);

        // Barra (oriente) y el estante detrás, con la repisa baja donde están las balas
        CartoonKit.Box("Barra", new Vector3(6.4f, 0.52f, 64f), new Vector3(0.9f, 1.04f, 6.4f), K("MaderaOscura"), hall);
        CartoonKit.Box("Barra_Tapa", new Vector3(6.4f, 1.08f, 64f), new Vector3(1.1f, 0.08f, 6.6f), K("Laton"), deco, default, 0.02f, false);
        Cabinet("Estante_Barra", new Vector3(8.6f, 0f, 63.9f), 3.6f, 0.45f, 2.2f, -90f, hall, 0.45f, 1.05f, 1.6f);
        MeshGo("Botellas", "Camp_Botellas", b =>
        {
            System.Random rng = CartoonKit.Rng("Camp_Botellas");
            for (float z = -1.5f; z <= 1.5f; z += 0.3f)
            {
                b.Submesh = rng.NextDouble() < 0.5 ? 0 : 1;
                float y = rng.NextDouble() < 0.5 ? 1.05f : 1.6f;
                CartoonKit.AddLathe(b, new Vector3(0f, y, z), Quaternion.identity, new[] { 0f, 0.2f, 0.26f, 0.36f }, new[] { 0.05f, 0.05f, 0.022f, 0.022f }, 6, true, true);
            }
        }, deco, new Vector3(8.6f, 0f, 63.9f), 0f, K("Botella"), K("BotellaAmbar"));

        // Billar (occidente) y sofá (al sur, mirando al salón)
        BuildPoolTable(hall, new Vector3(-7f, 0f, 64.2f));
        CartoonKit.Sofa("Sofa_Salon", new Vector3(4.2f, 0f, 60.9f), 0f, hall);
        Cabinet("Gabinete_Salon", new Vector3(-8.55f, 0f, 61.6f), 1.4f, 0.5f, 1.9f, 90f, hall, 0.45f, 1.05f);

        CartoonKit.Rug("Alfombra_Salon", new Vector3(0f, 0.02f, 65.5f), new Vector2(7f, 7f), 0f, deco);
        CartoonKit.Rug("Alfombra_Escritorio", new Vector3(0f, 0.02f, 71f), new Vector2(7.5f, 4.6f), 0f, deco);

        // Puertas laterales de los refuerzos (N2.2) y la puerta cerrada del cuarto de atrás
        Quaternion toEast = Quaternion.LookRotation(Vector3.right), toWest = Quaternion.LookRotation(Vector3.left);
        CartoonKit.DoorLeaf("Puerta_Lateral_A", new Vector3(-8.8f, 1.15f, 70f), toEast, 1.5f, 2.3f, K("Puerta"), hall);
        CartoonKit.DoorLeaf("Puerta_Lateral_B", new Vector3(8.8f, 1.15f, 70f), toWest, 1.5f, 2.3f, K("Puerta"), hall);
        BuildBackDoor(hall, deco);

        // Bien iluminado: es la pelea final
        foreach (float x in new[] { -4.2f, 4.2f })
        foreach (float z in new[] { 63.8f, 70.2f })
            CartoonKit.HangingLamp($"Salon_Lampara_{x:0}_{z:0}", new Vector3(x, HallH, z), 0.8f, hall, lighting, WarmLamp, 3.2f, 13f);
    }

    private static void BuildPoolTable(Transform parent, Vector3 ground)
    {
        const float w = 1.5f, d = 2.8f, h = 0.9f;
        GameObject go = MeshGo("Mesa_Billar", "Camp_Billar", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, h - 0.14f, 0f), new Vector3(w, 0.28f, d), Quaternion.identity, 0.04f);
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                CartoonKit.AddBox(b, new Vector3(sx * (w * 0.5f - 0.14f), (h - 0.28f) * 0.5f, sz * (d * 0.5f - 0.14f)), new Vector3(0.18f, h - 0.28f, 0.18f), Quaternion.identity, 0.03f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, h + 0.005f, 0f), new Vector3(w - 0.2f, 0.02f, d - 0.2f), Quaternion.identity, 0.005f);
        }, parent, ground, 0f, K("MaderaOscura"), _felt);
        Hug(go, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
    }

    private static void BuildBackDoor(Transform hall, Transform deco)
    {
        CartoonKit.DoorLeaf("Door_CuartoAtras", new Vector3(0f, 1.15f, 73.8f), Quaternion.LookRotation(Vector3.back), 1.5f, 2.3f, K("Puerta"), hall);
        // Cadena y candado: se lee "cerrada". Debajo, la raya de luz del cuarto
        MeshGo("Candado_CuartoAtras", "Camp_Candado", b =>
        {
            b.Submesh = 0;
            CartoonKit.AddBox(b, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0.07f, 0.05f), Quaternion.Euler(0f, 0f, 7f), 0.015f);
            CartoonKit.AddBox(b, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0.07f, 0.05f), Quaternion.Euler(0f, 0f, -7f), 0.015f);
            b.Submesh = 1;
            CartoonKit.AddBox(b, new Vector3(0f, -0.04f, -0.04f), new Vector3(0.2f, 0.24f, 0.08f), Quaternion.identity, 0.02f);
        }, deco, new Vector3(0f, 1.2f, 73.72f), 0f, K("Metal"), K("Laton"));
        CartoonKit.Box("Luz_Bajo_Puerta", new Vector3(0f, 0.04f, 73.72f), new Vector3(1.3f, 0.03f, 0.04f), K("VidrioCalido"), deco, default, 0.005f, false);
    }
}
