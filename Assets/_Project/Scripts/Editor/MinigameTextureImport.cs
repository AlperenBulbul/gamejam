using UnityEditor;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// Assets/_Project/Resources/Minigame altındaki minigame görselleri için içe aktarma ayarı (kendiliğinden):
    /// boyut küçültme yok (1280 × 720 olduğu gibi), mipmap yok, sıkıştırma yok, bant dokusu tekrarlı.
    /// Ayarlar yanlış kalmışsa proje açılınca klasör bir kez yeniden içe aktarılır.
    [InitializeOnLoad]
    public class MinigameTextureImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/Minigame";
        const string MonitorFbx = "Assets/Art/3D/Props/SM_Monitor.fbx";

        static MinigameTextureImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Monitör modeli okunabilir olsun: minigame tıklaması ekranın dokusundaki noktayı okur (oyun çıktısında da)
                if (AssetImporter.GetAtPath(MonitorFbx) is ModelImporter mi && !mi.isReadable)
                {
                    mi.isReadable = true;
                    mi.SaveAndReimport();
                }
                var probe = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/UI_MG_Mixer_BG.png");
                if (probe != null && probe.width != 1280) // küçültülmüş: eski ayarla içe aktarılmış
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                }
            };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = assetPath.Contains("Belt") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
        }
    }
}
