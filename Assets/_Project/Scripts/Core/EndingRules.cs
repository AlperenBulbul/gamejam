using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Gün sonu denetiminde ilk tutan kovulma nedeni (GDD v3.2 Eki, bölüm 3)
    public enum FireReason { None, Bribe, FaultLine, Flood, WrongfulRejects, Penalties, Bankrupt }

    /// Kurallara aykırı onayın türü (Validator'la aynı sıra: ilk tutan sayılır)
    public enum WrongApproval { None, FaultLine, Flood, FloorLimit, Soil }

    /// Depremden ve selden sonra şantiyenin durumu
    public enum SiteOutcome { Standing, Damaged, CollapsedQuake, CollapsedFlood }

    /// Oyunun sonu. Videolar: Resources/EndingVideos (EndScreenUI.VideoName eşler).
    /// Rüşvet artık hapis sonu (tasarımcı, 17 Ekim): Jail_Bribe.
    public enum EndingId
    {
        Jail_Bribe, Fired_FaultLine, Fired_Flood, Fired_WrongfulRejects, Fired_Penalties, Fired_Bankrupt,
        Jail_Quake, Jail_Flood, NoOneDied, StillOnTheJob,
    }

    public struct EndingResult
    {
        public EndingId id;
        public string title;
        public string body;
        public bool badge;                 // Kimse Ölmedi + 0 hata: "Yılın Denetçisi" plaketi
        public CustomerData featured;      // Hapis sonlarında yıkılan binanın sahibi
        public bool good;                  // ekranda yeşil (Kimse Ölmedi)
        public bool neutral;               // ekranda sarı (Hâlâ İşte)
    }

    /// Oyunun kuralları: yanlış onay türü, gün sonu denetimi, deprem / sel sonucu ve son (GDD v3.2 Eki).
    /// Sayılar GameConfig'te.
    public static class EndingRules
    {
        // ------------------------------------------------------------------ yanlış onay

        public static WrongApproval Classify(CustomerData c, int day, GameConfig cfg)
        {
            if (c == null) return WrongApproval.None;
            if (c.onFaultLine) return WrongApproval.FaultLine;
            if (c.inFloodZone) return WrongApproval.Flood;
            if (cfg != null && c.floors > cfg.FloorLimitFor(c.areaM2)) return WrongApproval.FloorLimit;
            if (day >= 2 && c.floors >= 5 && c.soil != SoilType.Solid) return WrongApproval.Soil;
            return WrongApproval.None;
        }

        /// Mühür basıldığında: sayaçlar ve rüşvet günü (StampController çağırır)
        public static void RecordDecision(RunState run, CustomerData c, bool approved, bool valid, GameConfig cfg)
        {
            if (run == null || c == null) return;
            if (approved && !valid)
            {
                var type = Classify(c, run.day, cfg);
                run.wrongApprovalIds.Add(c.id);
                run.wrongApprovalTypes.Add(type);
                if (type == WrongApproval.FaultLine) run.faultLineApprovals++;
                if (type == WrongApproval.Flood) run.floodApprovals++;
            }
            // Zarf alındıysa karar ne olursa olsun o günün sonunda kovulma
            if (run.bribeTakenCurrent) run.bribeTakenDay = run.day;
        }

        // ------------------------------------------------------------------ gün sonu denetimi

        public static FireReason Inspect(RunState run, int day, GameConfig cfg)
        {
            if (run == null) return FireReason.None;
            int faultLimit = cfg != null ? cfg.faultLineFireLimit : 2;
            int floodLimit = cfg != null ? cfg.floodFireLimit : 2;
            int rejectLimit = cfg != null ? cfg.wrongfulRejectLimit : 3;
            int penaltyLimit = cfg != null ? cfg.penaltyLimit : 3;
            if (run.bribeTakenDay == day) return FireReason.Bribe;
            if (run.faultLineApprovals >= faultLimit) return FireReason.FaultLine;
            if (run.floodApprovals >= floodLimit) return FireReason.Flood;
            if (run.wrongfulRejects >= rejectLimit) return FireReason.WrongfulRejects;
            if (run.penalties >= penaltyLimit) return FireReason.Penalties;
            if (run.balance < 0) return FireReason.Bankrupt;
            return FireReason.None;
        }

        // ------------------------------------------------------------------ deprem + sel

        /// Depremde: fay hattı / kat sınırı / 5+ kat sağlam zemin yok -> çöker; 3+ kusur -> hasarlı.
        /// Selde: taşkın alanındaki bina (depremde çökmediyse) yıkılır.
        public static SiteOutcome Outcome(ConstructionSite s, GameConfig cfg)
        {
            var c = s != null ? s.customer : null;
            if (c == null) return SiteOutcome.Standing;
            int damage = cfg != null ? cfg.defectDamage : 3;
            if (c.onFaultLine) return SiteOutcome.CollapsedQuake;
            if (cfg != null && c.floors > cfg.FloorLimitFor(c.areaM2)) return SiteOutcome.CollapsedQuake;
            if (c.floors >= 5 && c.soil != SoilType.Solid) return SiteOutcome.CollapsedQuake;
            if (c.inFloodZone) return SiteOutcome.CollapsedFlood;
            if (s.defects >= damage) return SiteOutcome.Damaged;
            return SiteOutcome.Standing;
        }

        // ------------------------------------------------------------------ son

        public static EndingResult Resolve(GameManager gm)
        {
            var run = gm != null ? gm.Run : null;
            var cfg = gm != null ? gm.Config : null;
            if (run == null) return Make(EndingId.StillOnTheJob);

            if (run.fireReason != FireReason.None) return Make(FiredId(run.fireReason));

            // Deprem ve sel
            CustomerData quake = null, flood = null;
            int damaged = 0;
            var cm = ConstructionManager.Instance;
            if (cm != null)
                foreach (var s in cm.Sites)
                {
                    switch (Outcome(s, cfg))
                    {
                        case SiteOutcome.CollapsedQuake:
                            if (quake == null || s.customer.floors > quake.floors) quake = s.customer; break;
                        case SiteOutcome.CollapsedFlood:
                            if (flood == null || s.customer.floors > flood.floors) flood = s.customer; break;
                        case SiteOutcome.Damaged: damaged++; break;
                    }
                }
            if (quake != null) { var r = Make(EndingId.Jail_Quake); r.featured = quake; return r; }
            if (flood != null) { var r = Make(EndingId.Jail_Flood); r.featured = flood; return r; }

            // Küçük hatalar: haksız ret + kaçan müşteri + hasarlı bina
            int small = SmallMistakes(run, damaged);
            int happyMax = cfg != null ? cfg.happyMaxSmallMistakes : 1;
            if (small <= happyMax)
            {
                var r = Make(EndingId.NoOneDied);
                r.badge = small == 0;
                return r;
            }
            return Make(EndingId.StillOnTheJob);
        }

        public static int SmallMistakes(RunState run, int damagedSites) =>
            (run != null ? run.wrongfulRejects + run.penalties : 0) + damagedSites;

        static EndingId FiredId(FireReason r) => r switch
        {
            FireReason.Bribe => EndingId.Jail_Bribe,
            FireReason.FaultLine => EndingId.Fired_FaultLine,
            FireReason.Flood => EndingId.Fired_Flood,
            FireReason.WrongfulRejects => EndingId.Fired_WrongfulRejects,
            FireReason.Penalties => EndingId.Fired_Penalties,
            _ => EndingId.Fired_Bankrupt,
        };

        /// Son yazıları (GDD v3.2 Eki, bölüm 7)
        public static EndingResult Make(EndingId id)
        {
            var r = new EndingResult { id = id };
            switch (id)
            {
                case EndingId.Jail_Quake: r.title = "HAPİS"; r.body = "Mühür senin, imza senin. Deprem birkaç saniye sürdü; dava yıllarca sürecek."; break;
                case EndingId.Jail_Flood: r.title = "HAPİS"; r.body = "Dere yatağını herkes biliyordu. Haritada da yazıyordu. Su da biliyordu."; break;
                case EndingId.Jail_Bribe: r.title = "HAPİS"; r.body = "Zarf çekmecende bir gün bile durmadı. Müfettişler akşam geldi."; break;
                case EndingId.Fired_FaultLine: r.title = "KOVULDUN"; r.body = "Bir kez hata denir. İkincisi dosya olur."; break;
                case EndingId.Fired_Flood: r.title = "KOVULDUN"; r.body = "İki aileyi dere kenarına yerleştirdin. Belediye seni yerinden etti."; break;
                case EndingId.Fired_WrongfulRejects: r.title = "KOVULDUN"; r.body = "Hiç bina yıkılmadı, çünkü hiç bina yapılmadı."; break;
                case EndingId.Fired_Penalties: r.title = "KOVULDUN"; r.body = "Kapıda bekleyenlerin şikâyet dilekçesi senin dosyandan kalın çıktı."; break;
                case EndingId.Fired_Bankrupt: r.title = "KOVULDUN"; r.body = "Belediyenin kasası boşaldı. İlk kesilen kalem sendin."; break;
                case EndingId.NoOneDied: r.title = "SAYENDE KİMSE ÖLMEDİ"; r.body = "Deprem Karaova'yı salladı ama yıkamadı. Kimse senin adını bilmeyecek. Olsun."; r.good = true; break;
                default: r.title = "HÂLÂ İŞTE"; r.body = "Karaova ayakta. Birkaç kişi sana kırgın. Yarın masada yeni dosyalar var."; r.neutral = true; break;
            }
            return r;
        }

        /// Kovulma nedeninin kısa adı (özet satırı için)
        public static string ReasonLabel(EndingId id) => id switch
        {
            EndingId.Jail_Bribe => "Hapis — Rüşvet",
            EndingId.Fired_FaultLine => "Kovuldun — Fay Hattı",
            EndingId.Fired_Flood => "Kovuldun — Dere Yatağı",
            EndingId.Fired_WrongfulRejects => "Kovuldun — Ruhsat Vermeyen Denetçi",
            EndingId.Fired_Penalties => "Kovuldun — Kapıdan Çevrilenler",
            EndingId.Fired_Bankrupt => "Kovuldun — İflas",
            EndingId.Jail_Quake => "Hapis — Deprem",
            EndingId.Jail_Flood => "Hapis — Sel",
            EndingId.NoOneDied => "Kimse Ölmedi",
            _ => "Hâlâ İşte",
        };
    }
}
