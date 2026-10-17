using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Tuş listesindeki simgeler (kodla çizilir, dosya gerekmez):
    ///  LMB / RMB: fare, basılı tuş turuncu · WHEEL: tekerlek turuncu · MOUSE: düz fare
    ///  ESC, SPACE (BOŞLUK), TAB, ENTER, A, D, 1-7 ...: klavye tuşu (yuvarlak köşeli kutu + yazı)
    public static class KeyIcons
    {
        static readonly Dictionary<string, Texture2D> mice = new Dictionary<string, Texture2D>();
        static Sprite keySprite;

        static readonly Color32 Line = new Color32(235, 238, 242, 255);
        static readonly Color32 Fill = new Color32(28, 30, 34, 255);
        static readonly Color32 Lit = new Color32(255, 102, 15, 255); // HintColors.Orange

        public static void Add(Transform parent, string token, TMP_FontAsset font)
        {
            string t = token.Trim().ToUpperInvariant();
            switch (t)
            {
                case "LMB": case "RMB": case "WHEEL": case "MOUSE":
                    Mouse(parent, t);
                    return;
            }
            string label = t switch
            {
                "SPACE" => "BOŞLUK",
                "ESC" => "Esc",
                "TAB" => "Tab",
                "ENTER" => "Enter",
                _ => token.Trim(),
            };
            Key(parent, label, font);
        }

        // ------------------------------------------------------------------ fare

        static void Mouse(Transform parent, string kind)
        {
            var go = new GameObject("Mouse_" + kind, typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = 24f; le.preferredHeight = 32f;
            le.minWidth = 24f; le.minHeight = 32f;
            var img = go.GetComponent<RawImage>();
            img.texture = MouseTex(kind);
            img.raycastTarget = false;
        }

        static Texture2D MouseTex(string kind)
        {
            if (mice.TryGetValue(kind, out var tex) && tex != null) return tex;
            const int W = 48, H = 64;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Icon_" + kind };
            var px = new Color32[W * H];
            var c = new Vector2(W * 0.5f, H * 0.5f);
            var half = new Vector2(20f, 29f);
            const float radius = 18f, stroke = 3f;
            float splitY = H * 0.58f;                    // tuşlarla gövdenin ayrıldığı çizgi (y yukarı)
            var wheelC = new Vector2(W * 0.5f, H * 0.72f);
            var wheelHalf = new Vector2(3.2f, 6f);

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = RoundBox(p - c, half, radius);
                    Color32 col = new Color32(0, 0, 0, 0);
                    if (d < 0f)
                    {
                        col = Fill;
                        bool top = p.y > splitY;
                        if (top && kind == "LMB" && p.x < c.x) col = Lit;
                        if (top && kind == "RMB" && p.x > c.x) col = Lit;
                        // tuş çizgileri
                        if (top && Mathf.Abs(p.x - c.x) < 1.2f) col = Line;
                        if (Mathf.Abs(p.y - splitY) < 1.2f) col = Line;
                        // tekerlek
                        float wd = RoundBox(p - wheelC, wheelHalf, 3f);
                        if (wd < 0f) col = kind == "WHEEL" ? Lit : Line;
                        if (d > -stroke) col = Line;                    // dış çizgi
                    }
                    // kenar yumuşatma
                    float a = Mathf.Clamp01(-d + 0.5f);
                    col.a = (byte)(col.a * a);
                    px[y * W + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply();
            mice[kind] = tex;
            return tex;
        }

        static float RoundBox(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // ------------------------------------------------------------------ klavye tuşu

        static void Key(Transform parent, string label, TMP_FontAsset font)
        {
            float w = label.Length <= 1 ? 30f : label.Length <= 3 ? 44f : 18f + label.Length * 11f;
            var go = new GameObject("Key_" + label, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = 30f;
            le.minWidth = w; le.minHeight = 30f;
            var img = go.GetComponent<Image>();
            img.sprite = KeySprite();
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var t = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)t.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(-2f, 0f);
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 15f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color32(235, 238, 242, 255);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
        }

        /// 32 x 32 yuvarlak köşeli kutu (9 dilim: kenarlar uzamaz)
        static Sprite KeySprite()
        {
            if (keySprite != null) return keySprite;
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Icon_Key" };
            var px = new Color32[N * N];
            var c = new Vector2(N * 0.5f, N * 0.5f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = RoundBox(p - c, new Vector2(15f, 15f), 7f);
                    Color32 col = d > -2f ? Line : (y < 6 ? new Color32(18, 19, 22, 255) : Fill); // alt kenar biraz koyu (basılabilir tuş hissi)
                    col.a = (byte)(255 * Mathf.Clamp01(-d + 0.5f));
                    px[y * N + x] = col;
                }
            tex.SetPixels32(px);
            tex.Apply();
            keySprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            return keySprite;
        }
    }
}
