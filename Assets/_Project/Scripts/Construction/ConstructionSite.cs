using System;
using System.Collections.Generic;

namespace LoadBearing
{
    /// Bir şantiyenin verisi. Mantığı (ilerleme, hata başlatma, düzeltme) ConstructionManager'da.
    [Serializable]
    public class ConstructionSite
    {
        public char letter;                 // A, B, C ...
        public CustomerData customer;
        public int totalSteps;              // floors + 1 (temel dahil)
        public int step;                    // 0 = temel, 1 = 1. kat ...
        public int workers;                 // bu adımdaki işçi sayısı
        public float timer;                 // adımın kaçıncı saniyesi
        public float duration;              // adım süresi (yanlış tıklamayla uzar)
        public List<float> pendingFaultTimes = new List<float>();   // henüz başlamamış hataların zamanları
        public List<int> activeFaults = new List<int>();            // hatalı işçilerin indeksleri
        public List<int> activeFaultTypes = new List<int>();        // F1..F5 (1-5), activeFaults ile aynı sırada
        public int defects;                 // düzeltilmeden geçen hata sayısı (depremde 3+ = çöker)
        public SiteState state = SiteState.Building;
        public bool paused;
        public bool wasActiveToday;         // gün sonu işçi maliyeti için

        public ConstructionSite(char letter, CustomerData customer)
        {
            this.letter = letter;
            this.customer = customer;
            totalSteps = customer.floors + 1;
        }

        /// Monitör üst şeridi için: kaçıncı kat yapılıyor (temel = 0)
        public int CurrentFloor => step;
        public int Floors => customer.floors;
        public bool HasFaults => activeFaults.Count > 0;
        public bool IsBuilding => state == SiteState.Building;

        /// Bu işçi şu an hatalı mı; hatalıysa F1..F5 türü, değilse 0
        public int FaultTypeOf(int worker)
        {
            int i = activeFaults.IndexOf(worker);
            return i >= 0 ? activeFaultTypes[i] : 0;
        }

        /// "Temel" ya da "2. kat"
        public string StepLabel => state == SiteState.Done ? "Bitti" : (step == 0 ? "Temel" : $"{step}. kat");
    }
}
