using System.Text;
using TMPro;
using UnityEngine;

namespace LoadBearing
{
    /// Ekranın sağ altında kısa tuş listesi. Nereye bakıldığına göre değişir.
    /// Metinleri değiştirmek için aşağıdaki Hints() fonksiyonunu düzenle.
    public class KeyHintsUI : MonoBehaviour
    {
        public GameObject root;
        public TMP_Text text;
        public Color keyColor = new Color(0.95f, 0.70f, 0.20f);

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.FocusChanged += OnFocusChanged;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.FocusChanged -= OnFocusChanged;
        }

        void Start() => Refresh();

        string lastKey;

        // Mühür alınıp bırakılınca da güncellensin (olay yok, ucuz kontrol)
        void Update()
        {
            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            var sc = StampController.Instance;
            string key = $"{(gm != null ? gm.State.ToString() : "-")}|{(fc != null ? fc.State.ToString() : "-")}|{(sc != null && sc.IsHolding)}|{OverlayVisible}";
            if (key != lastKey) { lastKey = key; Refresh(); }
        }
        void OnStateChanged(GameState _) => Refresh();
        void OnFocusChanged(FocusState _) => Refresh();

        void Refresh()
        {
            var gm = GameManager.Instance;
            bool show = gm != null && gm.DeskInputEnabled;
            root.SetActive(show);
            if (!show) return;

            var focus = FocusController.Instance != null ? FocusController.Instance.State : FocusState.Desk;
            bool holding = StampController.Instance != null && StampController.Instance.IsHolding;
            text.text = Build(Hints(gm.State, focus, holding));
        }

        static MapView map;
        static bool OverlayVisible
        {
            get
            {
                if (map == null) map = Object.FindAnyObjectByType<MapView>();
                return map != null && map.OverlayVisible;
            }
        }

        static (string key, string action)[] Hints(GameState state, FocusState focus, bool holding)
        {
            switch (focus)
            {
                case FocusState.Folder:
                    return holding
                        ? new[] { ("Sol tık form", "Mühür bas"), ("Sağ tık", "Mührü bırak") }
                        : new[] { ("Sol tık mühür", "Mührü al"), ("Sağ tık / Esc", "Geri") };
                case FocusState.Map:
                    return new[] { ("Tekerlek", "Yakınlaştır"),
                                   ("Boşluk", OverlayVisible ? "Nehir yatağı ve fay hattını gizle" : "Nehir yatağı ve fay hattını göster"),
                                   ("Sağ tık / Esc", "Geri") };
                case FocusState.Binder:
                    return new[] { ("Tekerlek / Oklar", "Sayfa çevir"), ("Sağ tık / Esc", "Geri") };
                case FocusState.Monitor:
                    return new[] { ("Monitör < > / A D", "Kanal değiştir"), ("1-7", "Kanal seç"), ("Sağ tık / Esc", "Geri") };
                case FocusState.Newspaper:
                    return state == GameState.DayIntro
                        ? new[] { ("Güne Başla / Enter", "Mesaiyi başlat"), ("Sağ tık / Esc", "Geri") }
                        : new[] { ("Sağ tık / Esc", "Geri") };
                case FocusState.HoldingStamp:
                    return new[] { ("Sol tık dosya", "Mühür bas"), ("Sağ tık", "Mührü bırak") };
                default: // Desk
                    return state == GameState.DayIntro
                        ? new[] { ("Fare", "Etrafa bak"), ("Gazete", "Oku ve güne başla") }
                        : new[] { ("Fare", "Etrafa bak"), ("Sol tık", "Seç / incele"), ("Dosyayı aç", "Mühürle karar ver") };
            }
        }

        string Build((string key, string action)[] hints)
        {
            string hex = ColorUtility.ToHtmlStringRGB(keyColor);
            var sb = new StringBuilder();
            for (int i = 0; i < hints.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append($"<color=#{hex}><b>{hints[i].key}</b></color>  {hints[i].action}");
            }
            return sb.ToString();
        }
    }
}
