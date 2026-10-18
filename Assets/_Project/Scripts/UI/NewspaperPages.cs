using UnityEngine;

namespace LoadBearing
{
    /// Hazır gazete ön sayfaları (tasarımcı, 18 Ekim): Resources/Newspaper/T_NewspaperFront_<ad>.
    ///  - Gün başı (2. gün ve sonrası): masadaki gazete önceki günün olayına göre değişir
    ///  - Oyun sonu: sinematikten sonra ertesi sabahın gazetesi gösterilir
    public static class NewspaperPages
    {
        public static Texture2D Load(string name) =>
            string.IsNullOrEmpty(name) ? null : Resources.Load<Texture2D>("Newspaper/T_NewspaperFront_" + name);

        /// Bugünün (run.day) masadaki gazetesi: dünkü kararlara göre. null = ilk gün gazetesi kalır.
        public static string ForDay(GameManager gm)
        {
            var run = gm != null ? gm.Run : null;
            if (run == null || run.day < 2) return null;
            int prev = run.day - 1;
            int fault = 0, flood = 0, wrongApprove = 0, wrongReject = 0;
            foreach (var d in run.decisions)
            {
                var c = gm.FindCustomer(d.customerId);
                if (c == null || c.day != prev) continue;
                if (d.approved && !d.valid)
                {
                    wrongApprove++;
                    if (c.onFaultLine) fault++;
                    else if (c.inFloodZone) flood++;
                }
                if (!d.approved && d.valid) wrongReject++;
            }
            bool someoneLeft = run.todayHeadlineKeys.Contains("CustomerLeft"); // dünün anahtarları (mesai başında silinir)

            if (fault > 0) return run.faultLineApprovals >= 2 ? "FaultApproval_Plural" : "FaultApproval";
            if (flood > 0) return run.floodApprovals >= 2 ? "FloodApproval_Plural" : "FloodApproval";
            if (wrongReject > 0) return wrongReject >= 2 ? "WrongfulReject_Plural" : "WrongfulReject";
            if (wrongApprove == 0 && !someoneLeft) return "Positive_Day";
            return null;
        }

        /// Oyun sonundaki gazete
        public static string ForEnding(EndingResult end) => end.id switch
        {
            EndingId.Jail_Quake => "CollapseQuake",
            EndingId.Jail_Flood => "CollapseFlood",
            EndingId.Jail_Bribe => "Bribe",
            EndingId.Fired_FaultLine => "Fired_Fault",
            EndingId.Fired_Flood => "Fired_Flood",
            EndingId.Fired_WrongfulRejects => "Fired_NoPermit",
            EndingId.Fired_Penalties => "Fired_TurnedAway",
            EndingId.Fired_Bankrupt => "Fired_Bankrupt",
            EndingId.NoOneDied => "Positive_QuakeStanding",
            EndingId.StillOnTheJob => "Positive_QuakeStanding",
            _ => null, // bir günde iki yanlış mühür: uygun manşet yok
        };
    }
}
