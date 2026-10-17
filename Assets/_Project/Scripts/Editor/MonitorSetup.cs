using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/21 - Güvenlik monitörünü kur"  (önce menü 14)
    /// - RT_Monitor (1280 x 720, 16:9 — yatay monitörün ekranı) Render Texture
    /// - FeedWorld (x = 5000): ortografik izometrik FeedCamera -> RT_Monitor, SiteRoot, kendi ışığı,
    ///   kamera görüntüsü üstü yazılar (REC şeridi, KUSUR, saat, köşe çerçeveleri, tarama çizgileri, SİNYAL YOK)
    /// - Masadaki monitörün ekranı (SM_Monitor_Screen) bu dokuyu gösterir (Unlit, ışıktan etkilenmez)
    /// - Ekrana MeshCollider (adım 13'te işçi tıklama için), Desk/Monitor'e MonitorView
    /// - Ana kameranın uzak kesme mesafesi 1200 m (gök kubbesi 650 m); FeedWorld 5000 m uzakta, görünmez
    /// Tekrar çalıştırılabilir.
    public static class MonitorSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string Dir = "Assets/_Project/Monitor";
        const string RtPath = Dir + "/RT_Monitor.renderTexture";
        const int RtW = 1280, RtH = 720;

        static readonly Color Bg = new Color(0.20f, 0.24f, 0.27f);
        static readonly Color Amber = new Color(0.86f, 0.66f, 0.25f);
        static readonly Color OffWhite = new Color(0.86f, 0.88f, 0.90f);

        [MenuItem("Load Bearing/21 - Güvenlik monitörünü kur", priority = 21)]
        public static void Run()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro eksik", "Önce: Window > TextMeshPro > Import TMP Essential Resources", "Tamam");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project", "Monitor");

            // ---- Render Texture
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (rt == null)
            {
                rt = new RenderTexture(RtW, RtH, 24) { name = "RT_Monitor", antiAliasing = 2, filterMode = FilterMode.Bilinear };
                AssetDatabase.CreateAsset(rt, RtPath);
            }
            else if (rt.width != RtW || rt.height != RtH)
            {
                rt.Release();
                rt.width = RtW;
                rt.height = RtH;
                EditorUtility.SetDirty(rt);
            }

            // ---- Materyaller
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var mFeed = MakeMat("M_Feed", lit, m => { m.SetFloat("_Smoothness", 0.1f); });
            var mFeedUnlit = MakeMat("M_FeedUnlit", unlit, m => { });
            var mScreen = MakeMat("M_MonitorScreen_RT", unlit, m => { m.SetTexture("_BaseMap", rt); m.SetColor("_BaseColor", Color.white); });

            // ---- FeedWorld
            var old = GameObject.Find("FeedWorld");
            if (old != null) Object.DestroyImmediate(old);
            var world = new GameObject("FeedWorld");
            world.transform.position = new Vector3(5000f, 0f, 0f); // gök kubbesinin (650 m) ve kameranın (1200 m) çok dışında

            var siteRoot = new GameObject("SiteRoot").transform;
            siteRoot.SetParent(world.transform, false);

            var camGo = new GameObject("FeedCamera");
            camGo.transform.SetParent(world.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            cam.targetTexture = rt;
            cam.allowHDR = false;
            cam.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            cam.transform.localPosition = -(cam.transform.rotation * Vector3.forward) * 30f;

            // Sadece maketi aydınlatan ışık (ofisten 1 km uzakta)
            var lightGo = new GameObject("FeedLight");
            lightGo.transform.SetParent(world.transform, false);
            lightGo.transform.localPosition = new Vector3(-3f, 7f, -4f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 30f;
            l.intensity = 6f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.None;

            var feed = world.AddComponent<MonitorFeed>();
            feed.feedCamera = cam;
            feed.siteRoot = siteRoot;
            feed.litMaterial = mFeed;
            feed.unlitMaterial = mFeedUnlit;

            BuildOverlay(world.transform, cam, feed);

            // ---- Masadaki monitör
            var monitor = GameObject.Find("Desk/Monitor");
            Renderer screen = null;
            if (monitor != null)
            {
                var t = FindDeep(monitor.transform, "SM_Monitor_Screen");
                screen = t != null ? t.GetComponent<Renderer>() : null;
                if (screen == null)
                {
                    // Tasarım modeli yoksa greybox ekran
                    var g = monitor.transform.Find("Screen");
                    screen = g != null ? g.GetComponent<Renderer>() : null;
                    if (screen != null) screen.enabled = true;
                }
                if (screen != null)
                {
                    var mats = screen.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mScreen;
                    screen.sharedMaterials = mats;
                    if (screen.GetComponent<Collider>() == null) screen.gameObject.AddComponent<MeshCollider>();
                }
                // Greybox kutuların çarpışma kutuları tasarım modelinin önünü kapatmasın (düğmelere tıklanabilsin)
                foreach (var n in new[] { "Body", "Stand", "Screen" })
                {
                    var g = monitor.transform.Find(n);
                    if (g == null || (screen != null && g == screen.transform)) continue;
                    foreach (var c in g.GetComponents<Collider>()) c.enabled = false;
                }
                // Kasa tıklanabilsin (masadan monitöre bakmak için)
                var body = FindDeep(monitor.transform, "SM_Monitor");
                if (body != null && body.GetComponent<MeshFilter>() != null && body.GetComponent<Collider>() == null)
                    body.gameObject.AddComponent<MeshCollider>();

                var view = monitor.GetComponent<MonitorView>();
                if (view == null) view = monitor.AddComponent<MonitorView>();
                view.screenRenderer = screen;
                view.fitMargin = 1.12f; // yakın bakışta ön yüz kadrajı doldursun
                view.ledOff = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_LedOff.mat");
                view.ledGreen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_LedGreen.mat");
                view.ledYellow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_LedYellow.mat");
                view.ledGrey = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_LedGrey.mat");
                EditorUtility.SetDirty(view);
            }

            // ---- Ana kamera FeedWorld'ü görmesin
            var main = GameObject.FindWithTag("MainCamera");
            if (main != null) { var mc = main.GetComponent<Camera>(); if (mc != null) mc.farClipPlane = 1200f; }

            EditorUtility.SetDirty(feed);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[Load Bearing]</color> Güvenlik monitörü kuruldu. Ekran: {(screen != null ? screen.name : "BULUNAMADI")}");
        }

        // ------------------------------------------------------------------ kamera görüntüsü üstü yazılar

        static void BuildOverlay(Transform world, Camera cam, MonitorFeed feed)
        {
            var go = new GameObject("FeedOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(world, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RtW, RtH);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // Tarama çizgileri
            var lines = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            var lrt = (RectTransform)lines.transform;
            lrt.SetParent(root, false);
            Stretch(lrt);
            var raw = lines.GetComponent<RawImage>();
            raw.texture = ScanlineTexture();
            raw.uvRect = new Rect(0, 0, 1, RtH / 4f);
            raw.raycastTarget = false;

            // Üst şerit
            var strip = Img("Strip", root, new Color(0.08f, 0.09f, 0.10f, 0.85f));
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1); strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(0, 52); strip.anchoredPosition = Vector2.zero;

            var dot = Img("RecDot", strip, new Color(0.85f, 0.20f, 0.18f));
            dot.anchorMin = dot.anchorMax = new Vector2(0, 0.5f);
            dot.sizeDelta = new Vector2(16, 16); dot.anchoredPosition = new Vector2(26, 0);
            var dotImg = dot.GetComponent<Image>();
            dotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            feed.recDot = dotImg;

            feed.stripText = Text("StripText", strip, "REC   KANAL 1", 26, OffWhite, TextAlignmentOptions.MidlineLeft);
            var st = feed.stripText.rectTransform;
            st.anchorMin = new Vector2(0, 0); st.anchorMax = new Vector2(1, 1); st.offsetMin = new Vector2(44, 0); st.offsetMax = new Vector2(-200, 0);

            feed.defectText = Text("DefectText", strip, "KUSUR: 0", 26, Amber, TextAlignmentOptions.MidlineRight);
            var dt = feed.defectText.rectTransform;
            dt.anchorMin = new Vector2(1, 0); dt.anchorMax = new Vector2(1, 1); dt.pivot = new Vector2(1, 0.5f);
            dt.sizeDelta = new Vector2(200, 0); dt.anchoredPosition = new Vector2(-24, 0);

            // Saat
            feed.timeText = Text("TimeText", root, "16.10.26  09:00:00", 24, OffWhite, TextAlignmentOptions.BottomRight);
            var tt = feed.timeText.rectTransform;
            tt.anchorMin = tt.anchorMax = new Vector2(1, 0); tt.pivot = new Vector2(1, 0);
            tt.sizeDelta = new Vector2(360, 40); tt.anchoredPosition = new Vector2(-86, 40);

            // Köşe çerçeveleri
            Corner(root, new Vector2(0, 1), new Vector2(20, -92));
            Corner(root, new Vector2(1, 1), new Vector2(-20, -92));
            Corner(root, new Vector2(0, 0), new Vector2(20, 22));
            Corner(root, new Vector2(1, 0), new Vector2(-20, 22));

            // SİNYAL YOK
            var ns = new GameObject("NoSignal", typeof(RectTransform));
            var nrt = (RectTransform)ns.transform;
            nrt.SetParent(root, false);
            Stretch(nrt);
            nrt.SetSiblingIndex(0); // yazıların arkasında
            var noise = new GameObject("Noise", typeof(RectTransform), typeof(RawImage));
            var nzr = (RectTransform)noise.transform;
            nzr.SetParent(nrt, false);
            Stretch(nzr);
            feed.noiseImage = noise.GetComponent<RawImage>();
            feed.noiseImage.raycastTarget = false;
            feed.noiseImage.color = new Color(1, 1, 1, 0.55f);
            feed.noSignalText = Text("Label", nrt, "SİNYAL YOK", 72, OffWhite, TextAlignmentOptions.Center);
            Stretch(feed.noSignalText.rectTransform);
            feed.noSignalText.fontStyle = FontStyles.Bold;
            feed.noSignalText.characterSpacing = 8f;
            feed.noSignalRoot = ns;
            lines.transform.SetAsLastSibling(); // tarama çizgileri en üstte
        }

        static void Corner(RectTransform root, Vector2 anchor, Vector2 pos)
        {
            const float len = 62f, th = 4f;
            var c = new GameObject("Corner", typeof(RectTransform)).transform as RectTransform;
            c.SetParent(root, false);
            c.anchorMin = c.anchorMax = anchor;
            c.pivot = anchor;
            c.sizeDelta = new Vector2(len, len);
            c.anchoredPosition = pos;
            var h = Img("H", c, OffWhite);
            h.anchorMin = h.anchorMax = new Vector2(anchor.x, anchor.y); h.pivot = anchor;
            h.sizeDelta = new Vector2(len, th); h.anchoredPosition = Vector2.zero;
            var v = Img("V", c, OffWhite);
            v.anchorMin = v.anchorMax = new Vector2(anchor.x, anchor.y); v.pivot = anchor;
            v.sizeDelta = new Vector2(th, len); v.anchoredPosition = Vector2.zero;
        }

        static Texture2D ScanlineTexture()
        {
            const string path = Dir + "/T_Scanlines.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "T_Scanlines" };
            tex.SetPixels(new[] { new Color(0, 0, 0, 0.22f), new Color(0, 0, 0, 0.05f), new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.05f) });
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static Material MakeMat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = $"{Dir}/{name}.mat";
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

        static RectTransform Img(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Text(string name, Transform parent, string s, float size, Color c, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = s;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
