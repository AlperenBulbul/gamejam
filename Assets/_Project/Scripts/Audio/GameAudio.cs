using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Final ses paketi (zip 24): Resources/Audio/SFX, AMB, MUS. Sadece paketteki sesler, sadece tablodaki yerlerde.
    ///  Sfx.Play("SFX_Stamp_Approve")  -> A / _B versiyonlarından rastgele biri, tablodaki önerilen ses × Ayarlar > Efektler
    ///  Ortam: AMB_Office + AMB_Clock_Tick (masada), AMB_Rain (1. gün), AMB_Construction (monitöre bakarken)
    ///  Müzik: MUS_Menu (ana menü). MUS_DayEnd (fiş / dükkân) bu ekranlar oyunda olmadığı için çalmaz.
    /// GameManager bu bileşeni kendisi ekler, sahnede kurulum gerekmez.
    public static class Sfx
    {
        /// Tablodaki "Önerilen ses" (1'den farklı olanlar)
        static readonly Dictionary<string, float> Volume = new Dictionary<string, float>
        {
            { "SFX_Alert_Pulse", 0.5f }, { "SFX_Monitor_Static", 0.45f }, { "SFX_Score_Tick", 0.5f },
            { "SFX_Stamp_Pickup", 0.56f }, { "SFX_Stamp_PutDown", 0.63f }, { "SFX_UI_Hover", 0.5f },
            { "SFX_Worker_WrongClick", 0.71f },
            { "AMB_Clock_Tick", 0.11f }, { "AMB_Construction", 0.56f }, { "AMB_Office", 0.35f }, { "AMB_Rain", 0.45f },
            { "MUS_DayEnd", 0.79f }, { "MUS_Menu", 0.79f },
        };

        static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static float VolumeOf(string name) => Volume.TryGetValue(name, out var v) ? v : 1f;

        static string Folder(string name) => name.StartsWith("AMB_") ? "AMB" : name.StartsWith("MUS_") ? "MUS" : "SFX";

        /// A + (varsa) _B versiyonu
        public static AudioClip[] Clips(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            var list = new List<AudioClip>();
            var a = Resources.Load<AudioClip>($"Audio/{Folder(name)}/{name}");
            var b = Resources.Load<AudioClip>($"Audio/{Folder(name)}/{name}_B");
            if (a != null) list.Add(a);
            if (b != null) list.Add(b);
            if (list.Count == 0) Debug.LogWarning($"[Ses] Resources/Audio/{Folder(name)}/{name} bulunamadı");
            c = list.ToArray();
            cache[name] = c;
            return c;
        }

        public static AudioClip Pick(string name)
        {
            var c = Clips(name);
            return c.Length == 0 ? null : c[Random.Range(0, c.Length)];
        }

        /// Kısa efekt (oyun içi; mola menüsünde duraklar)
        public static void Play(string name, float pitch = 1f) { if (GameAudio.Instance != null) GameAudio.Instance.PlayOneShot(name, pitch, false); }
        /// Arayüz efekti (mola menüsünde de çalar)
        public static void PlayUI(string name) { if (GameAudio.Instance != null) GameAudio.Instance.PlayOneShot(name, 1f, true); }
        public static void PlayDelayed(string name, float delay) { if (GameAudio.Instance != null) GameAudio.Instance.Delayed(name, delay); }
        /// Döngü aç / kapat (ör. SFX_Belt_Loop)
        public static void Loop(string name, bool on) { if (GameAudio.Instance != null) GameAudio.Instance.SetLoop(name, on); }

        /// Tek adım: SFX_Footstep_01–05 rastgele, perde ±0,05
        public static void Footstep() => Play($"SFX_Footstep_0{Random.Range(1, 6)}", 1f + Random.Range(-0.05f, 0.05f));
    }

    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        const int PoolSize = 10;
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioSource uiSource;
        int next;

        class LoopState { public AudioSource src; public float target; public bool music; }
        readonly Dictionary<string, LoopState> loops = new Dictionary<string, LoopState>();
        string officeAmb; // AMB_Office ya da _B (oturum başına biri)

        // İzlenen durumlar
        int lastSelected = -99, lastResults;
        float nextAlert;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < PoolSize; i++) pool.Add(NewSource(false));
            uiSource = NewSource(false);
            uiSource.ignoreListenerPause = true;
        }

        AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void OnEnable()
        {
            GameEvents.CustomerArrived += OnArrived;
            GameEvents.CustomerLeft += OnLeft;
            GameEvents.Stamped += OnStamped;
            GameEvents.FaultFixed += OnFixed;
            GameEvents.SiteCreated += OnSiteCreated;
            GameEvents.BribeTaken += OnBribe;
            GameEvents.FocusTransitionStarted += OnFocus;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= OnArrived;
            GameEvents.CustomerLeft -= OnLeft;
            GameEvents.Stamped -= OnStamped;
            GameEvents.FaultFixed -= OnFixed;
            GameEvents.SiteCreated -= OnSiteCreated;
            GameEvents.BribeTaken -= OnBribe;
            GameEvents.FocusTransitionStarted -= OnFocus;
        }

        // ------------------------------------------------------------------ çalma

        public void PlayOneShot(string name, float pitch, bool ui)
        {
            var clip = Sfx.Pick(name);
            if (clip == null) return;
            var src = ui ? uiSource : pool[next++ % pool.Count];
            float vol = Sfx.VolumeOf(name) * GameSettings.Sfx01;
            if (ui) { uiSource.PlayOneShot(clip, vol); return; }
            src.Stop();
            src.clip = clip;
            src.pitch = pitch;
            src.volume = vol;
            src.Play();
        }

        public void Delayed(string name, float delay) => StartCoroutine(DelayRoutine(name, delay));

        IEnumerator DelayRoutine(string name, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayOneShot(name, 1f, false);
        }

        /// Döngü: AMB_* ve SFX_*_Loop / Static efekt sesine, MUS_* müzik sesine bağlı; yumuşak açılır / kapanır
        public void SetLoop(string name, bool on)
        {
            if (!loops.TryGetValue(name, out var st))
            {
                if (!on) return;
                var clip = Sfx.Pick(name);
                if (clip == null) return;
                st = new LoopState { src = NewSource(true), music = name.StartsWith("MUS_") };
                st.src.clip = clip;
                st.src.volume = 0f;
                loops[name] = st;
            }
            st.target = on ? Sfx.VolumeOf(name) : 0f;
            if (on && !st.src.isPlaying) st.src.Play();
        }

        void UpdateLoops()
        {
            foreach (var kv in loops)
            {
                var st = kv.Value;
                float want = st.target * (st.music ? GameSettings.Music01 : GameSettings.Sfx01);
                st.src.volume = Mathf.MoveTowards(st.src.volume, want, Time.unscaledDeltaTime * 1.5f);
                if (st.target <= 0f && st.src.volume <= 0.001f && st.src.isPlaying) st.src.Stop();
            }
        }

        // ------------------------------------------------------------------ ortam + müzik

        void Update()
        {
            var gm = GameManager.Instance;
            var state = gm != null ? gm.State : GameState.MainMenu;
            var fc = FocusController.Instance;
            bool desk = gm != null && gm.InDeskState && !IntroCinematic.Playing;
            bool ending = state == GameState.Ending;

            // Müzik: ana menü (açılış sinematiği ve bitiş videoları kendi sesini çalar)
            SetLoop("MUS_Menu", state == GameState.MainMenu && !IntroCinematic.Playing);

            // Ofis + saat: masadayken
            if (officeAmb == null) officeAmb = Random.value < 0.5f ? "AMB_Office" : "AMB_Office_B";
            SetLoopExact(officeAmb, "AMB_Office", desk && !ending);
            SetLoop("AMB_Clock_Tick", desk && !ending);
            // Yağmur: 1. gün
            SetLoop("AMB_Rain", desk && gm != null && gm.Run != null && gm.Run.day == 1);

            // Monitör: şantiye ortamı ya da SİNYAL YOK paraziti
            bool onMonitor = desk && fc != null && fc.State == FocusState.Monitor && !fc.IsTransitioning;
            var feed = MonitorFeed.Instance;
            bool hasSite = feed != null && feed.Shown != null;
            bool mgOn = MinigameDirector.OnScreen;
            SetLoop("AMB_Construction", onMonitor && hasSite && !mgOn);
            SetLoop("SFX_Monitor_Static", onMonitor && !hasSite && !mgOn);

            // Kanal düğmesi (monitöre bakarken kanal değişti)
            var cm = ConstructionManager.Instance;
            int sel = cm != null ? cm.Selected : -1;
            if (sel != lastSelected)
            {
                if (onMonitor && lastSelected != -99) Sfx.Play("SFX_Monitor_Channel");
                lastSelected = sel;
            }

            // Minigame: bant döngüsü (mikser), sonuç
            var mg = MinigameDirector.Instance;
            SetLoop("SFX_Belt_Loop", mg != null && mg.MixerRunning);
            int results = MinigameDirector.Results.Count;
            if (results > lastResults)
            {
                var r = MinigameDirector.Results[results - 1];
                Sfx.Play(r.defects == 0 ? "SFX_Event_Success" : "SFX_Event_Fail");
                Sfx.PlayDelayed(r.defects == 0 ? "SFX_Score_Good" : "SFX_Score_Bad", 0.6f); // sonuç ekranı
            }
            lastResults = results;

            // Monitörün turuncu uyarı çerçevesi yanıp sönerken bip
            if (mg != null && mg.MonitorAlertOn && desk)
            {
                if (Time.unscaledTime >= nextAlert) { Sfx.Play("SFX_Alert_Pulse"); nextAlert = Time.unscaledTime + 1f / 1.4f; }
            }
            else nextAlert = 0f;

            if (state == GameState.MainMenu) lastResults = results;
            UpdateLoops();
        }

        /// _B ambiyanslardan biri seçili: sesi asıl adın tablosundan al
        void SetLoopExact(string file, string tableName, bool on)
        {
            if (!loops.TryGetValue(file, out var st))
            {
                if (!on) return;
                var clips = Sfx.Clips(tableName);
                AudioClip clip = null;
                foreach (var c in clips) if (c != null && c.name == file) clip = c;
                if (clip == null && clips.Length > 0) clip = clips[0];
                if (clip == null) return;
                st = new LoopState { src = NewSource(true) };
                st.src.clip = clip;
                st.src.volume = 0f;
                loops[file] = st;
            }
            st.target = on ? Sfx.VolumeOf(tableName) : 0f;
            if (on && !st.src.isPlaying) st.src.Play();
        }

        // ------------------------------------------------------------------ olaylar

        void OnArrived(CustomerData c)
        {
            Sfx.Play("SFX_Folder_Drop");                                   // dosya masaya düşer
            // zarf sesi: BribeEnvelope zarfı 2-3 sn sonra uzattığında çalar
        }

        void OnLeft(CustomerData c, bool angry) { if (angry && (c == null || !c.calmWhileWaiting)) Sfx.Play("SFX_Door_Slam"); }

        void OnStamped(CustomerData c, bool approved) => Sfx.Play(approved ? "SFX_Stamp_Approve" : "SFX_Stamp_Reject");

        void OnFixed(ConstructionSite s)
        {
            Sfx.Play("SFX_Worker_Fix");        // olumlu ding
            Sfx.PlayDelayed("SFX_Slacker_Jump", 0.05f); // işçi uyarılınca zıplar
        }

        void OnSiteCreated(ConstructionSite s) => Sfx.PlayDelayed("SFX_Monitor_NewChannel", 0.3f);

        void OnBribe(int amount) => Sfx.Play("SFX_Envelope_Take");

        void OnFocus(FocusState from, FocusState to)
        {
            if (to == FocusState.Binder) Sfx.Play("SFX_Binder_Open");
            else if (to == FocusState.Folder && from != FocusState.Folder) Sfx.Play("SFX_Paper_Slide"); // belge önüne kayar
        }
    }
}
