using UnityEngine;

namespace LoadBearing
{
    [CreateAssetMenu(menuName = "Load Bearing/Customer", fileName = "C0_Customer")]
    public class CustomerData : ScriptableObject
    {
        [Header("Kimlik")]
        public string id = "C0";
        public int day = 1;
        public string displayName;
        public string role;

        [Header("Başvuru formu")]
        public int ada;
        public int parsel;
        public int areaM2;
        public int floors;
        public string use = "Konut";
        public string licenseNo;
        public SoilType soil = SoilType.None;

        [Header("Harita (oyuncuya gösterilmez, kural kontrolü için)")]
        public bool inFloodZone;
        public bool onFaultLine;
        [Tooltip("T_Map_Karaova üzerinde arsa merkezi (piksel). Tasarım ekibinin koordinat tablosundan.")]
        public Vector2 mapPixel;

        [Header("Davranış")]
        public int bribe;
        public float patienceSeconds = 90f;
        [Range(0.5f, 1.6f)] public float voicePitch = 1f;
        public BodyType body = BodyType.Male;
        public Material variantMaterial;
        [Tooltip("Bu müşterinin karakter prefabı. Boşsa CustomerQueue cinsiyete göre varsayılanı kullanır.")]
        public GameObject characterPrefab;
        [Tooltip("Bekletilince (Sabırsız aşaması) arkadaki banka gidip oturur, karar verilince gişeye geri gelir")]
        public bool sitsWhenKept;

        [Header("Replikler")]
        [TextArea] public string greeting;
        [TextArea] public string approveLine;
        [TextArea] public string rejectLine;
        [Tooltip("Rüşvetçi: dosyayı verdikten 2-3 sn sonra zarfı uzatırken söylediği söz")]
        [TextArea] public string bribeLine;

        [Header("Sadece test")]
        [Tooltip("Validator sonucu bununla karşılaştırılır")]
        public bool expectedValid;

        public string AdaParsel => $"{ada}/{parsel}";
    }
}
