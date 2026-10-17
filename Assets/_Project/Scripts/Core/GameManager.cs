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
        public bool DeskInputEnabled => InDeskState && !PauseMenu.IsPaused;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GameConfig.Current = config;
            if (config == null) Debug.LogError("[GM] GameConfig atanmamış! Systems > GameManager'a GameConfig.asset'i sürükle.");
            if (GetComponent<ConstructionManager>() == null) gameObject.AddComponent<ConstructionManager>(); // şantiyeler
            if (GetComponent<TutorialHints>() == null) gameObject.AddComponent<TutorialHints>();             // ilk tur göstergeleri
            if (GetComponent<PauseMenu>() == null) gameObject.AddComponent<PauseMenu>();                     // Esc: duraklatma menüsü
            NewRun();
        }

        void Start() => ChangeState(GameState.MainMenu);

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
            ChangeState(IsLastDay ? GameState.FinalShift : GameState.Receipt);
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
