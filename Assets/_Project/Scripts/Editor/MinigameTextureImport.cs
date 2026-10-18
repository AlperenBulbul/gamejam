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
                // Dokular bu betikten önce içe aktarıldıysa (küçültülmüş) yeniden içe aktar
                foreach (var f in UiFolders)
                {
                    string dir = Res + f;
                    if (!AssetDatabase.IsValidFolder(dir)) continue;
                    foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        int w = PngWidth(path);
                        if (tex != null && w > 0 && tex.width != w) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
                // İşçi animasyonları döngüde değilse (betikten önce içe aktarıldı) yeniden içe aktar
                if (AssetDatabase.IsValidFolder(Res + "Workers"))
                    foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Res + "Workers" }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                            if (o is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.StartsWith("Work") && !c.isLooping)
                            { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); break; }
                    }
            };
        }

        const string Res = "Assets/_Project/Resources/";

        /// PNG dosyasının gerçek genişliği (başlıktan)
        static int PngWidth(string assetPath)
        {
            try
            {
                using (var fs = System.IO.File.OpenRead(assetPath))
                {
                    var b = new byte[24];
                    if (fs.Read(b, 0, 24) < 24 || b[1] != 'P' || b[2] != 'N' || b[3] != 'G') return 0;
                    return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                }
            }
            catch { return 0; }
        }
        static readonly string[] UiFolders = { "Minigame", "RuleBook", "FormButtons", "Tutorial", "HowTo", "Pause" };

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            var ti = (TextureImporter)assetImporter;

            // Şantiye işçilerinin palet dokusu: piksel keskin (Point), mipmap yok
            if (p.StartsWith(Res + "Workers/"))
            {
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            // Gazete ön sayfaları ve son görselleri: oranı korunur (2'nin katına büyütülmez), masadaki gazetede mipmap açık
            if (p.StartsWith(Res + "Newspaper/") || p.StartsWith(Res + "Endings/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.mipmapEnabled = p.Contains("/Newspaper/");
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.sRGBTexture = true;
                return;
            }

            bool ui = false;
            foreach (var f in UiFolders) if (p.StartsWith(Res + f + "/")) { ui = true; break; }
            if (!ui) return;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = p.Contains("Belt") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.sRGBTexture = true;
        }

        // Şantiye işçileri (tasarımcı notu): Generic rig, kendi avatarı, animasyonlar döngüde (React_Hey hariç)
        void OnPreprocessModel()
        {
            string wp = assetPath.Replace('\\', '/');
            if (!wp.StartsWith(Res + "Workers/")) return;
            var mi = (ModelImporter)assetImporter;
            if (System.IO.Path.GetFileName(wp).StartsWith("SM_"))
            {
                // Gazoz şişesi / pet bardak: düz model, iskelet ve animasyon yok, materyali kod verir
                mi.globalScale = 1f;
                mi.bakeAxisConversion = true;
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                return;
            }
            mi.globalScale = 1f;
            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // materyali kod verir (palet dokusu)
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Res + "Workers/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                int bar = c.name.LastIndexOf('|');
                if (bar >= 0) c.name = c.name.Substring(bar + 1);
                c.loopTime = !c.name.Contains("React");
            }
            mi.clipAnimations = clips;
        }
    }
}
