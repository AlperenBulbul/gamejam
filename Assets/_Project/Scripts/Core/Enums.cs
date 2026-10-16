namespace LoadBearing
{
    /// Oyun akışı (Geliştirme dokümanı bölüm 4)
    public enum GameState { MainMenu, DayIntro, Working, FinalShift, Receipt, Shop, DayEvent, Newspaper, Ending }

    /// Oyuncunun o an nereye baktığı
    public enum FocusState { Desk, Folder, Map, Binder, Monitor, HoldingStamp }

    /// Müşteri sabır aşamaları (GDD bölüm 3): Sakin, Sabırsız, Sinirli, Patlama, Gider
    public enum PatienceStage { Calm, Impatient, Angry, Rage, Gone }

    public enum SoilType { None, Solid, Soft }
    public enum BodyType { Male, Female }
    public enum DayEndEvent { Rain, Quake }
    public enum SiteState { Building, Done, Flooded, Collapsed }
    public enum StampType { Approve, Reject }
}
