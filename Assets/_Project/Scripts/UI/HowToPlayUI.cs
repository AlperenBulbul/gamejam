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

    /// Nasıl Oynanır sayfaları. İçerik Inspector'dan "pages" listesine yazılır.
    /// Son sayfada ✓ BAŞLA düğmesi oyunu başlatır.
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

        [Tooltip("Kullanılmıyor: sayfalar koddan gelir (DefaultPages, 3 görselli sayfa)")]
        public HowToPlayPage[] pages =
        {
            new HowToPlayPage { title = "Sayfa 1" },
            new HowToPlayPage { title = "Sayfa 2" },
            new HowToPlayPage { title = "Sayfa 3" },
            new HowToPlayPage { title = "Sayfa 4" },
        };

        [Header("Görsel varken metin aşağı kayar")]
        public float bodyTopWithImage = 490f;
        public float bodyTopWithoutImage = 190f;

        int index;
        public bool IsOpen => root != null && root.activeSelf;

        void Awake()
        {
            // Kısa, görselli 3 sayfa (minigame tanıtımı gibi: bak, anla, geç)
            pages = DefaultPages();
            EnlargeLayout();

            prevButton.onClick.AddListener(Prev);
            nextButton.onClick.AddListener(Next);
            doneButton.onClick.AddListener(Done);
            if (menuButton != null) menuButton.onClick.AddListener(BackToMenu);
            root.SetActive(false);
        }

        /// Okunabilirlik (jüri): kart ekranın çoğunu kaplar, görsel ve yazılar büyür.
        /// Sahnedeki düzen (menü 'UI kur') koddan büyütülür, sahneyi yeniden kurmaya gerek yok.
        void EnlargeLayout()
        {
            var card = titleText != null ? titleText.rectTransform.parent as RectTransform : null;
            if (card == null) return;
            card.sizeDelta = new Vector2(1680f, 900f);
            card.anchoredPosition = new Vector2(0f, 30f);

            titleText.fontSize = 60f;
            var rule = card.Find("Rule") as RectTransform;
            if (rule != null) rule.sizeDelta = new Vector2(card.sizeDelta.x - 112f, rule.sizeDelta.y);

            // Görsel: başlık çizgisinin altından 460 px yükseklik (eskisi 280)
            var img = pageImage != null ? pageImage.rectTransform : null;
            if (img != null)
            {
                img.anchorMin = new Vector2(0f, 1f); img.anchorMax = new Vector2(1f, 1f); img.pivot = new Vector2(0.5f, 1f);
                img.offsetMin = new Vector2(56f, -650f); img.offsetMax = new Vector2(-56f, -190f);
            }

            // Metin görselin altında, düğmelerin üstünde
            bodyTopWithImage = 668f;
            if (bodyText != null)
            {
                var b = bodyText.rectTransform;
                b.offsetMin = new Vector2(80f, 124f);
                b.offsetMax = new Vector2(-80f, b.offsetMax.y);
            }

            foreach (var btn in new[] { prevButton, nextButton, doneButton })
            {
                if (btn == null) continue;
                var rt = (RectTransform)btn.transform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x + 40f, 74f);
                var l = btn.GetComponentInChildren<TMP_Text>();
                if (l != null) l.fontSize = 30f;
            }
            if (pageCounter != null) pageCounter.fontSize = 30f;
            if (menuButton != null)
            {
                var rt = (RectTransform)menuButton.transform;
                rt.sizeDelta = new Vector2(210f, 52f);
                var l = menuButton.GetComponentInChildren<TMP_Text>();
                if (l != null) l.fontSize = 24f;
            }
        }

        public void Open()
        {
            index = 0;
            root.SetActive(true);
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

        /// 3 sayfa: görsel + tek kısa cümle. Görseller Resources/Tutorial altında.
        static HowToPlayPage[] DefaultPages() => new[]
        {
            new HowToPlayPage { title = "1 · Belgeyi incele", image = Img("tut_1"),
                body = "Formdaki <b><color=#4A86C8>HARİTA</color></b> ve <b><color=#C8503C>KURAL</color></b> düğmeleriyle arsayı ve kuralı kontrol et." },
            new HowToPlayPage { title = "2 · Mühür bas", image = Img("tut_2"),
                body = "Kurala uyana <b><color=#5FA06A>ONAY</color></b>, uymayana <b><color=#C8503C>RET</color></b>.\n<size=80%><color=#6B6458>Uygun başvuruyu reddetmek de hatadır.</color></size>" },
            new HowToPlayPage { title = "3 · Şantiyeyi koru", image = Img("tut_3"),
                body = "Telsiz cızırdayınca monitöre bak, müteahhidin hilesini yakala." },
        };

        static Sprite Img(string name)
        {
            var tex = Resources.Load<Texture2D>("Tutorial/" + name);
            if (tex == null) return null;
            var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sp.name = name;
            return sp;
        }

        bool IsLast => pages == null || pages.Length == 0 || index >= pages.Length - 1;

        void Show()
        {
            int n = pages != null ? pages.Length : 0;
            var p = n > 0 ? pages[index] : new HowToPlayPage();

            titleText.text = p.title ?? "";
            bodyText.text = p.body ?? "";

            bool hasImage = p.image != null;
            pageImage.gameObject.SetActive(hasImage);
            if (hasImage) { pageImage.sprite = p.image; pageImage.preserveAspect = true; }
            titleText.alignment = TextAlignmentOptions.Center;
            bodyText.alignment = TextAlignmentOptions.Top;
            bodyText.fontSize = 40f;
            bodyText.enableAutoSizing = true; bodyText.fontSizeMin = 30f; bodyText.fontSizeMax = 40f;
            bodyText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            var bodyRt = bodyText.rectTransform;
            bodyRt.offsetMax = new Vector2(bodyRt.offsetMax.x, -(hasImage ? bodyTopWithImage : bodyTopWithoutImage));

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
