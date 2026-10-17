using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/13 - Tasarım asset'lerini içe aktar"
    /// Tasarım ekibinin teslim notuna (Assets/Art/3D/README_Teslim.md) göre:
    ///  - Doku ayarları (T_Palette: Point / mipmap yok, harita 4800x2640 tam çözünürlük, asetat şeffaf, mühür izleri sprite)
    ///  - URP materyalleri (M_Palette, M_Glass, M_Led..., M_Map_Karaova ...) -> Assets/Art/Materials
    ///  - Tüm FBX'ler: Bake Axis Conversion, ölçek 1, gömülü materyaller yukarıdakilere bağlanır
    ///  - Hasan Amca: Humanoid, animasyonlar onun avatarını kullanır ve döngüde çalar
    /// Tekrar çalıştırılabilir (materyal ayarlarını nottaki değerlere geri döndürür).
    public static class ArtImportSetup
    {
        const string Art = "Assets/Art";
        const string MatDir = "Assets/Art/Materials";
        const string PaletteTex = "Assets/Art/3D/Textures/T_Palette.png";
        const string MapBase = "Assets/Art/UI/Desk/T_Map_Karaova_Base.png";
        const string MapOverlay = "Assets/Art/UI/Desk/T_Map_Overlay_Asetat.png";
        const string HasanDir = "Assets/Art/Characters/HasanAmca";

        [MenuItem("Load Bearing/13 - Tasarım asset'lerini içe aktar", priority = 13)]
        public static void Run()
        {
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(Art, "Materials");

            int tex = SetupTextures();
            var mats = CreateMaterials();
            int models = SetupModels(mats);
            string ch = SetupCharacter();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=lime>[Load Bearing]</color> Tasarım asset'leri hazır: {tex} doku, {mats.Count} materyal, {models} model. {ch}");
        }

        // ------------------------------------------------------------------ dokular

        static int SetupTextures()
        {
            int n = 0;
            if (Tex(PaletteTex, ti =>
            {
                ti.textureType = TextureImporterType.Default;
                ti.filterMode = FilterMode.Point;      // palet: renkler karışmasın
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
            })) n++;

            foreach (var path in new[] { MapBase, MapOverlay })
                if (Tex(path, ti =>
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.npotScale = TextureImporterNPOTScale.None; // 4800x2640 olduğu gibi (koordinatlar piksel bazlı)
                    ti.maxTextureSize = 8192;
                    ti.mipmapEnabled = true;
                    ti.anisoLevel = 8;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    ti.alphaIsTransparency = path == MapOverlay;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                })) n++;

            foreach (var path in new[] { "Assets/Art/UI/Desk/T_StampMark_Approve.png", "Assets/Art/UI/Desk/T_StampMark_Reject.png" })
                if (Tex(path, ti =>
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = true;
                    ti.wrapMode = TextureWrapMode.Clamp;
                })) n++;

            foreach (var path in new[] { "Assets/Art/UI/Desk/T_Form_Sample_C1.png", "Assets/Art/UI/Desk/T_NewspaperFront_Sample.png",
                                         "Assets/Art/UI/Desk/T_RulePage_03_Sample.png", "Assets/Art/UI/Monitor/UI_Monitor_Sample.png" })
                if (Tex(path, ti => { ti.npotScale = TextureImporterNPOTScale.None; ti.wrapMode = TextureWrapMode.Clamp; })) n++;

            // ---- Teslim 2 (revize)
            foreach (var path in new[] { NewspaperDay1, RuleBlank, PosterTex })
                if (Exists(path) && Tex(path, ti => { ti.npotScale = TextureImporterNPOTScale.None; ti.maxTextureSize = 2048; ti.wrapMode = TextureWrapMode.Clamp; })) n++;
            foreach (var path in new[] { SkyDay1, SkyDay2, SkyRain })
                if (Exists(path) && Tex(path, ti => { ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed; })) n++;
            foreach (var path in new[] { RainDrops, RainStreaks, RainSheet })
                if (Exists(path) && Tex(path, ti => { ti.wrapMode = TextureWrapMode.Repeat; ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; })) n++;
            return n;
        }

        const string NewspaperDay1 = "Assets/Art/UI/Desk/T_NewspaperFront_Day1_GameJam.png";
        const string RuleBlank = "Assets/Art/UI/Desk/T_RulePage_Blank.png";
        const string SkyDay1 = "Assets/Art/3D/Textures/T_Sky_Day1.png";
        const string SkyDay2 = "Assets/Art/3D/Textures/T_Sky_Day2.png";
        const string SkyRain = "Assets/Art/3D/Textures/T_Sky_Rain.png";
        const string RainDrops = "Assets/Art/3D/Textures/T_RainGlass_Drops.png";
        const string RainStreaks = "Assets/Art/3D/Textures/T_RainGlass_Streaks.png";
        const string RainSheet = "Assets/Art/3D/Textures/T_RainSheet.png";
        const string PosterTex = "Assets/Art/3D/Textures/T_Poster_Deprem.png";

        static bool Exists(string path) => AssetImporter.GetAtPath(path) != null;
        static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        static bool Tex(string path, System.Action<TextureImporter> setup)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning($"Doku bulunamadı: {path}"); return false; }
            setup(ti);
            ti.SaveAndReimport();
            return true;
        }

        // ------------------------------------------------------------------ materyaller

        static Dictionary<string, Material> CreateMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTex);
            var mapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapBase);
            var overlayTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MapOverlay);
            var monitorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Monitor/UI_Monitor_Sample.png");
            var paperTex = Load(NewspaperDay1) ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Desk/T_NewspaperFront_Sample.png"); // 1. gün açılış gazetesi (teslim 2)
            var formTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Desk/T_Form_Sample_C1.png");
            var ruleTex = Load(RuleBlank) ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/Desk/T_RulePage_03_Sample.png"); // boş sayfa (teslim 2)

            var d = new Dictionary<string, Material>();
            d["M_Palette"] = Mat("M_Palette", lit, m => { Base(m, palette, Color.white); m.SetFloat("_Smoothness", 0.25f); });
            d["M_Palette_Metal"] = Mat("M_Palette_Metal", lit, m => { Base(m, palette, Color.white); m.SetFloat("_Metallic", 0.45f); m.SetFloat("_Smoothness", 0.6f); });
            d["M_Map_Karaova"] = Mat("M_Map_Karaova", lit, m => { Base(m, mapTex, Color.white); m.SetFloat("_Smoothness", 0.15f); });
            d["M_MapOverlay"] = Mat("M_MapOverlay", lit, m => { Base(m, overlayTex, Color.white); m.SetFloat("_Smoothness", 0.55f); Transparent(m); });
            d["M_Glass"] = Mat("M_Glass", lit, m => { Base(m, null, new Color(0.85f, 0.92f, 0.95f, 0.08f)); m.SetFloat("_Smoothness", 0.95f); Transparent(m); });
            d["M_GlassStreak"] = Mat("M_GlassStreak", unlit, m => { Base(m, null, new Color(1f, 1f, 1f, 0.07f)); Transparent(m); });
            d["M_MonitorScreen"] = Mat("M_MonitorScreen", lit, m => { Base(m, null, Color.black); Emission(m, Color.white, monitorTex); });
            d["M_NewspaperFront"] = Mat("M_NewspaperFront", lit, m => { Base(m, paperTex, Color.white); m.SetFloat("_Smoothness", 0.1f); });
            d["M_FormPage_Front"] = Mat("M_FormPage_Front", lit, m => { Base(m, formTex, Color.white); m.SetFloat("_Smoothness", 0.1f); });
            d["M_RulePage_Front"] = Mat("M_RulePage_Front", lit, m => { Base(m, ruleTex, Color.white); m.SetFloat("_Smoothness", 0.1f); });
            d["M_LedOff"] = Mat("M_LedOff", lit, m => { Base(m, null, new Color(0.12f, 0.12f, 0.12f)); NoEmission(m); });
            d["M_LedGreen"] = Mat("M_LedGreen", lit, m => { Base(m, null, new Color(0.2f, 0.8f, 0.3f)); Emission(m, new Color(0.2f, 1f, 0.35f) * 2f, null); });
            d["M_LedYellow"] = Mat("M_LedYellow", lit, m => { Base(m, null, new Color(0.95f, 0.75f, 0.15f)); Emission(m, new Color(1f, 0.8f, 0.15f) * 2f, null); });
            d["M_LedGrey"] = Mat("M_LedGrey", lit, m => { Base(m, null, new Color(0.45f, 0.45f, 0.45f)); NoEmission(m); });
            d["M_LedRed"] = Mat("M_LedRed", lit, m => { Base(m, null, new Color(0.85f, 0.15f, 0.12f)); Emission(m, new Color(1f, 0.15f, 0.1f) * 2f, null); });
            d["M_LampEmit"] = Mat("M_LampEmit", lit, m => { Base(m, null, new Color(1f, 0.96f, 0.88f)); Emission(m, new Color(1f, 0.93f, 0.80f) * 2.2f, null); });
            var skyTex = Load(SkyDay1);
            d["M_Sky"] = Mat("M_Sky", unlit, m => Base(m, skyTex, skyTex != null ? Color.white : new Color(0.62f, 0.78f, 0.92f)));

            // ---- Teslim 2: sıra ekranı ve yağmur
            d["M_QueueLED"] = Mat("M_QueueLED", lit, m => { Base(m, null, new Color(1f, 0.12f, 0.06f)); Emission(m, new Color(1f, 0.12f, 0.06f) * 5f, null); });
            d["M_RainGlass_Drops"] = Mat("M_RainGlass_Drops", lit, m => { Base(m, Load(RainDrops), new Color(1f, 1f, 1f, 0.6f)); m.SetFloat("_Smoothness", 0.95f); Transparent(m); });
            d["M_RainGlass_Streaks"] = Mat("M_RainGlass_Streaks", lit, m => { Base(m, Load(RainStreaks), new Color(1f, 1f, 1f, 0.55f)); m.SetFloat("_Smoothness", 0.95f); Transparent(m); });
            d["M_Poster_Deprem"] = Mat("M_Poster_Deprem", lit, m => { Base(m, Load(PosterTex), Color.white); m.SetFloat("_Smoothness", 0.25f); });
            d["M_RainSheet"] = Mat("M_RainSheet", unlit, m => { Base(m, Load(RainSheet), new Color(1f, 1f, 1f, 0.6f)); Transparent(m); });
            return d;
        }

        static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader) m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Base(Material m, Texture tex, Color c)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        static void Emission(Material m, Color c, Texture tex)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static void NoEmission(Material m)
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        // ------------------------------------------------------------------ modeller

        static int SetupModels(Dictionary<string, Material> mats)
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { Art + "/3D" });
            int n = 0;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;

                mi.bakeAxisConversion = true;   // not: "Bake Axis Conversion açık"
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.importCameras = path.Contains("LB_FocusCameras");
                mi.importLights = false;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.addCollider = false;         // çarpışma kutularını oyun kodu ekler
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                // FBX içindeki materyal adlarını bizim URP materyallerimize bağla
                foreach (var kv in mats)
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);

                mi.SaveAndReimport();
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ karakter

        static string SetupCharacter()
        {
            string modelPath = $"{HasanDir}/HasanAmca.fbx";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter mi)) return "Hasan Amca bulunamadı.";

            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.addCollider = false;
            mi.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) return "UYARI: Hasan Amca için Humanoid avatar oluşmadı (rig'i kontrol et).";

            int clips = 0;
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { HasanDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.Contains("@") || !(AssetImporter.GetAtPath(p) is ModelImporter ai)) continue;
                ai.bakeAxisConversion = true;
                ai.animationType = ModelImporterAnimationType.Human;
                ai.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                ai.sourceAvatar = avatar;
                ai.importAnimation = true;
                ai.importCameras = false;
                ai.importLights = false;

                string clipName = System.IO.Path.GetFileNameWithoutExtension(p).Split('@')[1].Replace(" ", "");
                var defs = ai.defaultClipAnimations;
                foreach (var c in defs)
                {
                    c.name = clipName;              // "Talking", "AngryPoint"
                    c.loopTime = true;
                    c.lockRootPositionXZ = true;    // olduğu yerde oynasın
                    c.keepOriginalPositionXZ = true;
                    c.lockRootHeightY = true;
                    c.keepOriginalPositionY = true;
                    c.lockRootRotation = true;
                    c.keepOriginalOrientation = true;
                }
                ai.clipAnimations = defs;
                ai.SaveAndReimport();
                clips++;
            }
            return $"Hasan Amca: Humanoid avatar + {clips} animasyon.";
        }
    }
}
