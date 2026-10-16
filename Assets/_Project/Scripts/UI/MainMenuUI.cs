using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Başlangıç menüsü. BAŞLA -> Nasıl Oynanır açılır (oyun orada ✓ ile başlar).
    /// Bu bileşen her zaman aktif kalan bir objede durur; gizlenen kısım "root" paneldir.
    public class MainMenuUI : MonoBehaviour
    {
        public GameObject root;
        public Button startButton;
        public Button quitButton;
        public HowToPlayUI howToPlay;

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void Start()
        {
            startButton.onClick.AddListener(OnStartClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
#if UNITY_WEBGL && !UNITY_EDITOR
            quitButton.gameObject.SetActive(false); // tarayıcıda çıkış olmaz
#endif
            var gm = GameManager.Instance;
            OnStateChanged(gm != null ? gm.State : GameState.MainMenu);
        }

        void OnStateChanged(GameState s)
        {
            bool inMenu = s == GameState.MainMenu;
            if (!inMenu && howToPlay != null) howToPlay.Close();
            root.SetActive(inMenu && (howToPlay == null || !howToPlay.IsOpen));
        }

        void OnStartClicked()
        {
            root.SetActive(false);
            if (howToPlay != null) howToPlay.Open();
            else GameManager.Instance.StartGame();
        }

        /// Nasıl Oynanır'dan Esc / MENÜ ile dönüş
        public void ShowMenu() => root.SetActive(true);

        void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
