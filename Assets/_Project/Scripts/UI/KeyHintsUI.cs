using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Tooltip("Tuş listesinin arka planı (okunur olsun diye koyu)")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0.88f);

        [Tooltip("Tanıtım sırasında bulanık arka planın karartması (0 siyah, 1 karartma yok)")]
        [Range(0f, 1f)] public float blurTint = 0.45f;

        UnityEngine.UI.Image bgImg;
        UnityEngine.UI.RawImage blurImg;
        RenderTexture blurRT;

        void Start()
        {
            bgImg = root != null ? root.GetComponent<UnityEngine.UI.Image>() : null;
            if (bgImg != null) bgImg.color = backgroundColor;
            Refresh();
        }

        void OnDestroy()
        {
            if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
        }

        string lastKey;

        // Mühür alınıp bırakılınca da güncellensin (olay yok, ucuz kontrol)
        void Update()
        {
            var gm = GameManager.Instance;
            var fc = FocusController.Instance;
            var sc = StampController.Instance;
            string key = $"{GuidedTour.Running}|{(gm != null ? gm.State.ToString() : "-")}|{(fc != null ? fc.State.ToString() : "-")}|{(sc != null && sc.IsHolding)}|{OverlayVisible}|{MinigameDirector.Hints != null}|{FolderView.DocOpen}";
            if (key != lastKey) { lastKey = key; Refresh(); }
        }

        // ------------------------------------------------------------------ ilk açılışta ortada göster
        readonly System.Collections.Generic.HashSet<FocusState> introduced = new System.Collections.Generic.HashSet<FocusState>();
        Coroutine introRoutine;
        Vector2 homePos;
        bool homeSaved;

        void PlayIntro(FocusState f)
        {
            // Ortadaki bulanık tanıtım kaldırıldı: liste hep sağ altta (ilk turu GuidedTour anlatıyor)
        }

        System.Collections.IEnumerator Intro()
        {
            var panel = root.transform as RectTransform;
            var parent = panel != null ? panel.parent as RectTransform : null;
            if (panel == null || parent == null) yield break;
            if (!homeSaved) { homePos = panel.anchoredPosition; homeSaved = true; }

            // Ekranın görüntüsünü al (tuş listesi görünmeden) ve bulanıklaştır
            panel.localScale = Vector3.zero;
            yield return new WaitForEndOfFrame();
            BuildBlur();
            Canvas.ForceUpdateCanvases();

            const float big = 1.6f;
            var size = panel.rect.size;
            var W = parent.rect.size;
            // Panel sağ alta tutturulmuş (pivot sağ alt): ortaya getirmek için konum
            var center = new Vector2(-W.x * 0.5f + size.x * big * 0.5f, W.y * 0.5f - size.y * big * 0.5f);

            panel.anchoredPosition = center;
            panel.localScale = Vector3.one * big;
            if (bgImg != null) bgImg.enabled = false; // ortadayken kutu yok: yazı bulanık ekranın üstünde

            // Bulanıklık yumuşakça gelir
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { SetBlurAlpha(t / 0.25f); yield return null; }
            SetBlurAlpha(1f);

            // Oyuncu ekrana tıklayana kadar ortada kalır
            while (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) yield return null;

            // Sağ alta kayar, bulanıklık kalkar, kutu geri gelir
            if (bgImg != null) bgImg.enabled = true;
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                panel.anchoredPosition = Vector2.Lerp(center, homePos, k);
                panel.localScale = Vector3.one * Mathf.Lerp(big, 1f, k);
                SetBlurAlpha(1f - k);
                if (bgImg != null) { var c = backgroundColor; c.a *= k; bgImg.color = c; }
                yield return null;
            }
            ResetPanel();
            introRoutine = null;
        }

        /// Ekran görüntüsünü küçültüp büyüterek bulanıklaştırır ve tam ekran resim olarak gösterir
        void BuildBlur()
        {
            if (blurImg == null)
            {
                var go = new GameObject("IntroBlur", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root.transform.parent, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.SetSiblingIndex(root.transform.GetSiblingIndex()); // tuş listesinin hemen arkasında
                blurImg = go.GetComponent<UnityEngine.UI.RawImage>();
                blurImg.raycastTarget = false;
            }

            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return;
            RenderTexture cur = Temp(shot.width / 2, shot.height / 2);
            Graphics.Blit(shot, cur);
            Destroy(shot);
            for (int i = 0; i < 4; i++) cur = Step(cur, cur.width / 2, cur.height / 2); // 1/32
            for (int i = 0; i < 3; i++) cur = Step(cur, cur.width * 2, cur.height * 2); // yumuşak büyüt (1/4)

            if (blurRT == null || blurRT.width != cur.width || blurRT.height != cur.height)
            {
                if (blurRT != null) { blurRT.Release(); Destroy(blurRT); }
                blurRT = new RenderTexture(cur.width, cur.height, 0) { filterMode = FilterMode.Bilinear };
            }
            Graphics.Blit(cur, blurRT);
            RenderTexture.ReleaseTemporary(cur);
            blurImg.texture = blurRT;
            blurImg.gameObject.SetActive(true);
            SetBlurAlpha(0f);
        }

        static RenderTexture Temp(int w, int h)
        {
            var r = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0);
            r.filterMode = FilterMode.Bilinear;
            return r;
        }

        static RenderTexture Step(RenderTexture src, int w, int h)
        {
            var dst = Temp(w, h);
            Graphics.Blit(src, dst);
            RenderTexture.ReleaseTemporary(src);
            return dst;
        }

        void SetBlurAlpha(float a)
        {
            if (blurImg == null) return;
            blurImg.color = new Color(blurTint, blurTint, blurTint, Mathf.Clamp01(a));
        }

        void ResetPanel()
        {
            if (blurImg != null) blurImg.gameObject.SetActive(false);
            if (bgImg != null) { bgImg.enabled = true; bgImg.color = backgroundColor; }
            var panel = root != null ? root.transform as RectTransform : null;
            if (panel == null || !homeSaved) return;
            panel.anchoredPosition = homePos;
            panel.localScale = Vector3.one;
        }

        void OnStateChanged(GameState s)
        {
            if (s == GameState.MainMenu)
            {
                introduced.Clear();
                if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
                ResetPanel();
            }
            Refresh();
        }
        void OnFocusChanged(FocusState f)
        {
            Refresh();
            if (f == FocusState.Desk && introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; ResetPanel(); }
            else PlayIntro(f);
        }

        void Refresh()
        {
            var gm = GameManager.Instance;
            bool show = gm != null && gm.DeskInputEnabled && !GuidedTour.Running;
            root.SetActive(show);
            if (!show) return;

            var focus = FocusController.Instance != null ? FocusController.Instance.State : FocusState.Desk;
            bool holding = StampController.Instance != null && StampController.Instance.IsHolding;
            BuildRows(Hints(gm.State, focus, holding));
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

        /// Tuşlar simgeyle: {LMB} sol tık, {RMB} sağ tık, {WHEEL} tekerlek, {MOUSE} fare, {ESC} {SPACE} {TAB} {ENTER} {A} {D} {1-7}
        static (string key, string action)[] Hints(GameState state, FocusState focus, bool holding)
        {
            switch (focus)
            {
                case FocusState.Folder:
                    return holding
                        ? new[] { ("{LMB} form", "Mühür bas"), ("{RMB}", "Mührü bırak") }
                        : new[] { ("{LMB}", "Mührü al"), ("{RMB} / {ESC}", "Geri") };
                case FocusState.Map:
                    return FolderView.DocOpen
                        ? new[] { ("{WHEEL}", "Yakınlaştır"),
                                  ("{SPACE}", OverlayVisible ? "Tehlikeli bölgeyi gizle" : "Tehlikeli bölgeyi göster"),
                                  ("{TAB}", "Belgeye dön"), ("{RMB} / {ESC}", "Geri") }
                        : new[] { ("{WHEEL}", "Yakınlaştır"),
                                  ("{SPACE}", OverlayVisible ? "Tehlikeli bölgeyi gizle" : "Tehlikeli bölgeyi göster"),
                                  ("{RMB} / {ESC}", "Geri") };
                case FocusState.Binder:
                    return FolderView.DocOpen
                        ? new[] { ("{TAB}", "Belgeye dön"), ("{RMB} / {ESC}", "Geri") }
                        : new[] { ("{RMB} / {ESC}", "Geri") };
                case FocusState.Monitor:
                    if (MinigameDirector.Hints != null) return Normalize(MinigameDirector.Hints);
                    return new[] { ("{LMB} ünlemli işçi", "Uyar"), ("{A} {D}", "Kanal değiştir"), ("{1-7}", "Kanal seç"), ("{RMB} / {ESC}", "Geri") };
                case FocusState.Newspaper:
                    return state == GameState.DayIntro
                        ? new[] { ("{ENTER}", "Güne başla"), ("{RMB} / {ESC}", "Geri") }
                        : new[] { ("{RMB} / {ESC}", "Geri") };
                case FocusState.HoldingStamp:
                    return new[] { ("{LMB} dosya", "Mühür bas"), ("{RMB}", "Mührü bırak") };
                default: // Desk
                    return state == GameState.DayIntro
                        ? new[] { ("{MOUSE}", "Etrafa bak"), ("{LMB}", NewspaperView.RulesFirst ? "Kural defterini aç" : "Gazeteyi oku"), ("{ESC}", "Duraklat") }
                        : new[] { ("{MOUSE}", "Etrafa bak"), ("{LMB}", "Seç / incele"), ("{ESC}", "Duraklat") };
            }
        }

        /// Minigame'in düz yazılı tuşlarını simgeye çevir ("Sol tık / Boşluk" -> {LMB} / {SPACE})
        static (string key, string action)[] Normalize((string key, string action)[] hints)
        {
            var r = new (string, string)[hints.Length];
            for (int i = 0; i < hints.Length; i++)
            {
                string k = hints[i].key
                    .Replace("Sol tık", "{LMB}").Replace("Sağ tık", "{RMB}").Replace("Tekerlek", "{WHEEL}")
                    .Replace("Boşluk", "{SPACE}").Replace("Enter", "{ENTER}").Replace("Esc", "{ESC}").Replace("Tab", "{TAB}");
                r[i] = (k, hints[i].action);
            }
            return r;
        }

        // ------------------------------------------------------------------ satırlar (simge + yazı)

        readonly System.Collections.Generic.List<GameObject> rows = new System.Collections.Generic.List<GameObject>();
        string lastRowsKey;

        void BuildRows((string key, string action)[] hints)
        {
            var sbk = new StringBuilder();
            foreach (var h in hints) sbk.Append(h.key).Append('|').Append(h.action).Append(';');
            if (sbk.ToString() == lastRowsKey && rows.Count > 0) return;
            lastRowsKey = sbk.ToString();

            foreach (var r in rows) if (r != null) { r.SetActive(false); Destroy(r); }
            rows.Clear();
            if (text != null) text.gameObject.SetActive(false);
            var vl = root.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vl != null) vl.spacing = 6f;

            foreach (var h in hints)
            {
                var row = new GameObject("Row", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
                row.transform.SetParent(root.transform, false);
                var hl = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                hl.spacing = 6f;
                hl.childAlignment = TextAnchor.MiddleLeft;
                hl.childControlWidth = hl.childControlHeight = true;
                hl.childForceExpandWidth = hl.childForceExpandHeight = false;
                rows.Add(row);

                foreach (var (isIcon, val) in Tokens(h.key))
                {
                    if (isIcon) KeyIcons.Add(row.transform, val, textFont);
                    else AddText(row.transform, val, keyColor, true);
                }
                var gap = new GameObject("Gap", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
                gap.transform.SetParent(row.transform, false);
                gap.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 6f;
                AddText(row.transform, h.action, Color.white, false);
            }
        }

        TMP_FontAsset textFont => text != null ? text.font : null;

        void AddText(Transform parent, string s, Color c, bool bold)
        {
            var go = new GameObject("T", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (textFont != null) t.font = textFont;
            t.text = s;
            t.fontSize = 22f;
            t.color = c;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.raycastTarget = false;
        }

        /// "{LMB} / {ESC}" -> simge, yazı("/"), simge
        static System.Collections.Generic.List<(bool, string)> Tokens(string key)
        {
            var list = new System.Collections.Generic.List<(bool, string)>();
            int i = 0;
            while (i < key.Length)
            {
                int o = key.IndexOf('{', i);
                if (o < 0) { Add(list, key.Substring(i)); break; }
                Add(list, key.Substring(i, o - i));
                int c = key.IndexOf('}', o);
                if (c < 0) { Add(list, key.Substring(o)); break; }
                list.Add((true, key.Substring(o + 1, c - o - 1)));
                i = c + 1;
            }
            return list;
        }

        static void Add(System.Collections.Generic.List<(bool, string)> list, string s)
        {
            s = s.Trim();
            if (s.Length > 0) list.Add((false, s));
        }

        string Build((string key, string action)[] hints, int pulseIndex = -1)
        {
            string hex = ColorUtility.ToHtmlStringRGB(keyColor);
            var sb = new StringBuilder();
            for (int i = 0; i < hints.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                if (i == pulseIndex)
                {
                    // Gösterge rengiyle (kırmızımsı turuncu) yanıp söner
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 1.4f * 2f * Mathf.PI);
                    var c = Color.Lerp(Color.white, HintColors.Orange, k);
                    string ph = ColorUtility.ToHtmlStringRGB(c);
                    sb.Append($"<color=#{ph}><b>{hints[i].key}  {hints[i].action}</b></color>");
                }
                else sb.Append($"<color=#{hex}><b>{hints[i].key}</b></color>  {hints[i].action}");
            }
            return sb.ToString();
        }
    }
}
