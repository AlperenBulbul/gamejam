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

        [Header("Tasarım modelleri (boşsa greybox davranışı: form her zaman görünür)")]
        [Tooltip("Masada görünen kapalı dosya (SM_Folder)")]
        public GameObject closedModel;
        [Tooltip("Yakın bakışta görünen açık dosya (SM_Folder_Open)")]
        public GameObject openModel;
        [Tooltip("Açık dosyanın sayfasındaki başvuru formu (sadece açıkken görünür)")]
        public GameObject formRoot;
        [Tooltip("Kapak üstündeki mühür izi (T_StampMark görselleri)")]
        public Image coverStamp;
        public Sprite approveSprite;
        public Sprite rejectSprite;

        public bool IsOpen { get; private set; }

        [Header("Mühür izi (yoksa çalışınca kendisi oluşturur)")]
        public RectTransform stampArea;
        public GameObject stampMark;
        public TMP_Text stampText;
        public Color approveInk = new Color(0.12f, 0.55f, 0.22f, 0.9f);
        public Color rejectInk = new Color(0.78f, 0.12f, 0.10f, 0.9f);

        public CustomerData Current { get; private set; }
        /// Bu dosyaya mühür basıldı mı (aynı dosyaya ikinci kez basılamaz)
        public bool Decided { get; private set; }
        /// Müşteri dosyasını geri alıyor (artık mühür basılamaz)
        public bool Leaving { get; private set; }

        /// Mührün değeceği nokta: kapak varsa kapağın ortası, yoksa formdaki KARAR kutusu
        public Vector3 StampAreaWorld => coverStamp != null
            ? coverStamp.rectTransform.position
            : stampArea != null
                ? stampArea.TransformPoint(stampArea.rect.center)
                : transform.position + Vector3.up * 0.016f;

        /// Açık dosyadaki formun KARAR kutusunun ortası (yakın bakışta mühür buraya basılır)
        public Vector3 FormStampWorld => stampArea != null ? stampArea.TransformPoint(stampArea.rect.center) : StampAreaWorld;

        /// Başvuru formunun RectTransform'u
        public RectTransform FormRect => formRoot != null ? formRoot.transform as RectTransform
                                        : (content != null ? content.transform.Find("Form") as RectTransform : null);

        /// Yakın bakış: tam tepeden, form sayfası ve iki mühür birlikte görünsün
        void Start()
        {
            if (interactable == null || interactable.focusPoint == null) return;
            var form = FormRect;
            if (form == null) return;

            var corners = new Vector3[4];
            form.GetWorldCorners(corners);
            var b = new Bounds(corners[0], Vector3.zero);
            foreach (var c in corners) b.Encapsulate(c);
            var sc = StampController.Instance;
            if (sc != null)
            {
                foreach (var st in new[] { sc.approveStamp, sc.rejectStamp })
                    if (st != null) { b.Encapsulate(st.HomePos + new Vector3(0.05f, 0f, 0.05f)); b.Encapsulate(st.HomePos - new Vector3(0.05f, 0f, 0.05f)); }
            }
            float h = Mathf.Max(b.size.z, b.size.x / 1.7f) * 0.5f / Mathf.Tan(35f * Mathf.Deg2Rad) * 1.08f;
            interactable.focusPoint.position = new Vector3(b.center.x, form.position.y + Mathf.Max(0.2f, h), b.center.z);
            var up = form.up; up.y = 0f; // formun üst kenarı ekranın üstüne gelsin
            interactable.focusPoint.rotation = Quaternion.LookRotation(Vector3.down, up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward);
        }

        /// Yakın bakışta açık, masada kapalı dosya
        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (closedModel == null || openModel == null) return; // greybox: değişiklik yok
            closedModel.SetActive(!open);
            openModel.SetActive(open);
            if (formRoot != null) formRoot.SetActive(open);
        }

        void OnFocusTransition(FocusState from, FocusState to)
        {
            if (to == FocusState.Folder) SetOpen(true);
            else if (from == FocusState.Folder) SetOpen(false);
        }

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
            if (stampMark == null) { ShowCoverStamp(approved); return; }
            var ink = approved ? approveInk : rejectInk;
            stampText.text = approved ? "ONAYLANDI" : "REDDEDİLDİ";
            foreach (var g in stampMark.GetComponentsInChildren<Graphic>(true)) g.color = ink;
            stampMark.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-9f, 9f));
            stampMark.SetActive(true);
            ShowCoverStamp(approved);
        }

        void ShowCoverStamp(bool approved)
        {
            if (coverStamp == null) return;
            var sp = approved ? approveSprite : rejectSprite;
            if (sp != null) { coverStamp.sprite = sp; coverStamp.color = Color.white; }
            else coverStamp.color = approved ? approveInk : rejectInk;
            coverStamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-10f, 10f));
            coverStamp.gameObject.SetActive(true);
        }

        void OnEnable()
        {
            GameEvents.CustomerArrived += Show;
            GameEvents.FocusTransitionStarted += OnFocusTransition;
            GameEvents.CustomerLeaving += OnCustomerLeaving;
            GameEvents.CustomerLeft += OnCustomerLeft;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= Show;
            GameEvents.FocusTransitionStarted -= OnFocusTransition;
            GameEvents.CustomerLeaving -= OnCustomerLeaving;
            GameEvents.CustomerLeft -= OnCustomerLeft;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void OnCustomerLeft(CustomerData c, bool angry) { if (c == Current) Hide(); }

        /// Müşteri dosyasını geri alıyor: gişeye doğru kayarak kaybolur
        void OnCustomerLeaving(CustomerData c)
        {
            if (c != Current) return;
            Leaving = true;
            var fc = FocusController.Instance;
            if (fc != null && fc.State == FocusState.Folder) fc.Back();
            if (interactable != null) interactable.enabled = false;
            StopAllCoroutines();
            StartCoroutine(SlideOutAndHide());
        }

        IEnumerator SlideOutAndHide()
        {
            var t = content.transform;
            Vector3 from = t.localPosition, to = slideFromOffset;
            float time = slideTime * 0.8f;
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                float k = Mathf.Pow(e / time, 2f); // hızlanarak gider
                t.localPosition = Vector3.Lerp(from, to, k);
                yield return null;
            }
            HideImmediate();
        }

        void OnStateChanged(GameState s)
        {
            if (s != GameState.Working) Hide();
        }

        public void Show(CustomerData c)
        {
            if (c == null) return;
            Current = c;
            Decided = false;
            Leaving = false;
            if (stampMark != null) stampMark.SetActive(false);
            if (coverStamp != null) coverStamp.gameObject.SetActive(false);
            SetOpen(false);
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
            Leaving = false;
            if (stampMark != null) stampMark.SetActive(false);
            if (coverStamp != null) coverStamp.gameObject.SetActive(false);
            SetOpen(false);
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
