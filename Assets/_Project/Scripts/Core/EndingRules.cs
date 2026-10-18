using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Gün sonu denetiminde ilk tutan kovulma nedeni (GDD v3.2 Eki, bölüm 3)
    public enum FireReason { None, Bribe, FaultLine, Flood, WrongfulRejects, Penalties, Bankrupt, DailyMistakes, MinigameFailed }

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
        Fired_DailyMistakes,   // aynı gün 2 yanlış mühür (tehlikeli onay yoksa)
        Fired_Minigame,        // monitör kontrolünde (mikser / kolon) çok hata
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
        /// Sonuç sinematiğinden önce oynayan felaket videosu (Resources/EndingVideos): CIN_Final_Deprem / CIN_Final_Sel, yoksa null
        public string prelude;
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

        /// Mühür basıldığında: sayaçlar ve rüşvet günü (StampController çağırır).
        /// Aynı gün 2. yanlış mühürde oyun biter (müşteri gişeden çıkınca son sinematiği).
        public static void RecordDecision(RunState run, CustomerData c, bool approved, bool valid, GameConfig cfg)
        {
            if (run == null || c == null) return;
            // Zarf alındıysa karar ne olursa olsun o günün sonunda hapis
            if (run.bribeTakenCurrent) run.bribeTakenDay = run.day;

            if (approved == valid) return;
            var type = WrongApproval.None;
            if (approved)
            {
                type = Classify(c, run.day, cfg);
                run.wrongApprovalIds.Add(c.id);
                run.wrongApprovalTypes.Add(type);
                if (type == WrongApproval.FaultLine) run.faultLineApprovals++;
                if (type == WrongApproval.Flood) run.floodApprovals++;
            }

            if (run.mistakesDay != run.day)
            {
                run.mistakesDay = run.day;
                run.mistakesToday = run.wrongRejectsToday = run.floodApprovalsToday = 0;
            }
            run.mistakesToday++;
            if (!approved) run.wrongRejectsToday++;
            else if (type == WrongApproval.Flood) run.floodApprovalsToday++;

            int limit = cfg != null ? cfg.dailyMistakeLimit : 2;
            if (run.mistakesToday >= limit && run.fireReason == FireReason.None)
            {
                run.fireReason = run.bribeTakenDay == run.day ? FireReason.Bribe
                               : run.wrongRejectsToday >= limit ? FireReason.WrongfulRejects
                               : run.floodApprovalsToday >= limit ? FireReason.Flood
                               : FireReason.DailyMistakes;
                run.pendingFired = true;
                Debug.Log($"[Denetim] Gün {run.day}: bugün {run.mistakesToday}. yanlış mühür, oyun bitiyor ({run.fireReason})");
            }
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
            if (MinigameFailed(day, cfg)) return FireReason.MinigameFailed;
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

        /// O gün monitör kontrolü (mikser / kolon) kaybedildi mi: tek oyunda defectDamage (3) ve üstü hata
        public static bool MinigameFailed(int day, GameConfig cfg)
        {
            int limit = cfg != null ? cfg.defectDamage : 3;
            foreach (var r in MinigameDirector.Results)
                if (r.day == day && r.defects >= limit) return true;
            return false;
        }

        public const string PreludeQuake = "CIN_Final_Deprem";
        public const string PreludeFlood = "CIN_Final_Sel";

        /// Oyunun sonu. Sıra: felaket videosu (varsa) -> sonuç videosu (hapis / kovulma / kimse ölmedi).
        ///
        /// FELAKET VİDEOSU: yapılan yanlış onaylara bakılır
        ///   fay hattı / fazla kat / zemin onayı daha çoksa -> Deprem
        ///   dere yatağı onayı daha çoksa                   -> Sel
        ///   ikisi eşitse (ve en az biri varsa)            -> ikisinden rastgele biri
        ///   hiç yoksa                                      -> felaket videosu yok
        ///
        /// SONUÇ VİDEOSU: oyunun neden bittiğine göre
        ///   Rüşvet                         -> Hapis–Rüşvet (İsmail'le hücrede)
        ///   Aynı gün 2 yanlış mühür        -> felaket Deprem ise Hapis–Deprem, Sel ise Hapis–Sel,
        ///                                     felaket yoksa: 2 haksız ret -> Kovuldun–Ruhsat vermeyen, diğer -> Kovuldun–"İkincisi dosya olur"
        ///   Gün sonu: 2 fay onayı          -> Kovuldun–Fay
        ///   Gün sonu: 2 dere onayı         -> Kovuldun–Dere
        ///   Gün sonu: 3 haksız ret         -> Kovuldun–Ruhsat vermeyen
        ///   Gün sonu: 3 kaçan müşteri      -> Kovuldun–Kapıdan çevrilenler
        ///   Gün sonu: minigame kaybı       -> Kovuldun–Kasa boşaldı
        ///   Son gün: bina çöktü / yıkıldı  -> Hapis–Deprem / Hapis–Sel (felaket videosu yıkılan binalara göre)
        ///   Son gün: en fazla 1 küçük hata -> Kimse Ölmedi (0 hata: plaketli) · diğer -> Hâlâ İşte
        public static EndingResult Resolve(GameManager gm)
        {
            var run = gm != null ? gm.Run : null;
            var cfg = gm != null ? gm.Config : null;
            if (run == null) return Make(EndingId.StillOnTheJob);

            if (run.fireReason != FireReason.None)
            {
                int disaster = PickDisaster(gm, run, out var wrongOne);
                EndingResult r;
                switch (run.fireReason)
                {
                    case FireReason.Bribe:
                        r = Make(EndingId.Jail_Bribe);
                        break;
                    case FireReason.DailyMistakes:
                    case FireReason.WrongfulRejects when run.pendingFired:
                    case FireReason.Flood when run.pendingFired:
                        // Aynı gün 2 yanlış mühür: oyun anında biter
                        if (disaster == 1) r = Make(EndingId.Jail_Quake);
                        else if (disaster == 2) r = Make(EndingId.Jail_Flood);
                        else r = Make(run.fireReason == FireReason.WrongfulRejects ? EndingId.Fired_WrongfulRejects : EndingId.Fired_DailyMistakes);
                        break;
                    default:
                        r = Make(FiredId(run.fireReason));
                        break;
                }
                r.prelude = disaster == 1 ? PreludeQuake : disaster == 2 ? PreludeFlood : null;
                r.featured = wrongOne;
                return r;
            }

            // Son gün: deprem ve sel
            CustomerData quake = null, flood = null;
            int quakeN = 0, floodN = 0, damaged = 0;
            var cm = ConstructionManager.Instance;
            if (cm != null)
                foreach (var s in cm.Sites)
                {
                    switch (Outcome(s, cfg))
                    {
                        case SiteOutcome.CollapsedQuake:
                            quakeN++;
                            if (quake == null || s.customer.floors > quake.floors) quake = s.customer; break;
                        case SiteOutcome.CollapsedFlood:
                            floodN++;
                            if (flood == null || s.customer.floors > flood.floors) flood = s.customer; break;
                        case SiteOutcome.Damaged: damaged++; break;
                    }
                }
            if (quakeN + floodN > 0)
            {
                bool q = quakeN > floodN || (quakeN == floodN && Random.value < 0.5f);
                var r = Make(q ? EndingId.Jail_Quake : EndingId.Jail_Flood);
                r.prelude = q ? PreludeQuake : PreludeFlood;
                r.featured = q ? quake : flood;
                return r;
            }

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

        /// Yanlış onaylardan felaket: 0 yok, 1 deprem (fay / fazla kat / zemin), 2 sel (dere yatağı).
        /// Hangisi daha çoksa o; eşitse rastgele. wrongOne: o türdeki ilk yanlış onaylanan müşteri.
        static int PickDisaster(GameManager gm, RunState run, out CustomerData wrongOne)
        {
            int quakeN = 0, floodN = 0;
            foreach (var t in run.wrongApprovalTypes)
            {
                if (t == WrongApproval.FaultLine || t == WrongApproval.FloorLimit || t == WrongApproval.Soil) quakeN++;
                else if (t == WrongApproval.Flood) floodN++;
            }
            wrongOne = null;
            if (quakeN + floodN == 0) return 0;
            bool q = quakeN > floodN || (quakeN == floodN && Random.value < 0.5f);
            wrongOne = WrongCustomer(gm, run, q);
            Debug.Log($"[Son] Yanlış onaylar: deprem türü {quakeN}, sel türü {floodN} -> {(q ? "Deprem" : "Sel")} videosu");
            return q ? 1 : 2;
        }

        /// Kurallara aykırı onaylanan ilk müşteri: deprem türü (fay / kat / zemin) ya da sel (dere yatağı)
        static CustomerData WrongCustomer(GameManager gm, RunState run, bool quakeType)
        {
            for (int i = 0; i < run.wrongApprovalTypes.Count && i < run.wrongApprovalIds.Count; i++)
            {
                var t = run.wrongApprovalTypes[i];
                bool isQuake = t == WrongApproval.FaultLine || t == WrongApproval.FloorLimit || t == WrongApproval.Soil;
                bool isFlood = t == WrongApproval.Flood;
                if ((quakeType && isQuake) || (!quakeType && isFlood)) return gm.FindCustomer(run.wrongApprovalIds[i]);
            }
            return null;
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
            FireReason.DailyMistakes => EndingId.Fired_DailyMistakes,
            FireReason.MinigameFailed => EndingId.Fired_Minigame,
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
                case EndingId.Fired_DailyMistakes: r.title = "KOVULDUN"; r.body = "Bir kez hata denir. İkincisi dosya olur."; break;
                case EndingId.Fired_FaultLine: r.title = "KOVULDUN"; r.body = "Bir kez hata denir. İkincisi dosya olur."; break;
                case EndingId.Fired_Flood: r.title = "KOVULDUN"; r.body = "İki aileyi dere kenarına yerleştirdin. Belediye seni yerinden etti."; break;
                case EndingId.Fired_WrongfulRejects: r.title = "KOVULDUN"; r.body = "Hiç bina yıkılmadı, çünkü hiç bina yapılmadı."; break;
                case EndingId.Fired_Penalties: r.title = "KOVULDUN"; r.body = "Kapıda bekleyenlerin şikâyet dilekçesi senin dosyandan kalın çıktı."; break;
                case EndingId.Fired_Bankrupt: r.title = "KOVULDUN"; r.body = "Belediyenin kasası boşaldı. İlk kesilen kalem sendin."; break;
                case EndingId.Fired_Minigame: r.title = "KOVULDUN"; r.body = "Belediyenin kasası boşaldı. İlk kesilen kalem sendin."; break; // hatalı şantiyeyi belediye ödedi
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
            EndingId.Fired_DailyMistakes => "Kovuldun — Bir günde iki yanlış mühür",
            EndingId.Fired_Minigame => "Kovuldun — Hatalı şantiye (monitör kontrolü)",
            EndingId.Jail_Quake => "Hapis — Deprem",
            EndingId.Jail_Flood => "Hapis — Sel",
            EndingId.NoOneDied => "Kimse Ölmedi",
            _ => "Hâlâ İşte",
        };
    }
}
