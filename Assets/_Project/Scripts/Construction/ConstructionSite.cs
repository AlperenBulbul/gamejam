using System;
using System.Collections.Generic;

namespace LoadBearing
{
    /// Bir şantiyenin verisi. Mantığı (Update, hata başlatma) ConstructionManager'da yazılacak (D-11).
    [Serializable]
    public class ConstructionSite
    {
        public char letter;                 // A, B, C ...
        public CustomerData customer;
        public int totalSteps;              // floors + 1 (temel dahil)
        public int step;                    // 0 = temel
        public float timer;
        public float duration;
        public List<float> pendingFaultTimes = new List<float>();
        public List<int> activeFaults = new List<int>();   // hatalı işçilerin indeksleri
        public List<int> activeFaultTypes = new List<int>(); // F1..F5 (1-5), activeFaults ile aynı sırada
        public int defects;
        public SiteState state = SiteState.Building;
        public bool paused;
        public bool wasActiveToday;

        public ConstructionSite(char letter, CustomerData customer)
        {
            this.letter = letter;
            this.customer = customer;
            totalSteps = customer.floors + 1;
        }

        /// Monitör üst şeridi için: kaçıncı kat yapılıyor (temel = 0)
        public int CurrentFloor => step;
        public int Floors => customer.floors;
    }
}
