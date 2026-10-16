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
