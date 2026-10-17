using UnityEngine;

namespace LoadBearing
{
    /// Dışarısı (tasarım teslimi 2): gökyüzü günlere göre değişir, yağmur açılıp kapanır.
    /// - 1. gün: T_Sky_Day1, 2. gün: T_Sky_Day2, yağmurda: T_Sky_Rain
    /// - Yağmur: pencere camlarındaki damla / iz katmanları + dışarıdaki yağmur perdeleri; izler ve perdeler aşağı akar
    /// Materyal dosyalarına dokunmaz (MaterialPropertyBlock), oyun bitince her şey eski hâline döner.
    /// Gün sonu yağmur olayı (adım 17) SetRain(true) çağıracak. Test: F12.
    public class EnvironmentController : MonoBehaviour
    {
        public static EnvironmentController Instance { get; private set; }

        [Header("Gökyüzü (M_Sky kullanan parçalar)")]
        public Renderer[] skyRenderers;
        public Texture2D skyDay1, skyDay2, skyRain;

        [Header("Yağmur")]
        public GameObject[] rainRoots;
        [Tooltip("Aşağı akan iz katmanları (M_RainGlass_Streaks)")]
        public Renderer[] streakRenderers;
        [Tooltip("Dışarıdaki yağmur perdeleri (M_RainSheet)")]
        public Renderer[] sheetRenderers;
        public float streakSpeed = 0.15f;
        public float sheetSpeed = 1.5f;

        [Header("Yağmurda ışık")]
        public Light sun;

        public bool Raining { get; private set; }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");
        MaterialPropertyBlock mpb;
        float sunIntensity = -1f;

        void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
            if (sun != null) sunIntensity = sun.intensity;
        }

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void Start()
        {
            SetRain(false);
            ApplySky();
        }

        void OnStateChanged(GameState s)
        {
            if (s == GameState.DayIntro || s == GameState.MainMenu)
            {
                SetRain(false);
                ApplySky();
            }
        }

        static int Day => GameManager.Instance != null && GameManager.Instance.Run != null ? GameManager.Instance.Run.day : 1;

        void ApplySky()
        {
            var tex = Raining ? skyRain : (Day >= 2 ? skyDay2 : skyDay1);
            if (tex == null) return;
            SetTex(skyRenderers, "M_Sky", tex);
        }

        public void SetRain(bool on)
        {
            Raining = on;
            if (rainRoots != null) foreach (var r in rainRoots) if (r != null) r.SetActive(on);
            if (sun != null && sunIntensity >= 0f) sun.intensity = on ? sunIntensity * 0.25f : sunIntensity;
            ApplySky();
        }

        public void ToggleRain() => SetRain(!Raining);

        void Update()
        {
            if (!Raining) return;
            float t = Time.time;
            Scroll(streakRenderers, "M_RainGlass_Streaks", t * streakSpeed);
            Scroll(sheetRenderers, "M_RainSheet", t * sheetSpeed);
        }

        // Sadece adı verilen materyali kullanan alt mesh'e uygula (çok materyalli parçalarda diğerlerine dokunma)
        void SetTex(Renderer[] rends, string matName, Texture tex)
        {
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.StartsWith(matName)) continue;
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetTexture(BaseMapId, tex);
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }

        void Scroll(Renderer[] rends, string matName, float offset)
        {
            if (rends == null) return;
            foreach (var r in rends)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.name.StartsWith(matName)) continue;
                    var s = m.GetTextureScale(BaseMapId);
                    var o = m.GetTextureOffset(BaseMapId);
                    r.GetPropertyBlock(mpb, i);
                    mpb.SetVector(BaseMapStId, new Vector4(s.x, s.y, o.x, Mathf.Repeat(o.y + offset, 1f))); // aşağı akar
                    r.SetPropertyBlock(mpb, i);
                }
            }
        }
    }
}
