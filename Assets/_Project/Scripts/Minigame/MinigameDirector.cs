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
        RawImage cardBg;
        Texture2D noiseTex;
        Color32[] noisePx;
        HintGlow monitorGlow, radioGlow;
        AudioSource radioAudio, radioHiss;
        AudioClip hissClip;
        Light radioLight;
        AudioClip crackle;
        CustomerData radioVoice;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Results.Clear(); }

        void Awake() => Instance = this;

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
                ? new[] { $"Şefim, {L} şantiyesinden arıyorum! Müteahhit malzemeden kaçıyor, mikserin bandına deniz kumu, deniz suyu karıştırmışlar!",
                          "Monitöre geç, irsaliyede olmayanı banttan at. İnşaatı sen kurtaracaksın şefim!" }
                : new[] { $"Şefim, {L} şantiyesinde kolonları dökmeye başladık ama kalıplar sallanıyor, şakülünde değil!",
                          "Yarın deprem testi var! Monitörden bak, kolonları dimdik dök ki bina ayakta kalsın!" };

            Show(radioGlow, true);
            Show(monitorGlow, true);
            foreach (var line in call) yield return RadioLine(line);
            Show(radioGlow, false);

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

            // ---- Başlık kartı: tıkla, başla
            ShowCard(game.Title, game.Instructions, "BAŞLAMAK İÇİN TIKLA");
            while (!(OnMonitor && Pressed(out _))) { Blink(cardHint); UpdateStrip(); yield return null; }
            HideCard();

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
            Show(monitorGlow, false);

            // ---- Sonuç
            int defects = game.Defects;
            if (site != null) site.defects += defects;
            var gmr = GameManager.Instance;
            Results.Add(new Result { day = gmr != null ? gmr.Run.day : 0, title = kind == Kind.Mixer ? "Mikser kontrolü" : "Kolon dökümü", defects = defects, site = site != null ? site.letter : '?' });
            ShowCard(defects == 0 ? "TEMİZ İŞ!" : $"KUSUR +{defects}", game.Summary +
                     (defects == 0 ? "" : $"\n\nŞantiye {L} binasına {defects} kusur yazıldı."), "");
            cardTitle.color = defects == 0 ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.42f, 0.35f);
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) { UpdateStrip(); yield return null; }
            cardHint.text = "DEVAM ETMEK İÇİN TIKLA";
            float wait = 0f;
            while (!(OnMonitor && Pressed(out _)) && wait < 8f) { if (OnMonitor) wait += Time.deltaTime; Blink(cardHint); yield return null; }

            // ---- Ekran kapanışı, telsizden son söz
            yield return Shutdown();
            string end = defects == 0
                ? (kind == Kind.Mixer ? "Eline sağlık şefim, beton tertemiz! Müteahhidin yüzü düştü ama bina sağlam."
                                      : "Kolonlar şakül gibi şefim! Deprem testinden alnımızın akıyla çıkarız.")
                : (kind == Kind.Mixer ? "Biraz kaçırdık şefim... O beton pek iç açıcı değil, Allah korusun."
                                      : "Birkaç kolon eğri gitti şefim... Deprem olursa ne olur bilemem.");
            yield return RadioLine(end);

            running = false;
            var cb = onDone;
            onDone = null;
            cb?.Invoke();
        }

        void StopAll()
        {
            StopAllCoroutines();
            running = playing = false;
            screenOn = false;
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
        bool PointerOnScreen(out Vector2 px)
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
            if (radioAudio != null && crackle != null) radioAudio.PlayOneShot(crackle, 0.8f);
            yield return new WaitForSeconds(0.35f);
            Say(line);
            float talk = TalkTime(line);
            if (radioHiss != null && hissClip != null)
            {
                radioHiss.clip = hissClip;
                radioHiss.loop = true;
                radioHiss.volume = 0.22f;
                radioHiss.Play();
            }
            yield return new WaitForSeconds(talk);
            if (radioHiss != null) radioHiss.Stop();
            if (radioAudio != null && crackle != null) radioAudio.PlayOneShot(crackle, 0.6f);
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

            // Bilgi kartı (başlık / sonuç)
            cardBg = MgUI.Rect(screenRoot, new Color(0.04f, 0.05f, 0.06f, 0.88f), 240f, 170f, 800f, 380f);
            cardTitle = MgUI.Text(screenRoot, "", 240f, 196f, 800f, 80f, 58f, Color.white, TextAlignmentOptions.Center);
            cardTitle.fontStyle = FontStyles.Bold; cardTitle.characterSpacing = 6f;
            cardBody = MgUI.Text(screenRoot, "", 290f, 280f, 700f, 190f, 25f, new Color(0.88f, 0.9f, 0.92f), TextAlignmentOptions.Center);
            cardBody.textWrappingMode = TextWrappingModes.Normal;
            cardBody.lineSpacing = 12f;
            cardHint = MgUI.Text(screenRoot, "", 240f, 480f, 800f, 50f, 26f, new Color(0.95f, 0.70f, 0.20f), TextAlignmentOptions.Center);
            cardHint.fontStyle = FontStyles.Bold;
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
            int total = (site != null ? site.defects : 0) + (playing ? game.Defects : 0);
            defectText.text = $"KUSUR: {total}";
            defectText.color = total > 0 ? new Color(0.949f, 0.757f, 0.306f) : new Color(0.875f, 0.906f, 0.925f);
        }

        void ShowCard(string title, string body, string hint)
        {
            cardBg.gameObject.SetActive(true);
            cardTitle.gameObject.SetActive(true); cardTitle.text = title; cardTitle.color = Color.white;
            cardBody.gameObject.SetActive(true); cardBody.text = body;
            cardHint.gameObject.SetActive(true); cardHint.text = hint;
        }

        void HideCard()
        {
            if (cardBg == null) return;
            cardBg.gameObject.SetActive(false);
            cardTitle.gameObject.SetActive(false);
            cardBody.gameObject.SetActive(false);
            cardHint.gameObject.SetActive(false);
        }

        static void Blink(TextMeshProUGUI t)
        {
            if (t == null) return;
            var c = t.color; c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)); t.color = c;
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
