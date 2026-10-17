using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Belge ile harita / kural klasörü arasında kısayollar:
    ///  - Formda ARSA NO satırının sağında mavi HARİTA düğmesi: tıklayınca doğrudan haritaya geçer
    ///  - ARSA ALANI, İSTENEN KAT ve ZEMİN ETÜDÜ satırlarında kırmızı KURAL düğmesi: kural klasörü ilgili sayfada açılır
    ///  - Harita / kural klasöründeyken ekranın sağ üstünde "BELGEYE DÖN" düğmesi (ya da Tab): doğrudan belgeye döner
    /// FolderView bu bileşeni kendisi ekler.
    public class DocLinks : MonoBehaviour
    {
        FolderView folder;
        GameObject backButton;
        DeskInteractor interactor;

        static Texture2D mapIcon, bookIcon;

        public void Init(FolderView f, TMP_Text parcel, TMP_Text area, TMP_Text floors, TMP_Text soil)
        {
            folder = f;
            if (parcel != null) mapBtn = AddLink(parcel, "Map", MapIcon(), "Haritaya git", () => Go(FocusState.Map, null));
            if (area != null) AddLink(area, "Rules", BookIcon(), "Kurala bak", () => Go(FocusState.Binder, "kat"));
            if (floors != null) floorsBtn = AddLink(floors, "Rules", BookIcon(), "Kurala bak", () => Go(FocusState.Binder, "kat"));
            if (soil != null) AddLink(soil, "Rules", BookIcon(), "Kurala bak", () => Go(FocusState.Binder, "zemin"));
            BuildBackButton();
        }

        // ------------------------------------------------------------------ formdaki HARİTA / KURAL düğmeleri

        class Link
        {
            public Interactable it;
            public RectTransform rt;
            public RawImage img;
            public Texture2D normal, hover, pressed;
            public Vector3 home;
        }
        readonly System.Collections.Generic.List<Link> buttons = new System.Collections.Generic.List<Link>();

        // İlk tur (GuidedTour) için: hangi düğme yanacak / tıklanabilir
        Link mapBtn, floorsBtn;
        public Interactable MapLink => mapBtn?.it;
        public Interactable FloorsLink => floorsBtn?.it;
        public RectTransform MapRect => mapBtn?.rt;
        public RectTransform FloorsRect => floorsBtn?.rt;
        public RectTransform BackRect => backButton != null ? backButton.transform as RectTransform : null;

        static Texture2D Btn(string kind, string state) => Resources.Load<Texture2D>($"FormButtons/UI_Btn_{kind}_{state}");

        Link AddLink(TMP_Text value, string kind, Texture2D fallbackIcon, string prompt, UnityEngine.Events.UnityAction onClick)
        {
            var vrt = value.rectTransform;
            var parent = vrt.parent as RectTransform;
            if (parent == null) return null;

            // Tasarımcının düğmesi (108 x 44); yoksa eski küçük simge
            var normal = Btn(kind, "Normal");
            const float rowH = 23f;                       // satır aralığı (form birimi ≈ mm)
            float h = normal != null ? rowH * 0.72f : 13f; // satırın ~%75'i
            float w = normal != null ? h * normal.width / (float)normal.height : 13f;

            var corners = new Vector3[4];
            vrt.GetWorldCorners(corners); // sol alt, sol üst, sağ üst, sağ alt
            Vector3 topRight = parent.InverseTransformPoint(corners[2]);
            // Değer kutusunu daralt: düğme yazının üstüne binmesin
            vrt.sizeDelta = new Vector2(vrt.sizeDelta.x - (w + 3f), vrt.sizeDelta.y);

            var go = new GameObject("Link_" + value.name, typeof(RectTransform), typeof(RawImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            // Sağa hizalı (formun sağ kenarı), satırın dikey ortasında
            rt.localPosition = new Vector3(topRight.x - w * 0.5f, topRight.y - 7.5f, -0.2f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            var img = go.GetComponent<RawImage>();
            img.texture = normal != null ? normal : fallbackIcon;
            img.raycastTarget = false;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(w + 2f, h + 2f, 6f);
            var it = go.AddComponent<Interactable>();
            it.clickableIn = new[] { FocusState.Folder };
            it.prompt = prompt;
            it.focusPoint = null;
            it.Clicked += _ => onClick();
            var link = new Link
            {
                it = it, rt = rt, img = img, home = rt.localPosition,
                normal = normal, hover = Btn(kind, "Hover"), pressed = Btn(kind, "Pressed"),
            };
            buttons.Add(link);
            return link;
        }

        /// Sprite Swap: üzerine gelince _Hover, basılıyken _Pressed ve 2 px aşağı (tasarımcının notu)
        void UpdateButtons()
        {
            if (interactor == null) interactor = FindAnyObjectByType<DeskInteractor>();
            var mouse = Mouse.current;
            bool down = mouse != null && mouse.leftButton.isPressed;
            foreach (var b in buttons)
            {
                if (b.rt == null) continue;
                bool hover = interactor != null && interactor.Hovered == b.it;
                bool press = hover && down;
                if (b.normal != null)
                {
                    var tex = press && b.pressed != null ? b.pressed : hover && b.hover != null ? b.hover : b.normal;
                    if (b.img.texture != tex) b.img.texture = tex;
                    b.rt.localPosition = b.home + (press ? new Vector3(0f, -0.8f, 0f) : Vector3.zero);
                }
                else
                {
                    // eski simge: üzerine gelince büyür
                    float target = hover ? 1.3f : 1f;
                    float s = Mathf.Lerp(b.rt.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                    b.rt.localScale = new Vector3(s, s, 1f);
                }
            }
        }

        void Go(FocusState target, string ruleKey)
        {
            var sc = StampController.Instance;
            if (sc != null && sc.IsHolding) return; // mühür elindeyken
            var fc = FocusController.Instance;
            if (fc == null || fc.IsTransitioning) return;
            if (target == FocusState.Binder)
            {
                var binder = FindAnyObjectByType<BinderView>();
                if (binder != null) binder.JumpTo(ruleKey);
            }
            var point = FocusPointFor(target);
            if (point != null) fc.SwitchTo(point, target);
        }

        static Transform FocusPointFor(FocusState s)
        {
            foreach (var it in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (it.focusState == s && it.focusPoint != null) return it.focusPoint;
            return null;
        }

        // ------------------------------------------------------------------ "BELGEYE DÖN"

        bool CanGoBack
        {
            get
            {
                var fc = FocusController.Instance;
                return folder != null && fc != null && !fc.IsTransitioning && !PauseMenu.IsPaused && GuidedTour.AllowsDocBack
                       && (fc.State == FocusState.Map || fc.State == FocusState.Binder)
                       && folder.Current != null && !folder.Leaving
                       && folder.interactable != null && folder.interactable.focusPoint != null;
            }
        }

        void Back()
        {
            if (!CanGoBack) return;
            FocusController.Instance.SwitchTo(folder.interactable.focusPoint, FocusState.Folder);
        }

        void Update()
        {
            bool can = CanGoBack;
            if (backButton != null && backButton.activeSelf != can) backButton.SetActive(can);
            var kb = Keyboard.current;
            if (can && kb != null && kb.tabKey.wasPressedThisFrame) Back();

            UpdateButtons();
        }

        void BuildBackButton()
        {
            var go = new GameObject("DocBackUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            backButton = new GameObject("BackToDoc", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)backButton.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); // sağ üst köşe
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -28f);
            rt.sizeDelta = new Vector2(330f, 74f);
            var img = backButton.GetComponent<Image>();
            img.color = Color.white;
            var btn = backButton.GetComponent<Button>();
            var c = btn.colors;
            c.normalColor = new Color(0.07f, 0.075f, 0.08f, 0.9f);
            c.highlightedColor = new Color(0.95f, 0.70f, 0.20f, 1f);
            c.pressedColor = new Color(0.78f, 0.55f, 0.15f, 1f);
            c.selectedColor = c.normalColor;
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(Back);

            // Belge simgesi + yazı
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage));
            var irt = (RectTransform)icon.transform;
            irt.SetParent(rt, false);
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(18f, 0f);
            irt.sizeDelta = new Vector2(40f, 40f);
            icon.GetComponent<RawImage>().texture = DocIcon();
            icon.GetComponent<RawImage>().raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(70f, 0f); lrt.offsetMax = new Vector2(-10f, 0f);
            var t = label.GetComponent<TextMeshProUGUI>();
            t.text = "BELGEYE DÖN  <size=70%><color=#B8C0C8>Tab</color></size>";
            t.fontSize = 30f;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;

            backButton.SetActive(false);
        }

        // ------------------------------------------------------------------ simgeler (kodla çizilir)

        static Texture2D NewTex(out Color32[] px)
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[64 * 64];
            return t;
        }

        static void Fill(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(64, y1); y++)
                for (int x = Mathf.Max(0, x0); x < Mathf.Min(64, x1); x++)
                    px[y * 64 + x] = c;
        }

        static void Disc(Color32[] px, float cx, float cy, float r, Color32 c)
        {
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a <= 0f) continue;
                    var o = px[y * 64 + x];
                    px[y * 64 + x] = Color32.Lerp(o, c, a);
                }
        }

        /// Katlanmış harita + kırmızı iğne
        static Texture2D MapIcon()
        {
            if (mapIcon != null) return mapIcon;
            mapIcon = NewTex(out var px);
            var a = new Color32(76, 140, 200, 255);
            var b = new Color32(58, 112, 168, 255);
            var edge = new Color32(25, 40, 60, 255);
            Fill(px, 4, 8, 60, 50, edge);
            Fill(px, 6, 10, 23, 48, a);
            Fill(px, 23, 10, 41, 48, b);
            Fill(px, 41, 10, 58, 48, a);
            Fill(px, 8, 26, 56, 28, new Color32(230, 230, 210, 255)); // yol
            Disc(px, 32, 44, 11f, edge);
            Disc(px, 32, 44, 9f, new Color32(226, 72, 58, 255));
            Disc(px, 32, 44, 3.5f, new Color32(255, 255, 255, 255));
            mapIcon.SetPixels32(px); mapIcon.Apply();
            return mapIcon;
        }

        /// Kapalı kitap (kural klasörü)
        static Texture2D BookIcon()
        {
            if (bookIcon != null) return bookIcon;
            bookIcon = NewTex(out var px);
            var edge = new Color32(30, 25, 20, 255);
            Fill(px, 8, 4, 56, 60, edge);
            Fill(px, 10, 6, 54, 58, new Color32(200, 60, 45, 255));      // kapak
            Fill(px, 10, 6, 17, 58, new Color32(150, 40, 30, 255));      // sırt
            Fill(px, 50, 8, 54, 56, new Color32(240, 236, 220, 255));    // sayfa kenarı
            Fill(px, 24, 38, 46, 42, new Color32(245, 200, 90, 255));    // etiket
            Fill(px, 24, 30, 42, 33, new Color32(245, 200, 90, 255));
            bookIcon.SetPixels32(px); bookIcon.Apply();
            return bookIcon;
        }

        static Texture2D docIcon;
        /// Kâğıt belge (geri dön düğmesi)
        static Texture2D DocIcon()
        {
            if (docIcon != null) return docIcon;
            docIcon = NewTex(out var px);
            Fill(px, 12, 4, 52, 60, new Color32(240, 236, 220, 255));
            var line = new Color32(120, 120, 120, 255);
            for (int i = 0; i < 5; i++) Fill(px, 18, 44 - i * 8, 46, 46 - i * 8, line);
            Fill(px, 18, 8, 32, 14, new Color32(30, 140, 60, 255)); // mühür izi
            docIcon.SetPixels32(px); docIcon.Apply();
            return docIcon;
        }
    }
}
