using UnityEngine;

namespace LoadBearing
{
    /// Duvar saati: ibreler oyun saatini gösterir (mesai 09:00'da başlar, monitördeki saatle aynı: 1 sn = 6 sn).
    /// Saniye ibresi gerçek saniyeyle döner. İbrelerin pivotu saat merkezinde, 0 dönüşte 12'yi gösterir (tasarım notu).
    public class WallClock : MonoBehaviour
    {
        public Transform hourHand, minuteHand, secondHand;
        [Tooltip("Oyun saatinin gerçek zamana oranı (monitördeki saatle aynı)")]
        public float timeScale = 6f;

        Quaternion hourBase, minuteBase, secondBase;
        Vector3 axis = Vector3.back;
        float workStart;
        bool working;

        void OnEnable() => GameEvents.StateChanged += OnState;
        void OnDisable() => GameEvents.StateChanged -= OnState;

        void OnState(GameState s)
        {
            if (s == GameState.Working) { workStart = Time.time; working = true; }
            else if (s == GameState.DayIntro || s == GameState.MainMenu) working = false;
        }

        void Start()
        {
            // Yerel dönüş 0 = 12'yi gösterir (modelde 10:10'a çevrilmiş olabilir; ona değil sıfıra göre döndür)
            hourBase = BaseOf(hourHand);
            minuteBase = BaseOf(minuteHand);
            secondBase = BaseOf(secondHand);

            // Kadrana dik eksen: saat gövdesinin en ince ekseni, oyuncuya bakan yönü
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var s = Vector3.Scale(mf.sharedMesh.bounds.size, transform.lossyScale);
                Vector3 local = s.x <= s.y && s.x <= s.z ? Vector3.right : (s.y <= s.z ? Vector3.up : Vector3.forward);
                axis = transform.TransformDirection(local).normalized;
            }
            var cam = Camera.main;
            if (cam != null && Vector3.Dot(axis, cam.transform.position - transform.position) < 0f) axis = -axis;
        }

        static Quaternion BaseOf(Transform hand) =>
            hand == null ? Quaternion.identity : (hand.parent != null ? hand.parent.rotation : Quaternion.identity);

        void Update()
        {
            float secs = 9f * 3600f + (working ? (Time.time - workStart) * timeScale : 0f);
            float hours = secs / 3600f % 12f;
            float minutes = secs / 60f % 60f;
            // Bakan kişiye göre saat yönü: eksen oyuncuya doğru, pozitif açı saat yönünde
            if (hourHand != null) hourHand.rotation = Quaternion.AngleAxis(hours * 30f, axis) * hourBase;
            if (minuteHand != null) minuteHand.rotation = Quaternion.AngleAxis(minutes * 6f, axis) * minuteBase;
            if (secondHand != null) secondHand.rotation = Quaternion.AngleAxis(Mathf.Floor(Time.time % 60f) * 6f, axis) * secondBase;
        }
    }
}
