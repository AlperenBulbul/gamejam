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

        [Tooltip("Sayfa ekle/çıkar, başlık ve metinleri buradan doldur")]
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
            // Sayfalar boşsa (Inspector'da henüz doldurulmadıysa) varsayılan anlatımı kullan
            bool empty = pages == null || pages.Length == 0;
            if (!empty)
            {
                empty = true;
                foreach (var pg in pages) if (pg != null && !string.IsNullOrEmpty(pg.body)) { empty = false; break; }
            }
            if (empty) pages = DefaultPages();

            prevButton.onClick.AddListener(Prev);
            nextButton.onClick.AddListener(Next);
            doneButton.onClick.AddListener(Done);
            if (menuButton != null) menuButton.onClick.AddListener(BackToMenu);
            root.SetActive(false);
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

        /// Şu ana kadarki oynanışın anlatımı. Ekip Inspector'dan "pages" listesini doldurursa onlar kullanılır.
        static HowToPlayPage[] DefaultPages() => new[]
        {
            new HowToPlayPage { title = "Hoş geldin, denetçi",
                body = "Karaova Belediyesi Yapı Denetim Müdürlüğü'ndesin.\n\n" +
                       "Gişeye gelen vatandaşların inşaat başvurularını incele, her birine <b>ONAY</b> ya da <b>RET</b> mührü bas.\n\n" +
                       "Oyun iki gün sürer. Onayladığın binalar şantiyede yükselir ve sonunda bir depremle sınanır." },
            new HowToPlayPage { title = "Kontroller",
                body = "<b>Fare</b>: etrafa bak\n" +
                       "<b>Sol tık</b>: nesneyi aç / incele\n" +
                       "<b>Sağ tık / Esc</b>: geri dön\n\n" +
                       "Sağ alttaki kısa liste, o an baktığın yerde hangi tuşların çalıştığını gösterir.\n" +
                       "Turuncu yanıp sönen çerçeveler sıradaki adımı gösterir." },
            new HowToPlayPage { title = "Güne başla",
                body = "Gün başında masadaki <b>gazeteyi</b> aç ve <b>GÜNE BAŞLA</b>'ya bas (Enter).\n\n" +
                       "Müşteriler sırayla gişeye gelir ve dosyalarını masana bırakır.\n\n" +
                       "Müşteriyi çok bekletirsen sinirlenir; sabrı biterse çekip gider ve <b>ceza</b> alırsın." },
            new HowToPlayPage { title = "Belgeyi incele",
                body = "<b>Dosyaya</b> tıkla: mal sahibi, ada numarası, arsa alanı, istenen kat, kullanım, mühendis kaşesi ve zemin etüdü yazar.\n\n" +
                       "Başvurunun kurallara uyup uymadığını anlamak için kural klasörüne ve haritaya bakman gerekir." },
            new HowToPlayPage { title = "Kurallar ve harita",
                body = "<b>Kural klasörü</b>: hangi başvurunun reddedileceğini söyler. Tekerlek ya da A / D ile sayfa çevir. 2. gün yeni kurallar eklenir.\n\n" +
                       "<b>Harita</b>: dosyadaki ada numarasıyla arsayı bul. Tekerlekle yakınlaş.\n" +
                       "<b>Boşluk</b>: tehlikeli bölgeyi (nehir yatağı ve fay hattı) göster / gizle. Açıkken numaralar zor okunur." },
            new HowToPlayPage { title = "Karar ver",
                body = "Dosya açıkken bir <b>mühre</b> tıkla, sonra formun üstüne tıkla.\n\n" +
                       "Kurala uymayanı <b>reddet</b>, uyanı <b>onayla</b>. Kurallara uyan bir başvuruyu reddetmek de hatadır (<b>haksız ret</b>).\n\n" +
                       "3 ceza ya da 3 haksız ret: kovulursun. Masaya bırakılan zarflara dikkat." },
            new HowToPlayPage { title = "Şantiyeleri izle",
                body = "Onayladığın her proje bir şantiye olur. Masadaki <b>monitörden</b> izle.\n\n" +
                       "Ekranın altındaki <b>ok düğmeleri</b> (ya da A / D) kanal değiştirir.\n" +
                       "Işıklar: <b>yeşil</b> çalışıyor, <b>sarı yanıp sönen</b> şantiyede hatalı işçi var.\n\n" +
                       "Düzeltilmeyen hatalar binaya kusur olarak işler. Hazırsan başla!" },
        };

        bool IsLast => pages == null || pages.Length == 0 || index >= pages.Length - 1;

        void Show()
        {
            int n = pages != null ? pages.Length : 0;
            var p = n > 0 ? pages[index] : new HowToPlayPage();

            titleText.text = p.title ?? "";
            bodyText.text = p.body ?? "";

            bool hasImage = p.image != null;
            pageImage.gameObject.SetActive(hasImage);
            if (hasImage) pageImage.sprite = p.image;
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
