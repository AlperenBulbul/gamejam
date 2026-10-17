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
            blurImg.color = new Color(blurTint, blurTint, blurTint, 1f);

            // Masaya bırakılmış küçük bir not kâğıdı (bitiş raporuyla aynı dil): gölge + kâğıt, hafif eğik
            var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)shadow.transform;
            srt.SetParent(root.transform, false);
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(540f, 560f);
            srt.anchoredPosition = new Vector2(8f, -10f);
            srt.localRotation = Quaternion.Euler(0f, 0f, 0.7f);
            shadow.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var col = new GameObject("Paper", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            var crt = (RectTransform)col.transform;
            crt.SetParent(root.transform, false);
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(540f, 560f);
            crt.localRotation = Quaternion.Euler(0f, 0f, 0.7f);
            col.GetComponent<Image>().color = Paper;
            var v = col.GetComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(56, 56, 48, 40);
            v.spacing = 14f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true; v.childControlHeight = false;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            Label(crt, "Karaova Yapı Denetim", 20f, InkSoft, 26f);
            var title = Label(crt, "Mola", 52f, PaperInk, 64f);
            title.fontStyle = FontStyles.Bold;

            var line = new GameObject("Line", typeof(RectTransform), typeof(Image));
            var lrt = (RectTransform)line.transform;
            lrt.SetParent(crt, false);
            lrt.sizeDelta = new Vector2(0f, 2f);
            line.GetComponent<Image>().color = new Color(0.13f, 0.12f, 0.11f, 0.55f);

            Spacer(crt, 14f);
            Button(crt, "Masaya dön", true, Resume);
            Button(crt, "Ana menü", false, ToMainMenu);
            var quit = Button(crt, "Oyundan çık", false, Quit);
#if UNITY_WEBGL && !UNITY_EDITOR
            quit.SetActive(false); // tarayıcıda çıkış olmaz
#endif
            Spacer(crt, 6f);

            // Esc tuşu simgesi + "devam"
            var hint = new GameObject("Hint", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var hrt = (RectTransform)hint.transform;
            hrt.SetParent(crt, false);
            hrt.sizeDelta = new Vector2(0f, 32f);
            var hl = hint.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            KeyIcons.Add(hrt, "ESC", null);
            var ht = Label(hrt, "ile de devam edebilirsin", 20f, InkSoft, 30f);
            ht.alignment = TextAlignmentOptions.MidlineLeft;

            root.SetActive(false);
        }

        static readonly Color Paper = new Color(0.937f, 0.914f, 0.859f);
        static readonly Color PaperInk = new Color(0.13f, 0.12f, 0.11f);
        static readonly Color InkSoft = new Color(0.40f, 0.38f, 0.35f);

        GameObject Button(RectTransform parent, string text, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(0f, 64f);
            var img = go.GetComponent<Image>();
            img.color = Color.white; // renk düğmenin durum renklerinden gelir
            var btn = go.GetComponent<Button>();
            var c = btn.colors;
            c.normalColor = primary ? PaperInk : new Color(0.13f, 0.12f, 0.11f, 0.06f);
            c.highlightedColor = primary ? new Color(0.25f, 0.23f, 0.21f) : new Color(0.13f, 0.12f, 0.11f, 0.14f);
            c.pressedColor = primary ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.13f, 0.12f, 0.11f, 0.22f);
            c.selectedColor = c.normalColor;
            c.colorMultiplier = 1f;
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);

            var label = Label(rt, text, 28f, primary ? Paper : PaperInk, 0f);
            label.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var lrt = label.rectTransform;
            Stretch(lrt);
            lrt.offsetMin = new Vector2(24f, 0f);
            return go;
        }

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
}
