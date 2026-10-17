using System;

namespace LoadBearing
{
    /// Bir başvurunun kurallara uygun olup olmadığı (GDD bölüm 3 / Geliştirme 6.3).
    public static class Validator
    {
        public static bool IsValid(CustomerData c, int day) => IsValid(c, day, GameConfig.Current);

        public static bool IsValid(CustomerData c, int day, GameConfig cfg) => Explain(c, day, cfg) == null;

        /// Geçerliyse null, değilse ret nedeni (debug ve test için).
        public static string Explain(CustomerData c, int day, GameConfig cfg)
        {
            if (c.onFaultLine) return "Fay hattı";
            if (c.inFloodZone) return "Dere yatağı (taşkın alanı)";

            int limit = cfg.FloorLimitFor(c.areaM2);
            if (c.floors > limit) return $"Fazla kat ({c.areaM2} m² için sınır {limit}, istenen {c.floors})";

            if (day >= 2)
            {
                // Mühendis kaşesi (İMO sicil) kuralı oyundan çıkarıldı
                if (c.floors >= 5 && c.soil != SoilType.Solid) return "5+ kat için sağlam zemin etüdü yok";
            }
            return null;
        }
    }
}
