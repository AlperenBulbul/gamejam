using UnityEngine;

namespace LoadBearing
{
    /// Ana menü > Ayarlar değerleri (0–100). PlayerPrefs'te saklanır, oyun açılınca uygulanır.
    ///  Genel ses -> AudioListener.volume · Efektler -> konuşma mırıltısı + telsiz · Fare hızı -> masa bakışı
    ///  Müzik: henüz müzik yok, değer saklanır (MUS_* mixer grubu gelince bağlanacak)
    public static class GameSettings
    {
        const string KMaster = "set_master", KMusic = "set_music", KSfx = "set_sfx", KMouse = "set_mouse", KFull = "set_fullscreen";

        public static int Master { get; private set; } = 80;
        public static int Music { get; private set; } = 60;
        public static int Sfx { get; private set; } = 90;
        public static int Mouse { get; private set; } = 50;
        public static bool Fullscreen { get; private set; } = true;

        public static float Music01 => Music / 100f;
        public static float Sfx01 => Sfx / 100f;
        /// Fare hızı çarpanı: 50 = 1x, 0 = 0,2x, 100 = 2x
        public static float MouseScale => Mathf.Max(0.2f, Mouse / 50f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Master = PlayerPrefs.GetInt(KMaster, 80);
            Music = PlayerPrefs.GetInt(KMusic, 60);
            Sfx = PlayerPrefs.GetInt(KSfx, 90);
            Mouse = PlayerPrefs.GetInt(KMouse, 50);
            Fullscreen = PlayerPrefs.GetInt(KFull, 1) == 1;
            AudioListener.volume = Master / 100f;
#if !UNITY_EDITOR
            Screen.fullScreen = Fullscreen;
#endif
        }

        public static void SetMaster(int v) { Master = Mathf.Clamp(v, 0, 100); PlayerPrefs.SetInt(KMaster, Master); AudioListener.volume = Master / 100f; }
        public static void SetMusic(int v) { Music = Mathf.Clamp(v, 0, 100); PlayerPrefs.SetInt(KMusic, Music); }
        public static void SetSfx(int v) { Sfx = Mathf.Clamp(v, 0, 100); PlayerPrefs.SetInt(KSfx, Sfx); }
        public static void SetMouse(int v) { Mouse = Mathf.Clamp(v, 0, 100); PlayerPrefs.SetInt(KMouse, Mouse); }

        public static void SetFullscreen(bool on)
        {
            Fullscreen = on;
            PlayerPrefs.SetInt(KFull, on ? 1 : 0);
#if !UNITY_EDITOR
            Screen.fullScreen = on;
#endif
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
