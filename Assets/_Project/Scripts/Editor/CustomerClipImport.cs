using UnityEditor;

namespace LoadBearing.EditorTools
{
    /// Resources/Characters altındaki müşteri çıkış klipleri (ör. A_Customer_Selin_WalkOut / WalkOutAngry):
    /// Humanoid (diğer Selin klipleri gibi kendi iskeletinden avatar), döngü yok.
    /// Dönüş klibin içinde: Root Rotation pose'a gömülmez (Body Orientation), Y ve XZ gömülü (yerinde yürür).
    /// Kod klibi oynatırken Apply Root Motion açar; karakteri kapıya doğru ilerletmeyi kendisi yapar.
    public class CustomerClipImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/Characters/";

        static bool Ours(string path) => path.Replace('\\', '/').StartsWith(Folder);

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            foreach (var c in clips)
            {
                // "A_Customer_Selin_WalkOutAngry" -> "WalkOutAngry"
                int us = file.LastIndexOf('_');
                c.name = us >= 0 ? file.Substring(us + 1) : file;
                c.loopTime = false;
                c.lockRootRotation = false;          // dönüş kök hareketine geçsin
                c.keepOriginalOrientation = false;   // Based Upon: Body Orientation
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;         // yerinde yürür, ilerlemeyi kod verir
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
