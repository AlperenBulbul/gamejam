using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Masadaki kural klasörü.
    /// - Kapalı durur; tıklanınca kapak halka tarafından açılır, masaya dönünce kapanır.
    /// - Gün başında (DayIntro) o güne kadarki tüm RuleData sayfalarını toplar; o gün eklenenlere YENİ etiketi
    ///   koyar, klasörü ilk yeni sayfada açar ve oyuncu klasörü açana kadar etrafında beyaz çerçeve parlar.
    /// - Sayfa çevirme sadece klasöre yakından bakarken: tekerlek ya da sol/sağ ok.
    /// - Tasarım modeli bağlıysa (menü 19): masada kapalı SM_RuleBinder, yakın bakışta açık SM_RuleBinder_Open;
    ///   2. günden itibaren kapalı klasörde turuncu YENİ sekmesi görünür.
    public class BinderView : MonoBehaviour
    {
        [Header("Sayfa")]
        public RectTransform pageRoot;
        public TMP_Text title;
        public TMP_Text body;
        public TMP_Text pageNo;
        public GameObject newSticker;
        public float flipTime = 0.16f;
        [Tooltip("Görselli sayfalarda tam sayfa çevirme süresi (sn)")]
        public float pageTurnTime = 0.45f;
        public float wheelCooldown = 0.12f;

        [Header("Kapak")]
        public Transform coverHinge;
        public float openAngle = 178f;
        public float coverTime = 0.35f;

        [Header("Tasarım modeli (menü 19)")]
        public GameObject closedModel;
        public GameObject openModel;
        [Tooltip("Kapalı klasördeki turuncu YENİ sekmesi (2. günden itibaren)")]
        public GameObject newTab;
        [Tooltip("Yakın bakışta sayfanın etrafında kalan pay (1 = sayfa ekranı tam doldurur)")]
        public float focusMargin = 1.2f;

        [Header("Yeni kural uyarısı (beyaz çerçeve parıltısı)")]
        public GameObject alertFrame;
        [Tooltip("Saniyede kaç kez parlayıp söner")]
        public float pulsePerSecond = 0.7f;
        [Range(0f, 1f)] public float minAlpha = 0.10f;
        [Range(0f, 1f)] public float maxAlpha = 0.80f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        Renderer[] alertRenderers;
        MaterialPropertyBlock alertMpb;

        readonly List<RuleData> pages = new List<RuleData>();
        readonly HashSet<RuleData> newToday = new HashSet<RuleData>();
        int index;
        bool flipping;
        float nextWheelTime;
        bool alerting;
        Coroutine coverRoutine;

        public int PageCount => pages.Count;
        public int Index => index;
        public bool IsAlerting => alerting;

        void Awake()
        {
            alertRenderers = alertFrame != null ? alertFrame.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            alertMpb = new MaterialPropertyBlock();
            EnsureUnderPage();
            EnsurePageArt();
        }

        RawImage pageArt;

        /// Tasarımcının sayfa görselleri (T_RulePage_01-04) için tam sayfa resim alanı
        void EnsurePageArt()
        {
            if (pageRoot == null) return;
            var t = pageRoot.Find("PageArt");
            if (t == null)
            {
                var go = new GameObject("PageArt", typeof(RectTransform), typeof(RawImage));
                t = go.transform;
                t.SetParent(pageRoot, false);
                var rt = (RectTransform)t;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            pageArt = t.GetComponent<RawImage>();
            pageArt.raycastTarget = false;
            pageArt.enabled = false;

            // Çevrilen sayfa: sol kenardan (halkalardan) menteşeli, sayfanın üstünde
            var page = pageRoot.parent;
            var tt = page.Find("TurnPage");
            if (tt == null)
            {
                var go = new GameObject("TurnPage", typeof(RectTransform), typeof(RawImage));
                tt = go.transform;
                tt.SetParent(page, false);
            }
            var trt = (RectTransform)tt;
            trt.anchorMin = pageRoot.anchorMin; trt.anchorMax = pageRoot.anchorMax;
            trt.pivot = pageRoot.pivot;
            trt.offsetMin = pageRoot.offsetMin; trt.offsetMax = pageRoot.offsetMax;
            trt.SetAsLastSibling();
            turnPage = tt.GetComponent<RawImage>();
            turnPage.raycastTarget = false;
            var paperImg = pageRoot.GetComponent<Image>();
            paperColor = paperImg != null ? paperImg.color : new Color(0.97f, 0.96f, 0.92f);
            tt.gameObject.SetActive(false);
        }

        RawImage turnPage;
        Color paperColor;

        /// Görselli sayfada metin, çizgiler ve delikler gizlenir (hepsi görselde var)
        void SetArtMode(Texture2D tex)
        {
            bool art = tex != null && pageArt != null;
            if (pageArt != null) { pageArt.texture = tex; pageArt.enabled = art; }
            foreach (Transform c in pageRoot)
            {
                if (pageArt != null && c == pageArt.transform) continue;
                if (newSticker != null && c == newSticker.transform) continue;
                c.gameObject.SetActive(!art);
            }
        }

        /// Sayfa çevrilirken arkada modelin kendi (farklı renkli) sayfası görünmesin:
        /// sayfanın arkasına aynı renkte sabit, boş bir kâğıt koy.
        void EnsureUnderPage()
        {
            var page = pageRoot != null ? pageRoot.parent as RectTransform : null;
            if (page == null || page.Find("UnderPage") != null) return;
            var go = new GameObject("UnderPage", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(page, false);
            rt.SetAsFirstSibling();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            var src = pageRoot.GetComponent<Image>();
            img.color = src != null ? src.color : new Color(0.97f, 0.96f, 0.92f);
            img.raycastTarget = false;
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.FocusTransitionStarted += OnFocusTransition;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.FocusTransitionStarted -= OnFocusTransition;
        }

        void Start()
        {
            Rebuild(CurrentDay());

            // Yakın bakış: sayfanın tam tepesinden, sayfa ekranla hizalı (çapraz bakış sayfayı yamuk gösteriyordu)
            var it = GetComponent<Interactable>();
            var page = pageRoot != null ? pageRoot.parent as RectTransform : null;
            if (it != null && it.focusPoint != null && page != null)
            {
                Vector3 center = page.TransformPoint(page.rect.center);
                float dist = Mathf.Max(0.2f, Vector3.Distance(it.focusPoint.position, center));
                if (openModel != null)
                {
                    // Sayfanın tamamı ekrana sığsın
                    var cam = Camera.main;
                    float fov = cam != null ? cam.fieldOfView : 60f;
                    float aspect = cam != null ? cam.aspect : 16f / 9f;
                    float h = page.rect.height * page.lossyScale.y, w = page.rect.width * page.lossyScale.x;
                    float half = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                    dist = Mathf.Max(h, w / aspect) * 0.5f / half * focusMargin;
                }
                it.focusPoint.position = center + Vector3.up * dist;
                it.focusPoint.rotation = Quaternion.LookRotation(Vector3.down, FlatUp(page));
            }
        }

        /// Sayfanın üst kenarı yönü (yatay). Kamera bununla hizalanır ki sayfa düz görünsün.
        static Vector3 FlatUp(Transform page)
        {
            var u = page.up; u.y = 0f;
            return u.sqrMagnitude > 1e-4f ? u.normalized : Vector3.forward;
        }

        static int CurrentDay() => GameManager.Instance != null && GameManager.Instance.Run != null ? GameManager.Instance.Run.day : 1;

        void OnStateChanged(GameState s)
        {
            if (s == GameState.DayIntro || s == GameState.MainMenu) Rebuild(CurrentDay());
        }

        void OnFocusTransition(FocusState from, FocusState to)
        {
            if (to == FocusState.Binder)
            {
                SetOpen(true);
                SetCover(true);
                SetAlert(false); // oyuncu yeni kuralları gördü
            }
            else if (from == FocusState.Binder)
            {
                SetOpen(false);
                SetCover(false);
            }
        }

        /// Tasarım modeli: açık / kapalı klasör
        void SetOpen(bool open)
        {
            if (openModel == null || closedModel == null) return;
            openModel.SetActive(open);
            closedModel.SetActive(!open);
        }

        public void Rebuild(int day)
        {
            pages.Clear();
            newToday.Clear();
            var gm = GameManager.Instance;
            if (gm != null && gm.Days != null)
            {
                foreach (var d in gm.Days)
                {
                    if (d == null || d.dayNumber > day || d.newRulePages == null) continue;
                    foreach (var r in d.newRulePages)
                    {
                        if (r == null || pages.Contains(r)) continue;
                        pages.Add(r);
                        if (day >= 2 && d.dayNumber == day) newToday.Add(r);
                    }
                }
            }

            // Yeni sayfa varsa klasör ilk yeni sayfada açılsın
            index = 0;
            for (int i = 0; i < pages.Count; i++)
                if (newToday.Contains(pages[i])) { index = i; break; }

            StopAllCoroutines();
            coverRoutine = null;
            flipping = false;
            if (turnPage != null) turnPage.gameObject.SetActive(false);
            pageRoot.localScale = Vector3.one;
            SetCoverImmediate(false);
            SetOpen(false);
            if (newTab != null) newTab.SetActive(day >= 2);
            SetAlert(newToday.Count > 0);
            ShowPage();
        }

        void Update()
        {
            UpdateAlert();

            var fc = FocusController.Instance;
            if (fc == null || fc.State != FocusState.Binder || fc.IsTransitioning || flipping) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) { Turn(+1); return; }
                if (kb.leftArrowKey.wasPressedThisFrame) { Turn(-1); return; }
            }

            var mouse = Mouse.current;
            if (mouse != null && Time.unscaledTime >= nextWheelTime)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel < -0.01f) { Turn(+1); nextWheelTime = Time.unscaledTime + wheelCooldown; }      // aşağı = sonraki
                else if (wheel > 0.01f) { Turn(-1); nextWheelTime = Time.unscaledTime + wheelCooldown; }  // yukarı = önceki
            }
        }

        // ---------------- Uyarı ----------------

        void SetAlert(bool on)
        {
            alerting = on;
            if (alertFrame != null) alertFrame.SetActive(on);
        }

        /// Beyaz çerçevenin saydamlığı yumuşakça artıp azalır (yanıp sönme yok)
        void UpdateAlert()
        {
            if (!alerting || alertRenderers == null) return;
            float s = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsePerSecond * 2f * Mathf.PI);
            var c = new Color(HintColors.Orange.r, HintColors.Orange.g, HintColors.Orange.b, Mathf.Lerp(minAlpha, maxAlpha, s));
            foreach (var r in alertRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(alertMpb);
                alertMpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(alertMpb);
            }
        }

        // ---------------- Kapak ----------------

        void SetCover(bool open)
        {
            if (coverHinge == null) return;
            if (coverRoutine != null) StopCoroutine(coverRoutine);
            coverRoutine = StartCoroutine(AnimateCover(open ? openAngle : 0f));
        }

        void SetCoverImmediate(bool open)
        {
            if (coverHinge == null) return;
            coverHinge.localRotation = Quaternion.Euler(0f, 0f, open ? openAngle : 0f);
        }

        IEnumerator AnimateCover(float target)
        {
            float start = coverHinge.localEulerAngles.z;
            if (start > 270f) start -= 360f;
            for (float t = 0f; t < coverTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / coverTime);
                coverHinge.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(start, target, k));
                yield return null;
            }
            coverHinge.localRotation = Quaternion.Euler(0f, 0f, target);
            coverRoutine = null;
        }

        // ---------------- Sayfa ----------------

        void Turn(int dir)
        {
            int next = Mathf.Clamp(index + dir, 0, Mathf.Max(0, pages.Count - 1));
            if (next == index) return;
            bool art = turnPage != null && pages[index].pageTexture != null && pages[next].pageTexture != null;
            StartCoroutine(art ? PageTurn(next, dir) : Flip(next));
        }

        /// Gerçek sayfa çevirme: sayfa halkalardan kalkar, havada döner, sola yatar.
        /// İleri: alttaki yeni sayfa hemen görünür, eski sayfa çevrilir. Geri: önceki sayfa soldan gelip üstüne kapanır.
        IEnumerator PageTurn(int next, int dir)
        {
            flipping = true;
            var rt = (RectTransform)turnPage.transform;
            Texture2D front;
            float from, to;
            if (dir > 0)
            {
                front = pages[index].pageTexture;
                index = next;
                ShowPage();
                from = 0f; to = 180f;
            }
            else
            {
                front = pages[next].pageTexture;
                from = 180f; to = 0f;
            }

            turnPage.gameObject.SetActive(true);
            float d = Mathf.Max(0.05f, pageTurnTime);
            for (float t = 0f; t <= d; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
                float ang = Mathf.Lerp(from, to, k);
                SetTurnPose(rt, ang);
                yield return null;
            }
            SetTurnPose(rt, to);
            turnPage.gameObject.SetActive(false);
            if (dir < 0) { index = next; ShowPage(); }
            flipping = false;

            void SetTurnPose(RectTransform r, float ang)
            {
                r.localRotation = Quaternion.Euler(0f, ang, 0f);           // halka ekseni etrafında döner, oyuncuya doğru kalkar
                r.localPosition = new Vector3(r.localPosition.x, r.localPosition.y, -1.5f); // sayfanın 1,5 mm üstünde
                bool showFront = ang < 90f;
                turnPage.texture = showFront ? front : null;                // arka yüz boş kâğıt
                turnPage.color = showFront ? Color.white : new Color(paperColor.r * 0.93f, paperColor.g * 0.93f, paperColor.b * 0.93f, 1f);
            }
        }

        IEnumerator Flip(int next)
        {
            flipping = true;
            float half = flipTime * 0.5f;
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                pageRoot.localScale = new Vector3(1f - t / half, 1f, 1f);
                yield return null;
            }
            index = next;
            ShowPage();
            for (float t = 0f; t < half; t += Time.unscaledDeltaTime)
            {
                pageRoot.localScale = new Vector3(t / half, 1f, 1f);
                yield return null;
            }
            pageRoot.localScale = Vector3.one;
            flipping = false;
        }

        void ShowPage()
        {
            if (pages.Count == 0)
            {
                title.text = "";
                body.text = "";
                pageNo.text = "";
                newSticker.SetActive(false);
                return;
            }
            var r = pages[index];
            title.text = r.title;
            body.text = r.body;
            pageNo.text = $"Sayfa {index + 1} / {pages.Count}";
            SetArtMode(r.pageTexture);
            newSticker.SetActive(newToday.Contains(r) && r.pageTexture == null); // görselde YENİ etiketi basılı
        }
    }
}
