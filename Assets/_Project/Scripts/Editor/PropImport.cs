using UnityEditor;

namespace LoadBearing.EditorTools
{
    /// Resources/Props altındaki masa süsleri (ODTÜ kupası): düz model, materyali kod verir.
    /// Normaller yeniden hesaplanır (45°): kupanın tabanıyla yan yüzü arasındaki köşe sert kalır,
    /// yoksa yan yüzün altı aşağı bakan normal yüzünden karanlık görünüyordu.
    public class PropImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/Props/";

        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.normalCalculationMode = ModelImporterNormalCalculationMode.AngleWeighted;
            mi.normalSmoothingAngle = 45f;
        }
    }
}
