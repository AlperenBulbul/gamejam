using UnityEngine;

namespace LoadBearing
{
    /// Oyun akışının tek durum makinesi (Geliştirme dokümanı bölüm 4).
    /// Diğer sistemler durum değiştirmek için aşağıdaki "geçiş" metotlarını çağırır,
    /// durumu dinlemek için GameEvents.StateChanged'e abone olur.
    [DefaultExecutionOrder(-100)] // diğer scriptlerden önce Awake olsun
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] DayData[] days;

        public GameConfig Config => config;
        public DayData[] Days => days;

        /// "C3" gibi id ile müşteri verisini bulur (debug ve testler için)
        public CustomerData FindCustomer(string id)
        {
            foreach (var d in days)
                if (d != null && d.customers != null)
                    foreach (var c in d.customers)
                        if (c != null && c.id == id) return c;
            return null;
        }
        public RunState Run { get; private set; }
        public GameState State { get; private set; } = GameState.MainMenu;
        public DayData CurrentDay => days[Mathf.Clamp(Run.day - 1, 0, days.Length - 1)];
        public bool IsLastDay => Run.day >= days.Length;

        /// Masaya bakma / tıklama sadece bu durumlarda açık (menü, fiş, dükkân vb. ekranlarda kapalı)
        public bool InDeskState => State == GameState.DayIntro || State == GameState.Working || State == GameState.FinalShift;
        /// Masa kontrolleri açık mı (masa durumunda ve oyun duraklatılmamış)
        public bool DeskInputEnabled => InDeskState && !PauseMenu.IsPaused && !MinigameDirector.IntroOpen && !EndScreenUI.Blocking;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameConfig.Current = config;
            if (config == null) Debug.LogError("[GM] GameConfig atanmamış! Systems > GameManager'a GameConfig.asset'i sürükle.");
            if (GetComponent<ConstructionManager>() == null) gameObject.AddComponent<ConstructionManager>(); // şantiyeler
            if (GetComponent<TutorialHints>() == null) gameObject.AddComponent<TutorialHints>();             // ilk tur göstergeleri
            if (GetComponent<GuidedTour>() == null) gameObject.AddComponent<GuidedTour>();                   // hazır oynatılan ilk tur
            if (GetComponent<DeskMug>() == null) gameObject.AddComponent<DeskMug>();                         // masadaki ODTÜ kupası
            if (GetComponent<UiTextScale>() == null) gameObject.AddComponent<UiTextScale>();                 // ekran yazıları bir tık büyük
            if (GetComponent<SiteWatch>() == null) gameObject.AddComponent<SiteWatch>();                     // monitörde kaytaran işçiyi uyarma
            if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();                     // Esc: duraklatma menüsü
            if (GetComponent<MinigameDirector>() == null) gameObject.AddComponent<MinigameDirector>();       // gün sonu monitör minigame'i
            if (GetComponent<EndScreenUI>() == null) gameObject.AddComponent<EndScreenUI>();                 // gün geçişi + bitiş ekranı
            if (GetComponent<GameAudio>() == null) gameObject.AddComponent<GameAudio>();                     // final ses paketi (zip 24)
            if (GetComponent<BrightnessFx>() == null) gameObject.AddComponent<BrightnessFx>();               // Ayarlar > Parlaklık
            if (GetComponent<IntroCinematic>() == null) gameObject.AddComponent<IntroCinematic>();           // açılış sinematiği (Nasıl Oynanır > BAŞLA sonrası)
            NewRun();
        }

        void Start()
        {
            HideCounterGlass();
            ChangeState(GameState.MainMenu);
        }

        /// Müşteriyle aramızdaki cam kaldırıldı (görünmez; cam sallanma efekti için nesne yerinde kalır)
        static void HideCounterGlass()
        {
            var glass = GameObject.Find("Counter/Glass");
            if (glass != null)
                foreach (var r in glass.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r != null && (r.name.StartsWith("SM_CounterGlass_Glass") || r.name.StartsWith("SM_CounterGlass_Streak"))) r.enabled = false;
        }

        public void NewRun() => Run = new RunState(config);

        public void ChangeState(GameState next)
        {
            State = next;
            Debug.Log($"[GM] -> {next}  (gün {Run.day})");
            GameEvents.RaiseStateChanged(next);
        }

        // ---------------- Geçişler ----------------

        /// MainMenu: BAŞLA
        public void StartGame()
        {
            NewRun();
            Run.day = 1;
            ChangeState(GameState.DayIntro);
        }

        /// DayIntro: gazeteye tıklandı
        public void BeginWorking()
        {
            if (State != GameState.DayIntro) return;
            Run.todayHeadlineKeys.Clear();
            Run.ledger.Reset();
            ChangeState(GameState.Working);
        }

        /// Working: bir müşteri çıktı. Kovulma bekliyorsa doğrudan sona git.
        /// true dönerse oyun bitti, sıradaki müşteri çağrılmamalı.
        public bool CheckPendingFired()
        {
            if (!Run.pendingFired) return false;
            ChangeState(GameState.Ending);
            return true;
        }

        /// Working: günün son müşterisi çıktı
        public void OnLastCustomerLeft()
        {
            if (State != GameState.Working) return;
            if (CheckPendingFired()) return;
            // Önce telsiz + monitör minigame'i (1. gün mikser, 2. gün kolon); bitince gün devam eder
            var mg = MinigameDirector.Instance;
            if (mg != null && mg.TryStart(Run.day, FinishWorkingDay)) return;
            FinishWorkingDay();
        }

        void FinishWorkingDay()
        {
            if (State != GameState.Working) return;
            // Gün sonu denetimi (GDD v3.2 Eki): ilk tutan kovulma nedeni -> kovulma sonu
            var reason = EndingRules.Inspect(Run, Run.day, Config);
            if (reason != FireReason.None)
            {
                Run.fireReason = reason;
                Debug.Log($"[Denetim] Gün {Run.day}: kovuldun ({reason})");
                ChangeState(GameState.Ending);
                return;
            }
            // Son gün: deprem + sel sonucu ve son (EndingRules.Resolve), bitiş ekranı
            if (IsLastDay) { ChangeState(GameState.Ending); return; }
            // 1. gün bitti: doğrudan ertesi güne geç (fiş / dükkân ekranları henüz yok)
            GameEvents.RaiseDayEnded(Run.day);
            Run.day++;
            ChangeState(GameState.DayIntro);
        }

        /// FinalShift: tüm şantiyeler Done / Flooded
        public void OnAllSitesSettled()
        {
            if (State == GameState.FinalShift) ChangeState(GameState.Receipt);
        }

        /// Receipt: DEVAM (iflas kontrolü dükkândan önce)
        public void OnReceiptContinue()
        {
            ChangeState(Run.bankrupt ? GameState.Ending : GameState.Shop);
        }

        /// Shop: DEVAM
        public void OnShopContinue() => ChangeState(GameState.DayEvent);

        /// DayEvent: yağmur / deprem animasyonu bitti
        public void OnDayEventFinished() => ChangeState(GameState.Newspaper);

        /// Newspaper: kapatıldı
        public void OnNewspaperClosed()
        {
            GameEvents.RaiseDayEnded(Run.day);
            if (IsLastDay || Run.pendingFired) { ChangeState(GameState.Ending); return; }
            Run.day++;
            ChangeState(GameState.DayIntro);
        }

        /// Ending: YENİDEN OYNA
        public void OnRestart()
        {
            NewRun();
            ChangeState(GameState.MainMenu);
        }

        // ---------------- Debug ----------------

        /// F8: sistemler henüz yokken akışı elle bir adım ilerletir (D-05 testi).
        public void DebugAdvance()
        {
            switch (State)
            {
                case GameState.MainMenu: StartGame(); break;
                case GameState.DayIntro: BeginWorking(); break;
                case GameState.Working: OnLastCustomerLeft(); break;
                case GameState.FinalShift: OnAllSitesSettled(); break;
                case GameState.Receipt: OnReceiptContinue(); break;
                case GameState.Shop: OnShopContinue(); break;
                case GameState.DayEvent: OnDayEventFinished(); break;
                case GameState.Newspaper: OnNewspaperClosed(); break;
                case GameState.Ending: OnRestart(); break;
            }
        }

        /// F9
        public void DebugJumpToDay(int day)
        {
            Run.day = Mathf.Clamp(day, 1, days.Length);
            ChangeState(GameState.DayIntro);
        }
    }
}
