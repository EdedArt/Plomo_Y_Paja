using UnityEditor;

/// <summary>
/// Normaliza los modelos que el equipo suelta en Assets/_Game/Art/Environment/Models/ (paquetes CC0 o generados):
/// unidades del archivo, conversión de ejes horneada, sin cámaras/luces/animación, normales importadas.
/// El tamaño final lo ajusta CartoonKit.TryModel a la altura de referencia de cada pieza del kit.
/// </summary>
public class EnvironmentModelImporter : AssetPostprocessor
{
    private void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').StartsWith(CartoonKit.ModelFolder + "/")) return;

        var importer = (ModelImporter)assetImporter;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.isReadable = false;
    }
}
