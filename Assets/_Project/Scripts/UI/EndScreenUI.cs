using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// - Gün geçişi: 1. günün minigame'i bitince siyah ekranda "2. GÜN" yazar, sonra masaya açılır
    /// - Bitiş ekranı (Ending): KAZANDIN / KAYBETTİN, yaptıkların ve yapamadıkların listesi, ANA MENÜ / OYUNDAN ÇIK
    /// Kaybetme sebepleri: kurallara aykırı binaya onay, rüşvetle onay, 3+ kusurlu bina (depremde çöker), kovulma.
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public class EndScreenUI : MonoBehaviour
    {
        static readonly Color Amber = new Color(0.95f, 0.70f, 0.20f);
        static readonly Color Good = new Color(0.45f, 0.92f, 0.50f);
        static readonly Color Bad = new Color(1f, 0.40f, 0.35f);
        static readonly Color Ink = new Color(0.07f, 0.075f, 0.08f);

        readonly List<CustomerData> angryLeft = new List<CustomerData>();
        int faultsFixed;

        Canvas canvas;
        GameObject dayCard, finish;
        CanvasGroup dayGroup, finishGroup;
        TextMeshProUGUI dayText, title, subtitle, goodText, badText, scoreText;

        void OnEnable()
        {
            GameEvents.StateChanged += OnState;
            GameEvents.CustomerLeft += OnLeft;
            GameEvents.FaultFixed += OnFixed;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnState;
            GameEvents.CustomerLeft -= OnLeft;
            GameEvents.FaultFixed -= OnFixed;
        }

        void OnLeft(CustomerData c, bool angry) { if (angry && c != null) angryLeft.Add(c); }
        void OnFixed(ConstructionSite s) => faultsFixed++;

        void OnState(GameState s)
        {
            if (canvas == null) Build();
            var gm = GameManager.Instance;
            switch (s)
            {
                case GameState.MainMenu:
                    angryLeft.Clear(); faultsFixed = 0;
                    StopAllCoroutines();
                    dayCard.SetActive(false);
                    finish.SetActive(false);
                    break;
                case GameState.DayIntro:
                    finish.SetActive(false);
                    if (gm != null && gm.Run.day >= 2) StartCoroutine(DayCard(gm.Run.day));
                    break;
                case GameState.Ending:
                    StopAllCoroutines();
                    dayCard.SetActive(false);
                    StartCoroutine(ShowFinish());
                    break;
            }
        }

        // ------------------------------------------------------------------ gün geçişi

        IEnumerator DayCard(int day)
        {
            var fc = FocusController.Instance;
            if (fc != null) fc.Back(); // monitörden masaya (siyah ekranın arkasında)
            dayText.text = $"{day}. GÜN\n<size=40%><color=#F2B233>Yeni kurallar klasörde</color></size>";
            dayCard.SetActive(true);
            dayGroup.alpha = 1f;
            for (float t = 0f; t < 1.8f; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < 0.9f; t += Time.unscaledDeltaTime) { dayGroup.alpha = 1f - t / 0.9f; yield return null; }
            dayCard.SetActive(false);
        }

        // ------------------------------------------------------------------ bitiş ekranı

        IEnumerator ShowFinish()
        {
            var good = new List<string>();
            var bad = new List<string>();
            var fatal = new List<string>();
            int correct = 0, total = 0;
            Report(good, bad, fatal, ref correct, ref total);

            bool win = fatal.Count == 0;
            title.text = win ? "KAZANDIN" : "KAYBETTİN";
            title.color = win ? Good : Bad;
            subtitle.text = win
                ? "Karaova'nın binaları ilk depremde ayakta kalacak. Mesai bitti, eline sağlık müfettiş."
                : string.Join("  ·  ", fatal);
            scoreText.text = total > 0 ? $"Doğru karar: <b>{correct} / {total}</b>" : "";
            goodText.text = Lines(good, Good, "Kayda değer bir başarı yok.");
            badText.text = Lines(bad, Bad, "Hiç hata yapmadın!");

            finish.SetActive(true);
            finishGroup.alpha = 0f;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                finishGroup.alpha = k;
                title.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
            }
            finishGroup.alpha = 1f;
            title.transform.localScale = Vector3.one;
        }

        static string Lines(List<string> list, Color c, string empty)
        {
            if (list.Count == 0) return $"<color=#9AA3AB><i>{empty}</i></color>";
            string hex = ColorUtility.ToHtmlStringRGB(c);
            var sb = new StringBuilder();
            foreach (var l in list) sb.Append($"<color=#{hex}>•</color>  {l}\n");
            return sb.ToString();
        }

        void Report(List<string> good, List<string> bad, List<string> fatal, ref int correct, ref int total)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Run == null) return;
            var run = gm.Run;
            var cfg = gm.Config;
            int collapse = cfg != null ? cfg.defectCollapse : 3;

            // Kararlar
            bool unsafeApproval = false, bribeCustomerSeen = false;
            foreach (var d in run.decisions)
            {
                var c = gm.FindCustomer(d.customerId);
                string name = c != null && !string.IsNullOrEmpty(c.displayName) ? c.displayName : d.customerId;
                string why = c != null && cfg != null ? Validator.Explain(c, c.day, cfg) : null;
                if (c != null && c.bribe > 0) bribeCustomerSeen = true;
                total++;
                if (d.approved == d.valid) correct++;
                if (d.approved && d.valid) good.Add($"{name}: doğru onay");
                else if (!d.approved && !d.valid) good.Add($"{name}: doğru ret <color=#9AA3AB>({why})</color>");
                else if (d.approved) { bad.Add($"{name}: kurallara aykırı onay <color=#9AA3AB>({why})</color>"); unsafeApproval = true; }
                else bad.Add($"{name}: haksız ret <color=#9AA3AB>(başvuru uygundu)</color>");
            }
            if (unsafeApproval) fatal.Add("Kurallara aykırı binaya onay verdin");

            foreach (var c in angryLeft)
                bad.Add($"{(c != null ? c.displayName : "Müşteri")}: bekletildi, sinirlenip gitti");

            // Rüşvet
            if (!string.IsNullOrEmpty(run.bribeOnApproved)) { bad.Add("Rüşvet alıp onay verdin"); fatal.Add("Rüşvet aldın"); }
            else if (run.anyBribe) bad.Add("Rüşvet zarfını aldın");
            else if (bribeCustomerSeen) good.Add("Rüşvete yüz vermedin");

            // Monitör minigame'leri
            foreach (var r in MinigameDirector.Results)
            {
                if (r.defects == 0) good.Add($"{r.title} (Şantiye {r.site}): temiz iş");
                else bad.Add($"{r.title} (Şantiye {r.site}): {r.defects} kusur");
            }

            // Şantiyeler
            var cm = ConstructionManager.Instance;
            if (cm != null)
            {
                foreach (var s in cm.Sites)
                {
                    string owner = s.customer != null ? s.customer.displayName : "";
                    if (s.defects >= collapse) { bad.Add($"Şantiye {s.letter} ({owner}): {s.defects} kusur, depremde çöker"); fatal.Add($"Şantiye {s.letter} depremde çöker"); }
                    else if (s.defects > 0) bad.Add($"Şantiye {s.letter} ({owner}): {s.defects} kusur");
                    else good.Add($"Şantiye {s.letter} ({owner}): kusursuz");
                }
            }
            if (faultsFixed > 0) good.Add($"{faultsFixed} hatalı işçiyi yakaladın");

            if (run.pendingFired)
            {
                fatal.Insert(0, "Kovuldun");
                if (run.wrongfulRejects >= (cfg != null ? cfg.wrongfulRejectLimit : 3)) bad.Add($"{run.wrongfulRejects} haksız ret: kovuldun");
                else bad.Add($"{run.penalties} ceza: kovuldun");
            }
        }

        void ToMenu()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnRestart();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ arayüz

        void Build()
        {
            var go = new GameObject("EndScreens", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)go.transform;

            // Gün kartı
            dayCard = Panel(root, "DayCard", Color.black);
            dayGroup = dayCard.AddComponent<CanvasGroup>();
            dayText = Label((RectTransform)dayCard.transform, "", 120f, Color.white, TextAlignmentOptions.Center);
            Stretch(dayText.rectTransform);
            dayText.fontStyle = FontStyles.Bold;
            dayText.characterSpacing = 10f;
            dayCard.SetActive(false);

            // Bitiş ekranı
            finish = Panel(root, "Finish", new Color(0.035f, 0.04f, 0.045f, 0.97f));
            finishGroup = finish.AddComponent<CanvasGroup>();
            var f = (RectTransform)finish.transform;

            title = Label(f, "", 120f, Color.white, TextAlignmentOptions.Center);
            Place(title.rectTransform, 0.5f, 1f, new Vector2(0f, -100f), new Vector2(1400f, 150f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 12f;

            subtitle = Label(f, "", 30f, new Color(0.88f, 0.9f, 0.92f), TextAlignmentOptions.Center);
            Place(subtitle.rectTransform, 0.5f, 1f, new Vector2(0f, -225f), new Vector2(1500f, 50f));
            subtitle.textWrappingMode = TextWrappingModes.Normal;

            scoreText = Label(f, "", 30f, Amber, TextAlignmentOptions.Center);
            Place(scoreText.rectTransform, 0.5f, 1f, new Vector2(0f, -285f), new Vector2(1000f, 44f));

            goodText = Column(f, "YAPTIKLARIN", Good, -420f);
            badText = Column(f, "YAPAMADIKLARIN", Bad, 420f);

            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var brt = (RectTransform)buttons.transform;
            brt.SetParent(f, false);
            Place(brt, 0.5f, 0f, new Vector2(0f, 70f), new Vector2(760f, 80f));
            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 30f; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true;
            Button(brt, "ANA MENÜ", true, ToMenu);
            var quit = Button(brt, "OYUNDAN ÇIK", false, Quit);
#if UNITY_WEBGL && !UNITY_EDITOR
            quit.SetActive(false);
#endif
            finish.SetActive(false);
        }

        TextMeshProUGUI Column(RectTransform parent, string header, Color color, float x)
        {
            var box = Panel(parent, header, new Color(1f, 1f, 1f, 0.05f));
            var rt = (RectTransform)box.transform;
            Place(rt, 0.5f, 0.5f, new Vector2(x, -70f), new Vector2(780f, 560f));
            var head = Label(rt, header, 34f, color, TextAlignmentOptions.TopLeft);
            Place(head.rectTransform, 0f, 1f, new Vector2(390f, -42f), new Vector2(720f, 50f));
            head.fontStyle = FontStyles.Bold;
            head.characterSpacing = 6f;
            var line = Panel(rt, "Line", color);
            Place((RectTransform)line.transform, 0f, 1f, new Vector2(390f, -78f), new Vector2(720f, 3f));
            var body = Label(rt, "", 26f, new Color(0.92f, 0.93f, 0.94f), TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, 0f, 1f, new Vector2(390f, -96f - 220f), new Vector2(720f, 440f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 16f; body.fontSizeMax = 26f;
            body.lineSpacing = 10f;
            return body;
        }

        GameObject Button(RectTransform parent, string text, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;
            var btn = go.GetComponent<Button>();
            var c = btn.colors;
            c.normalColor = primary ? Amber : new Color(1f, 1f, 1f, 0.10f);
            c.highlightedColor = primary ? new Color(1f, 0.82f, 0.38f) : new Color(1f, 1f, 1f, 0.24f);
            c.pressedColor = primary ? new Color(0.78f, 0.55f, 0.15f) : new Color(1f, 1f, 1f, 0.34f);
            c.selectedColor = c.normalColor;
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var label = Label((RectTransform)go.transform, text, 32f, primary ? Ink : Color.white, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 4f;
            return go;
        }

        static GameObject Panel(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return go;
        }

        static TextMeshProUGUI Label(RectTransform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            return t;
        }

        /// anchor (ax, ay) noktasına göre ortası pos'ta, boyutu size olan kutu
        static void Place(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
