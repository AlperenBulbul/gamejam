using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing
{
    /// - Gün geçişi: 1. günün minigame'i bitince bulanık masanın üstünde "2. gün" yazar, tıklayınca masaya açılır
    /// - Bitiş ekranı (Ending): denetim raporu kâğıdı + son mührü, doğru yapılanlar / hatalar, Ana menüye dön / Oyundan çık
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public class EndScreenUI : MonoBehaviour
    {

        /// Gün kartı açık: masa kontrolleri kapalı
        public static bool Blocking { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Blocking = false;

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
                    Blocking = false;
                    dayCard.SetActive(false);
                    finish.SetActive(false);
                    break;
                case GameState.DayIntro:
                    finish.SetActive(false);
                    if (gm != null && gm.Run.day >= 2) StartCoroutine(DayCard(gm.Run.day));
                    break;
                case GameState.Ending:
                    StopAllCoroutines();
                    Blocking = false;
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
            dayText.text = $"{day}. gün\n<size=30%><color=#D8D2C4>Kural kitapçığına yeni kural eklendi</color></size>";
            dayCard.SetActive(true);
            dayGroup.alpha = 1f;
            dayBlur.color = new Color(0.5f, 0.5f, 0.5f, 0f);
            Blocking = true; // kart açıkken masa kapalı (tıklama gazeteyi açmasın)

            // Kamera masaya dönünce arkadaki masa görüntüsünü bulanıklaştır (siyah yerine)
            float wait = 0f;
            while (fc != null && fc.IsTransitioning && wait < 1.5f) { wait += Time.unscaledDeltaTime; yield return null; }
            yield return null;
            if (RenderBlur())
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    wait += Time.unscaledDeltaTime;
                    dayBlur.color = new Color(0.5f, 0.5f, 0.5f, t / 0.35f);
                    yield return null;
                }
            dayBlur.color = new Color(0.5f, 0.5f, 0.5f, dayBlur.texture != null ? 1f : 0f);

            while (true)
            {
                wait += Time.unscaledDeltaTime;
                var m = UnityEngine.InputSystem.Mouse.current; var kb = UnityEngine.InputSystem.Keyboard.current;
                bool click = (m != null && m.leftButton.wasPressedThisFrame) ||
                             (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
                if (wait > 0.6f && click && !PauseMenu.IsPaused) break;
                yield return null;
            }
            yield return null; // geçiş tıklaması masaya geçmesin
            Blocking = false;
            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime) { dayGroup.alpha = 1f - t / 0.6f; yield return null; }
            dayCard.SetActive(false);
        }

        // ------------------------------------------------------------------ bulanık arka plan (gün kartı)

        RawImage dayBlur;
        RenderTexture dayBlurRT;

        /// Ana kamerayı (arayüzsüz) küçük bir dokuya çizip birkaç kez küçült / büyüt: yumuşak bulanıklık
        bool RenderBlur()
        {
            var fc = FocusController.Instance;
            var cam = fc != null ? fc.GetComponentInChildren<Camera>() : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;
            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(36, Screen.height / 2);
            var cur = RenderTexture.GetTemporary(w, h, 24);
            cur.filterMode = FilterMode.Bilinear;
            var prev = cam.targetTexture;
            try { cam.targetTexture = cur; cam.Render(); }
            catch (System.Exception e) { Debug.LogWarning("[DayCard] blur: " + e.Message); cam.targetTexture = prev; RenderTexture.ReleaseTemporary(cur); return false; }
            cam.targetTexture = prev;
            for (int i = 0; i < 4; i++) cur = BlurStep(cur, cur.width / 2, cur.height / 2);
            for (int i = 0; i < 3; i++) cur = BlurStep(cur, cur.width * 2, cur.height * 2);
            if (dayBlurRT == null || dayBlurRT.width != cur.width || dayBlurRT.height != cur.height)
            {
                if (dayBlurRT != null) { dayBlurRT.Release(); Destroy(dayBlurRT); }
                dayBlurRT = new RenderTexture(cur.width, cur.height, 0) { filterMode = FilterMode.Bilinear };
            }
            Graphics.Blit(cur, dayBlurRT);
            RenderTexture.ReleaseTemporary(cur);
            dayBlur.texture = dayBlurRT;
            return true;
        }

        static RenderTexture BlurStep(RenderTexture src, int w, int h)
        {
            var dst = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0);
            dst.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, dst);
            RenderTexture.ReleaseTemporary(src);
            return dst;
        }

        void OnDestroy()
        {
            if (dayBlurRT != null) { dayBlurRT.Release(); Destroy(dayBlurRT); }
        }

        // ------------------------------------------------------------------ bitiş ekranı

        IEnumerator ShowFinish()
        {
            var gm = GameManager.Instance;
            var end = EndingRules.Resolve(gm);
            Debug.Log($"[Son] {end.id}{(end.featured != null ? " (" + end.featured.id + ")" : "")}{(end.badge ? " + Yılın Denetçisi" : "")}");

            var good = new List<string>();
            var bad = new List<string>();
            int correct = 0, total = 0;
            Report(good, bad, ref correct, ref total, end);

            var bdName = ImageName(end.id);
            var bdTex = bdName != null ? Resources.Load<Texture2D>("Endings/" + bdName) : null;
            if (backdrop != null) { backdrop.texture = bdTex; backdrop.gameObject.SetActive(bdTex != null); }
            finish.GetComponent<Image>().color = bdTex != null ? Color.black : new Color(0.05f, 0.045f, 0.04f, 0.92f);

            title.text = end.title;
            SetStampColor(end.good ? StampGreen : end.neutral ? StampOchre : StampRed);
            string sub = end.body;
            if (end.featured != null) sub += $"\n\n<size=78%><i>Dosya: {end.featured.displayName}, ada {end.featured.ada}, {end.featured.floors} kat</i></size>";
            if (end.badge) sub += "\n\n<size=85%><b><i>Yılın Denetçisi plaketi</i></b></size>";
            subtitle.text = sub;
            scoreText.text = $"Sonuç: {EndingRules.ReasonLabel(end.id)}" + (total > 0 ? $"     Doğru karar: {correct} / {total}" : "");
            goodText.text = Lines(good, "Kayda değer bir şey yok.");
            badText.text = Lines(bad, "Hata yok.");

            // Rapor hazır; sinematik varsa onun altında bekler (video bitince arada oyun sahnesi görünmesin)
            bool hasVideo = FindVideo(end) != null;
            finish.SetActive(true);
            finishGroup.alpha = hasVideo ? 1f : 0f;
            if (stampRt != null) stampRt.localScale = Vector3.one * 1.6f;
            yield return PlayEndingVideo(end);

            for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.6f;
                finishGroup.alpha = Mathf.Max(finishGroup.alpha, k);
                if (stampRt != null) stampRt.localScale = Vector3.one * Mathf.Lerp(1.6f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.6f))); // mühür basılır
                yield return null;
            }
            finishGroup.alpha = 1f;
            if (stampRt != null) stampRt.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ bitiş videosu (tasarımcı, 17 Ekim)

        /// Resources/EndingVideos altındaki sinematik. Hâlâ İşte için video yok.
        static string VideoName(EndingResult end) => end.id switch
        {
            EndingId.Jail_Quake => "CIN_End_Jail_Quake",
            EndingId.Jail_Flood => "CIN_End_Jail_Flood",
            EndingId.Jail_Bribe => "CIN_End_Jail_Bribe",
            EndingId.Fired_FaultLine => "CIN_End_Fired_Fault",
            EndingId.Fired_Flood => "CIN_End_Fired_Flood",
            EndingId.Fired_WrongfulRejects => "CIN_End_Fired_NoPermit",
            EndingId.Fired_Penalties => "CIN_End_Fired_TurnedAway",
            EndingId.Fired_Bankrupt => "CIN_End_Fired_Bankrupt",
            EndingId.NoOneDied => end.badge ? "CIN_End_NoOneDied_Plaque" : "CIN_End_NoOneDied",
            _ => null,
        };

        /// Rapor kâğıdının arkasındaki boyalı görsel (sinematiğin son sahnesi): Resources/Endings
        static string ImageName(EndingId id) => id switch
        {
            EndingId.Jail_Quake => "IMG_End_Jail_Quake",
            EndingId.Jail_Flood => "IMG_End_Jail_Flood",
            EndingId.Jail_Bribe => "IMG_End_Jail_Bribe",
            EndingId.NoOneDied => "IMG_End_NoOneDied",
            EndingId.StillOnTheJob => null,
            _ => "IMG_End_Fired",
        };

        static UnityEngine.Video.VideoClip FindVideo(EndingResult end)
        {
            string n = VideoName(end);
            if (n == null) return null;
            var clip = Resources.Load<UnityEngine.Video.VideoClip>("EndingVideos/" + n);
            if (clip == null && end.badge) clip = Resources.Load<UnityEngine.Video.VideoClip>("EndingVideos/CIN_End_NoOneDied");
            return clip;
        }

        static bool ClickedThisFrame()
        {
            var m = UnityEngine.InputSystem.Mouse.current; var kb = UnityEngine.InputSystem.Keyboard.current;
            return (m != null && m.leftButton.wasPressedThisFrame) ||
                   (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
        }

        /// Sinematik oynar. Tıklanırsa sondaki yazı kartına atlar; video bitince son karede (yazı kartı) bekler,
        /// tıklayınca rapora geçilir. Ses videonun içinde.
        IEnumerator PlayEndingVideo(EndingResult end)
        {
            var clip = FindVideo(end);
            if (clip == null) yield break;

            var back = new GameObject("VideoBack", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)back.transform);
            back.GetComponent<Image>().color = Color.black;

            var go = new GameObject("EndingVideo", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var img = go.GetComponent<RawImage>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rtex = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
            var vp = go.AddComponent<UnityEngine.Video.VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.waitForFirstFrame = true;
            vp.clip = clip;
            vp.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            vp.targetTexture = rtex;
            vp.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.Direct;
            bool ended = false;
            vp.loopPointReached += v => { ended = true; v.Pause(); }; // son karede (yazı kartı) kalsın
            vp.Prepare();
            for (float w = 0f; !vp.isPrepared && w < 6f; w += Time.unscaledDeltaTime) yield return null;

            // Sağ altta küçük "devam" ipucu (video bitince görünür)
            var hint = new GameObject("Hint", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
            var hr = (RectTransform)hint.transform;
            hr.SetParent(canvas.transform, false);
            hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-48f, 40f);
            hr.localScale = Vector3.one * 1.3f;
            var hl = hint.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f; hl.childAlignment = TextAnchor.MiddleRight;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var hf = hint.GetComponent<ContentSizeFitter>();
            hf.horizontalFit = hf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            KeyIcons.Add(hr, "LMB", null);
            Label(hr, "Devam", 22f, new Color(0.85f, 0.83f, 0.78f), TextAlignmentOptions.MidlineLeft);
            var hintGroup = hint.GetComponent<CanvasGroup>();
            hintGroup.alpha = 0f;

            if (vp.isPrepared)
            {
                img.texture = rtex;
                img.color = Color.white;
                var fit = go.AddComponent<AspectRatioFitter>(); // oranı bozmadan ekranı kapla
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = clip.width > 0 && clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;
                vp.Play();

                double length = clip.length;
                float t = 0f;
                bool skipped = false;
                while (true)
                {
                    t += Time.unscaledDeltaTime;
                    bool done = ended || (t > 1f && !vp.isPlaying && !vp.isPaused) || t > length + 3.0;
                    if (done)
                    {
                        hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, 1f, Time.unscaledDeltaTime * 2f);
                        if (ClickedThisFrame() && hintGroup.alpha > 0.3f) break;
                    }
                    else if (t > 0.8f && ClickedThisFrame())
                    {
                        if (!skipped) { skipped = true; vp.time = System.Math.Max(0.0, length - 0.15); } // yazı kartına atla
                        else break;
                    }
                    yield return null;
                }
            }
            vp.Stop();
            Destroy(hint);
            Destroy(go);
            Destroy(back);
            rtex.Release();
            Destroy(rtex);
            yield return null; // tıklama rapordaki düğmelere geçmesin
        }

        static string Lines(List<string> list, string empty)
        {
            if (list.Count == 0) return $"<color=#6A655D><i>{empty}</i></color>";
            var sb = new StringBuilder();
            foreach (var l in list) sb.Append("–  ").Append(l).Append('\n');
            return sb.ToString();
        }

        void Report(List<string> good, List<string> bad, ref int correct, ref int total, EndingResult end)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Run == null) return;
            var run = gm.Run;
            var cfg = gm.Config;

            // Kararlar
            bool bribeCustomerSeen = false;
            foreach (var d in run.decisions)
            {
                var c = gm.FindCustomer(d.customerId);
                string name = c != null && !string.IsNullOrEmpty(c.displayName) ? c.displayName : d.customerId;
                string why = c != null && cfg != null ? Validator.Explain(c, c.day, cfg) : null;
                if (c != null && c.bribe > 0) bribeCustomerSeen = true;
                total++;
                if (d.approved == d.valid) correct++;
                if (d.approved && d.valid) good.Add($"{name}: doğru onay");
                else if (!d.approved && !d.valid) good.Add($"{name}: doğru ret <color=#6A655D>({why})</color>");
                else if (d.approved) bad.Add($"{name}: kurallara aykırı onay <color=#6A655D>({why})</color>");
                else bad.Add($"{name}: haksız ret <color=#6A655D>(başvuru uygundu)</color>");
            }

            foreach (var c in angryLeft)
                bad.Add($"{(c != null ? c.displayName : "Müşteri")}: bekletildi, sinirlenip gitti");

            // Rüşvet
            if (run.bribeTakenDay > 0) bad.Add("Rüşvet zarfını aldın");
            else if (bribeCustomerSeen) good.Add("Rüşvete yüz vermedin");

            // Monitör minigame'leri
            foreach (var r in MinigameDirector.Results)
            {
                if (r.defects == 0) good.Add($"{r.title} (Şantiye {r.site}): temiz iş");
                else bad.Add($"{r.title} (Şantiye {r.site}): {r.defects} hata");
            }

            // Şantiyeler: deprem ve sel (kovulduysan felaketi masandan izlemedin)
            bool fired = run.fireReason != FireReason.None;
            var cm = ConstructionManager.Instance;
            if (cm != null)
            {
                foreach (var s in cm.Sites)
                {
                    string owner = s.customer != null ? s.customer.displayName : "";
                    if (fired)
                    {
                        if (s.defects > 0) bad.Add($"Şantiye {s.letter} ({owner}): {s.defects} hata");
                        continue;
                    }
                    switch (EndingRules.Outcome(s, cfg))
                    {
                        case SiteOutcome.CollapsedQuake: bad.Add($"Şantiye {s.letter} ({owner}): depremde çöktü"); break;
                        case SiteOutcome.CollapsedFlood: bad.Add($"Şantiye {s.letter} ({owner}): selde yıkıldı"); break;
                        case SiteOutcome.Damaged: bad.Add($"Şantiye {s.letter} ({owner}): depremde çatladı ({s.defects} hata), kimse ölmedi"); break;
                        default: good.Add($"Şantiye {s.letter} ({owner}): depremde ayakta"); break;
                    }
                }
            }
            if (faultsFixed > 0) good.Add($"{faultsFixed} hatalı işçiyi yakaladın");
            if (fired) bad.Insert(0, $"<b>{EndingRules.ReasonLabel(end.id)}</b> (gün {run.day} sonu denetimi)");
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
            var blurGo = new GameObject("Blur", typeof(RectTransform), typeof(RawImage));
            blurGo.transform.SetParent(dayCard.transform, false);
            Stretch((RectTransform)blurGo.transform);
            dayBlur = blurGo.GetComponent<RawImage>();
            dayBlur.raycastTarget = false;
            dayBlur.color = new Color(0.5f, 0.5f, 0.5f, 0f);
            dayText = Label((RectTransform)dayCard.transform, "", 120f, Color.white, TextAlignmentOptions.Center);
            Stretch(dayText.rectTransform);
            dayText.fontStyle = FontStyles.Bold;
            dayText.color = new Color(0.95f, 0.93f, 0.88f);
            dayText.lineSpacing = 10f;

            // Altta sabit küçük ipucu: [sol tık] Devam (yanıp sönen büyük harfli yazı yok)
            var dayHint = new GameObject("Hint", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var dhr = (RectTransform)dayHint.transform;
            dhr.SetParent(dayCard.transform, false);
            dhr.anchorMin = dhr.anchorMax = new Vector2(0.5f, 0f);
            dhr.pivot = new Vector2(0.5f, 0.5f);
            dhr.anchoredPosition = new Vector2(0f, 110f);
            dhr.localScale = Vector3.one * 1.3f;
            var dhl = dayHint.GetComponent<HorizontalLayoutGroup>();
            dhl.spacing = 10f; dhl.childAlignment = TextAnchor.MiddleCenter;
            dhl.childControlWidth = dhl.childControlHeight = true;
            dhl.childForceExpandWidth = dhl.childForceExpandHeight = false;
            var dfit = dayHint.GetComponent<ContentSizeFitter>();
            dfit.horizontalFit = dfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            KeyIcons.Add(dhr, "LMB", null);
            Label(dhr, "Devam", 22f, new Color(0.85f, 0.83f, 0.78f), TextAlignmentOptions.MidlineLeft);
            dayCard.SetActive(false);

            // Bitiş ekranı: masaya bırakılmış bir denetim raporu (kâğıt + mühür), süslü kutu / parlama yok
            finish = Panel(root, "Finish", new Color(0.05f, 0.045f, 0.04f, 0.92f));
            finishGroup = finish.AddComponent<CanvasGroup>();
            var f = (RectTransform)finish.transform;

            // Sinematiğin son sahnesi (boyalı görsel), koyulaştırılmış: kâğıt öne çıksın
            var bdGo = new GameObject("Backdrop", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            backdrop = bdGo.GetComponent<RawImage>();
            var bdr = (RectTransform)bdGo.transform;
            bdr.SetParent(f, false);
            Stretch(bdr);
            var bdFit = bdGo.GetComponent<AspectRatioFitter>();
            bdFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bdFit.aspectRatio = 16f / 9f;
            backdrop.color = new Color(0.38f, 0.37f, 0.36f, 1f);
            backdrop.raycastTarget = false;
            bdGo.SetActive(false);

            // Kâğıdın gölgesi + kâğıt (hafif eğik)
            var shadow = Panel(f, "PaperShadow", new Color(0f, 0f, 0f, 0.45f));
            Place((RectTransform)shadow.transform, 0.5f, 0.5f, new Vector2(10f, -12f), new Vector2(1240f, 960f));
            shadow.transform.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
            var paperGo = Panel(f, "Paper", Paper);
            var paper = (RectTransform)paperGo.transform;
            Place(paper, 0.5f, 0.5f, new Vector2(0f, 0f), new Vector2(1240f, 960f));
            paper.localRotation = Quaternion.Euler(0f, 0f, -0.8f);

            // Antet
            var org = Label(paper, "Karaova Belediyesi  ·  Yapı Denetim Müdürlüğü", 22f, InkSoft, TextAlignmentOptions.MidlineLeft);
            Place(org.rectTransform, 0f, 1f, new Vector2(80f + 380f, -64f), new Vector2(760f, 34f));
            var docNo = Label(paper, "Denetçi dosyası  ·  2026/LB", 22f, InkSoft, TextAlignmentOptions.MidlineRight);
            Place(docNo.rectTransform, 1f, 1f, new Vector2(-80f - 230f, -64f), new Vector2(460f, 34f));
            var head = Label(paper, "Denetim raporu", 46f, PaperInk, TextAlignmentOptions.MidlineLeft);
            Place(head.rectTransform, 0f, 1f, new Vector2(80f + 380f, -116f), new Vector2(760f, 60f));
            head.fontStyle = FontStyles.Bold;
            Rule(paper, -152f, 2f);

            // Sonuç cümlesi (sol) + mühür (sağ üst)
            subtitle = Label(paper, "", 32f, PaperInk, TextAlignmentOptions.TopLeft);
            Place(subtitle.rectTransform, 0f, 1f, new Vector2(80f + 340f, -270f), new Vector2(680f, 200f));
            subtitle.textWrappingMode = TextWrappingModes.Normal;
            subtitle.fontStyle = FontStyles.Italic;
            subtitle.lineSpacing = 8f;

            var stampGo = new GameObject("Stamp", typeof(RectTransform));
            stampRt = (RectTransform)stampGo.transform;
            stampRt.SetParent(paper, false);
            Place(stampRt, 1f, 1f, new Vector2(-80f - 190f, -270f), new Vector2(380f, 150f));
            stampRt.localRotation = Quaternion.Euler(0f, 0f, 7f);
            stampBars.Clear();
            StampBar(new Vector2(0f, 0f), new Vector2(0f, 1f));
            StampBar(new Vector2(1f, 0f), new Vector2(1f, 1f));
            StampBar(new Vector2(0f, 0f), new Vector2(1f, 0f));
            StampBar(new Vector2(0f, 1f), new Vector2(1f, 1f));
            title = Label(stampRt, "", 52f, StampRed, TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            title.rectTransform.offsetMin = new Vector2(16f, 8f); title.rectTransform.offsetMax = new Vector2(-16f, -8f);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 2f;
            title.enableAutoSizing = true; title.fontSizeMin = 28f; title.fontSizeMax = 52f;
            title.textWrappingMode = TextWrappingModes.Normal;

            scoreText = Label(paper, "", 24f, InkSoft, TextAlignmentOptions.MidlineLeft);
            Place(scoreText.rectTransform, 0f, 1f, new Vector2(80f + 540f, -392f), new Vector2(1080f, 36f));
            Rule(paper, -420f, 1f);

            goodText = Column(paper, "Doğru yapılanlar", 80f);
            badText = Column(paper, "Hatalar", 660f);

            // Düğmeler: düz, mürekkep rengi
            var buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var brt = (RectTransform)buttons.transform;
            brt.SetParent(paper, false);
            Place(brt, 0.5f, 0f, new Vector2(0f, 70f), new Vector2(640f, 68f));
            var h = buttons.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 24f; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true;
            Button(brt, "Ana menüye dön", true, ToMenu);
            var quit = Button(brt, "Oyundan çık", false, Quit);
#if UNITY_WEBGL && !UNITY_EDITOR
            quit.SetActive(false);
#endif
            finish.SetActive(false);
        }

        static readonly Color Paper = new Color(0.937f, 0.914f, 0.859f);
        static readonly Color PaperInk = new Color(0.13f, 0.12f, 0.11f);
        static readonly Color InkSoft = new Color(0.40f, 0.38f, 0.35f);
        static readonly Color StampRed = new Color(0.70f, 0.20f, 0.16f);
        static readonly Color StampGreen = new Color(0.18f, 0.47f, 0.27f);
        static readonly Color StampOchre = new Color(0.55f, 0.42f, 0.10f);

        RawImage backdrop;
        RectTransform stampRt;
        readonly List<Image> stampBars = new List<Image>();

        void StampBar(Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(stampRt, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(6f, 6f);
            var img = go.GetComponent<Image>();
            img.color = StampRed;
            img.raycastTarget = false;
            stampBars.Add(img);
        }

        void SetStampColor(Color c)
        {
            c.a = 0.88f; // mürekkep hafif soluk
            foreach (var b in stampBars) if (b != null) b.color = c;
            if (title != null) title.color = c;
        }

        static void Rule(RectTransform paper, float y, float thickness)
        {
            var line = Panel(paper, "Rule", new Color(0.13f, 0.12f, 0.11f, 0.55f));
            var rt = (RectTransform)line.transform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(80f, y - thickness * 0.5f); rt.offsetMax = new Vector2(-80f, y + thickness * 0.5f);
        }

        TextMeshProUGUI Column(RectTransform paper, string header, float left)
        {
            var head = Label(paper, header, 28f, PaperInk, TextAlignmentOptions.MidlineLeft);
            Place(head.rectTransform, 0f, 1f, new Vector2(left + 250f, -462f), new Vector2(500f, 40f));
            head.fontStyle = FontStyles.Bold;
            var body = Label(paper, "", 22f, PaperInk, TextAlignmentOptions.TopLeft);
            Place(body.rectTransform, 0f, 1f, new Vector2(left + 250f, -490f - 160f), new Vector2(500f, 320f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.enableAutoSizing = true;
            body.fontSizeMin = 15f; body.fontSizeMax = 22f;
            body.lineSpacing = 6f;
            return body;
        }

        GameObject Button(RectTransform parent, string text, bool primary, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Color.white;
            var btn = go.GetComponent<Button>();
            var c = btn.colors;
            c.normalColor = primary ? PaperInk : new Color(0.13f, 0.12f, 0.11f, 0.06f);
            c.highlightedColor = primary ? new Color(0.25f, 0.23f, 0.21f) : new Color(0.13f, 0.12f, 0.11f, 0.14f);
            c.pressedColor = primary ? new Color(0.05f, 0.05f, 0.05f) : new Color(0.13f, 0.12f, 0.11f, 0.22f);
            c.selectedColor = c.normalColor;
            c.fadeDuration = 0.08f;
            btn.colors = c;
            btn.onClick.AddListener(onClick);
            var label = Label((RectTransform)go.transform, text, 26f, primary ? Paper : PaperInk, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
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
