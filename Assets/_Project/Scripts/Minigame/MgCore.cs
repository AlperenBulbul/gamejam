using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Monitör minigame'lerinin ortak tabanı. Koordinatlar tasarımcının dokümanındaki gibi:
    /// 1280 × 720 ekran, sol üst (0, 0), y aşağı doğru artar.
    public abstract class MgGame
    {
        protected RectTransform root;
        protected ConstructionSite site;
        protected int channel;

        public bool Finished { get; protected set; }
        public int Defects { get; protected set; }

        public abstract string Title { get; }
        /// Sonuç ekranındaki skor (0-100). Varsayılan: her hata 20 puan götürür
        /// Sonuç ekranı: doğru hamle / toplam deneme
        public virtual int Attempts => 0;
        public virtual int Hits => 0;
        public abstract string Instructions { get; }
        /// Üst şeritteki sol yazı (REC'den sonrası)
        public abstract string Strip { get; }
        /// Sonuç ekranındaki açıklama
        public abstract string Summary { get; }
        /// Tuş ipuçları (sağ alttaki liste)
        public abstract (string key, string action)[] Hints { get; }
        /// Başlık kartında gösterilecek görsel (Resources/Minigame altındaki ad) ve altındaki yazı
        public virtual string CardImage => null;
        public virtual string CardImageCaption => "";

        /// Oyun başlamadan önce ekranı bulanıklaştıran tanıtım: başlık + tek cümle "önce [görsel] sonra"
        public virtual string IntroTitle => Title;
        public virtual string IntroBefore => "";
        public virtual string IntroAfter => "";
        public virtual Vector2 IntroVisualSize => new Vector2(180f, 146f);
        /// Cümlenin ortasındaki görsel (parent boyutu IntroVisualSize). Varsayılan: CardImage dokusu
        public virtual void BuildIntroVisual(RectTransform parent)
        {
            var tex = string.IsNullOrEmpty(CardImage) ? null : MgUI.Tex(CardImage);
            if (tex == null) return;
            var go = new GameObject("Image", typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var box = IntroVisualSize;
            float k = Mathf.Min(box.x / tex.width, box.y / tex.height);
            rt.sizeDelta = new Vector2(tex.width * k, tex.height * k);
            go.GetComponent<RawImage>().texture = tex;
            go.GetComponent<RawImage>().raycastTarget = false;
        }

        public void Init(RectTransform root, ConstructionSite site, int channel)
        {
            this.root = root;
            this.site = site;
            this.channel = channel;
            Build();
        }

        protected abstract void Build();

        /// click: bu karede tıklanan ekran noktası (yoksa null). action: Boşluk / tıklama
        public abstract void Tick(float dt, Vector2? click, bool action);

        protected string SiteLabel => site != null ? $"KANAL {channel + 1} — ŞANTİYE {site.letter}" : "KANAL —";

        // ------------------------------------------------------------------ yazı balonu (yukarı süzülüp kaybolur)

        readonly List<(TextMeshProUGUI t, float age, Vector2 start)> popups = new List<(TextMeshProUGUI, float, Vector2)>();

        protected void Popup(string text, Vector2 at, Color color, float size = 34f)
        {
            var t = MgUI.Text(root, text, at.x - 200f, at.y - 25f, 400f, 50f, size, color, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            popups.Add((t, 0f, ((RectTransform)t.transform).anchoredPosition));
        }

        protected void TickPopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.age += dt;
                if (p.t == null) { popups.RemoveAt(i); continue; }
                float k = p.age / 1.1f;
                ((RectTransform)p.t.transform).anchoredPosition = p.start + new Vector2(0f, 50f * k);
                var c = p.t.color; c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f); p.t.color = c;
                if (k >= 1f) { Object.Destroy(p.t.gameObject); popups.RemoveAt(i); }
                else popups[i] = p;
            }
        }

        // ------------------------------------------------------------------ kare kare efekt

        protected class Fx
        {
            public RawImage img;
            public Texture2D[] frames;
            public float t;
            public float fps = 12f;
        }

        readonly List<Fx> fxs = new List<Fx>();

        protected void PlayFx(string prefix, int count, float x, float y, float size = 96f)
        {
            var frames = new Texture2D[count];
            for (int i = 0; i < count; i++) frames[i] = MgUI.Tex($"{prefix}_{i + 1:00}");
            var img = MgUI.Raw(root, frames[0], x - size * 0.5f, y - size * 0.5f, size, size);
            fxs.Add(new Fx { img = img, frames = frames });
        }

        protected void TickFx(float dt)
        {
            for (int i = fxs.Count - 1; i >= 0; i--)
            {
                var f = fxs[i];
                f.t += dt;
                int n = Mathf.FloorToInt(f.t * f.fps);
                if (f.img == null || n >= f.frames.Length) { if (f.img != null) Object.Destroy(f.img.gameObject); fxs.RemoveAt(i); continue; }
                f.img.texture = f.frames[n];
            }
        }
    }

    /// Minigame arayüz yardımcıları (RawImage ile; doku içe aktarma ayarı gerekmez)
    public static class MgUI
    {
        public const float W = 1280f, H = 720f;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static Texture2D circle;

        public static Texture2D Tex(string name)
        {
            if (cache.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>("Minigame/" + name);
            if (t == null) Debug.LogWarning("[Minigame] doku yok: Resources/Minigame/" + name);
            cache[name] = t;
            return t;
        }

        public static Texture2D Circle()
        {
            if (circle != null) return circle;
            const int n = 32;
            circle = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    byte a = (byte)(Mathf.Clamp01(n * 0.5f - d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            circle.SetPixels32(px);
            circle.Apply();
            return circle;
        }

        /// Sol üst köşesi (x, y) olan dikdörtgen. pivot: döndürme / ölçekleme merkezi (0..1, Unity yönünde: y yukarı)
        public static void Place(RectTransform rt, float x, float y, float w, float h, Vector2? pivot = null)
        {
            var p = pivot ?? new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = p;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + p.x * w, -y - (1f - p.y) * h);
        }

        public static RawImage Raw(Transform parent, Texture tex, float x, float y, float w = -1f, float h = -1f, Vector2? pivot = null)
        {
            var go = new GameObject(tex != null ? tex.name : "Raw", typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            if (w < 0f) w = tex != null ? tex.width : 64f;
            if (h < 0f) h = tex != null ? tex.height : 64f;
            Place(rt, x, y, w, h, pivot);
            var img = go.GetComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        public static RawImage Rect(Transform parent, Color color, float x, float y, float w, float h)
        {
            var img = Raw(parent, null, x, y, w, h);
            img.color = color;
            img.name = "Rect";
            return img;
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, 0f, 0f, W, H);
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float x, float y, float w, float h, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
