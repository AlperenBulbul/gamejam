using System;
using System.Collections.Generic;

namespace LoadBearing
{
    [Serializable]
    public struct Decision
    {
        public string customerId;
        public bool approved;
        public bool valid;
    }

    /// Bir oyunun (run) tüm durumu. YENİDEN OYNA'da sıfırdan oluşturulur.
    [Serializable]
    public class RunState
    {
        public int day = 1;
        public int balance;

        public int penalties;
        public int wrongfulRejects;
        public List<string> penaltyReasons = new List<string>();
        public List<Decision> decisions = new List<Decision>();

        // Rüşvet
        public bool anyBribe;
        public bool bribeTakenCurrent;   // şu anki müşterinin zarfı alındı mı
        public string bribeOnApproved;   // rüşveti alınıp onaylanan müşterinin id'si (C7)

        // Sonlar
        public bool jailed;
        public bool collapseFired;
        public bool bankrupt;
        public bool pendingFired;        // 3. ceza / 3. haksız ret: müşteri çıkınca Ending

        // Gazete için o günün olayları (HeadlineData.key listesi)
        public List<string> todayHeadlineKeys = new List<string>();

        public DayLedger ledger = new DayLedger();

        public RunState(GameConfig cfg)
        {
            balance = cfg != null ? cfg.startBalance : 0;
        }

        public void AddPenalty(string reason, GameConfig cfg)
        {
            penalties++;
            penaltyReasons.Add(reason);
            GameEvents.RaisePenaltyAdded(reason);
            if (penalties >= cfg.penaltyLimit) pendingFired = true;
        }

        public void AddWrongfulReject(GameConfig cfg)
        {
            wrongfulRejects++;
            GameEvents.RaiseWrongfulReject();
            if (wrongfulRejects >= cfg.wrongfulRejectLimit) pendingFired = true;
        }
    }
}
