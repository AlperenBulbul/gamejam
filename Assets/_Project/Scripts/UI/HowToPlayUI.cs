using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    [Serializable]
    public class HowToPlayPage
    {
        public string title;
        [TextArea(3, 10)] public string body;
        public Sprite image;
    }

    /// Nasıl Oynanır (tasarımcı UI paketi, 18 Ekim): 3 sayfa, her sayfada tek orta görsel (yazılar görselin içinde).
    ///  Yerleşim 1920 × 1080 tasarımına göre: "NASIL OYNANIR" etiketi, "N Başlık" (numara gri), çizgi,
    ///  orta görsel 1620 × 713 (sol 150, üst 234), altta GERİ / sayfa no / İLERİ-BAŞLA.
    /// Sahnedeki eski düzen (menü 'UI kur') koddan bu yerleşime çevrilir; sahneyi yeniden kurmaya gerek yok.
    public class HowToPlayUI : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text titleText;
        public TMP_Text bodyText;
        public TMP_Text pageCounter;
        public Image pageImage;
        public Button prevButton;
        public Button nextButton;
        public Button doneButton;
        public Button menuButton;
        public MainMenuUI mainMenu;

        [Tooltip("Kullanılmıyor: sayfalar koddan gelir (DefaultPages)")]
        public HowToPlayPage[] pages =
        {
            new HowToPlayPage { title = "Sayfa 1" },
            new HowToPlayPage { title = "Sayfa 2" },
            new HowToPlayPage { title = "Sayfa 3" },
        };

        // Eski alanlar (sahnede kayıtlı olabilir)
        [HideInInspector] public float bodyTopWithImage = 490f;
        [HideInInspector] public float bodyTopWithoutImage = 190f;

        static readonly Color Bg = new Color32(0xF0, 0xE8, 0xDE, 0xFF);
        static readonly Color Ink = new Color32(0x22, 0x22, 0x22, 0xFF);
        static readonly Color Gold = new Color32(0x8A, 0x6A, 0x2E, 0xFF);
        static readonly Color Muted = new Color32(0x9A, 0x92, 0x86, 0xFF);

        int index;
        RectTransform layout, card;
        public bool IsOpen => root != null && root.activeSelf;

        void Awake()
        {
            pages = DefaultPages();
            BuildLayout();

            prevButton.onClick.AddListener(Prev);
            nextButton.onClick.AddListener(Next);
            doneButton.onClick.AddListener(Done);
            if (menuButton != null) menuButton.onClick.AddListener(BackToMenu);
            root.SetActive(false);
        }

        // ------------------------------------------------------------------ yerleşim (1920 × 1080)

        void BuildLayout()
        {
            card = titleText != null ? titleText.rectTransform.parent as RectTransform : null;
            if (card == null) return;

            // Kart tam ekran, düz bej zemin (tasarımda çerçeve yok)
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one;
            card.offsetMin = card.offsetMax = Vector2.zero;
            card.localRotation = Quaternion.identity;
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) cardImg.color = Bg;
            var keys = root != null ? root.transform.Find("KeysHint") : null;  // eski tuş satırı tasarımda yok
            if (keys != null) keys.gameObject.SetActive(false);

            // 1920 × 1080 tasarım alanı, ekrana oranı bozmadan sığar (LateUpdate)
            layout = new GameObject("Layout1080", typeof(RectTransform)).GetComponent<RectTransform>();
            layout.SetParent(card, false);
            layout.anchorMin = layout.anchorMax = new Vector2(0.5f, 0.5f);
            layout.pivot = new Vector2(0.5f, 0.5f);
            layout.sizeDelta = new Vector2(1920f, 1080f);
            FitLayout();

            var heavy = UiFonts.Heavy;
            var semi = UiFonts.SemiBold;

            // "NASIL OYNANIR"
            var tag = card.Find("Header") as RectTransform;
            if (tag != null)
            {
                Move(tag, 90f, 56f, 700f, 34f);
                var t = tag.GetComponent<TMP_Text>();
                if (t != null)
                {
                    t.fontSize = 24f; t.color = Gold; t.characterSpacing = 14f;
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                    if (semi != null) { t.font = semi; t.fontStyle = FontStyles.Normal; }
                }
            }

            // MENÜ (Esc)
            if (menuButton != null)
            {
                Move((RectTransform)menuButton.transform, 1647f, 46f, 203f, 60f);
                var l = menuButton.GetComponentInChildren<TMP_Text>();
                if (l != null)
                {
                    l.text = "MENÜ <color=#8A8478>(Esc)</color>";
                    l.fontSize = 24f; l.color = Ink; l.characterSpacing = 2f;
                    if (semi != null) { l.font = semi; l.fontStyle = FontStyles.Normal; }
                }
                var bi = menuButton.GetComponent<Image>();
                if (bi != null) bi.color = new Color32(0xE6, 0xDE, 0xD2, 0xFF);
            }

            // Başlık
            Move(titleText.rectTransform, 90f, 100f, 1500f, 86f);
            titleText.fontSize = 64f;
            titleText.color = Ink;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.characterSpacing = 0f;
            if (heavy != null) { titleText.font = heavy; titleText.fontStyle = FontStyles.Normal; }

            // Çizgi
            var rule = card.Find("Rule") as RectTransform;
            if (rule != null)
            {
                Move(rule, 90f, 199f, 1740f, 3f);
                var ri = rule.GetComponent<Image>();
                if (ri != null) ri.color = new Color32(0x22, 0x22, 0x22, 0xFF);
            }

            // Orta görsel
            if (pageImage != null)
            {
                Move(pageImage.rectTransform, 150f, 234f, 1620f, 713f);
                pageImage.preserveAspect = true;
            }
            if (bodyText != null) bodyText.gameObject.SetActive(false); // yazılar görselin içinde

            // Alt sıra
            if (prevButton != null) StyleButton(prevButton, 90f, 938f, 212f, 87f, "‹ GERİ", heavy);
            if (nextButton != null) StyleButton(nextButton, 1613f, 938f, 217f, 87f, "İLERİ ›", heavy);
            if (doneButton != null)
            {
                StyleButton(doneButton, 1595f, 938f, 235f, 87f, "BAŞLA", heavy);
                var tick = doneButton.transform.Find("Tick");
                if (tick != null) tick.gameObject.SetActive(false);
                var bi = doneButton.GetComponent<Image>();
                if (bi != null) bi.color = new Color32(0x3E, 0x8E, 0x55, 0xFF);
            }
            if (pageCounter != null)
            {
                Move(pageCounter.rectTransform, 860f, 966f, 200f, 44f);
                pageCounter.fontSize = 28f;
                pageCounter.color = Muted;
                pageCounter.alignment = TextAlignmentOptions.Center;
                if (semi != null) { pageCounter.font = semi; pageCounter.fontStyle = FontStyles.Normal; }
            }
        }

        void StyleButton(Button b, float x, float y, float w, float h, string text, TMP_FontAsset font)
        {
            var rt = (RectTransform)b.transform;
            Move(rt, x, y, w, h);
            var l = b.GetComponentInChildren<TMP_Text>(true);
            if (l == null) return;
            l.text = text;
            l.fontSize = 30f;
            l.characterSpacing = 3f;
            l.alignment = TextAlignmentOptions.Center;
            var lrt = l.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            if (font != null) { l.font = font; l.fontStyle = FontStyles.Normal; }
        }

        /// Elemanı 1920 × 1080 tasarım alanına taşı (x, y: sol üst köşe, piksel)
        void Move(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null || layout == null) return;
            rt.SetParent(layout, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        void LateUpdate() { if (IsOpen) FitLayout(); }

        void FitLayout()
        {
            if (layout == null || card == null) return;
            var size = card.rect.size;
            if (size.x < 1f || size.y < 1f) return;
            float k = Mathf.Min(size.x / 1920f, size.y / 1080f);
            layout.localScale = new Vector3(k, k, 1f);
        }

        // ------------------------------------------------------------------ sayfalar

        public void Open()
        {
            index = 0;
            root.SetActive(true);
            FitLayout();
            Show();
        }

        public void Close() => root.SetActive(false);

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Next();
            else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Prev();
            else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && IsLast) Done();
            else if (kb.escapeKey.wasPressedThisFrame) BackToMenu();
        }

        /// 3 sayfa: başlık + orta görsel (Resources/HowTo, tasarımcı). Görsel yoksa eski görsel + kısa yazı.
        static HowToPlayPage[] DefaultPages() => new[]
        {
            Page("Belgeyi incele", "HowTo/UI_HowTo_Step1_Middle", "Tutorial/tut_1",
                 "Formdaki <b><color=#4A86C8>HARİTA</color></b> ve <b><color=#C8503C>KURAL</color></b> düğmeleriyle arsayı ve kuralı kontrol et."),
            Page("Mühür bas", "HowTo/UI_HowTo_Step2_Middle", "Tutorial/tut_2",
                 "Kurala uyana <b><color=#5FA06A>ONAY</color></b>, uymayana <b><color=#C8503C>RET</color></b>."),
            Page("Şantiyeyi koru", "HowTo/UI_HowTo_Step3_Middle", "Tutorial/tut_3",
                 "Telsiz çalınca monitöre bak, müteahhidin hilesini yakala."),
        };

        static HowToPlayPage Page(string title, string image, string fallbackImage, string fallbackBody)
        {
            var sp = Img(image);
            return sp != null
                ? new HowToPlayPage { title = title, image = sp, body = "" }
                : new HowToPlayPage { title = title, image = Img(fallbackImage), body = fallbackBody };
        }

        static Sprite Img(string path)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;
            var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = System.IO.Path.GetFileName(path);
            return sp;
        }

        bool IsLast => pages == null || pages.Length == 0 || index >= pages.Length - 1;

        void Show()
        {
            int n = pages != null ? pages.Length : 0;
            var p = n > 0 ? pages[index] : new HowToPlayPage();

            titleText.text = $"<color=#9A9286>{index + 1}</color> {p.title}";

            bool hasImage = p.image != null;
            pageImage.gameObject.SetActive(hasImage);
            if (hasImage) { pageImage.sprite = p.image; pageImage.preserveAspect = true; }

            // Yedek (tasarım görseli yoksa): görselin altında kısa yazı
            bool body = !string.IsNullOrEmpty(p.body) && bodyText != null;
            if (bodyText != null)
            {
                bodyText.gameObject.SetActive(body);
                if (body)
                {
                    Move(bodyText.rectTransform, 150f, 850f, 1620f, 80f);
                    if (pageImage != null) Move(pageImage.rectTransform, 150f, 234f, 1620f, 600f);
                    bodyText.text = p.body;
                    bodyText.fontSize = 40f;
                    bodyText.alignment = TextAlignmentOptions.Center;
                }
                else if (pageImage != null) Move(pageImage.rectTransform, 150f, 234f, 1620f, 713f);
            }

            pageCounter.text = n > 0 ? $"{index + 1} / {n}" : "";
            prevButton.gameObject.SetActive(index > 0);
            nextButton.gameObject.SetActive(!IsLast);
            doneButton.gameObject.SetActive(IsLast);
        }

        void Next()
        {
            if (IsLast) return;
            index++;
            Show();
        }

        void Prev()
        {
            if (index <= 0) return;
            index--;
            Show();
        }

        void Done()
        {
            Close();
            GameManager.Instance.StartGame();
        }

        void BackToMenu()
        {
            Close();
            if (mainMenu != null) mainMenu.ShowMenu();
        }
    }
}
