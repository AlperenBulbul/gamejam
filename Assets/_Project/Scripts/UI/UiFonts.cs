using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoadBearing
{
    /// Ek yazı tipleri (Resources/Fonts). Oyun sırasında TextMesh Pro font asset'ine çevrilir, sahnede kurulum gerekmez.
    ///  Inter (tasarımcının arayüz ve son sinematiği yazı tipi): ExtraBold, SemiBold, Regular. Türkçe harfler dahil.
    public static class UiFonts
    {
        static readonly Dictionary<string, TMP_FontAsset> cache = new Dictionary<string, TMP_FontAsset>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static TMP_FontAsset Heavy => Get("Inter-ExtraBold");
        public static TMP_FontAsset SemiBold => Get("Inter-SemiBold");
        public static TMP_FontAsset Regular => Get("Inter-Regular");

        static TMP_FontAsset Get(string file)
        {
            if (cache.TryGetValue(file, out var fa)) return fa;
            fa = null;
            var f = Resources.Load<Font>("Fonts/" + file);
            if (f != null)
            {
                try
                {
                    fa = TMP_FontAsset.CreateFontAsset(f);
                    if (fa != null)
                    {
                        fa.name = file + " (runtime)";
                        var def = TMP_Settings.defaultFontAsset; // olmayan karakterler için yedek
                        if (def != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { def };
                    }
                }
                catch (System.Exception e) { Debug.LogWarning($"[UiFonts] {file} yüklenemedi: {e.Message}"); fa = null; }
            }
            cache[file] = fa;
            return fa;
        }
    }
}
