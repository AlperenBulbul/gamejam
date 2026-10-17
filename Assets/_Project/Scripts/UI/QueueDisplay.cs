using System.Collections;
using TMPro;
using UnityEngine;

namespace LoadBearing
{
    /// Müşteri odasının arka duvarındaki LED sıra ekranı (tasarım teslimi 2).
    /// Her müşteri gişeye gelince numara değişir (C1 = 001 ... C7 = 007) ve kısa bir süre yanıp söner.
    public class QueueDisplay : MonoBehaviour
    {
        public TMP_Text number;
        public float blinkTime = 0.9f;

        Coroutine blink;

        void OnEnable()
        {
            GameEvents.CustomerArrived += OnArrived;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= OnArrived;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void Start() => Show(0, false);

        void OnStateChanged(GameState s)
        {
            if (s == GameState.MainMenu) Show(0, false);
        }

        void OnArrived(CustomerData c)
        {
            int n = 0;
            if (c != null && c.id != null)
                foreach (char ch in c.id) if (char.IsDigit(ch)) n = n * 10 + (ch - '0');
            Show(n, true);
        }

        void Show(int n, bool flash)
        {
            if (number == null) return;
            number.text = n.ToString("000");
            if (blink != null) StopCoroutine(blink);
            number.enabled = true;
            if (flash && isActiveAndEnabled) blink = StartCoroutine(Blink());
        }

        IEnumerator Blink()
        {
            for (float t = 0f; t < blinkTime; t += 0.15f)
            {
                number.enabled = !number.enabled;
                yield return new WaitForSeconds(0.15f);
            }
            number.enabled = true;
            blink = null;
        }
    }
}
