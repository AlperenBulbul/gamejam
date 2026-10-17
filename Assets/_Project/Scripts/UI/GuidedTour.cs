using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Hazır oynatılan ilk tur (1. gün, ilk müşteri). Her adımda SADECE yapılması gereken şey yanar ve tıklanabilir:
    ///  1) Gazete            "Gazeteyi aç"
    ///  2) GÜNE BAŞLA        müşteri gelir
    ///  3) Belge (dosya)     "Belgeyi aç"
    ///  4) HARİTA düğmesi    haritaya gider (arsa zaten parlıyor)
    ///  5) Boşluk            tehlikeli bölgeyi göster (basmadan geçilmez)
    ///  6) BELGEYE DÖN
    ///  7) KURAL (İSTENEN KAT satırı) -> kural sayfasında ilgili satır çerçevelenir
    ///  8) BELGEYE DÖN
    ///  9) ONAY mührü -> formdaki KARAR kutusu
    /// Ekranın altında küçük tek satır yönerge. Tur boyunca sağ alttaki tuş listesi gizli, müşterinin sabrı azalmaz.
    /// Oyun açıkken bir kez oynatılır (sonraki yeni oyunlarda tekrar çıkmaz). GameManager bu bileşeni kendisi ekler.
    public class GuidedTour : MonoBehaviour
    {
        /// Tur şu an oynuyor (masa girdisi kısıtlı)
        public static bool Running { get; private set; }
        /// Bu oyunda tur oynandı / oynuyor: eski yanıp sönen ilk tur göstergeleri (TutorialHints) çıkmaz
        public static bool ActiveThisRun { get; private set; }
        static bool completedThisSession;

        static Interactable allowed;
        static bool backAllowed, docBackAllowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Running = ActiveThisRun = completedThisSession = false;
            allowed = null; backAllowed = docBackAllowed = false;
        }

        /// Masadaki tıklama: tur sırasında sadece o adımın nesnesi
        public static bool Allows(Interactable it) => !Running || (it != null && it == allowed);
        /// Sağ tık / Esc ile yakın bakıştan çıkış
        public static bool AllowsBack => !Running || backAllowed;
        /// "BELGEYE DÖN" düğmesi / Tab
        public static bool AllowsDocBack => !Running || docBackAllowed;

        // ------------------------------------------------------------------ yaşam döngüsü

        void OnEnable()
        {
            GameEvents.StateChanged += OnState;
            GameEvents.Stamped += OnStamped;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnState;
            GameEvents.Stamped -= OnStamped;
        }

        bool stamped;

        void OnStamped(CustomerData c, bool approved) => stamped = true;

        void OnState(GameState s)
        {
            if (s == GameState.MainMenu) { Stop(); ActiveThisRun = false; return; }
            var gm = GameManager.Instance;
            if (s == GameState.DayIntro && gm != null && gm.Run != null && gm.Run.day == 1 && !Running) // her yeni oyunda (ana menüden BAŞLA)
            {
                StopAllCoroutines();
                StartCoroutine(Run());
            }
        }

        void Stop()
        {
            StopAllCoroutines();
            ClearVisuals();
            Running = false;
            allowed = null;
            backAllowed = docBackAllowed = false;
            if (bar != null) bar.SetActive(false);
        }

        // ------------------------------------------------------------------ akış

        FocusController fc;
        GameManager gm;
        FolderView folder;
        DocLinks links;
        HintGlow folderGlow, approveGlow;

        bool At(FocusState s) => fc != null && fc.State == s && !fc.IsTransitioning;

        IEnumerator Run()
        {
            Running = true;
            ActiveThisRun = true;
            stamped = false;
            BuildBar();
            yield return null; // diğer bileşenler (gazete düğmesi, dosya) kurulsun

            gm = GameManager.Instance;
            fc = FocusController.Instance;
            folder = FolderView.Instance;
            links = folder != null ? folder.GetComponent<DocLinks>() : null;
            var paper = FindAnyObjectByType<NewspaperView>();
            var map = FindAnyObjectByType<MapView>();
            var binder = FindAnyObjectByType<BinderView>();
            var sc = StampController.Instance;
            if (fc == null || gm == null || folder == null || paper == null) { Debug.LogWarning("[İlk tur] eksik nesne, tur atlandı"); Finish(); yield break; }
            if (folderGlow == null) folderGlow = HintGlow.Create(transform, TutorialHints.FolderModel(folder), "Tour_Folder");
            if (approveGlow == null && sc != null && sc.approveStamp != null)
                approveGlow = HintGlow.Create(transform, TutorialHints.LowestPart(sc.approveStamp.transform), "Tour_StampApprove", true);

            // 1) Gazete (kendi çerçevesi gün başında yanıyor)
            Step("<b>Gazeteyi</b> aç", paper.GetComponent<Interactable>());
            yield return new WaitUntil(() => At(FocusState.Newspaper));

            // 2) GÜNE BAŞLA
            Step("", null); // düğmenin üstüne yazı binmesin: sadece çerçeve
            yield return new WaitUntil(() => paper.StartButton != null && paper.StartButton.activeInHierarchy);
            Frame(paper.StartButton.transform as RectTransform, 6f, 6f);
            yield return new WaitUntil(() => gm.State == GameState.Working);
            ClearVisuals();

            // 3) Müşteri gelip belgeyi bırakınca: belge
            Step("Müşteri geliyor…", null);
            yield return new WaitUntil(() => folder.Current != null && At(FocusState.Desk));
            yield return new WaitForSeconds(0.5f); // dosya masaya kaysın
            Show(folderGlow, true);
            Step("Müşterinin bıraktığı <b>belgeyi</b> aç", folder.interactable);
            yield return new WaitUntil(() => At(FocusState.Folder));
            Show(folderGlow, false);

            // 4) HARİTA düğmesi
            Step("Arsayı bulmak için <b>HARİTA</b>'ya bas", links != null ? links.MapLink : null);
            Frame(links != null ? links.MapRect : null, 1.2f, 1.2f);
            yield return new WaitUntil(() => At(FocusState.Map));
            ClearVisuals();

            // 5) Boşluk: tehlikeli bölge (basmadan geçilmez)
            Step($"{Key("Boşluk")}  Tehlikeli bölgeyi göster", null);
            yield return new WaitUntil(() => map == null || map.OverlayVisible);
            yield return new WaitForSeconds(0.7f);

            // 6) Belgeye dön
            Step("12 numaralı arsa tehlikeli bölgenin dışında · <b>BELGEYE DÖN</b>", null, docBack: true);
            if (links != null) { yield return null; Frame(links.BackRect, 5f, 6f); }
            yield return new WaitUntil(() => At(FocusState.Folder));
            ClearVisuals();

            // 7) KURAL (istenen kat)
            Step("İstenen kat için <b>KURAL</b>'a bas", links != null ? links.FloorsLink : null);
            Frame(links != null ? links.FloorsRect : null, 1.2f, 1.2f);
            yield return new WaitUntil(() => At(FocusState.Binder));
            ClearVisuals();

            // 8) Kural satırı + belgeye dön
            yield return new WaitForSeconds(0.35f); // kapak açılsın
            if (binder != null) RowFrame(binder);
            Step("Arsa 150 m² → en fazla 2 kat · İstenen 2 kat: <color=#7CD67C><b>UYGUN</b></color>", null);
            yield return new WaitForSeconds(1.2f);
            Step("Arsa 150 m² → en fazla 2 kat · İstenen 2 kat: <color=#7CD67C><b>UYGUN</b></color> · <b>BELGEYE DÖN</b>", null, docBack: true);
            if (links != null) Frame(links.BackRect, 5f, 6f);
            yield return new WaitUntil(() => At(FocusState.Folder));
            ClearVisuals();

            // 9) ONAY mührü -> KARAR kutusu
            var approveIt = sc != null && sc.approveStamp != null ? sc.approveStamp.GetComponent<Interactable>() : null;
            bool lastHolding = !(sc != null && sc.IsHolding);
            while (!stamped)
            {
                bool holding = sc != null && sc.IsHolding;
                if (holding != lastHolding)
                {
                    lastHolding = holding;
                    ClearVisuals();
                    if (holding)
                    {
                        Step("Formdaki <b>KARAR</b> kutusuna bas", null);
                        Frame(folder.StampAreaRect, 1.4f, 1.5f);
                    }
                    else
                    {
                        Show(approveGlow, true);
                        Step("Başvuru kurallara uygun · <b>ONAY</b> mührünü al", approveIt);
                    }
                }
                yield return null;
            }
            Finish();
        }

        void Finish()
        {
            completedThisSession = true;
            Stop();
        }

        void Step(string text, Interactable only, bool back = false, bool docBack = false)
        {
            allowed = only;
            backAllowed = back;
            docBackAllowed = docBack;
            SetText(text);
        }

        static string Key(string k) => $"<color=#F2B233><b>[{k}]</b></color>";

        static void Show(HintGlow g, bool on)
        {
            if (g != null) g.Show(on);
        }

        // ------------------------------------------------------------------ görseller (çerçeveler)

        readonly System.Collections.Generic.List<GameObject> frames = new System.Collections.Generic.List<GameObject>();

        void Frame(RectTransform target, float thickness, float pad)
        {
            var f = UiGlow.Frame(target, thickness, pad);
            if (f != null) frames.Add(f);
        }

        /// Kural sayfasında "200 m² altı · en fazla 2 kat" satırı (birleşik sayfa görselinde, oranla)
        void RowFrame(BinderView binder)
        {
            var art = binder.PageArtRect;
            if (art == null) return;
            var go = new GameObject("TourRow", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(art, false);
            // Sayfa 648 x 906: satır x 104–612, y 384–420 (üstten)
            rt.anchorMin = new Vector2(104f / 648f, 1f - 420f / 906f);
            rt.anchorMax = new Vector2(612f / 648f, 1f - 384f / 906f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            frames.Add(go);
            float th = Mathf.Max(0.5f, art.rect.width * 0.007f);
            UiGlow.Frame(rt, th, th);
        }

        void ClearVisuals()
        {
            foreach (var f in frames) if (f != null) Destroy(f);
            frames.Clear();
            Show(folderGlow, false);
            Show(approveGlow, false);
        }

        // ------------------------------------------------------------------ alttaki küçük yazı

        GameObject bar;
        RectTransform barRt;
        TextMeshProUGUI barText;

        void BuildBar()
        {
            if (bar != null) { bar.SetActive(true); return; }
            var go = new GameObject("GuidedTourUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            barRt = (RectTransform)bar.transform;
            barRt.SetParent(go.transform, false);
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 36f);
            barRt.sizeDelta = new Vector2(600f, 54f);
            var img = bar.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.78f);
            img.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var trt = (RectTransform)t.transform;
            trt.SetParent(barRt, false);
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(24f, 0f); trt.offsetMax = new Vector2(-24f, 0f);
            barText = t.GetComponent<TextMeshProUGUI>();
            barText.fontSize = 26f;
            barText.color = Color.white;
            barText.alignment = TextAlignmentOptions.Center;
            barText.textWrappingMode = TextWrappingModes.NoWrap;
            barText.raycastTarget = false;
        }

        void SetText(string s)
        {
            if (barText == null) return;
            barText.text = s;
            barText.ForceMeshUpdate();
            float w = barText.preferredWidth + 48f;
            barRt.sizeDelta = new Vector2(Mathf.Clamp(w, 260f, 1500f), 54f);
            bar.SetActive(!string.IsNullOrEmpty(s));
        }
    }

    /// Arayüz elemanının çevresinde sabit (yanıp sönmeyen) turuncu çerçeve
    public static class UiGlow
    {
        public static GameObject Frame(RectTransform target, float thickness, float pad)
        {
            if (target == null) return null;
            var go = new GameObject("TourGlow", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(target, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-pad, -pad); rt.offsetMax = new Vector2(pad, pad);
            rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, -0.3f); // dünya kanvasında kâğıdın biraz önünde
            rt.SetAsLastSibling();
            Bar(rt, new Vector2(0f, 0f), new Vector2(0f, 1f), thickness); // sol
            Bar(rt, new Vector2(1f, 0f), new Vector2(1f, 1f), thickness); // sağ
            Bar(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), thickness); // alt
            Bar(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), thickness); // üst
            return go;
        }

        static void Bar(RectTransform parent, Vector2 aMin, Vector2 aMax, float th)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(th, th);
            var img = go.GetComponent<Image>();
            img.color = HintColors.Orange;
            img.raycastTarget = false;
            var o = go.GetComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(th * 0.35f, th * 0.35f);
        }
    }
}
