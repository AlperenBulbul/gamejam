using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LoadBearing.EditorTools
{
    /// Türkçe harfler (ş ğ ı İ Ş Ğ) ana fontta (LiberationSans SDF) yok; TextMesh Pro bunları oyun sırasında
    /// "dinamik" yedek fonttan üretiyor. Dinamik yedek bazen sıfırlanıyor ve harfler kayboluyor.
    /// Bu menü Türkçe harflerin önceden basıldığı SABİT bir yedek font oluşturur (Assets/_Project/Fonts)
    /// ve ana fontun yedek listesinin başına ekler. Bir kez çalışması yeter; dosya git ile herkese gider.
    /// Font dosyası yoksa proje açılınca kendiliğinden de çalışır.
    [InitializeOnLoad]
    public static class TurkishFontSetup
    {
        const string SourceFont = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        const string MainAsset = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string OutDir = "Assets/_Project/Fonts";
        const string OutAsset = OutDir + "/LiberationSans SDF - Turkce.asset";

        // Türkçe + sık kullanılan işaretler (ASCII ana fontta var)
        const string Chars =
            "çğıöşüÇĞİÖŞÜâîûÂÎÛ" +
            "–—…‘’“”•·°×₺€%" +
            "✓✔✗→←↑↓◀▶";

        static TurkishFontSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutAsset) == null) Run();
                else EnsureFallback(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutAsset));
            };
        }

        [MenuItem("Load Bearing/23 - Türkçe karakter fontunu kur", priority = 23)]
        public static void Run()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFont);
            if (font == null) { Debug.LogWarning("[Load Bearing] Türkçe font: " + SourceFont + " bulunamadı (git lfs pull?)"); return; }

            var old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutAsset);
            if (old != null) AssetDatabase.DeleteAsset(OutAsset);
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Project", "Fonts");

            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            if (fa == null) { Debug.LogWarning("[Load Bearing] Türkçe font oluşturulamadı"); return; }
            fa.name = "LiberationSans SDF - Turkce";
            fa.TryAddCharacters(Chars, out string missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static; // harfler sabit: bir daha silinmez

            AssetDatabase.CreateAsset(fa, OutAsset);
            foreach (var tex in fa.atlasTextures.Where(t => t != null))
            {
                tex.name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(OutAsset);

            fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutAsset);
            EnsureFallback(fa);
            Debug.Log($"[Load Bearing] Türkçe font kuruldu: {OutAsset}" + (string.IsNullOrEmpty(missing) ? "" : $" (fontta olmayan: {missing})"));
        }

        /// Sabit Türkçe fontu ana fontun yedek listesinin başına koyar (dinamik yedekten önce bakılır)
        static void EnsureFallback(TMP_FontAsset turkish)
        {
            var main = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainAsset);
            if (main == null || turkish == null) return;
            var list = main.fallbackFontAssetTable ?? new System.Collections.Generic.List<TMP_FontAsset>();
            if (list.Count > 0 && list[0] == turkish) return;
            list.Remove(turkish);
            list.Insert(0, turkish);
            main.fallbackFontAssetTable = list;
            EditorUtility.SetDirty(main);
            AssetDatabase.SaveAssets();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, main);
        }
    }
}
