using System.Collections;
using TMPro;
using UnityEngine;

namespace LoadBearing
{
    /// Masadaki başvuru dosyası. Müşteri gelince (CustomerArrived) gişeden masaya kayar ve
    /// formu müşteri verisiyle doldurur; müşteri gidince (CustomerLeft) kaybolur.
    public class FolderView : MonoBehaviour
    {
        [Header("Bağlantılar")]
        public GameObject content;          // dosyanın tamamı (görsel + form)
        public Interactable interactable;   // FolderSpot'un tıklanabilirliği

        [Header("Form alanları")]
        public TMP_Text appNo;
        public TMP_Text owner;
        public TMP_Text adaParsel;
        public TMP_Text area;
        public TMP_Text floors;
        public TMP_Text use;
        public TMP_Text license;
        public TMP_Text soil;

        [Header("Kayarak gelme")]
        public Vector3 slideFromOffset = new Vector3(0f, 0.06f, 0.45f); // gişe tarafından
        public float slideTime = 0.45f;

        public CustomerData Current { get; private set; }

        void Awake() => HideImmediate();

        void OnEnable()
        {
            GameEvents.CustomerArrived += Show;
            GameEvents.CustomerLeft += OnCustomerLeft;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= Show;
            GameEvents.CustomerLeft -= OnCustomerLeft;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void OnCustomerLeft(CustomerData c, bool angry) { if (c == Current) Hide(); }

        void OnStateChanged(GameState s)
        {
            if (s != GameState.Working && s != GameState.FinalShift) Hide();
        }

        public void Show(CustomerData c)
        {
            if (c == null) return;
            Current = c;
            Fill(c);
            content.SetActive(true);
            if (interactable != null) interactable.enabled = true;
            StopAllCoroutines();
            StartCoroutine(SlideIn());
        }

        public void Hide()
        {
            var fc = FocusController.Instance;
            if (fc != null && fc.State == FocusState.Folder) fc.Back();
            HideImmediate();
        }

        void HideImmediate()
        {
            Current = null;
            StopAllCoroutines();
            if (content != null) { content.SetActive(false); content.transform.localPosition = Vector3.zero; }
            if (interactable != null) interactable.enabled = false;
        }

        void Fill(CustomerData c)
        {
            int day = GameManager.Instance != null ? GameManager.Instance.Run.day : c.day;

            appNo.text = $"No: 2026/{c.id}";
            owner.text = c.displayName;
            adaParsel.text = $"{c.ada} / {c.parsel}";
            area.text = $"{c.areaM2} m²";
            floors.text = c.floors.ToString();
            use.text = c.use;
            license.text = c.licenseNo;

            // 1. günde zemin etüdü alanı boş (GDD): kural 2. gün geliyor
            if (day < 2) soil.text = "—";
            else soil.text = c.soil switch
            {
                SoilType.Solid => "Ekli · Sağlam zemin",
                SoilType.Soft => "Ekli · Yumuşak zemin",
                _ => "Ekli değil",
            };
        }

        IEnumerator SlideIn()
        {
            var t = content.transform;
            Vector3 from = slideFromOffset, to = Vector3.zero;
            for (float e = 0f; e < slideTime; e += Time.deltaTime)
            {
                float k = 1f - Mathf.Pow(1f - e / slideTime, 3f); // yavaşlayarak dur
                t.localPosition = Vector3.Lerp(from, to, k);
                yield return null;
            }
            t.localPosition = to;
        }
    }
}
