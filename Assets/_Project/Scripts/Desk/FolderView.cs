using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Mühür izi (yoksa çalışınca kendisi oluşturur)")]
        public RectTransform stampArea;
        public GameObject stampMark;
        public TMP_Text stampText;
        public Color approveInk = new Color(0.12f, 0.55f, 0.22f, 0.9f);
        public Color rejectInk = new Color(0.78f, 0.12f, 0.10f, 0.9f);

        public CustomerData Current { get; private set; }
        /// Bu dosyaya mühür basıldı mı (aynı dosyaya ikinci kez basılamaz)
        public bool Decided { get; private set; }

        /// Mührün değeceği nokta: KARAR kutusunun ortası
        public Vector3 StampAreaWorld => stampArea != null
            ? stampArea.TransformPoint(stampArea.rect.center)
            : transform.position + Vector3.up * 0.016f;

        void Awake()
        {
            EnsureStampMark();
            HideImmediate();
        }

        void EnsureStampMark()
        {
            if (stampArea == null && content != null)
                stampArea = content.transform.Find("Form/StampArea") as RectTransform;
            if (stampMark != null || stampArea == null) return;

            // Kutu içinde eğik, çerçeveli mühür yazısı
            var root = new GameObject("StampMark", typeof(RectTransform)).transform as RectTransform;
            root.SetParent(stampArea, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(150f, 38f);
            root.localRotation = Quaternion.Euler(0f, 0f, 6f);
            for (int i = 0; i < 4; i++)
            {
                var bar = new GameObject($"Edge{i}", typeof(RectTransform), typeof(Image)).transform as RectTransform;
                bar.SetParent(root, false);
                bool horizontal = i < 2;
                bar.anchorMin = horizontal ? new Vector2(0, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 0);
                bar.anchorMax = horizontal ? new Vector2(1, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 1);
                bar.sizeDelta = horizontal ? new Vector2(0, 2.4f) : new Vector2(2.4f, 0);
                bar.GetComponent<Image>().raycastTarget = false;
            }
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(root, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.fontSize = 19f; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
            t.characterSpacing = 4f; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            stampMark = root.gameObject;
            stampText = t;
            stampMark.SetActive(false);
        }

        /// StampController karar anında çağırır
        public void MarkDecided(bool approved)
        {
            Decided = true;
            if (stampMark == null) return;
            var ink = approved ? approveInk : rejectInk;
            stampText.text = approved ? "ONAYLANDI" : "REDDEDİLDİ";
            foreach (var g in stampMark.GetComponentsInChildren<Graphic>(true)) g.color = ink;
            stampMark.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-9f, 9f));
            stampMark.SetActive(true);
        }

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
            Decided = false;
            if (stampMark != null) stampMark.SetActive(false);
            Fill(c);

            // Bu müşteriye bu oyunda zaten karar verildiyse mühür izi dosyada kalır
            var run = GameManager.Instance != null ? GameManager.Instance.Run : null;
            if (run != null)
                for (int i = run.decisions.Count - 1; i >= 0; i--)
                    if (run.decisions[i].customerId == c.id) { MarkDecided(run.decisions[i].approved); break; }
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
            Decided = false;
            if (stampMark != null) stampMark.SetActive(false);
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
