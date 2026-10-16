using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Güvenlik kamerası görüntüsü (Geliştirme dokümanı 6.6 FeedRenderer).
    /// FeedWorld (x = 1000) altında seçili şantiyenin küçük izometrik maketini kurar; FeedCamera bunu
    /// RT_Monitor'e çizer, masadaki monitörün ekranı da bu dokuyu gösterir. Monitör hep açıktır.
    /// - Biten katlar dolu blok, yapılmakta olan kat: iskele + demir ızgara + yükselen kolonlar, üstünde işçiler
    /// - Üst şerit: "REC  KANAL n — ŞANTİYE X  KAT k / t", sağda KUSUR, altta tarih / saat
    /// - Şantiye yoksa: SİNYAL YOK + karıncalanma
    public class MonitorFeed : MonoBehaviour
    {
        public static MonitorFeed Instance { get; private set; }

        [Header("Sahne")]
        public Camera feedCamera;
        public Transform siteRoot;
        public Material litMaterial;
        public Material unlitMaterial;

        [Header("Ekran yazıları")]
        public TMP_Text stripText;
        public TMP_Text defectText;
        public TMP_Text timeText;
        public Image recDot;
        public GameObject noSignalRoot;
        public TMP_Text noSignalText;
        public RawImage noiseImage;

        [Header("Ölçüler (maket birimi)")]
        public float footprint = 1.6f;
        public float slabHeight = 0.12f;
        public float floorHeight = 0.42f;

        public IReadOnlyList<WorkerView> Workers => workers;
        public ConstructionSite Shown => shown;

        static readonly Color Dirt = new Color(0.40f, 0.32f, 0.24f);
        static readonly Color Concrete = new Color(0.80f, 0.81f, 0.82f);
        static readonly Color ConcreteDark = new Color(0.62f, 0.63f, 0.65f);
        static readonly Color Window = new Color(0.30f, 0.36f, 0.42f);
        static readonly Color Scaffold = new Color(0.95f, 0.55f, 0.10f);
        static readonly Color Rebar = new Color(0.42f, 0.27f, 0.16f);
        static readonly Color Mixer = new Color(0.93f, 0.55f, 0.12f);
        static readonly Color Sack = new Color(0.70f, 0.70f, 0.70f);

        readonly List<WorkerView> workers = new List<WorkerView>();
        readonly List<Transform> columns = new List<Transform>();
        ConstructionSite shown;
        int shownIndex = -2, shownStep = -1, shownWorkers = -1;
        SiteState shownState;
        Texture2D noiseTex;
        Color32[] noisePixels;
        float nextNoise, camSize = 2f;
        Vector3 camTarget;
        float workStartTime;

        void Awake() => Instance = this;

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnStateChanged(GameState s)
        {
            if (s == GameState.Working) workStartTime = Time.time;
            if (s == GameState.MainMenu) { shownIndex = -2; shown = null; }
        }

        void Start()
        {
            noiseTex = new Texture2D(160, 120, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            noisePixels = new Color32[noiseTex.width * noiseTex.height];
            if (noiseImage != null) noiseImage.texture = noiseTex;
            Rebuild();
        }

        void LateUpdate()
        {
            var cm = ConstructionManager.Instance;
            var site = cm != null ? cm.SelectedSite : null;
            int sel = cm != null ? cm.Selected : -1;
            if (sel != shownIndex || site != shown || (site != null && (site.step != shownStep || site.state != shownState || site.workers != shownWorkers)))
                Rebuild();

            if (site != null)
            {
                // Hatalı işçiler
                foreach (var w in workers) if (w != null) w.SetFault(site.FaultTypeOf(w.index));
                // Kolonlar adım ilerledikçe yükselir
                float k = site.state == SiteState.Building && site.duration > 0f ? Mathf.Clamp01(site.timer / site.duration) : 1f;
                foreach (var c in columns)
                    if (c != null) { var s = c.localScale; s.y = Mathf.Max(0.02f, floorHeight * k); c.localScale = s; c.localPosition = new Vector3(c.localPosition.x, columnBaseY + s.y * 0.5f, c.localPosition.z); }
            }

            UpdateOverlay(site, sel);
            UpdateCamera();
            if (site == null) UpdateNoise();
        }

        float columnBaseY;

        // ------------------------------------------------------------------ maket

        void Rebuild()
        {
            var cm = ConstructionManager.Instance;
            shown = cm != null ? cm.SelectedSite : null;
            shownIndex = cm != null ? cm.Selected : -1;
            shownStep = shown != null ? shown.step : -1;
            shownState = shown != null ? shown.state : SiteState.Building;
            shownWorkers = shown != null ? shown.workers : -1;

            for (int i = siteRoot.childCount - 1; i >= 0; i--) Destroy(siteRoot.GetChild(i).gameObject);
            workers.Clear();
            columns.Clear();

            if (noSignalRoot != null) noSignalRoot.SetActive(shown == null);
            if (shown == null) { camTarget = Vector3.zero; camSize = 2f; return; }

            var s = shown;
            float half = footprint * 0.5f;

            // Arsa ve çevresi
            Box("Ground", siteRoot, new Vector3(0f, -0.025f, 0f), new Vector3(3.4f, 0.05f, 3.4f), Dirt);
            Mixer3D(new Vector3(1.25f, 0f, 0.95f));
            for (int i = 0; i < 3; i++)
                Box($"Sack{i}", siteRoot, new Vector3(1.05f + i * 0.13f, 0.03f, -1.15f + (i % 2) * 0.04f), new Vector3(0.12f, 0.06f, 0.08f), Sack);

            bool done = s.state == SiteState.Done;
            int builtFloors = done ? s.Floors : Mathf.Max(0, s.step - 1); // tamamlanan katlar
            bool foundationDone = done || s.step > 0;

            if (foundationDone)
                Box("Foundation", siteRoot, new Vector3(0f, slabHeight * 0.5f, 0f), new Vector3(footprint + 0.1f, slabHeight, footprint + 0.1f), ConcreteDark);

            for (int f = 0; f < builtFloors; f++)
            {
                float y0 = slabHeight + f * floorHeight;
                Box($"Floor{f + 1}", siteRoot, new Vector3(0f, y0 + floorHeight * 0.5f, 0f), new Vector3(footprint, floorHeight, footprint), Concrete);
                // Pencere bantları (kameraya bakan iki yüzde)
                Box($"WinX{f + 1}", siteRoot, new Vector3(0f, y0 + floorHeight * 0.55f, -half - 0.003f), new Vector3(footprint * 0.8f, floorHeight * 0.32f, 0.01f), Window);
                Box($"WinZ{f + 1}", siteRoot, new Vector3(-half - 0.003f, y0 + floorHeight * 0.55f, 0f), new Vector3(0.01f, floorHeight * 0.32f, footprint * 0.8f), Window);
                Box($"Slab{f + 1}", siteRoot, new Vector3(0f, y0 + floorHeight - 0.015f, 0f), new Vector3(footprint + 0.04f, 0.03f, footprint + 0.04f), ConcreteDark);
            }

            float topY = slabHeight + builtFloors * floorHeight;
            if (done)
            {
                Box("Roof", siteRoot, new Vector3(0f, topY + 0.03f, 0f), new Vector3(footprint + 0.08f, 0.06f, footprint + 0.08f), ConcreteDark);
            }
            else if (s.state == SiteState.Building)
            {
                // Yapılmakta olan adım
                float baseY = s.step == 0 ? 0f : topY;
                columnBaseY = baseY;
                if (s.step == 0)
                {
                    // Temel: kalıp kenarları
                    Box("FormN", siteRoot, new Vector3(0f, 0.04f, half), new Vector3(footprint, 0.08f, 0.03f), new Color(0.55f, 0.40f, 0.25f));
                    Box("FormS", siteRoot, new Vector3(0f, 0.04f, -half), new Vector3(footprint, 0.08f, 0.03f), new Color(0.55f, 0.40f, 0.25f));
                    Box("FormE", siteRoot, new Vector3(half, 0.04f, 0f), new Vector3(0.03f, 0.08f, footprint), new Color(0.55f, 0.40f, 0.25f));
                    Box("FormW", siteRoot, new Vector3(-half, 0.04f, 0f), new Vector3(0.03f, 0.08f, footprint), new Color(0.55f, 0.40f, 0.25f));
                }
                // Demir ızgara
                int bars = 9;
                for (int i = 0; i < bars; i++)
                {
                    float o = -half + 0.1f + i * (footprint - 0.2f) / (bars - 1);
                    Box($"RX{i}", siteRoot, new Vector3(0f, baseY + 0.01f, o), new Vector3(footprint - 0.15f, 0.012f, 0.012f), Rebar);
                    Box($"RZ{i}", siteRoot, new Vector3(o, baseY + 0.01f, 0f), new Vector3(0.012f, 0.012f, footprint - 0.15f), Rebar);
                }
                // Kolonlar (adım ilerledikçe yükselir)
                if (s.step > 0)
                    foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                        columns.Add(Box("Column", siteRoot, new Vector3(c.x * (half - 0.08f), baseY, c.y * (half - 0.08f)), new Vector3(0.09f, 0.02f, 0.09f), ConcreteDark));
                // İskele
                float sh = floorHeight * 1.35f;
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                    Box("Post", siteRoot, new Vector3(c.x * (half + 0.06f), baseY + sh * 0.5f, c.y * (half + 0.06f)), new Vector3(0.025f, sh, 0.025f), Scaffold);
                Box("RailN", siteRoot, new Vector3(0f, baseY + sh, half + 0.06f), new Vector3(footprint + 0.14f, 0.02f, 0.02f), Scaffold);
                Box("RailE", siteRoot, new Vector3(half + 0.06f, baseY + sh, 0f), new Vector3(0.02f, 0.02f, footprint + 0.14f), Scaffold);
                Box("RailS", siteRoot, new Vector3(0f, baseY + sh * 0.55f, -half - 0.06f), new Vector3(footprint + 0.14f, 0.02f, 0.02f), Scaffold);
                Box("RailW", siteRoot, new Vector3(-half - 0.06f, baseY + sh * 0.55f, 0f), new Vector3(0.02f, 0.02f, footprint + 0.14f), Scaffold);

                // İşçiler: kat üstünde halka şeklinde
                int n = Mathf.Max(1, s.workers);
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f + 0.6f;
                    float r = n <= 3 ? 0.38f : 0.48f;
                    var go = new GameObject($"Worker_{i + 1}");
                    go.transform.SetParent(siteRoot, false);
                    go.transform.localPosition = new Vector3(Mathf.Cos(a) * r, baseY + 0.016f, Mathf.Sin(a) * r);
                    go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    var w = go.AddComponent<WorkerView>();
                    w.Build(i, litMaterial, unlitMaterial);
                    workers.Add(w);
                }
            }

            // Kadraj: binanın bitmiş hâli sığsın (kanal boyunca zıplamasın)
            float fullH = slabHeight + s.Floors * floorHeight + 0.4f;
            camTarget = new Vector3(0f, fullH * 0.42f, 0f);
            camSize = Mathf.Max(1.9f, 0.9f + fullH * 0.55f);
        }

        void Mixer3D(Vector3 p)
        {
            Shape(PrimitiveType.Sphere, "Mixer", siteRoot, p + new Vector3(0f, 0.17f, 0f), new Vector3(0.30f, 0.24f, 0.24f), Mixer, litMaterial, Quaternion.Euler(0f, 0f, 20f));
            Box("MixerBase", siteRoot, p + new Vector3(0f, 0.04f, 0f), new Vector3(0.30f, 0.08f, 0.16f), new Color(0.2f, 0.2f, 0.22f));
        }

        void UpdateCamera()
        {
            if (feedCamera == null) return;
            var rot = Quaternion.Euler(30f, 45f, 0f);
            var target = siteRoot.position + camTarget;
            feedCamera.transform.rotation = rot;
            feedCamera.transform.position = target - rot * Vector3.forward * 30f;
            feedCamera.orthographicSize = Mathf.Lerp(feedCamera.orthographicSize, camSize, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        // ------------------------------------------------------------------ ekran yazıları

        void UpdateOverlay(ConstructionSite site, int sel)
        {
            int channel = Mathf.Max(0, sel) + 1;
            if (stripText != null)
            {
                if (site == null) stripText.text = $"REC   KANAL {channel}";
                else
                {
                    string where = site.state == SiteState.Done ? "TAMAMLANDI"
                                 : site.state == SiteState.Flooded ? "SU ALTINDA"
                                 : site.state == SiteState.Collapsed ? "YIKILDI"
                                 : site.step == 0 ? $"TEMEL / {site.Floors} KAT"
                                 : $"KAT {site.step} / {site.Floors}";
                    stripText.text = $"REC   KANAL {channel} — ŞANTİYE {site.letter}   {where}";
                }
            }
            if (defectText != null)
            {
                defectText.gameObject.SetActive(site != null);
                if (site != null) defectText.text = $"KUSUR: {site.defects}";
            }
            if (recDot != null) recDot.enabled = Mathf.Repeat(Time.time, 1f) < 0.6f;
            if (timeText != null)
            {
                var gm = GameManager.Instance;
                int day = gm != null && gm.Run != null ? gm.Run.day : 1;
                bool working = gm != null && (gm.State == GameState.Working || gm.State == GameState.FinalShift);
                float secs = 9f * 3600f + (working ? (Time.time - workStartTime) * 6f : 0f); // 1 sn = 6 sn oyun saati
                int h = (int)(secs / 3600f) % 24, m = (int)(secs / 60f) % 60, sc = (int)secs % 60;
                timeText.text = $"{15 + day:00}.10.26  {h:00}:{m:00}:{sc:00}";
            }
            if (noSignalText != null && site == null) noSignalText.text = $"SİNYAL YOK\n<size=50%>KANAL {channel}</size>";
        }

        void UpdateNoise()
        {
            if (noiseTex == null || Time.time < nextNoise) return;
            nextNoise = Time.time + 0.05f;
            for (int i = 0; i < noisePixels.Length; i++)
            {
                byte v = (byte)Random.Range(20, 140);
                noisePixels[i] = new Color32(v, v, v, 255);
            }
            noiseTex.SetPixels32(noisePixels);
            noiseTex.Apply(false);
        }

        // ------------------------------------------------------------------ şekil yardımcıları

        Transform Box(string n, Transform p, Vector3 pos, Vector3 size, Color c)
            => Shape(PrimitiveType.Cube, n, p, pos, size, c, litMaterial, Quaternion.identity);

        static MaterialPropertyBlock mpb;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// Collider'sız, renkli basit şekil
        public static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 size, Color c, Material mat, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mat != null) r.sharedMaterial = mat;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.Clear();
            mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(mpb);
            return go.transform;
        }
    }
}
