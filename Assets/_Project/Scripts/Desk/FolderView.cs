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

        [Header("Yakın bakış kamerası")]
        [Tooltip("Ne kadar yüksekten bakılsın (1 = form + mühürler kadrajı tam doldurur)")]
        public float focusHeightScale = 1.5f;
        [Tooltip("Tam tepeden ne kadar eğik bakılsın (derece). 0 = tam tepeden")]
        [Range(0f, 45f)] public float focusTilt = 22f;

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

        public static FolderView Instance { get; private set; }
        /// Açık bir belge var mı (harita / kural klasöründen "Belgeye dön" için)
        public static bool DocOpen => Instance != null && Instance.Current != null && !Instance.Leaving;

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
        public RectTransform StampAreaRect => stampArea;
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
            // Kadraj formun ortasında kalsın (mühürler yana düşüyor diye kamera kaymasın); mühürler sığacak kadar geniş
            var fb = new Bounds(corners[0], Vector3.zero);
            foreach (var c in corners) fb.Encapsulate(c);
            float hx = Mathf.Max(b.max.x - fb.center.x, fb.center.x - b.min.x);
            float hz = Mathf.Max(b.max.z - fb.center.z, fb.center.z - b.min.z);
            b = new Bounds(fb.center, new Vector3(hx * 2f, b.size.y, hz * 2f));
            float dist = Mathf.Max(0.2f, Mathf.Max(b.size.z, b.size.x / 1.7f) * 0.5f / Mathf.Tan(35f * Mathf.Deg2Rad) * 1.08f * focusHeightScale);
            var up = form.up; up.y = 0f; // formun üst kenarı ekranın üstüne gelsin
            up = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward;

            // Hafif eğik: kamera oyuncu tarafına doğru yatar, mühürler yandan da görünür
            float t = focusTilt * Mathf.Deg2Rad;
            var center = new Vector3(b.center.x, form.position.y, b.center.z);
            var dir = Vector3.up * Mathf.Cos(t) - up * Mathf.Sin(t);   // merkezden kameraya
            interactable.focusPoint.position = center + dir * dist;
            interactable.focusPoint.rotation = Quaternion.LookRotation(-dir, up);
        }

        /// Yakın bakışta açık, masada kapalı dosya
        [Tooltip("Açık dosya masadan ne kadar yukarıda dursun (metre). Açılınca genişleyip haritanın üstüne taşıyor; içine girmesin diye.")]
        public float openLift = 0.008f;

        Vector3 openHome, formHome;
        bool homesSaved;

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (closedModel == null || openModel == null) return; // greybox: değişiklik yok
            if (!homesSaved)
            {
                openHome = openModel.transform.localPosition;
                if (formRoot != null) formHome = formRoot.transform.localPosition;
                homesSaved = true;
            }
            // Açık dosya haritanın üstünde dursun: model ve form birlikte biraz yukarı
            var lift = openModel.transform.parent != null ? openModel.transform.parent.InverseTransformVector(Vector3.up * openLift) : Vector3.up * openLift;
            openModel.transform.localPosition = openHome + (open ? lift : Vector3.zero);
            if (formRoot != null)
            {
                var fl = formRoot.transform.parent != null ? formRoot.transform.parent.InverseTransformVector(Vector3.up * openLift) : Vector3.up * openLift;
                formRoot.transform.localPosition = formHome + (open ? fl : Vector3.zero);
            }
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
            // Form: "ADA" etiketi -> "PARSEL"; mühendis kaşesi satırı kaldırıldı, zemin etüdü onun yerine çıkar
            TMP_Text kaseLabel = null, soilLabel = null;
            if (content != null)
                foreach (var t in content.GetComponentsInChildren<TMP_Text>(true))
                {
                    // Etiketler okunur olsun (jüri): koyu gri, yarı kalın, ~%8 büyük
                    if (t.name.StartsWith("L_"))
                    {
                        t.color = new Color32(0x26, 0x26, 0x26, 0xFF);
                        t.fontStyle = FontStyles.Normal;
                        t.fontWeight = FontWeight.SemiBold;
                        t.fontSize *= 1.08f;
                    }
                    // Antet, başvuru no ve KARAR kutusu başlığı da koyulaşsın
                    else if (t.name == "Org" || t.name == "AppNo" || t.text == "KARAR")
                        t.color = new Color32(0x33, 0x33, 0x33, 0xFF);
                    if (t.text == "ADA / PARSEL" || t.text == "ADA" || t.text == "PARSEL") t.text = "ARSA NO";
                    else if (t.text == "MÜHENDİS KAŞESİ") kaseLabel = t;
                    else if (t.text == "ZEMİN ETÜDÜ") soilLabel = t;
                }
            if (kaseLabel != null && soilLabel != null && license != null && soil != null)
            {
                kaseLabel.text = soilLabel.text;
                soilLabel.gameObject.SetActive(false);
                var oldLine = RowLine(soilLabel);          // boş kalan son satırın çizgisi de gitsin
                if (oldLine != null) oldLine.SetActive(false);
                license.color = soil.color;
                license.fontStyle = soil.fontStyle;
                soil.gameObject.SetActive(false);
                soil = license;   // zemin etüdü artık kaşe satırında
                license = null;
                soilRowLabel = kaseLabel;
            }
            else soilRowLabel = soilLabel;
            soilRowLine = RowLine(soilRowLabel);
            Instance = this;
            EnsureStampMark();
            HideImmediate(); // müşteri gelene kadar dosya masada görünmez

            // Belge <-> harita / kural klasörü kısayolları (hata olsa bile dosya gizli kalsın)
            try { gameObject.AddComponent<DocLinks>().Init(this, adaParsel, area, floors, soil); }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        // ------------------------------------------------------------------ zemin etüdü satırı (1. gün yok)

        TMP_Text soilRowLabel;
        GameObject soilRowLine;

        /// Satırın altındaki ince çizgi (FolderSetup: etiket y - 3, çizgi y - 19)
        GameObject RowLine(TMP_Text label)
        {
            if (label == null || content == null) return null;
            float want = label.rectTransform.anchoredPosition.y - 16f;
            foreach (var img in content.GetComponentsInChildren<Image>(true))
            {
                if (img.name != "Line" || img.transform.parent != label.transform.parent) continue;
                if (Mathf.Abs(img.rectTransform.offsetMax.y - want) < 1f) return img.gameObject;
            }
            return null;
        }

        /// 1. günde zemin etüdü kuralı yok: satır (etiket, değer, KURAL düğmesi, çizgi) hiç görünmesin
        void SetSoilRow(bool visible)
        {
            if (soilRowLabel != null) soilRowLabel.gameObject.SetActive(visible);
            if (soil != null)
            {
                soil.gameObject.SetActive(visible);
                var link = soil.transform.parent != null ? soil.transform.parent.Find("Link_" + soil.name) : null;
                if (link != null) link.gameObject.SetActive(visible);
            }
            if (soilRowLine != null) soilRowLine.SetActive(visible);
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
            GameEvents.Stamped += OnStamped;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= Show;
            GameEvents.FocusTransitionStarted -= OnFocusTransition;
            GameEvents.CustomerLeaving -= OnCustomerLeaving;
            GameEvents.CustomerLeft -= OnCustomerLeft;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.Stamped -= OnStamped;
        }

        void OnCustomerLeft(CustomerData c, bool angry) { if (c == Current) Hide(); }

        /// Müşteri dosyasını geri alıyor: gişeye doğru kayarak kaybolur
        void OnCustomerLeaving(CustomerData c)
        {
            if (c != Current) return;
            Leaving = true;
            var fc = FocusController.Instance;
            if (fc != null && fc.State == FocusState.Folder) LookAtCustomer(fc, c);
            if (interactable != null) interactable.enabled = false;
            StopAllCoroutines();
            StartCoroutine(SlideOutAndHide());
        }

        // ------------------------------------------------------------------ mühürden sonra müşteriye dön

        const float AutoReturnDelay = 0.8f; // mühür izini görsün, sonra müşterinin tepkisine dön

        void OnStamped(CustomerData c, bool approved)
        {
            if (c != Current) return;
            StartCoroutine(AutoReturn(c));
        }

        /// Mühür basıldıktan ~2 sn sonra belgeden çık; bakış gişedeki müşteriye (tepkisini görelim)
        IEnumerator AutoReturn(CustomerData c)
        {
            yield return new WaitForSeconds(AutoReturnDelay);
            var sc = StampController.Instance;
            for (float w = 0f; w < 1.5f && sc != null && sc.IsHolding; w += Time.deltaTime) yield return null; // mühür yerine dönsün
            while (PauseMenu.IsPaused) yield return null;
            var fc = FocusController.Instance;
            if (fc == null || c != Current || Leaving) yield break;
            while (fc.IsTransitioning) yield return null;
            if (fc.State != FocusState.Folder) yield break; // oyuncu kendisi çıktı / haritaya geçti
            LookAtCustomer(fc, c);
        }

        static void LookAtCustomer(FocusController fc, CustomerData c)
        {
            if (TryCustomerFace(c, out var face)) fc.BackLookingAt(face);
            else fc.Back();
        }

        /// Müşterinin yüzü (modelin tepesinin biraz altı)
        static bool TryCustomerFace(CustomerData c, out Vector3 face)
        {
            face = default;
            foreach (var cc in FindObjectsByType<CustomerController>(FindObjectsSortMode.None))
            {
                if (cc == null || cc.Data != c) continue;
                var anim = cc.GetComponentInChildren<Animator>();
                var head = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head != null) { face = head.position + Vector3.up * 0.05f; return true; }
                bool any = false;
                var b = new Bounds(cc.transform.position, Vector3.zero);
                foreach (var r in cc.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                face = any ? new Vector3(b.center.x, b.max.y - Mathf.Min(0.2f, b.size.y * 0.12f), b.center.z)
                           : cc.transform.position + Vector3.up * 1.55f;
                return true;
            }
            return false;
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
            adaParsel.text = c.ada.ToString(); // parsel numarası oyunda kullanılmıyor
            area.text = $"{c.areaM2} m²";
            floors.text = c.floors.ToString();
            use.text = c.use;
            if (license != null) license.text = c.licenseNo;

            // 1. günde zemin etüdü satırı hiç yok (kural 2. gün geliyor)
            SetSoilRow(day >= 2);
            if (day < 2) soil.text = "—";
            else soil.text = c.soil switch
            {
                SoilType.Solid => "Sağlam zemin",
                SoilType.Soft => "Yumuşak zemin",
                _ => "Yok",
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
