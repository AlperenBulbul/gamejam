using System;
using UnityEngine;

namespace LoadBearing
{
    /// Sistemler birbirini doğrudan çağırmaz; bu olaylarla haberleşir.
    /// Dinlemek:  GameEvents.CustomerArrived += OnArrived;   (OnDisable'da -= ile bırak)
    /// Yayınlamak: GameEvents.RaiseCustomerArrived(customer);
    public static class GameEvents
    {
        public static event Action<GameState> StateChanged;
        public static event Action<CustomerData> CustomerArrived;
        public static event Action<CustomerData> CustomerLeaving;            // dosyasını alıp kapıya yürümeye başladı
        public static event Action<CustomerData, bool> CustomerLeft;        // bool = sinirli mi gitti
        public static event Action<PatienceStage> PatienceStageChanged;
        public static event Action<CustomerData, bool> Stamped;             // bool = onaylandı mı
        public static event Action<int> BribeTaken;
        public static event Action<string> PenaltyAdded;
        public static event Action WrongfulReject;
        public static event Action<ConstructionSite> SiteCreated;
        public static event Action<ConstructionSite> FaultStarted;
        public static event Action<ConstructionSite> FaultFixed;
        public static event Action<ConstructionSite> FaultsCleared;
        public static event Action<ConstructionSite> StepCompleted;
        public static event Action<ConstructionSite> SiteFinished;
        public static event Action<ConstructionSite> SiteFlooded;
        public static event Action<ConstructionSite> SiteCollapsed;
        public static event Action<FocusState> FocusChanged;
        public static event Action<FocusState, FocusState> FocusTransitionStarted; // (nereden, nereye) kamera hareketi başlarken
        public static event Action<int> DayEnded;

        public static void RaiseStateChanged(GameState s) => StateChanged?.Invoke(s);
        public static void RaiseCustomerArrived(CustomerData c) => CustomerArrived?.Invoke(c);
        public static void RaiseCustomerLeaving(CustomerData c) => CustomerLeaving?.Invoke(c);
        public static void RaiseCustomerLeft(CustomerData c, bool angry) => CustomerLeft?.Invoke(c, angry);
        public static void RaisePatienceStageChanged(PatienceStage s) => PatienceStageChanged?.Invoke(s);
        public static void RaiseStamped(CustomerData c, bool approved) => Stamped?.Invoke(c, approved);
        public static void RaiseBribeTaken(int amount) => BribeTaken?.Invoke(amount);
        public static void RaisePenaltyAdded(string reason) => PenaltyAdded?.Invoke(reason);
        public static void RaiseWrongfulReject() => WrongfulReject?.Invoke();
        public static void RaiseSiteCreated(ConstructionSite s) => SiteCreated?.Invoke(s);
        public static void RaiseFaultStarted(ConstructionSite s) => FaultStarted?.Invoke(s);
        public static void RaiseFaultFixed(ConstructionSite s) => FaultFixed?.Invoke(s);
        public static void RaiseFaultsCleared(ConstructionSite s) => FaultsCleared?.Invoke(s);
        public static void RaiseStepCompleted(ConstructionSite s) => StepCompleted?.Invoke(s);
        public static void RaiseSiteFinished(ConstructionSite s) => SiteFinished?.Invoke(s);
        public static void RaiseSiteFlooded(ConstructionSite s) => SiteFlooded?.Invoke(s);
        public static void RaiseSiteCollapsed(ConstructionSite s) => SiteCollapsed?.Invoke(s);
        public static void RaiseFocusChanged(FocusState f) => FocusChanged?.Invoke(f);
        public static void RaiseFocusTransitionStarted(FocusState from, FocusState to) => FocusTransitionStarted?.Invoke(from, to);
        public static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);

        // Play'e her basışta eski abonelikleri temizle (Domain Reload kapalıysa bile güvenli)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            StateChanged = null; CustomerArrived = null; CustomerLeaving = null; CustomerLeft = null; PatienceStageChanged = null;
            Stamped = null; BribeTaken = null; PenaltyAdded = null; WrongfulReject = null;
            SiteCreated = null; FaultStarted = null; FaultFixed = null; FaultsCleared = null;
            StepCompleted = null; SiteFinished = null; SiteFlooded = null; SiteCollapsed = null;
            FocusChanged = null; FocusTransitionStarted = null; DayEnded = null;
        }
    }
}
