using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Esc (masadayken): oyunu duraklatır, arka plan bulanıklaşır, ortada menü çıkar.
    ///  Küçük not kâğıdı: Masaya dön (ya da tekrar Esc) / Ana menü / Oyundan çık
    /// Duraklatınca zaman durur (müşteri sabrı, şantiyeler, konuşmalar), sesler susar, masa kontrolleri kapanır.
    /// Yakın bakıştayken Esc önce masaya döndürür; ikinci Esc menüyü açar.
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }
        /// Menü açılıp kapandığı kare (o tıklama masaya geçmesin diye)
        public static int LastToggleFrame { get; private set; } = -10;

        [Range(0f, 1f)] public float blurTint = 0.40f;


        GameObject root;
        RawImage blurImg;
        RenderTexture blurRT;
        CanvasGroup group;
        float savedTimeScale = 1f;
        bool opening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { IsPaused = false; LastToggleFrame = -10; }

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnDestroy()
        {
            if (IsPaused) { Time.timeScale = 1f; AudioListener.pause = false; IsPaused = false; }
            if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
        }

        void OnStateChanged(GameState s)
        {
            var gm = GameManager.Instance;
            if (IsPaused && gm != null && !gm.InDeskState) Resume(); // oyun bitti / menüye dönüldü
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame || opening) return;
            if (Time.frameCount == LastToggleFrame) return;

            if (IsPaused) { Resume(); return; }

            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            if (gm == null || !gm.InDeskState) return;
            if (fc != null && fc.IsTransitioning) return;
            if (fc != null && fc.State != FocusState.Desk && GuidedTour.AllowsBack) return; // yakın bakışta Esc = geri (ilk turda geri kapalı: menü açılır)
            StartCoroutine(Open());
        }

        // ------------------------------------------------------------------ aç / kapat

        IEnumerator Open()
        {
            opening = true;
            if (root == null) Build();
            yield return new WaitForEndOfFrame();
            CaptureBlur(); // menü görünmeden önceki ekran

            IsPaused = true;
            LastToggleFrame = Time.frameCount;
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            root.SetActive(true);
            opening = false;

            for (float t = 0f; t < 0.18f && IsPaused; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / 0.18f;
                yield return null;
            }
            if (IsPaused) group.alpha = 1f;
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            LastToggleFrame = Time.frameCount;
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
            if (root != null) root.SetActive(false);
        }

        void ToMainMenu()
        {
            Resume();
            var gm = GameManager.Instance;
            if (gm != null) gm.OnRestart();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ arayüz

        void Build()
        {
            root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // her şeyin üstünde
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = root.GetComponent<CanvasGroup>();

            // Bulanık arka plan (tıklamaları da tutar)
            var bg = new GameObject("Blur", typeof(RectTransform), typeof(RawImage));
            var bgrt = (RectTransform)bg.transform;
            bgrt.SetParent(root.transform, false);
            Stretch(bgrt);
            blurImg = bg.GetComponent<RawImage>();
            blurImg.color = Color.white;

            // Üstüne siyah %38 (tasarımcı): panel öne çıksın
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dim.transform;
            drt.SetParent(root.transform, false);
            Stretch(drt);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);
            dim.GetComponent<Image>().raycastTarget = false;

            // Panel (tasarımcı UI paketi, 18 Ekim): 720 × 640, düz, gölge görselin içinde, 9 dilim (140 px kenar)
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            var panel = (RectTransform)panelGo.transform;
            panel.SetParent(root.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(720f, 640f);
            var pimg = panelGo.GetComponent<Image>();
            var panelSp = Sprite2x("Pause/UI_Pause_Panel", 140f);
            if (panelSp != null) { pimg.sprite = panelSp; pimg.type = Image.Type.Sliced; pimg.color = Color.white; }
            else pimg.color = new Color32(0xF3, 0xEF, 0xE7, 0xFF);

            var semi = UiFonts.SemiBold;
            var heavy = UiFonts.Heavy;
            var regular = UiFonts.Regular;

            // İçerik: panel görselinin görünen kenarından 52 birim içeride (görselde 60 birim gölge payı)
            const float left = 112f;
            var tag = Place(Label(panel, "KARAOVA YAPI DENETİM", 20f, new Color32(0x8A, 0x6A, 0x2E, 0xFF), 0f), left, 110f, 496f, 28f);
            tag.characterSpacing = 14f;
            if (semi != null) tag.font = semi; else tag.fontStyle = FontStyles.Bold;

            var title = Place(Label(panel, "Mola", 72f, new Color32(0x22, 0x22, 0x22, 0xFF), 0f), left, 138f, 496f, 86f);
            if (heavy != null) title.font = heavy; else title.fontStyle = FontStyles.Bold;

            var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            var lrt = (RectTransform)line.transform;
            lrt.SetParent(panel, false);
            TopLeft(lrt, left, 240f, 496f, 3f);
            line.GetComponent<Image>().color = new Color32(0x2B, 0x2B, 0x2B, 0xFF);

            // Düğmeler: 496 × 76 (görsel 1000 × 172 px, altta basılma payı), aralarında 14
            Button(panel, "Masaya dön", true, Resume, left, 273f, new Color32(0xF7, 0xF4, 0xEE, 0xFF), semi);
            Button(panel, "Ana menü", false, ToMainMenu, left, 363f, new Color32(0x2B, 0x2B, 0x2B, 0xFF), regular);
            var quit = Button(panel, "Oyundan çık", false, Quit, left, 453f, new Color32(0x8B, 0x3A, 0x30, 0xFF), regular);
#if UNITY_WEBGL && !UNITY_EDITOR
            quit.SetActive(false); // tarayıcıda çıkış olmaz
#endif
            root.SetActive(false);
        }

        /// 2x çözünürlüklü tasarım görseli -> sprite (1 birim = 2 piksel), isteğe bağlı 9 dilim kenarı (piksel)
        static Sprite Sprite2x(string path, float border = 0f)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 200f, 0,
                                 SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static void TopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        static TextMeshProUGUI Place(TextMeshProUGUI t, float x, float y, float w, float h)
        {
            TopLeft(t.rectTransform, x, y, w, h);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        GameObject Button(RectTransform parent, string text, bool primary, UnityEngine.Events.UnityAction onClick,
                          float x, float y, Color textColor, TMP_FontAsset font)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            TopLeft(rt, x - 2f, y, 500f, 86f);   // görsel 1000 × 172 px: görünen kısım 496 × ~80
            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            string kind = primary ? "Primary" : "Secondary";
            var normal = Sprite2x($"Pause/UI_Pause_Btn_{kind}_Normal", 40f);
            if (normal != null)
            {
                img.sprite = normal;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = Sprite2x($"Pause/UI_Pause_Btn_{kind}_Hover", 40f);
                ss.pressedSprite = Sprite2x($"Pause/UI_Pause_Btn_{kind}_Pressed", 40f);
                ss.selectedSprite = normal;
                btn.spriteState = ss;
            }
            else img.color = primary ? new Color32(0x2B, 0x2B, 0x2B, 0xFF) : new Color32(0xF3, 0xEF, 0xE7, 0xFF);
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 30f, textColor, 0f);
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(28f, 6f); lr.offsetMax = new Vector2(-20f, 0f); // alttaki basılma payının üstünde
            label.alignment = TextAlignmentOptions.MidlineLeft;
            if (font != null) label.font = font; else label.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;

            if (primary)
            {
                // Sağda ESC çipi (60 × 34)
                var chip = new GameObject("EscChip", typeof(RectTransform), typeof(Image));
                var crt = (RectTransform)chip.transform;
                crt.SetParent(rt, false);
                crt.anchorMin = crt.anchorMax = new Vector2(1f, 0.5f);
                crt.pivot = new Vector2(1f, 0.5f);
                crt.sizeDelta = new Vector2(60f, 34f);
                crt.anchoredPosition = new Vector2(-28f, 3f);
                var ci = chip.GetComponent<Image>();
                var chipSp = Sprite2x("Pause/UI_Pause_EscChip");
                if (chipSp != null) ci.sprite = chipSp; else ci.color = new Color(1f, 1f, 1f, 0.15f);
                ci.raycastTarget = false;
                var ct = Label(crt, "ESC", 18f, new Color32(0xE9, 0xE2, 0xD5, 0xFF), 0f);
                ct.rectTransform.anchorMin = Vector2.zero; ct.rectTransform.anchorMax = Vector2.one;
                ct.rectTransform.offsetMin = ct.rectTransform.offsetMax = Vector2.zero;
                ct.alignment = TextAlignmentOptions.Center;
                ct.characterSpacing = 4f;
                if (semi2 != null) ct.font = semi2; else ct.fontStyle = FontStyles.Bold;

                // Basılınca yazı 3 birim aşağı (tasarımcı)
                go.AddComponent<PressNudge>().target = lr;
            }
            return go;
        }

        static TMP_FontAsset semi2 => UiFonts.SemiBold;

        TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0f, height);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
            return t;
        }

        static void Spacer(RectTransform parent, float h)
        {
            var go = new GameObject("Space", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0f, h);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ bulanık arka plan

        void CaptureBlur()
        {
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return;
            var cur = Temp(shot.width / 2, shot.height / 2);
            Graphics.Blit(shot, cur);
            Destroy(shot);
            for (int i = 0; i < 4; i++) cur = Step(cur, cur.width / 2, cur.height / 2);
            for (int i = 0; i < 3; i++) cur = Step(cur, cur.width * 2, cur.height * 2);
            if (blurRT == null || blurRT.width != cur.width || blurRT.height != cur.height)
            {
                if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
                blurRT = new RenderTexture(cur.width, cur.height, 0) { filterMode = FilterMode.Bilinear };
            }
            Graphics.Blit(cur, blurRT);
            RenderTexture.ReleaseTemporary(cur);
            blurImg.texture = blurRT;
        }

        static RenderTexture Temp(int w, int h)
        {
            var r = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0);
            r.filterMode = FilterMode.Bilinear;
            return r;
        }

        static RenderTexture Step(RenderTexture src, int w, int h)
        {
            var dst = Temp(w, h);
            Graphics.Blit(src, dst);
            RenderTexture.ReleaseTemporary(src);
            return dst;
        }
    }

    /// Düğmeye basılı tutulurken yazıyı biraz aşağı kaydırır (basılma hissi)
    public class PressNudge : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public RectTransform target;
        public float amount = 3f;
        bool down;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => Set(true);
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        void OnDisable() => Set(false);

        void Set(bool d)
        {
            if (d == down || target == null) return;
            down = d;
            target.anchoredPosition += new Vector2(0f, d ? -amount : amount);
        }
    }
}
