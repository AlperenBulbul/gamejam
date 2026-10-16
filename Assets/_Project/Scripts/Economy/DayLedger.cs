using System;

namespace LoadBearing
{
    /// Gün içi gelir/gider sayaçları. Para gün içinde değişmez; gün sonunda EconomyManager hesaplar.
    [Serializable]
    public class DayLedger
    {
        public int fees;
        public int bribe;
        public int sitesWorked;
        public int stepsCompleted;
        public int fixes;

        public void Reset()
        {
            fees = 0; bribe = 0; sitesWorked = 0; stepsCompleted = 0; fixes = 0;
        }
    }
}
