using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Müşteri konuşması: ekranın altında beyaz altyazı + anlamsız mırıltı sesi (GDD bölüm 4).
    ///  - "İsim: replik", replik harf harf açılır (speechCps; Sabırsız x1.1, Patlama x1.3 ve BÜYÜK HARF)
    ///  - Her blipEveryNChars harfte bir hece sesi; perde müşteriye göre, sinirlendikçe kalın ve yüksek
    ///  - Bitince bubbleHold sn bekler, söner. Yeni replik eskisini keser.
    public class SpeechSystem : MonoBehaviour
    {
        public static SpeechSystem Instance { get; private set; }

        [Header("Altyazı")]
        public CanvasGroup group;
        public TMP_Text text;
        public RectTransform background;
        public float maxWidth = 1100f;
        public Vector2 padding = new Vector2(44f, 22f);
        [Tooltip("Altyazının arkasındaki yarı şeffaf koyu kutu (oyun görünür kalır, yazı okunur)")]
        public Color backdropColor = new Color(0.04f, 0.035f, 0.03f, 0.58f);
        public float fadeTime = 0.25f;

        [Header("Ses")]
        public AudioSource voice;
        [Tooltip("Hece kayıtları (VO_Gib). Boşsa geçici sentetik heceler üretilir.")]
        public AudioClip[] syllables;
        [Tooltip("Sinirli / patlamada ağırlıklı çalan homurtu (VO_Grumble). Boşsa sentetik.")]
        public AudioClip[] grumbles;
        [Range(0f, 1f)] public float baseVolume = 0.45f;

        Coroutine speaking;

        void Awake()
        {
            Instance = this;
            if (syllables == null || syllables.Length == 0) syllables = GibberishSynth.Make(6, false);
            if (grumbles == null || grumbles.Length == 0) grumbles = GibberishSynth.Make(2, true);
            if (group != null) group.alpha = 0f;
            UseBackdrop();
            UseShadow();
        }

        /// Yazının arkasında yumuşak kenarlı, yarı şeffaf koyu kutu: arkadaki sahne seçilir, yazı her zeminde okunur
        void UseBackdrop()
        {
            if (padding.x < 56f) padding = new Vector2(56f, 26f);
            if (background == null || text == null) return;
            var img = background.GetComponent<Image>();
            if (img == null) img = background.gameObject.AddComponent<Image>();
            img.enabled = true;
            img.sprite = SoftBox();
            img.type = Image.Type.Sliced;
            img.color = backdropColor;
            img.raycastTarget = false;
            background.SetSiblingIndex(0); // yazının arkasında
            // Kutu ile yazı aynı alt orta noktaya bağlı: yazıyı dolgunun yarısı kadar yukarı al, ortalansın
            var trt = text.rectTransform;
            trt.anchoredPosition = new Vector2(trt.anchoredPosition.x, background.anchoredPosition.y + padding.y * 0.5f);
        }

        static Sprite softBox;

        /// 64 px yuvarlak köşeli kutu, kenarları 10 px'te yumuşakça kaybolur (9 dilimli)
        static Sprite SoftBox()
        {
            if (softBox != null) return softBox;
            const int n = 64; const float radius = 22f, feather = 10f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SpeechBackdrop" };
            var px = new Color32[n * n];
            float half = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // yuvarlak köşeli dikdörtgene uzaklık (içeride negatif)
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float a = Mathf.Clamp01(-outside / feather);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = radius + 2f;
            softBox = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            return softBox;
        }

        /// Yazının altında hafif gölge (kutuyla birlikte her zeminde okunsun)
        void UseShadow()
        {
            if (text == null) return;
            var m = text.fontMaterial; // örnek materyal: diğer yazıları etkilemez
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
            m.SetFloat("_UnderlayOffsetX", 0.45f);
            m.SetFloat("_UnderlayOffsetY", -0.55f);
            m.SetFloat("_UnderlayDilate", 0.35f);
            m.SetFloat("_UnderlaySoftness", 0.45f);
            text.fontMaterial = m;
            text.outlineWidth = 0.12f;
            text.outlineColor = new Color32(0, 0, 0, 200);
        }

        // ------------------------------------------------------------------ alttaki yönerge çubuklarıyla çakışmasın

        RectTransform root;
        float baseY = float.NaN;

        void LateUpdate()
        {
            if (root == null && group != null) root = group.transform as RectTransform;
            if (root == null) return;
            if (float.IsNaN(baseY)) baseY = root.anchoredPosition.y;
            // İlk tur yönergesi / "Gazeteye git" gibi çubuk açıkken altyazı onun üstüne çıkar
            bool barUp = GuidedTour.BarVisible || PromptBar.Visible;
            float want = barUp ? Mathf.Max(baseY, 112f) : baseY;
            var p = root.anchoredPosition;
            p.y = Mathf.MoveTowards(p.y, want, Time.unscaledDeltaTime * 600f);
            root.anchoredPosition = p;
        }

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnStateChanged(GameState s)
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.DeskInputEnabled) Clear();
        }

        public void Say(CustomerData who, string line, PatienceStage stage)
        {
            if (who == null || string.IsNullOrEmpty(line) || text == null) return;
            if (speaking != null) StopCoroutine(speaking);
            speaking = StartCoroutine(Speak(who, line, stage));
        }

        public void Clear()
        {
            if (speaking != null) { StopCoroutine(speaking); speaking = null; }
            if (group != null) group.alpha = 0f;
        }

        IEnumerator Speak(CustomerData who, string line, PatienceStage stage)
        {
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            float cps = cfg != null ? cfg.speechCps : 30f;
            int blipEvery = cfg != null ? Mathf.Max(1, cfg.blipEveryNChars) : 2;
            float hold = cfg != null ? cfg.bubbleHold : 2.5f;

            if (stage == PatienceStage.Impatient) cps *= 1.1f;
            if (stage == PatienceStage.Rage)
            {
                cps *= 1.3f;
                line = line.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
            }

            string prefix = $"<b>{who.displayName}:</b> ";
            int prefixChars = who.displayName.Length + 2; // "İsim: " (etiketler sayılmaz)
            string full = prefix + line;

            // Boyutu tam metne göre ayarla (yazı açılırken kutu zıplamasın)
            text.text = full;
            Vector2 pref = text.GetPreferredValues(full, maxWidth, 0f);
            var size = new Vector2(Mathf.Min(pref.x, maxWidth), pref.y);
            text.rectTransform.sizeDelta = size;
            if (background != null) background.sizeDelta = size + padding;
            text.maxVisibleCharacters = prefixChars;

            // Hızlı belir
            yield return Fade(group.alpha, 1f, fadeTime * 0.5f);

            int shown = 0, letters = 0;
            float acc = 0f;
            while (shown < line.Length)
            {
                acc += Time.unscaledDeltaTime * cps;
                while (acc >= 1f && shown < line.Length)
                {
                    acc -= 1f;
                    char ch = line[shown++];
                    text.maxVisibleCharacters = prefixChars + shown;
                    if (!char.IsWhiteSpace(ch) && ++letters % blipEvery == 0) Blip(who, stage);
                }
                yield return null;
            }

            yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeTime);
            speaking = null;
        }

        IEnumerator Fade(float from, float to, float time)
        {
            if (group == null) yield break;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            group.alpha = to;
        }

        void Blip(CustomerData who, PatienceStage stage)
        {
            if (voice == null) return;
            bool cross = stage == PatienceStage.Angry || stage == PatienceStage.Rage;
            var pool = cross && Random.value < 0.5f && grumbles.Length > 0 ? grumbles : syllables;
            if (pool.Length == 0) return;

            float pitch = who.voicePitch + Random.Range(-0.05f, 0.05f);
            float vol = baseVolume;
            if (stage == PatienceStage.Angry) { pitch -= 0.10f; vol *= 1.41f; } // +3 dB
            if (stage == PatienceStage.Rage) { pitch -= 0.15f; vol *= 2.0f; }   // +6 dB

            voice.pitch = Mathf.Max(0.3f, pitch);
            voice.PlayOneShot(pool[Random.Range(0, pool.Length)], Mathf.Clamp01(vol) * GameSettings.Sfx01); // Ayarlar > Efektler
        }
    }

    /// Ses dosyası gelene kadar geçici heceler: kısa, sönümlenen, iki frekanslı "mm/bra/nyeh" benzeri sesler
    public static class GibberishSynth
    {
        public static AudioClip[] Make(int count, bool grumble)
        {
            const int rate = 22050;
            var clips = new AudioClip[count];
            var rng = new System.Random(grumble ? 7 : 3);
            for (int i = 0; i < count; i++)
            {
                float dur = grumble ? 0.13f : 0.075f + (float)rng.NextDouble() * 0.03f;
                int n = Mathf.CeilToInt(dur * rate);
                float f0 = grumble ? 90f + (float)rng.NextDouble() * 30f : 170f + (float)rng.NextDouble() * 70f;
                float f1 = grumble ? 380f : 520f + (float)rng.NextDouble() * 420f;
                float noise = grumble ? 0.35f : 0.08f;
                var data = new float[n];
                for (int s = 0; s < n; s++)
                {
                    float t = s / (float)rate;
                    float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * (grumble ? 18f : 34f));
                    float v = Mathf.Sin(2f * Mathf.PI * f0 * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * f1 * t));
                    v += ((float)rng.NextDouble() * 2f - 1f) * noise * (t < 0.015f ? 1f : 0.3f);
                    data[s] = v * env * 0.6f;
                }
                var clip = AudioClip.Create($"{(grumble ? "Grumble" : "Gib")}_{i}", n, 1, rate, false);
                clip.SetData(data, 0);
                clips[i] = clip;
            }
            return clips;
        }
    }
}
