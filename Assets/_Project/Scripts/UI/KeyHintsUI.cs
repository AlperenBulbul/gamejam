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
        void OnStateChanged(GameState _) => Refresh();
        void OnFocusChanged(FocusState _) => Refresh();

        void Refresh()
        {
            var gm = GameManager.Instance;
            bool show = gm != null && gm.DeskInputEnabled;
            root.SetActive(show);
            if (!show) return;

            var focus = FocusController.Instance != null ? FocusController.Instance.State : FocusState.Desk;
            text.text = Build(Hints(gm.State, focus));
        }

        static (string key, string action)[] Hints(GameState state, FocusState focus)
        {
            switch (focus)
            {
                case FocusState.Folder:
                    return new[] { ("Sağ tık / Esc", "Geri") };
                case FocusState.Map:
                    return new[] { ("Tekerlek", "Yakınlaştır"), ("Sağ tık / Esc", "Geri") };
                case FocusState.Binder:
                    return new[] { ("Tekerlek / Oklar", "Sayfa çevir"), ("Sağ tık / Esc", "Geri") };
                case FocusState.Monitor:
                    return new[] { ("Sol tık", "İşçiyi düzelt"), ("A / D", "Kanal"), ("1-7", "Kanal seç"), ("Sağ tık / Esc", "Geri") };
                case FocusState.HoldingStamp:
                    return new[] { ("Sol tık dosya", "Mühür bas"), ("Sağ tık", "Mührü bırak") };
                default: // Desk
                    return new[] { ("Fare", "Etrafa bak"), ("Sol tık", "Seç / incele"), ("Mühür + dosya", "Karar ver") };
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
