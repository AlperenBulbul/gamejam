using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Masadaki gazete.
    /// - Gün başında (DayIntro) etrafında turuncu çerçeve parlar (oyuncuya 'buradan başla' göstergesi)
    /// - Tıklanınca kamera gazetenin tepesine gelir (okunur), ekranın altında "GÜNE BAŞLA" düğmesi çıkar
    /// - Düğme (ya da Enter): kamera masaya döner, mesai başlar, müşteriler gelir
    /// - Mesai sırasında da açılıp okunabilir (düğme yok)
    public class NewspaperView : MonoBehaviour
    {
        [Header("Yakın bakış")]
        [Tooltip("Sayfanın etrafında kalan pay (1 = gazete ekranı tam doldurur)")]
        public float focusMargin = 1.25f;

        [Header("Parıltı (kural klasörüyle aynı)")]
        public float pulsePerSecond = 0.7f;
        [Range(0f, 1f)] public float minAlpha = 0.10f;
        [Range(0f, 1f)] public float maxAlpha = 0.80f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Interactable interactable;
        Renderer paper;
        GameObject glowFrame;
        Renderer[] glowRenderers;
        MaterialPropertyBlock mpb;
        GameObject button;
        bool glowing;
        public GameObject StartButton => button;

        void Awake()
        {
            interactable = GetComponent<Interactable>();
            mpb = new MaterialPropertyBlock();
            if (interactable != null)
            {
                // Eski kurulum: gazeteye tıklayınca doğrudan güne başlıyordu. Artık düğme başlatır.
                for (int i = 0; i < interactable.onClick.GetPersistentEventCount(); i++)
                    if (interactable.onClick.GetPersistentMethodName(i) == "BeginWorking")
                        interactable.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
                interactable.prompt = "Oku";
            }
        }

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.FocusTransitionStarted += OnTransition;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.FocusTransitionStarted -= OnTransition;
        }

        void Start()
        {
            paper = FindPaper();
            SetupFocusPoint();
            BuildGlow();
            BuildButton();
            var gm = GameManager.Instance;
            SetGlow(gm != null && gm.State == GameState.DayIntro);
        }

        Renderer FindPaper()
        {
            Renderer best = null;
            float area = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled) continue;
                var s = r.bounds.size;
                if (s.x * s.z > area) { area = s.x * s.z; best = r; }
            }
            return best;
        }

        // ------------------------------------------------------------------ yakın bakış

        void SetupFocusPoint()
        {
            if (interactable == null || paper == null) return;
            var fp = transform.Find("FocusPoint_Newspaper");
            if (fp == null)
            {
                fp = new GameObject("FocusPoint_Newspaper").transform;
                fp.SetParent(transform, false);
            }

            // Gazetenin üst kenarı: modelin ileri ekseni (oyuncudan uzağa)
            var t = paper.transform;
            Vector3 up = t.forward; up.y = 0f;
            up = up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.forward;

            Vector2 size = new Vector2(paper.bounds.size.x, paper.bounds.size.z);
            var mf = paper.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var s = Vector3.Scale(mf.sharedMesh.bounds.size, t.lossyScale);
                size = new Vector2(Mathf.Abs(s.x), Mathf.Abs(s.z));
            }
            var cam = Camera.main;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float dist = Mathf.Max(size.y, size.x / aspect) * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * focusMargin;

            var b = paper.bounds;
            var center = new Vector3(b.center.x, b.max.y, b.center.z) - up * size.y * 0.06f; // biraz yukarı kaydır: alttaki düğmeye yer
            fp.position = center + Vector3.up * Mathf.Max(0.2f, dist);
            fp.rotation = Quaternion.LookRotation(Vector3.down, up);
            interactable.focusPoint = fp;
            interactable.focusState = FocusState.Newspaper;
        }

        FocusState lastTarget = FocusState.Desk;

        void OnTransition(FocusState from, FocusState to)
        {
            lastTarget = to;
            if (to == FocusState.Newspaper) SetGlow(false); // oyuncu gazeteyi açtı
        }

        void OnStateChanged(GameState s)
        {
            if (s == GameState.MainMenu) lastTarget = FocusState.Desk;
            SetGlow(s == GameState.DayIntro);
            if (s == GameState.DayIntro || s == GameState.MainMenu) ApplyFrontPage();
        }

        // ------------------------------------------------------------------ günün ön sayfası

        MaterialPropertyBlock pagePb;
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        /// 2. gün ve sonrası: dünkü olaya göre hazır ön sayfa (NewspaperPages). 1. gün / menü: ilk gün gazetesi (materyalin kendisi).
        /// Sadece gazete ön yüzü materyaline uygulanır (modelin diğer parçaları palet dokusunu kullanıyor).
        void ApplyFrontPage()
        {
            if (paper == null) return;
            // Önceki sürüm tüm renderer'a doku basıyordu: o bloğu temizle (vurgu rengi kalır)
            pagePb ??= new MaterialPropertyBlock();
            paper.GetPropertyBlock(pagePb);
            if (pagePb.HasTexture(BaseMapId) || pagePb.HasTexture(MainTexId))
            {
                var keep = new MaterialPropertyBlock();
                if (pagePb.HasColor(BaseColorId)) keep.SetColor(BaseColorId, pagePb.GetColor(BaseColorId));
                paper.SetPropertyBlock(keep);
            }

            var front = FrontSlot(out int slot);
            if (front == null) { Debug.LogWarning("[Gazete] Ön sayfa materyali (M_NewspaperFront) bulunamadı"); return; }
            var gm = GameManager.Instance;
            string name = gm != null && gm.State == GameState.DayIntro ? NewspaperPages.ForDay(gm) : null;
            var tex = NewspaperPages.Load(name);
            var pb = new MaterialPropertyBlock();
            if (tex != null)
            {
                pb.SetTexture(BaseMapId, tex);
                pb.SetTexture(MainTexId, tex);
                Debug.Log($"[Gazete] Gün {gm.Run.day} ön sayfası: {name}");
            }
            front.SetPropertyBlock(pb, slot); // boş blok = materyalin kendi dokusu (ilk gün gazetesi)
        }

        /// Gazete ön sayfası materyalinin sırası (adında / dokusunda "Newspaper" geçen)
        Renderer FrontSlot(out int slot)
        {
            slot = -1;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.name.StartsWith("Bar") || r.name == "Back" || r.name == "Halo") continue; // parıltı çerçevesi
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var t = m.HasProperty(BaseMapId) ? m.GetTexture(BaseMapId) : m.mainTexture;
                    if (m.name.Contains("Newspaper") || (t != null && t.name.Contains("Newspaper"))) { slot = i; return r; }
                }
            }
            return null;
        }

        /// Gün başında (her gün) gazete açılana kadar parlar; masadayken altta "Gazeteye git" yazar.
        /// Olaylara güvenmek yerine her karede kontrol edilir (2. gün geçişinde parıltı kayboluyordu).
        void KeepGlow()
        {
            var fc = FocusController.Instance;
            var gm = GameManager.Instance;
            bool atPaper = lastTarget == FocusState.Newspaper || (fc != null && fc.State == FocusState.Newspaper);
            bool want = gm != null && gm.State == GameState.DayIntro && !atPaper;
            if (want != glowing || (glowFrame != null && glowFrame.activeSelf != want)) SetGlow(want);

            bool prompt = want && fc != null && !fc.IsTransitioning && fc.State == FocusState.Desk
                          && !GuidedTour.Running && !EndScreenUI.Blocking;
            PromptBar.Set("newspaper", prompt ? "<b>Gazeteye</b> git" : null);
        }

        // ------------------------------------------------------------------ "GÜNE BAŞLA"

        void Update()
        {
            KeepGlow();
            UpdateGlow();

            var fc = FocusController.Instance;
            var gm = GameManager.Instance;
            bool show = fc != null && gm != null && fc.State == FocusState.Newspaper && !fc.IsTransitioning && gm.State == GameState.DayIntro;
            if (button != null && button.activeSelf != show) button.SetActive(show);

            var kb = Keyboard.current;
            if (show && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) StartDay();
        }

        public void StartDay()
        {
            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            if (gm == null || gm.State != GameState.DayIntro) return;
            if (fc != null && fc.State == FocusState.Newspaper) fc.Back(); // gazeteyi masaya bırak
            gm.BeginWorking();                                             // müşteriler gelmeye başlar
        }

        void BuildButton()
        {
            var uiGo = GameObject.Find("UI");
            var canvas = uiGo != null ? uiGo.GetComponent<Canvas>() : null;
            if (canvas == null) return;

            button = new GameObject("StartDayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)button.transform;
            rt.SetParent(canvas.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(380f, 86f);
            rt.anchoredPosition = new Vector2(0f, 70f);
            var img = button.GetComponent<Image>();
            img.color = new Color(0.95f, 0.70f, 0.20f);
            var btn = button.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(StartDay);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var lrt = (RectTransform)label.transform;
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "GÜNE BAŞLA";
            var heavy = UiFonts.Heavy; // Inter ExtraBold: tabela gibi sıkı, kalın
            if (heavy != null) { tmp.font = heavy; tmp.fontStyle = FontStyles.Normal; tmp.characterSpacing = 1f; tmp.fontSize = 40f; }
            else { tmp.fontSize = 38f; tmp.fontStyle = FontStyles.Bold; tmp.characterSpacing = 4f; }
            tmp.color = new Color(0.07f, 0.075f, 0.08f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            button.SetActive(false);
        }

        // ------------------------------------------------------------------ parıltı

        void BuildGlow()
        {
            if (paper == null) return;
            // Kural klasörünün parıltı materyalini kullan (aynı görünüm)
            Material glowMat = null;
            var binder = FindAnyObjectByType<BinderView>();
            if (binder != null && binder.alertFrame != null)
            {
                var r = binder.alertFrame.GetComponentInChildren<Renderer>(true);
                if (r != null) glowMat = r.sharedMaterial;
            }
            if (glowMat == null) return;

            var t = paper.transform;
            var mf = paper.GetComponent<MeshFilter>();
            var lb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, new Vector3(0.3f, 0.01f, 0.4f));
            var sc = t.lossyScale;
            float ex = Mathf.Abs(lb.extents.x * sc.x) + 0.006f, ez = Mathf.Abs(lb.extents.z * sc.z) + 0.006f;
            float h = Mathf.Max(0.008f, Mathf.Abs(lb.size.y * sc.y) + 0.006f), th = 0.006f;

            glowFrame = new GameObject("GlowFrame");
            glowFrame.transform.SetParent(transform, false);
            glowFrame.transform.position = t.TransformPoint(new Vector3(lb.center.x, lb.min.y, lb.center.z));
            glowFrame.transform.rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            Bar(new Vector3(-ex, h * 0.5f, 0f), new Vector3(th, h, ez * 2f + th), glowMat);
            Bar(new Vector3(ex, h * 0.5f, 0f), new Vector3(th, h, ez * 2f + th), glowMat);
            Bar(new Vector3(0f, h * 0.5f, -ez), new Vector3(ex * 2f + th, h, th), glowMat);
            Bar(new Vector3(0f, h * 0.5f, ez), new Vector3(ex * 2f + th, h, th), glowMat);
            glowRenderers = glowFrame.GetComponentsInChildren<Renderer>(true);
            HintFrameFx.Attach(glowFrame, glowMat, pulsePerSecond, Vector3.up); // koyu kenar + turuncu yansıma ışığı
            glowFrame.SetActive(false);
        }

        void Bar(Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(b.GetComponent<Collider>()); // tıklamayı engellemesin
            b.transform.SetParent(glowFrame.transform, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void SetGlow(bool on)
        {
            glowing = on;
            if (glowFrame != null) glowFrame.SetActive(on);
        }

        void UpdateGlow()
        {
            if (!glowing || glowRenderers == null) return;
            float s = HintColors.Steady ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsePerSecond * 2f * Mathf.PI);
            var c = new Color(HintColors.Orange.r, HintColors.Orange.g, HintColors.Orange.b, Mathf.Lerp(0f, 1f, s));
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
