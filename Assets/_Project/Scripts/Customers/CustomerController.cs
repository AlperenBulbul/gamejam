using System.Collections;
using UnityEngine;

namespace LoadBearing
{
    /// Tek bir müşteri: kapıdan gişeye yürür, dosyasını bırakır, karar bekler, tepki verir, çıkar.
    /// WalkIn -> Waiting (sabır azalır) -> Reacting -> WalkOut -> (yok olur)
    /// Sabır bitince (Gone) dosyasını alıp sinirli çıkar: +1 ceza (GDD bölüm 3).
    /// Replikler SpeechSystem ile altyazı + mırıltı sesi olarak çıkar.
    public class CustomerController : MonoBehaviour
    {
        public enum Phase { WalkIn, Waiting, Reacting, WalkOut, Gone }

        /// F6 debug: tüm müşterilerin sabrını dondur
        public static bool FreezePatience;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { FreezePatience = false; glass = null; }

        public CustomerData Data { get; private set; }
        public Phase Current { get; private set; } = Phase.WalkIn;
        public float Patience { get; private set; } = 1f;
        public PatienceStage Stage { get; private set; } = PatienceStage.Calm;

        [Header("Görsel")]
        [Tooltip("Gövdenin görsel kökü (zıplama / sallanma bu objeye uygulanır)")]
        public Transform visual;
        [Tooltip("Sinirlendikçe kızaran gövde (greybox). Model gelince boş kalabilir.")]
        public Renderer tintRenderer;
        public Color baseColor = Color.gray;
        public Color rageColor = new Color(0.85f, 0.15f, 0.12f);
        public DialogueSet dialogue;
        public float reactTime = 1.3f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Transform glass;
        static Vector3 glassHome;

        Vector3 doorPos, spotPos;
        float walkTime;
        bool decided, approved, angryExit;
        float stageTime, nextBang;
        MaterialPropertyBlock mpb;

        public void Begin(CustomerData data, Vector3 door, Vector3 spot, float walkSeconds)
        {
            Data = data;
            doorPos = door;
            spotPos = spot;
            walkTime = Mathf.Max(0.2f, walkSeconds);
            transform.position = door;
            mpb = new MaterialPropertyBlock();
            if (glass == null)
            {
                var g = GameObject.Find("Counter/Glass");
                if (g != null) { glass = g.transform; glassHome = glass.localPosition; }
            }
            StartCoroutine(Run());
        }

        void OnEnable() => GameEvents.Stamped += OnStamped;
        void OnDisable() => GameEvents.Stamped -= OnStamped;

        void OnStamped(CustomerData c, bool ok)
        {
            if (c != Data || Current != Phase.Waiting) return;
            decided = true;
            approved = ok;
        }

        IEnumerator Run()
        {
            // Gişeye yürü
            Current = Phase.WalkIn;
            yield return Walk(doorPos, spotPos);
            yield return Turn(Quaternion.LookRotation(FacePlayer()), 0.25f); // masaya (oyuncuya) dön

            // Dosyayı bırak, sabır işlemeye başlar
            Current = Phase.Waiting;
            Patience = 1f;
            SetStage(PatienceStage.Calm, speak: false);
            Say(Data.greeting);
            GameEvents.RaiseCustomerArrived(Data);

            while (!decided)
            {
                if (!FreezePatience) Patience -= Time.deltaTime / Mathf.Max(1f, Data.patienceSeconds);
                var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
                var st = cfg != null ? cfg.StageFor(Patience) : (Patience > 0f ? Stage : PatienceStage.Gone);
                if (st != Stage) SetStage(st, speak: true);
                if (Stage == PatienceStage.Gone) { LeaveAngry(); break; }
                AnimateWaiting();
                yield return null;
            }
            ResetPose();

            // Tepki (sinirli çıkışta tepki yok, direkt gider)
            if (!angryExit)
            {
                Current = Phase.Reacting;
                Say(approved ? Data.approveLine : Data.rejectLine);
                yield return approved ? Hop() : Shake();
            }

            // Dosyayı al, çık
            Current = Phase.WalkOut;
            GameEvents.RaiseCustomerLeaving(Data);
            yield return Turn(Quaternion.LookRotation(Flat(doorPos - transform.position)), 0.2f);
            if (angryExit) walkTime *= 0.75f; // sinirli: hızlı yürür
            yield return Walk(transform.position, doorPos);

            Current = Phase.Gone;
            GameEvents.RaiseCustomerLeft(Data, angryExit);
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ sabır

        void SetStage(PatienceStage s, bool speak)
        {
            Stage = s;
            stageTime = 0f;
            nextBang = 0.6f;
            GameEvents.RaisePatienceStageChanged(s);
            if (!speak || s == PatienceStage.Gone) return;
            var line = dialogue != null ? dialogue.RandomFor(s) : null;
            Say(line ?? "Hrrm...");
        }

        void LeaveAngry()
        {
            angryExit = true;
            decided = true;
            var leave = dialogue != null ? dialogue.RandomFor(PatienceStage.Gone) : null;
            Say(leave ?? "Başka kapı da var!");

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.Run.AddPenalty("CustomerLeft", gm.Config);
                if (!gm.Run.todayHeadlineKeys.Contains("CustomerLeft")) gm.Run.todayHeadlineKeys.Add("CustomerLeft");
                Debug.Log($"[Ceza] {Data.displayName} beklemekten sıkılıp gitti. Ceza: {gm.Run.penalties}");
            }
        }

        void AnimateWaiting()
        {
            if (visual == null) return;
            stageTime += Time.deltaTime;
            float t = stageTime;
            Vector3 pos = Vector3.zero;
            Vector3 rot = Vector3.zero;
            float red = 0f;

            switch (Stage)
            {
                case PatienceStage.Calm:          // hafifçe sallanır
                    rot.y = Mathf.Sin(t * 1.2f) * 4f;
                    break;
                case PatienceStage.Impatient:     // ayağını vurur
                    pos.y = Mathf.Max(0f, Mathf.Sin(t * 14f)) * 0.012f;
                    rot.y = Mathf.Sin(t * 0.8f) * 12f;
                    red = 0.15f;
                    break;
                case PatienceStage.Angry:         // geriye yaslanır, kızarır
                    rot.x = -6f;
                    pos.y = Mathf.Max(0f, Mathf.Sin(t * 6f)) * 0.01f;
                    red = 0.5f;
                    break;
                case PatienceStage.Rage:          // cama vurur
                    red = 0.9f;
                    rot.x = 4f;
                    nextBang -= Time.deltaTime;
                    float lunge = 0f;
                    if (nextBang < 0.18f) lunge = Mathf.Sin(Mathf.Clamp01(1f - nextBang / 0.18f) * Mathf.PI);
                    pos.z = lunge * 0.12f;
                    if (nextBang <= 0f) { nextBang = 1.4f; StartCoroutine(ShakeGlass()); }
                    break;
            }

            visual.localPosition = pos;
            visual.localRotation = Quaternion.Euler(rot);
            Tint(Color.Lerp(baseColor, rageColor, red));
        }

        IEnumerator ShakeGlass()
        {
            if (glass == null) yield break;
            for (float e = 0f; e < 0.18f; e += Time.deltaTime)
            {
                glass.localPosition = glassHome + Random.insideUnitSphere * 0.006f * (1f - e / 0.18f);
                yield return null;
            }
            glass.localPosition = glassHome;
        }

        void Tint(Color c)
        {
            if (tintRenderer == null || mpb == null) return;
            tintRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            tintRenderer.SetPropertyBlock(mpb);
        }

        void ResetPose()
        {
            if (visual != null) { visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; }
            if (glass != null) glass.localPosition = glassHome;
        }

        void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (SpeechSystem.Instance != null) SpeechSystem.Instance.Say(Data, line, Stage); // altyazı + mırıltı
            Debug.Log($"[Müşteri] {Data.displayName} ({Stage}): {line}");
        }

        // ------------------------------------------------------------------ hareket

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v; }

        Vector3 FacePlayer()
        {
            var cam = Camera.main;
            return cam != null ? Flat(cam.transform.position - transform.position) : Vector3.back;
        }

        IEnumerator Walk(Vector3 from, Vector3 to)
        {
            var dir = Flat(to - from);
            transform.rotation = Quaternion.LookRotation(dir);
            float steps = Mathf.Max(2f, Vector3.Distance(from, to) / 0.35f);
            for (float t = 0f; t < walkTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / walkTime);
                transform.position = Vector3.Lerp(from, to, k);
                if (visual != null) visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(k * steps * Mathf.PI)) * 0.035f, 0f);
                yield return null;
            }
            transform.position = to;
            if (visual != null) visual.localPosition = Vector3.zero;
        }

        IEnumerator Turn(Quaternion target, float time)
        {
            var start = transform.rotation;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.Slerp(start, target, t / time);
                yield return null;
            }
            transform.rotation = target;
        }

        IEnumerator Hop()
        {
            for (float t = 0f; t < reactTime; t += Time.deltaTime)
            {
                float k = t / reactTime;
                if (visual != null) visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(k * 3f * Mathf.PI)) * 0.08f * (1f - k), 0f);
                yield return null;
            }
            if (visual != null) visual.localPosition = Vector3.zero;
        }

        IEnumerator Shake()
        {
            for (float t = 0f; t < reactTime; t += Time.deltaTime)
            {
                float k = t / reactTime;
                if (visual != null) visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(k * 4f * Mathf.PI) * 14f * (1f - k), 0f);
                yield return null;
            }
            if (visual != null) visual.localRotation = Quaternion.identity;
        }

        void OnDestroy()
        {
            if (glass != null) glass.localPosition = glassHome;
        }
    }
}
