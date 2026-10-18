using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LoadBearing
{
    /// Gün sonu monitör minigame'i.
    /// Son müşteri çıkınca (GameManager.OnLastCustomerLeft) telsiz cızırdar, altyazıda ustanın sesi duyulur,
    /// monitör turuncu yanıp söner. Oyuncu monitöre bakınca ekran parazitle açılır ve minigame başlar:
    ///  1. gün: Mikser bandı (malzeme ayıklama)   2. gün: Kolon dökümü (şakül + deprem testi)
    /// Sonuçtaki kusurlar o şantiyeye yazılır, sonra gün normal akışına döner (fiş / son vardiya).
    /// Şantiye yoksa (hiç onay verilmediyse) minigame atlanır. GameManager bu bileşeni kendisi ekler.
    public class MinigameDirector : MonoBehaviour
    {
        public static MinigameDirector Instance { get; private set; }
        /// Minigame monitör ekranında (kanal düğmeleri kapalı)
        public static bool OnScreen => Instance != null && Instance.screenOn;
        /// Tuş listesi için: minigame ekrandayken gösterilecek tuşlar (yoksa null)
        public static (string key, string action)[] Hints => OnScreen && Instance.game != null && Instance.playing ? Instance.game.Hints : null;

        enum Kind { Mixer, Column }

        /// Oynanan minigame'lerin sonuçları (bitiş ekranı için)
        public struct Result { public int day; public string title; public int defects; public char site; }
        public static readonly System.Collections.Generic.List<Result> Results = new System.Collections.Generic.List<Result>();

        const float RtW = 1280f, RtH = 720f;

        MgGame game;
        ConstructionSite site;
        int siteIndex;
        Action onDone;
        bool running, screenOn, playing;
        int doneDay;

        Canvas canvas;
        RectTransform screenRoot, gameRoot;
        CanvasGroup screenGroup;
        RawImage noise, flash;
        TextMeshProUGUI stripText, defectText, cardTitle, cardBody, cardHint;
        RawImage cardBg, cardImg, cardDim;
        TextMeshProUGUI cardCaption;
        Texture2D noiseTex;
        Color32[] noisePx;
        HintGlow monitorGlow, radioGlow;
        AudioSource radioAudio, radioHiss;
        AudioClip hissClip;
        Light radioLight;
        AudioClip crackle;
        CustomerData radioVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Results.Clear(); IntroOpen = false; }

        void Awake() => Instance = this;

        // ------------------------------------------------------------------ "Monitöre git"

        const float NudgeDelay = 5f;
        bool wantMonitor;
        float awayTime;

        void Update()
        {
            if (wantMonitor && !OnMonitor && !PauseMenu.IsPaused) awayTime += Time.deltaTime;
            else awayTime = 0f;
            PromptBar.Set("monitor", wantMonitor && awayTime >= NudgeDelay ? "<b>Monitöre</b> git" : null);
        }

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        void OnStateChanged(GameState s)
        {
            if (s == GameState.MainMenu) { StopAll(); doneDay = 0; Results.Clear(); }
        }

        // ------------------------------------------------------------------ başlat

        /// true: minigame başladı, bittiğinde done çağrılır. false: atlandı, akış hemen devam etmeli.
        public bool TryStart(int day, Action done)
        {
            if (running || doneDay == day) return false;
            var cm = ConstructionManager.Instance;
            if (cm == null || cm.Sites.Count == 0) return false;

            // En son açılan, hâlâ yapılan şantiye (yoksa en sonuncusu)
            siteIndex = -1;
            for (int i = cm.Sites.Count - 1; i >= 0; i--)
                if (cm.Sites[i].IsBuilding) { siteIndex = i; break; }
            if (siteIndex < 0) siteIndex = cm.Sites.Count - 1;
            site = cm.Sites[siteIndex];

            doneDay = day;
            onDone = done;
            running = true;
            StartCoroutine(Flow(day <= 1 ? Kind.Mixer : Kind.Column));
            return true;
        }

        IEnumerator Flow(Kind kind)
        {
            Setup();
            var cm = ConstructionManager.Instance;
            game = kind == Kind.Mixer ? new MixerGame() : (MgGame)new ColumnGame();

            yield return new WaitForSeconds(1.5f);

            // ---- Telsiz
            string L = site != null ? site.letter.ToString() : "";
            string[] call = kind == Kind.Mixer
                ? new[] { $"Şefim, {L} şantiyesinden arıyorum! Müteahhit malzemeden kaçıyor, mikserin bandına çimentonun yanına deniz kumu karıştırmışlar!",
                          "Monitöre geç, deniz kumu çuvallarını banttan at. İnşaatı sen kurtaracaksın şefim!" }
                : new[] { $"Şefim, {L} şantiyesinde kolonları dökmeye başladık ama kalıplar sallanıyor, şakülünde değil!",
                          "Yarın deprem testi var! Monitörden bak, kolonları dimdik dök ki bina ayakta kalsın!" };

            Show(monitorGlow, true); // telsizin etrafında parıltı yok (sadece ses + ışık)
            wantMonitor = true; // 5 sn içinde monitöre geçmezse altta "Monitöre git" yazar
            foreach (var line in call) yield return RadioLine(line);

            // ---- Oyuncu monitöre baksın
            while (!OnMonitor) yield return null;
            Show(monitorGlow, false);
            if (cm != null && siteIndex >= 0) cm.Select(siteIndex);

            // ---- Ekran açılışı: parazit + tüp açılması
            BuildScreen();
            gameRoot = MgUI.Group(screenRoot, "Game");
            gameRoot.SetSiblingIndex(0);
            game.Init(gameRoot, site, siteIndex);
            screenOn = true;
            yield return Boot();

            // ---- Tanıtım: ekran bulanıklaşır, ne yapılacağı tek bakışta; tıkla, başla
            UpdateStrip();
            yield return Intro();

            // ---- Oyun (sadece monitöre bakarken ilerler)
            playing = true;
            while (!game.Finished)
            {
                if (OnMonitor)
                {
                    Show(monitorGlow, false);
                    bool action = Pressed(out var click);
                    game.Tick(Time.deltaTime, click, action);
                }
                else Show(monitorGlow, true);
                UpdateStrip();
                yield return null;
            }
            playing = false;
            wantMonitor = false;
            Show(monitorGlow, false);

            // ---- Sonuç
            int defects = game.Defects;
            if (site != null) site.defects += defects;
            var gmr = GameManager.Instance;
            Results.Add(new Result { day = gmr != null ? gmr.Run.day : 0, title = kind == Kind.Mixer ? "Mikser kontrolü" : "Kolon dökümü", defects = defects, site = site != null ? site.letter : '?' });
            // Sonuç ekranı (bulanık, tıkla devam)
            yield return ShowResult(defects, L);

            // ---- Monitör kapanır, kamera masaya döner, telsizden son söz (bu sırada sıradaki müşteri gelebilir)
            yield return Shutdown();
            var fcb = FocusController.Instance;
            if (fcb != null && fcb.State == FocusState.Monitor) fcb.Back();
            string end = defects == 0
                ? (kind == Kind.Mixer ? "Eline sağlık şefim, beton tertemiz! Müteahhidin yüzü düştü ama bina sağlam."
                                      : "Kolonlar şakül gibi şefim! Deprem testinden alnımızın akıyla çıkarız.")
                : (kind == Kind.Mixer ? "Biraz kaçırdık şefim... O beton pek iç açıcı değil, Allah korusun."
                                      : "Birkaç kolon eğri gitti şefim... Deprem olursa ne olur bilemem.");
            StartCoroutine(RadioLine(end));
            yield return new WaitForSeconds(0.6f);

            running = false;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        void StopAll()
        {
            StopAllCoroutines();
            running = playing = false;
            wantMonitor = false;
            awayTime = 0f;
            PromptBar.Set("monitor", null);
            screenOn = false;
            IntroOpen = false;
            var intro = transform.Find("MinigameIntro");
            if (intro != null) Destroy(intro.gameObject);
            onDone = null;
            if (canvas != null) Destroy(canvas.gameObject);
            screenRoot = null;
            game = null;
            Show(monitorGlow, false);
            Show(radioGlow, false);
            if (radioLight != null) radioLight.enabled = false;
            if (radioHiss != null) radioHiss.Stop();
        }

        // ------------------------------------------------------------------ giriş

        static bool OnMonitor
        {
            get
            {
                var fc = FocusController.Instance;
                return fc != null && fc.State == FocusState.Monitor && !fc.IsTransitioning && !PauseMenu.IsPaused;
            }
        }

        /// Bu karede tıklama / Boşluk var mı; tıklama ekranın üstündeyse ekran koordinatı (1280 × 720, sol üst)
        bool Pressed(out Vector2? click)
        {
            click = null;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool space = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
            bool lmb = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (lmb && PointerOnScreen(out var p)) click = p;
            return space || lmb;
        }

        /// Fare, monitör ekranının neresinde (1280 × 720 tasarım koordinatı, sol üst 0,0)
        public bool PointerOnScreen(out Vector2 px)
        {
            px = default;
            var mv = FindAnyObjectByType<MonitorView>();
            var cam = Camera.main;
            if (mv == null || mv.screenRenderer == null || cam == null) return false;
            var ray = CursorController.PointerRay(cam);

            // Ekranın kendi çarpıştırıcısı: dokunun tam noktası
            var mc = mv.screenRenderer.GetComponent<MeshCollider>();
            if (MonitorView.ScreenReadable && mc != null && mc.Raycast(ray, out var hit, 5f))
            {
                var uv = hit.textureCoord;
                var r = MonitorView.ScreenUv;
                float u = (uv.x - r.xMin) / Mathf.Max(1e-4f, r.width);
                float v = (uv.y - r.yMin) / Mathf.Max(1e-4f, r.height);
                px = new Vector2(u * RtW, (1f - v) * RtH);
                return true;
            }

            // Yedek: ekranın köşelerini kameraya izdüşür
            var mf = mv.screenRenderer.GetComponent<MeshFilter>();
            var b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            var t = mv.screenRenderer.transform;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var s = cam.WorldToScreenPoint(t.TransformPoint(c));
                minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x);
                minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
            }
            var m = CursorController.PointerScreen();
            if (maxX - minX < 1f || maxY - minY < 1f) return false;
            float uu = (m.x - minX) / (maxX - minX), vv = (m.y - minY) / (maxY - minY);
            px = new Vector2(uu * RtW, (1f - vv) * RtH);
            return uu >= -0.02f && uu <= 1.02f && vv >= -0.02f && vv <= 1.02f;
        }

        // ------------------------------------------------------------------ telsiz

        void Setup()
        {
            if (radioVoice == null)
            {
                radioVoice = ScriptableObject.CreateInstance<CustomerData>();
                radioVoice.displayName = "Telsiz";
                radioVoice.voicePitch = 0.78f;
            }
            if (crackle == null) crackle = MakeCrackle(0.9f, 7);
            if (hissClip == null) hissClip = MakeCrackle(3f, 11, true);

            var radio = GameObject.Find("Desk/Radio") ?? GameObject.Find("Radio");
            if (radio != null)
            {
                radioAudio = radio.GetComponent<AudioSource>();
                if (radioAudio == null) radioAudio = radio.AddComponent<AudioSource>();
                radioAudio.playOnAwake = false;
                radioAudio.spatialBlend = 0.6f;
                radioLight = radio.GetComponentInChildren<Light>(true);
                if (radioLight != null) radioLight.enabled = false; // telsizden ışık çıkmaz
                if (radioHiss == null)
                {
                    radioHiss = radio.AddComponent<AudioSource>();
                    radioHiss.playOnAwake = false;
                    radioHiss.spatialBlend = 0.6f;
                }
                if (radioGlow == null) radioGlow = HintGlow.Create(transform, Largest(radio.transform), "Hint_Radio");
            }
            if (monitorGlow == null)
            {
                var mv = FindAnyObjectByType<MonitorView>();
                if (mv != null) monitorGlow = HintGlow.Create(transform, MonitorCase(mv.transform), "Hint_MonitorMinigame");
            }
        }

        void Say(string line)
        {
            var sp = SpeechSystem.Instance;
            if (sp != null) sp.Say(radioVoice, line, PatienceStage.Calm);
        }

        static float LineTime(string line)
        {
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            float cps = cfg != null ? cfg.speechCps : 30f;
            float hold = cfg != null ? cfg.bubbleHold : 2.5f;
            return line.Length / Mathf.Max(5f, cps) + hold + 0.4f;
        }

        /// Telsizden bir cümle: başta cızırtı, konuşma boyunca altta hafif cızırtı, sonda kısa cızırtı
        IEnumerator RadioLine(string line)
        {
            if (radioAudio != null && crackle != null) radioAudio.PlayOneShot(crackle, 0.8f * GameSettings.Sfx01);
            yield return new WaitForSeconds(0.35f);
            Say(line);
            float talk = TalkTime(line);
            if (radioHiss != null && hissClip != null)
            {
                radioHiss.clip = hissClip;
                radioHiss.loop = true;
                radioHiss.volume = 0.22f * GameSettings.Sfx01;
                radioHiss.Play();
            }
            yield return new WaitForSeconds(talk);
            if (radioHiss != null) radioHiss.Stop();
            if (radioAudio != null && crackle != null) radioAudio.PlayOneShot(crackle, 0.6f * GameSettings.Sfx01);
            yield return new WaitForSeconds(Mathf.Max(0.5f, LineTime(line) - talk));
        }

        /// Altyazının yazılma süresi (+ biraz)
        static float TalkTime(string line)
        {
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            float cps = cfg != null ? cfg.speechCps : 30f;
            return line.Length / Mathf.Max(5f, cps) + 0.6f;
        }

        /// Telsiz cızırtısı: kesik kesik, filtrelenmiş gürültü
        static AudioClip MakeCrackle(float seconds, int seed, bool loop = false)
        {
            const int rate = 22050;
            int n = (int)(rate * seconds);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f, gate = 1f;
            for (int i = 0; i < n; i++)
            {
                if (i % 600 == 0) gate = rng.NextDouble() < 0.7 ? (float)(0.4 + rng.NextDouble() * 0.6) : 0.05f;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.35f;
                float pop = rng.NextDouble() < 0.002 ? (float)(rng.NextDouble() * 2 - 1) : 0f;
                float env = loop ? 1f : Mathf.Min(1f, i / (rate * 0.03f)) * Mathf.Min(1f, (n - i) / (rate * 0.1f));
                data[i] = Mathf.Clamp((white - lp) * 0.5f * gate + pop, -1f, 1f) * env * 0.6f;
            }
            var clip = AudioClip.Create("RadioCrackle", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ monitör ekranı

        void BuildScreen()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            var feed = MonitorFeed.Instance;
            var cam = feed != null ? feed.feedCamera : null;

            var go = new GameObject("MinigameScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            go.transform.SetParent(cam != null ? cam.transform.parent : transform, false);
            canvas = go.GetComponent<Canvas>();
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.6f);
            }
            else canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // kamera görüntüsü yazılarının üstünde
            screenGroup = go.GetComponent<CanvasGroup>();

            // Monitör ekranının dokudan gösterdiği bölge (ekran modelinin UV'leri): oyun tam bu alana sığdırılır
            region = new GameObject("Region", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            region.SetParent(go.transform, false);
            region.anchorMin = Vector2.zero; // doku ekranın tamamına gerili (MonitorView)
            region.anchorMax = Vector2.one;
            region.offsetMin = region.offsetMax = Vector2.zero;

            // 1280 × 720 tasarım alanı, bölgeye ölçeklenir (LateUpdate)
            fit = new GameObject("Fit", typeof(RectTransform)).GetComponent<RectTransform>();
            fit.SetParent(region, false);
            fit.anchorMin = fit.anchorMax = new Vector2(0.5f, 0.5f);
            fit.sizeDelta = new Vector2(RtW, RtH);

            var holder = new GameObject("Screen", typeof(RectTransform));
            screenRoot = (RectTransform)holder.transform;
            screenRoot.SetParent(fit, false);
            screenRoot.anchorMin = screenRoot.anchorMax = new Vector2(0.5f, 0.5f);
            screenRoot.sizeDelta = new Vector2(RtW, RtH);
            FitScreen();

            // Kamera çerçevesi (tarama çizgileri, köşeler, üst şerit)
            MgUI.Raw(screenRoot, MgUI.Tex("UI_Monitor_Overlay"), 0f, 0f, RtW, RtH);
            var dot = MgUI.Raw(screenRoot, MgUI.Circle(), 19f, 13f, 14f, 14f);
            dot.color = new Color(0.886f, 0.282f, 0.227f);
            stripText = MgUI.Text(screenRoot, "", 42f, 0f, 1000f, 40f, 21f, new Color(0.875f, 0.906f, 0.925f));
            defectText = MgUI.Text(screenRoot, "", 1000f, 0f, 254f, 40f, 21f, new Color(0.875f, 0.906f, 0.925f), TextAlignmentOptions.MidlineRight);

            // Bilgi kartı (başlık / sonuç): arkası karartılır, kart opak, yazılar büyük ve az
            cardDim = MgUI.Rect(screenRoot, new Color(0f, 0f, 0f, 0.6f), 0f, 40f, RtW, RtH - 40f);
            cardBg = MgUI.Rect(screenRoot, new Color(0.06f, 0.07f, 0.08f, 0.97f), 190f, 110f, 900f, 520f);
            cardTitle = MgUI.Text(screenRoot, "", 190f, 130f, 900f, 80f, 52f, Color.white, TextAlignmentOptions.Center);
            cardTitle.fontStyle = FontStyles.Bold; cardTitle.characterSpacing = 4f;
            cardBody = MgUI.Text(screenRoot, "", 240f, 230f, 800f, 250f, 34f, Color.white, TextAlignmentOptions.Center);
            cardBody.textWrappingMode = TextWrappingModes.Normal;
            cardBody.lineSpacing = 8f;
            cardHint = MgUI.Text(screenRoot, "", 190f, 560f, 900f, 50f, 30f, new Color(0.95f, 0.70f, 0.20f), TextAlignmentOptions.Center);
            cardHint.fontStyle = FontStyles.Bold;
            cardImg = MgUI.Raw(screenRoot, null, 260f, 250f, 256f, 208f);
            cardCaption = MgUI.Text(screenRoot, "", 230f, 480f, 316f, 40f, 28f, Color.white, TextAlignmentOptions.Center);
            HideCard();

            // Parazit ve beyaz parlama (açılış / kapanış)
            noiseTex = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            noisePx = new Color32[160 * 90];
            noise = MgUI.Raw(screenRoot, noiseTex, 0f, 0f, RtW, RtH);
            flash = MgUI.Rect(screenRoot, Color.white, 0f, 0f, RtW, RtH);
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        IEnumerator Boot()
        {
            screenGroup.alpha = 1f;
            noise.color = Color.white;
            var label = MgUI.Text(screenRoot, "SİNYAL ALINIYOR...", 0f, 330f, RtW, 60f, 40f, Color.white, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.3f; label.outlineColor = new Color32(0, 0, 0, 255);
            for (float t = 0f; t < 0.9f; t += Time.deltaTime) { Noise(); yield return null; }
            Destroy(label.gameObject);
            noise.color = new Color(1f, 1f, 1f, 0f);

            // Tüp açılması: ince yatay çizgiden tam ekrana, beyaz parlama
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                screenRoot.localScale = new Vector3(1f, Mathf.Lerp(0.01f, 1f, k * k), 1f);
                flash.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }
            screenRoot.localScale = Vector3.one;
            flash.color = new Color(1f, 1f, 1f, 0f);
        }

        IEnumerator Shutdown()
        {
            HideCard();
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                screenRoot.localScale = new Vector3(Mathf.Lerp(1f, 0.6f, k), Mathf.Lerp(1f, 0.01f, k), 1f);
                flash.color = new Color(1f, 1f, 1f, k);
                yield return null;
            }
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                screenRoot.localScale = new Vector3(Mathf.Lerp(0.6f, 0f, t / 0.15f), 0.01f, 1f);
                yield return null;
            }
            screenOn = false;
            if (canvas != null) Destroy(canvas.gameObject);
            screenRoot = null;
            game = null;
        }

        RectTransform region, fit;

        void LateUpdate() => FitScreen();

        /// 1280 × 720 tasarımı monitörün görünen alanına tam oturt
        void FitScreen()
        {
            if (region == null || fit == null) return;
            var size = region.rect.size;
            if (size.x < 1f || size.y < 1f) return;
            fit.localScale = new Vector3(size.x / RtW, size.y / RtH, 1f);
        }


        void Noise()
        {
            if (noiseTex == null) return;
            for (int i = 0; i < noisePx.Length; i++)
            {
                byte v = (byte)UnityEngine.Random.Range(30, 230);
                noisePx[i] = new Color32(v, v, v, 255);
            }
            noiseTex.SetPixels32(noisePx);
            noiseTex.Apply(false);
        }

        void UpdateStrip()
        {
            if (stripText == null || game == null) return;
            stripText.text = "REC   " + game.Strip;
            int total = game.Defects; // sadece bu minigame'in hataları
            defectText.text = $"HATA: {total}";
            defectText.color = total > 0 ? new Color(0.949f, 0.757f, 0.306f) : new Color(0.875f, 0.906f, 0.925f);
        }

        void ShowCard(string title, string body, string hint, string image = null, string caption = "")
        {
            cardDim.gameObject.SetActive(true);
            cardBg.gameObject.SetActive(true);
            cardTitle.gameObject.SetActive(true); cardTitle.text = title; cardTitle.color = Color.white;
            cardBody.gameObject.SetActive(true); cardBody.text = body;
            cardHint.gameObject.SetActive(true); cardHint.text = hint;

            // Görselli kart: büyük görsel solda, kısa yazı sağda
            var tex = string.IsNullOrEmpty(image) ? null : MgUI.Tex(image);
            bool withImage = tex != null;
            cardImg.gameObject.SetActive(withImage);
            cardCaption.gameObject.SetActive(withImage);
            if (withImage)
            {
                cardImg.texture = tex;
                float w = 256f, h = w * tex.height / Mathf.Max(1f, tex.width);
                MgUI.Place(cardImg.rectTransform, 260f, 360f - h * 0.5f, w, h);
                cardCaption.text = caption;
                MgUI.Place(cardBody.rectTransform, 580f, 230f, 470f, 280f);
                cardBody.alignment = TextAlignmentOptions.MidlineLeft;
            }
            else
            {
                MgUI.Place(cardBody.rectTransform, 240f, 230f, 800f, 280f);
                cardBody.alignment = TextAlignmentOptions.Center;
            }
        }

        void HideCard()
        {
            if (cardBg == null) return;
            cardDim.gameObject.SetActive(false);
            cardBg.gameObject.SetActive(false);
            cardTitle.gameObject.SetActive(false);
            cardBody.gameObject.SetActive(false);
            cardHint.gameObject.SetActive(false);
            if (cardImg != null) cardImg.gameObject.SetActive(false);
            if (cardCaption != null) cardCaption.gameObject.SetActive(false);
        }

        static readonly Color PaperWhite = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color SoftWhite = new Color(0.82f, 0.80f, 0.75f);

        // ------------------------------------------------------------------ tanıtım (ekran bulanık)

        /// Tanıtım açıkken masa kontrolleri kapalı, imleç serbest
        public static bool IntroOpen { get; private set; }
        RenderTexture introRT;

        IEnumerator Intro()
        {
            yield return Overlay((root, Label) =>
            {
                // Başlık + tek cümle: "Topla: [görsel] gerisini banttan at"
                Label(game.IntroTitle, 190f, 64f, PaperWhite, FontStyles.Bold);

                const float rowY = -10f, rowSize = 46f, gap = 26f;
                var before = Label(game.IntroBefore, rowY, rowSize, PaperWhite, FontStyles.Normal);
                var after = Label(game.IntroAfter, rowY, rowSize, PaperWhite, FontStyles.Normal);
                float wb = string.IsNullOrEmpty(game.IntroBefore) ? 0f : before.GetPreferredValues(game.IntroBefore).x;
                float wa = string.IsNullOrEmpty(game.IntroAfter) ? 0f : after.GetPreferredValues(game.IntroAfter).x;
                var vsize = game.IntroVisualSize;
                float total = wb + gap + vsize.x + gap + wa;
                float x0 = -total * 0.5f;
                before.rectTransform.sizeDelta = new Vector2(wb + 4f, rowSize * 1.6f);
                before.rectTransform.anchoredPosition = new Vector2(x0 + wb * 0.5f, rowY);
                after.rectTransform.sizeDelta = new Vector2(wa + 4f, rowSize * 1.6f);
                after.rectTransform.anchoredPosition = new Vector2(x0 + wb + gap + vsize.x + gap + wa * 0.5f, rowY);

                var vis = new GameObject("Visual", typeof(RectTransform));
                var vrt = (RectTransform)vis.transform;
                vrt.SetParent(root, false);
                vrt.anchorMin = vrt.anchorMax = new Vector2(0.5f, 0.5f);
                vrt.sizeDelta = vsize;
                vrt.anchoredPosition = new Vector2(x0 + wb + gap + vsize.x * 0.5f, rowY);
                game.BuildIntroVisual(vrt);
            }, "Başla");
        }

        /// Oyun sonu: bulanık ekranda sonuç, tıklayınca devam
        IEnumerator ShowResult(int defects, string siteLetter)
        {
            yield return Overlay((root, Label) =>
            {
                // Skor: doğru hamle / toplam deneme (ör. 12 / 16) + kısa özet
                int tries = Mathf.Max(0, game.Attempts), hits = Mathf.Clamp(game.Hits, 0, tries);
                Label($"Şantiye {siteLetter}  ·  {(game is MixerGame ? "Mikser kontrolü" : "Kolon dökümü")}", 280f, 28f, SoftWhite, FontStyles.Normal);
                Label($"{hits}<size=45%><color=#A9A396> / {tries} doğru</color></size>", 175f, 110f, PaperWhite, FontStyles.Bold);
                var body = Label(game.Summary, 10f, 36f, PaperWhite, FontStyles.Normal);
                body.rectTransform.sizeDelta = new Vector2(1400f, 220f);
                body.lineSpacing = 10f;
            }, "Devam");
        }

        /// Ekranı bulanıklaştırıp içerik gösterir, tıklayana (sol tık / Boşluk / Enter) kadar bekler
        IEnumerator Overlay(System.Action<RectTransform, System.Func<string, float, float, Color, FontStyles, TextMeshProUGUI>> build, string hintText)
        {
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();

            var go = new GameObject("MinigameIntro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            var cv = go.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 300;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            var group = go.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            var root = (RectTransform)go.transform;

            var bg = new GameObject("Blur", typeof(RectTransform), typeof(RawImage));
            var bgrt = (RectTransform)bg.transform;
            bgrt.SetParent(root, false);
            bgrt.anchorMin = Vector2.zero; bgrt.anchorMax = Vector2.one; bgrt.offsetMin = bgrt.offsetMax = Vector2.zero;
            var bgi = bg.GetComponent<RawImage>();
            bgi.texture = Blur(shot);
            bgi.color = new Color(0.32f, 0.32f, 0.32f, 1f);
            if (shot != null) Destroy(shot);

            TextMeshProUGUI Label(string text, float y, float size, Color c, FontStyles style)
            {
                var l = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rt = (RectTransform)l.transform;
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, y);
                rt.sizeDelta = new Vector2(1700f, size * 1.6f);
                var t = l.GetComponent<TextMeshProUGUI>();
                t.text = text; t.fontSize = size; t.color = c; t.fontStyle = style;
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.outlineWidth = 0.12f; t.outlineColor = new Color32(0, 0, 0, 160);
                t.raycastTarget = false;
                return t;
            }

            build(root, Label);

            // Altta sabit küçük ipucu: [sol tık] Başla / Devam
            var hintRow = new GameObject("Hint", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            var hr = (RectTransform)hintRow.transform;
            hr.SetParent(root, false);
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
            hr.anchoredPosition = new Vector2(0f, -260f);
            hr.localScale = Vector3.one * 1.4f;
            var hl = hintRow.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var hfit = hintRow.GetComponent<ContentSizeFitter>();
            hfit.horizontalFit = hfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            KeyIcons.Add(hr, "LMB", null);
            var hint = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            hint.transform.SetParent(hr, false);
            hint.text = hintText; hint.fontSize = 24f; hint.color = SoftWhite;
            hint.textWrappingMode = TextWrappingModes.NoWrap; hint.raycastTarget = false;

            IntroOpen = true;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.25f; yield return null; }
            group.alpha = 1f;

            float wait = 0f;
            while (true)
            {
                wait += Time.unscaledDeltaTime;
                var m = Mouse.current; var kb = Keyboard.current;
                bool click = (m != null && m.leftButton.wasPressedThisFrame) ||
                             (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame));
                if (wait > 0.4f && click && !PauseMenu.IsPaused) break;
                yield return null;
            }
            for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.2f; yield return null; }
            Destroy(go);
            yield return null; // tıklama oyunda / masada sayılmasın
            IntroOpen = false;
        }

        Texture Blur(Texture2D shot)
        {
            if (shot == null) return null;
            RenderTexture Temp(int w, int h) { var r = RenderTexture.GetTemporary(Mathf.Max(1, w), Mathf.Max(1, h), 0); r.filterMode = FilterMode.Bilinear; return r; }
            var cur = Temp(shot.width / 2, shot.height / 2);
            Graphics.Blit(shot, cur);
            for (int i = 0; i < 4; i++) { var n = Temp(cur.width / 2, cur.height / 2); Graphics.Blit(cur, n); RenderTexture.ReleaseTemporary(cur); cur = n; }
            for (int i = 0; i < 3; i++) { var n = Temp(cur.width * 2, cur.height * 2); Graphics.Blit(cur, n); RenderTexture.ReleaseTemporary(cur); cur = n; }
            if (introRT == null || introRT.width != cur.width || introRT.height != cur.height)
            {
                if (introRT != null) { introRT.Release(); Destroy(introRT); }
                introRT = new RenderTexture(cur.width, cur.height, 0) { filterMode = FilterMode.Bilinear };
            }
            Graphics.Blit(cur, introRT);
            RenderTexture.ReleaseTemporary(cur);
            return introRT;
        }

        // ------------------------------------------------------------------ yardımcılar

        static void Show(HintGlow g, bool on)
        {
            if (g != null && g.IsOn != on) g.Show(on);
        }

        static Renderer MonitorCase(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.name == "SM_Monitor") return r;
            return Largest(root);
        }

        static Renderer Largest(Transform root)
        {
            Renderer best = null;
            float vol = 0f;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.GetComponent<MeshFilter>() == null) continue;
                var s = r.bounds.size;
                float v = s.x * s.y * s.z + s.x * s.z * 0.01f;
                if (v > vol) { vol = v; best = r; }
            }
            return best;
        }
    }
}
