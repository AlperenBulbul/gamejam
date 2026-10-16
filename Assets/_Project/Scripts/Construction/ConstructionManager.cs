using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Şantiyeler (GDD bölüm 5, Geliştirme dokümanı 6.6).
    /// - Onaylanan her proje sıradaki harfle (A..G) bir şantiye olur ve arka planda ilerler.
    /// - Her adım (temel + her kat) stepSeconds sürer. Adımın faultWindow aralığında rastgele anlarda
    ///   o adımın hata sayısı kadar işçi hatalı çalışmaya başlar (F1..F5).
    /// - Adım bittiğinde düzeltilmemiş her hata binaya 1 kusur ekler.
    /// - Hatalı işçiye tıklamak düzeltir (+1 düzeltme, fişte 20 TL); düzgün işçiye tıklamak adımı 5 sn uzatır.
    /// - Sadece Working ve FinalShift'te ilerler; gece (diğer durumlar) durur, ertesi gün kaldığı yerden devam eder.
    /// - FinalShift'te bütün şantiyeler bitince (ya da su altında kalınca) GameManager.OnAllSitesSettled().
    /// GameManager bu bileşeni Systems'e kendisi ekler; sahnede kurulum gerekmez.
    public class ConstructionManager : MonoBehaviour
    {
        public static ConstructionManager Instance { get; private set; }
        public const int MaxSites = 7;

        public readonly List<ConstructionSite> Sites = new List<ConstructionSite>();

        /// F7: inşaat hızı çarpanı (1 ya da 10)
        public float SpeedMultiplier { get; private set; } = 1f;

        /// Monitörde seçili kanal (0 = A). Yeni şantiye açılınca ona geçer.
        public int Selected { get; private set; } = -1;
        public ConstructionSite SelectedSite => Selected >= 0 && Selected < Sites.Count ? Sites[Selected] : null;

        bool running;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.Stamped += OnStamped;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.Stamped -= OnStamped;
        }

        static GameConfig Cfg => GameManager.Instance != null ? GameManager.Instance.Config : GameConfig.Current;

        void OnStateChanged(GameState s)
        {
            switch (s)
            {
                case GameState.MainMenu:
                    Sites.Clear();
                    Selected = -1;
                    SpeedMultiplier = 1f;
                    break;
                case GameState.DayIntro:
                    // Yeni gün: işçi maliyeti sayacı sıfırdan başlar (şantiyeler kaldığı yerden devam eder)
                    foreach (var site in Sites) site.wasActiveToday = false;
                    break;
                case GameState.Receipt:
                    // Fiş: bugün çalışan şantiye sayısı
                    var gm = GameManager.Instance;
                    if (gm != null) gm.Run.ledger.sitesWorked = CountActiveToday();
                    break;
            }

            running = s == GameState.Working || s == GameState.FinalShift;
            foreach (var site in Sites) site.paused = !running;
        }

        void OnStamped(CustomerData c, bool approved)
        {
            if (!approved || c == null) return;
            CreateSite(c);
        }

        // ------------------------------------------------------------------ şantiye

        public ConstructionSite CreateSite(CustomerData c)
        {
            if (Sites.Count >= MaxSites) { Debug.LogWarning("[İnşaat] En fazla 7 şantiye olabilir."); return null; }
            foreach (var s in Sites) if (s.customer == c) return s; // aynı müşteri iki kez onaylanmaz

            var site = new ConstructionSite((char)('A' + Sites.Count), c) { paused = !running };
            Sites.Add(site);
            BeginStep(site);
            Selected = Sites.Count - 1; // monitör yeni şantiyeye geçer
            Debug.Log($"[İnşaat] Şantiye {site.letter} açıldı: {c.id} {c.displayName}, {c.floors} kat ({site.totalSteps} adım)");
            GameEvents.RaiseSiteCreated(site);
            return site;
        }

        void BeginStep(ConstructionSite site)
        {
            var cfg = Cfg;
            site.timer = 0f;
            site.duration = cfg != null ? cfg.stepSeconds : 40f;
            site.workers = cfg != null ? cfg.WorkersForStep(site.step) : 3;
            site.activeFaults.Clear();
            site.activeFaultTypes.Clear();
            site.pendingFaultTimes.Clear();
            int faults = cfg != null ? cfg.FaultsForStep(site.step) : 1;
            var win = cfg != null ? cfg.faultWindow : new Vector2(5f, 30f);
            for (int i = 0; i < faults; i++) site.pendingFaultTimes.Add(Random.Range(win.x, win.y));
            site.pendingFaultTimes.Sort();
        }

        void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime * SpeedMultiplier;
            for (int i = 0; i < Sites.Count; i++) Tick(Sites[i], dt);

            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.FinalShift && AllSettled()) gm.OnAllSitesSettled();
        }

        void Tick(ConstructionSite site, float dt)
        {
            if (site.state != SiteState.Building || site.paused) return;
            site.wasActiveToday = true;
            site.timer += dt;

            // Zamanı gelen hatalar başlar
            while (site.pendingFaultTimes.Count > 0 && site.timer >= site.pendingFaultTimes[0])
            {
                site.pendingFaultTimes.RemoveAt(0);
                StartFault(site);
            }

            if (site.timer < site.duration) return;

            // Adım bitti: düzeltilmeyen hatalar kusur olur
            int missed = site.activeFaults.Count;
            site.defects += missed;
            site.activeFaults.Clear();
            site.activeFaultTypes.Clear();
            site.pendingFaultTimes.Clear();
            if (missed > 0)
            {
                Debug.Log($"[İnşaat] {site.letter} {site.StepLabel}: {missed} hata düzeltilmedi -> kusur {site.defects}");
                GameEvents.RaiseFaultsCleared(site);
            }

            var gm = GameManager.Instance;
            if (gm != null) gm.Run.ledger.stepsCompleted++;
            GameEvents.RaiseStepCompleted(site);

            site.step++;
            if (site.step >= site.totalSteps)
            {
                site.state = SiteState.Done;
                Debug.Log($"[İnşaat] Şantiye {site.letter} bitti. Kusur: {site.defects}");
                GameEvents.RaiseSiteFinished(site);
            }
            else BeginStep(site);
        }

        /// Rastgele bir düzgün işçi hatalı çalışmaya başlar
        bool StartFault(ConstructionSite site)
        {
            if (site.state != SiteState.Building) return false;
            var free = new List<int>();
            for (int w = 0; w < site.workers; w++) if (!site.activeFaults.Contains(w)) free.Add(w);
            if (free.Count == 0) return false;

            int worker = free[Random.Range(0, free.Count)];
            // Aynı şantiyede aynı anda iki aynı tür hata olmasın
            var types = new List<int> { 1, 2, 3, 4, 5 };
            foreach (var t in site.activeFaultTypes) types.Remove(t);
            int type = types.Count > 0 ? types[Random.Range(0, types.Count)] : Random.Range(1, 6);

            site.activeFaults.Add(worker);
            site.activeFaultTypes.Add(type);
            Debug.Log($"[İnşaat] {site.letter} {site.StepLabel}: işçi {worker + 1} hatalı (F{type})");
            GameEvents.RaiseFaultStarted(site);
            return true;
        }

        /// Monitörde işçiye tıklandı. true = hatalıydı ve düzeltildi; false = düzgün işçiydi (adım uzadı)
        public bool ClickWorker(ConstructionSite site, int worker)
        {
            if (site == null || site.state != SiteState.Building) return false;
            int i = site.activeFaults.IndexOf(worker);
            if (i >= 0)
            {
                site.activeFaults.RemoveAt(i);
                site.activeFaultTypes.RemoveAt(i);
                var gm = GameManager.Instance;
                if (gm != null) gm.Run.ledger.fixes++;
                Debug.Log($"[İnşaat] {site.letter}: işçi {worker + 1} düzeltildi");
                GameEvents.RaiseFaultFixed(site);
                if (site.activeFaults.Count == 0) GameEvents.RaiseFaultsCleared(site);
                return true;
            }

            var cfg = Cfg;
            site.duration += cfg != null ? cfg.wrongClickPenalty : 5f;
            Debug.Log($"[İnşaat] {site.letter}: düzgün işçiye tıklandı, adım {(cfg != null ? cfg.wrongClickPenalty : 5f)} sn uzadı");
            return false;
        }

        // ------------------------------------------------------------------ kanal (monitör için)

        public void Select(int index)
        {
            if (index < 0 || index >= MaxSites) return;
            Selected = index;
        }

        public void SelectNext(int dir)
        {
            if (Sites.Count == 0) return;
            Selected = (Mathf.Max(0, Selected) + dir + Sites.Count) % Sites.Count;
        }

        // ------------------------------------------------------------------ sorgular

        public bool AllSettled()
        {
            foreach (var s in Sites) if (s.state == SiteState.Building) return false;
            return true;
        }

        public int CountActiveToday()
        {
            int n = 0;
            foreach (var s in Sites) if (s.wasActiveToday) n++;
            return n;
        }

        public ConstructionSite SiteOf(CustomerData c)
        {
            foreach (var s in Sites) if (s.customer == c) return s;
            return null;
        }

        // ------------------------------------------------------------------ debug

        /// F2: seçili şantiyede hemen bir hata başlat
        public void DebugStartFault()
        {
            var s = SelectedSite ?? LastBuilding();
            if (s == null || !StartFault(s)) Debug.Log("[Debug] Hata başlatılacak şantiye yok (ya da bütün işçiler zaten hatalı).");
        }

        /// F7: 10x hız aç / kapat
        public void DebugToggleSpeed()
        {
            SpeedMultiplier = SpeedMultiplier > 1f ? 1f : 10f;
            Debug.Log($"[Debug] İnşaat hızı x{SpeedMultiplier}");
        }

        /// F11: seçili şantiyedeki bir hatalı işçiyi düzelt (monitör gelene kadar test için).
        /// Shift+F11: düzgün bir işçiye tıkla (adım uzar).
        public void DebugClick(bool wrong)
        {
            var s = SelectedSite ?? LastBuilding();
            if (s == null || s.state != SiteState.Building) { Debug.Log("[Debug] Çalışan şantiye yok."); return; }
            if (!wrong)
            {
                if (s.activeFaults.Count == 0) { Debug.Log($"[Debug] {s.letter}: düzeltilecek hata yok."); return; }
                ClickWorker(s, s.activeFaults[0]);
                return;
            }
            for (int w = 0; w < s.workers; w++)
                if (!s.activeFaults.Contains(w)) { ClickWorker(s, w); return; }
        }

        ConstructionSite LastBuilding()
        {
            for (int i = Sites.Count - 1; i >= 0; i--) if (Sites[i].state == SiteState.Building) return Sites[i];
            return null;
        }

        /// Debug ekranı için kısa özet (her şantiye bir satır)
        public string DebugSummary()
        {
            if (Sites.Count == 0) return "Şantiye yok";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Sites.Count; i++)
            {
                var s = Sites[i];
                if (i > 0) sb.Append('\n');
                sb.Append(i == Selected ? "> " : "  ");
                sb.Append($"{s.letter} {s.customer.id} {s.StepLabel} ({Mathf.Min(s.step, s.totalSteps)}/{s.totalSteps})");
                if (s.state == SiteState.Building)
                    sb.Append($" {s.timer:0}/{s.duration:0}sn · hata {s.activeFaults.Count}");
                else if (s.state != SiteState.Done)
                    sb.Append($" {s.state}");
                sb.Append($" · kusur {s.defects}");
            }
            if (SpeedMultiplier > 1f) sb.Append($"\n(hız x{SpeedMultiplier})");
            return sb.ToString();
        }
    }
}
