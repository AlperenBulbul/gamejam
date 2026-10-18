using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LoadBearing
{
    /// Tek bir müşteri: kapıdan gişeye yürür, dosyasını bırakır, karar bekler, tepki verir, çıkar.
    /// WalkIn -> Waiting (sabır azalır) -> Reacting -> WalkOut -> (yok olur)
    /// Sabır bitince (Gone) dosyasını alıp sinirli çıkar: +1 ceza (GDD bölüm 3).
    /// Replikler SpeechSystem ile altyazı + mırıltı sesi olarak çıkar.
    /// Karakter modeli varsa (Animator) şu durumlardan elinde olanları oynar (yoksa bir sonrakine düşer):
    ///  yürürken Walk > Idle · beklerken Idle / Impatient / Angry / Rage (sabır aşamasına göre), konuşurken Talking
    ///  onaylanınca Happy > Talking · reddedilince Sad > Angry
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
        [Tooltip("Gişede durduğu yer, normal noktanın kaç metre gerisi (karaktere göre)")]
        public float standBack = 0f;
        [Tooltip("Onay alınca sevinç animasyonu kaç saniye sürsün (0 = varsayılan)")]
        public float happySeconds = 0f;
        [Tooltip("Konuşma animasyonu altyazıdan sonra kaç saniye daha sürsün (uzun konuşma)")]
        public float talkHold = 0f;
        [Tooltip("Reddedilince kendi tepki animasyonu (Sad durumu) kaç saniye oynasın (0 = kısa sallanma)")]
        public float rejectSeconds = 0f;

        [Header("Animasyon (karakter modeli)")]
        [Tooltip("Boşsa çocuklarda aranır. Greybox gövdede yoktur.")]
        public Animator animator;
        public float animFade = 0.25f;

        string animState;
        float talkUntil;
        bool hasWalk, richAnim, hasReact;
        bool tutorial;          // 1. günün ilk müşterisi: kaçmaz
        float nextGrumble;
        const float TutorialFloor = 0.2f; // Sinirli aşaması (0.15 üstü), patlama / gitme yok

        bool IsTutorialCustomer()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Run == null || gm.Run.day != 1 || gm.Days == null || gm.Days.Length == 0) return false;
            var first = gm.Days[0] != null && gm.Days[0].customers != null && gm.Days[0].customers.Length > 0 ? gm.Days[0].customers[0] : null;
            return first == Data;
        }

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
            if (standBack > 0f)
            {
                // Oyuncudan uzağa doğru biraz geride dur
                var cam = Camera.main;
                var away = cam != null ? Flat(spot - cam.transform.position).normalized : Flat(door - spot).normalized;
                spotPos = spot + away * standBack;
            }
            walkTime = Mathf.Max(0.2f, walkSeconds);
            transform.position = door;
            mpb = new MaterialPropertyBlock();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            hasWalk = animator != null && animator.HasState(0, Animator.StringToHash("Walk"));
            richAnim = animator != null && animator.HasState(0, Animator.StringToHash("Impatient"));
            hasReact = animator != null && animator.HasState(0, Animator.StringToHash("Happy"));
            if (glass == null)
            {
                var g = GameObject.Find("Counter/Glass");
                if (g != null) { glass = g.transform; glassHome = glass.localPosition; }
            }
            StartCoroutine(Run());
        }

        void Update() => UpdateAnimation();

        /// Hangi animasyon oynasın: yürürken duruş, konuşurken el kol, sinirliyken parmak sallama
        void UpdateAnimation()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            bool talking = Time.time < talkUntil;
            if (walking && Current != Phase.WalkIn && Current != Phase.WalkOut) { Play("Walk", "Idle"); return; } // banka gidip gelirken
            switch (Current)
            {
                case Phase.WalkIn:
                case Phase.WalkOut:
                    if (walking) Play("Walk", "Idle"); else Play("Idle");
                    break;
                case Phase.Waiting:
                    switch (Stage)
                    {
                        case PatienceStage.Calm: if (talking) Play("Talking", "Idle"); else Play("Idle"); break;
                        case PatienceStage.Impatient: if (talking) Play("Talking", "Impatient", "Idle"); else Play("Impatient", "Idle"); break;
                        case PatienceStage.Angry: Play("Angry", "Impatient", "Idle"); break;
                        default: Play("Rage", "Angry", "Idle"); break;
                    }
                    break;
                case Phase.Reacting:
                    if (approved) Play("Happy", "Talking", "Idle"); else Play("Sad", "Angry", "Idle");
                    break;
                default:
                    Play("Idle");
                    break;
            }
        }

        bool walking;

        /// Elinde olan ilk animasyon durumuna yumuşak geçiş
        void Play(params string[] names)
        {
            foreach (var n in names)
            {
                if (n == animState) return;
                int hash = Animator.StringToHash(n);
                if (!animator.HasState(0, hash)) continue;
                animator.CrossFadeInFixedTime(hash, animState == null ? 0f : animFade, 0);
                animState = n;
                return;
            }
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
            tutorial = IsTutorialCustomer();
            nextGrumble = Time.time + 12f;
            Current = Phase.Waiting;
            Patience = 1f;
            SetStage(PatienceStage.Calm, speak: false);
            Say(Data.greeting);
            GameEvents.RaiseCustomerArrived(Data);

            while (!decided)
            {
                if (!FreezePatience && !GuidedTour.Running) Patience -= Time.deltaTime / Mathf.Max(1f, Data.patienceSeconds);
                if (tutorial)
                {
                    // İlk tur: sinirlenir ama gitmez (Sinirli aşamasında kalır, arada bir söylenir)
                    Patience = Mathf.Max(Patience, TutorialFloor);
                    if (Patience <= TutorialFloor + 0.001f && Time.time >= nextGrumble)
                    {
                        nextGrumble = Time.time + 12f;
                        var line = dialogue != null ? dialogue.RandomFor(PatienceStage.Angry) : null;
                        Say(line ?? "Hâlâ bekliyoruz evladım...");
                    }
                }
                var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
                var st = cfg != null ? cfg.StageFor(Patience) : (Patience > 0f ? Stage : PatienceStage.Gone);
                if (st != Stage) SetStage(st, speak: true);
                if (Stage == PatienceStage.Gone) { LeaveAngry(); break; }
                // Bekletildi (Sabırsız): arkadaki banka gidip oturur (Murat Demir)
                if (!seated && Data.sitsWhenKept && !tutorial && Stage >= PatienceStage.Impatient)
                {
                    seated = true;
                    sitRoutine = StartCoroutine(GoSit());
                }
                if (!seated) AnimateWaiting();
                yield return null;
            }
            ResetPose();

            // Banktaysa (ya da banka yürüyorsa) kalkar; karar verildiyse gişeye geri gelir, sinirliyse kapıya gider
            if (seated)
            {
                if (sitRoutine != null) { StopCoroutine(sitRoutine); sitRoutine = null; }
                walking = false;
                yield return StandUp();
                if (!angryExit)
                {
                    yield return Turn(Quaternion.LookRotation(Flat(spotPos - transform.position)), 0.25f);
                    yield return Walk(transform.position, spotPos, WalkSecondsTo(spotPos));
                    yield return Turn(Quaternion.LookRotation(FacePlayer()), 0.25f);
                }
                seated = false;
            }

            // Tepki (sinirli çıkışta tepki yok, direkt gider)
            if (!angryExit)
            {
                // Mühürden sonra kamera müşteriye dönünce başlasın (belgeye bakarken kaçmasın)
                for (float w = 0f; w < 3f; w += Time.deltaTime)
                {
                    var fcw = FocusController.Instance;
                    if (fcw == null || (fcw.State != FocusState.Folder && !fcw.IsTransitioning)) break;
                    yield return null;
                }
                Current = Phase.Reacting;
                Say(approved ? Data.approveLine : Data.rejectLine);
                yield return approved ? Hop() : Shake();
            }

            // Dosyayı al, çık
            Current = Phase.WalkOut;
            GameEvents.RaiseCustomerLeaving(Data);
            // Sinirli çıkış: reddedildi, beklemekten gitti ya da öfkelenene kadar bekletildi
            bool angryOut = angryExit || !approved || Stage >= PatienceStage.Rage;
            var exitClip = ExitClip(angryOut);
            if (exitClip != null)
                yield return ClipExit(exitClip, angryOut);
            else
            {
                yield return Turn(Quaternion.LookRotation(Flat(doorPos - transform.position)), 0.2f);
                if (angryExit) walkTime *= 0.75f; // sinirli: hızlı yürür
                yield return Walk(transform.position, doorPos);
            }

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

            if (richAnim)
            {
                // Karakterin kendi bekleme animasyonları var: sadece cama atılma hareketi kalsın
                rot = Vector3.zero;
                pos = new Vector3(0f, 0f, Stage == PatienceStage.Rage ? pos.z : 0f);
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
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            float cps = cfg != null ? Mathf.Max(5f, cfg.speechCps) : 30f;
            talkUntil = Time.time + line.Length / cps + 0.4f + talkHold; // altyazı yazıldığı sürece (+ karakterin ek süresi) konuşur
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

        IEnumerator Walk(Vector3 from, Vector3 to, float seconds = -1f)
        {
            float dur = seconds > 0f ? seconds : walkTime;
            var dir = Flat(to - from);
            transform.rotation = Quaternion.LookRotation(dir);
            float steps = Mathf.Max(2f, Vector3.Distance(from, to) / 0.35f);
            walking = true;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                // Yürüme animasyonu varsa sabit hızla yürü (ayak kaymasın), yoksa yumuşak başla-dur + zıplama
                float k = hasWalk ? t / dur : Mathf.SmoothStep(0f, 1f, t / dur);
                transform.position = Vector3.Lerp(from, to, k);
                if (visual != null && !hasWalk) visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(k * steps * Mathf.PI)) * 0.035f, 0f);
                yield return null;
            }
            walking = false;
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
            if (hasReact) { yield return new WaitForSeconds(happySeconds > 0f ? happySeconds : Mathf.Max(reactTime, 2.2f)); yield break; } // sevinme animasyonu var
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
            if (hasReact) { yield return new WaitForSeconds(Mathf.Max(reactTime, rejectSeconds, 2.2f)); yield break; } // üzülme animasyonu var
            if (rejectSeconds > 0f && animator != null && animator.HasState(0, Animator.StringToHash("Sad")))
            { yield return new WaitForSeconds(rejectSeconds); yield break; }                          // reddedilince kendi tepkisi (tartışma / kızma)
            for (float t = 0f; t < reactTime; t += Time.deltaTime)
            {
                float k = t / reactTime;
                if (visual != null) visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(k * 4f * Mathf.PI) * 14f * (1f - k), 0f);
                yield return null;
            }
            if (visual != null) visual.localRotation = Quaternion.identity;
        }

        // ------------------------------------------------------------------ bankta oturma (bekletilince, CustomerData.sitsWhenKept)
        // Oturma animasyonu yok: insan iskeletinin bacakları / kolları LateUpdate'te oturuş pozuna bükülür,
        // gövde bankın oturak yüksekliğine indirilir. Üst gövde kendi bekleme animasyonunu oynamaya devam eder.

        const string SitLine = "Ben şuraya oturayım, işim bitince seslenin.";
        const float SeatRatio = 0.47f / 0.9f;   // SM_Cust_Bench: oturak 0,47 m, toplam yükseklik 0,9 m
        const float HipAboveSeat = 0.10f;      // kalça ekleminin oturak üstündeki yüksekliği

        bool seated;            // bankta ya da banka gidiyor
        float sitW;             // oturma pozunun ağırlığı (0 ayakta, 1 oturuyor)
        float seatY;
        Coroutine sitRoutine;

        float WalkSecondsTo(Vector3 target) => Mathf.Clamp(Vector3.Distance(Flat(transform.position), Flat(target)) / 0.8f, 0.6f, 5f);

        IEnumerator GoSit()
        {
            if (visual != null) { visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; }
            Say(SitLine);
            FindSeat(out var seatPos, out var face, out seatY);
            yield return new WaitForSeconds(0.8f); // söyleyip öyle yürüsün
            yield return Turn(Quaternion.LookRotation(Flat(seatPos - transform.position)), 0.3f);
            yield return Walk(transform.position, seatPos, WalkSecondsTo(seatPos));
            yield return Turn(Quaternion.LookRotation(face), 0.35f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { sitW = Mathf.SmoothStep(0f, 1f, t / 0.6f); yield return null; }
            sitW = 1f;
            sitRoutine = null;
        }

        IEnumerator StandUp()
        {
            float start = sitW;
            if (start > 0f)
                for (float t = 0f; t < 0.45f; t += Time.deltaTime) { sitW = start * (1f - Mathf.SmoothStep(0f, 1f, t / 0.45f)); yield return null; }
            sitW = 0f;
            if (visual != null) visual.localPosition = Vector3.zero;
        }

        /// Sahnedeki müşteri bankı (SM_Cust_Bench): gişeye yakın ucuna, yüzü oyuncuya dönük oturur
        void FindSeat(out Vector3 pos, out Vector3 face, out float seatTop)
        {
            Renderer bench = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.name.StartsWith("SM_Cust_Bench")) { bench = r; break; }

            Vector3 center; float floor, height, halfLen; Vector3 along;
            if (bench != null)
            {
                var b = bench.bounds;
                center = b.center; floor = b.min.y; height = b.size.y;
                bool alongX = b.extents.x >= b.extents.z;
                along = alongX ? Vector3.right : Vector3.forward;
                halfLen = alongX ? b.extents.x : b.extents.z;
            }
            else
            {
                Debug.LogWarning("[Müşteri] SM_Cust_Bench bulunamadı, varsayılan bank konumu kullanılıyor");
                center = new Vector3(-1.25f, 0.45f, 3.21f); floor = 0f; height = 0.9f;
                along = Vector3.right; halfLen = 0.65f;
            }
            seatTop = floor + height * SeatRatio;

            // Yüz: bankın uzun kenarına dik, oyuncuya doğru (sırtı arkalığa)
            var perp = Vector3.Cross(along, Vector3.up).normalized;
            var cam = Camera.main;
            Vector3 toCam = cam != null ? cam.transform.position - center : spotPos - center;
            face = Vector3.Dot(toCam, perp) >= 0f ? perp : -perp;

            // Gişeye yakın uca otur, kalça oturağın arka yarısında
            float side = Vector3.Dot(spotPos - center, along) >= 0f ? 1f : -1f;
            pos = center + along * (side * halfLen * 0.45f) - face * 0.06f;
            pos.y = transform.position.y;
        }

        void LateUpdate()
        {
            if (sitW <= 0.001f || animator == null || !animator.isHuman) return;
            ApplySitPose(sitW);
        }

        void ApplySitPose(float w)
        {
            var fwd = Flat(transform.forward).normalized;
            var right = Vector3.Cross(Vector3.up, fwd).normalized;
            var down = Vector3.down;

            // Bacaklar: uyluk öne (dizler hafif açık), baldır aşağı
            Aim(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, fwd - right * 0.12f + down * 0.05f, w);
            Aim(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, down + fwd * 0.12f, w);
            Aim(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, fwd + right * 0.12f + down * 0.05f, w);
            Aim(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, down + fwd * 0.12f, w);

            // Kollar: omuzdan aşağı, eller dizlerin üstünde
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, down + fwd * 0.45f - right * 0.08f, w * 0.9f);
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, fwd + down * 0.55f + right * 0.15f, w * 0.9f);
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, down + fwd * 0.45f + right * 0.08f, w * 0.9f);
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, fwd + down * 0.55f - right * 0.15f, w * 0.9f);

            // Gövdeyi oturağa indir (kalça oturak + HipAboveSeat yüksekliğinde)
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null && visual != null)
            {
                float offsetNow = visual.position.y - transform.position.y;
                float hipStanding = hips.position.y - offsetNow;
                visual.localPosition = new Vector3(0f, (seatY + HipAboveSeat - hipStanding) * w, 0f);
            }
        }

        /// Kemiği, çocuğuna giden yön hedef yöne bakacak şekilde (dünya uzayında) döndürür
        void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 dir, float w)
        {
            var t = animator.GetBoneTransform(bone);
            var c = animator.GetBoneTransform(child);
            if (t == null || c == null) return;
            var cur = c.position - t.position;
            if (cur.sqrMagnitude < 1e-6f || dir.sqrMagnitude < 1e-6f) return;
            var full = Quaternion.FromToRotation(cur, dir);
            t.rotation = Quaternion.Slerp(Quaternion.identity, full, w) * t.rotation;
        }

        // ------------------------------------------------------------------ çıkış klibi (Selin: selam verip / söylenerek döner, yürür)

        PlayableGraph exitGraph;

        /// Karakterin kendi çıkış klibi (Resources/Characters/<Ad>/A_Customer_<Ad>_WalkOut[Angry]). Şimdilik Selin.
        AnimationClip ExitClip(bool angry)
        {
            if (animator == null || !animator.isHuman) return null;
            string who = name.Contains("Selin") || (animator.avatar != null && animator.avatar.name.Contains("Selin")) ? "Selin" : null;
            if (who == null) return null;
            string path = $"Characters/{who}/A_Customer_{who}_{(angry ? "WalkOutAngry" : "WalkOut")}";
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        /// Klip karakteri kendisi 180° döndürür (kök hareketi açık); dönüş bitince kod kapıya doğru yürütür.
        /// Normal: dönüş 1,0 sn, 1,2 m/sn · Sinirli: dönüş 0,6 sn, 1,55 m/sn (tasarımcının notu)
        IEnumerator ClipExit(AnimationClip clip, bool angry)
        {
            float turnEnd = angry ? 0.6f : 1.0f;
            float speed = angry ? 1.55f : 1.2f;
            var model = animator.transform;
            bool oldRootMotion = animator.applyRootMotion;
            animator.applyRootMotion = true;
            AnimationPlayableUtilities.PlayClip(animator, clip, out exitGraph);
            walking = false;

            var target = new Vector3(doorPos.x, transform.position.y, doorPos.z);
            float t = 0f, guard = 0f;
            while (true)
            {
                float dt = Time.deltaTime;
                t += dt;
                if (t >= turnEnd)
                {
                    walking = true;
                    // Modelin baktığı yön kapıya dönsün (kök hafifçe çevrilir), sonra o yöne yürü
                    Vector3 fwd = Flat(model.forward), toDoor = Flat(target - transform.position);
                    float yaw = Vector3.SignedAngle(fwd, toDoor, Vector3.up);
                    transform.rotation = Quaternion.Euler(0f, yaw * (1f - Mathf.Exp(-6f * dt)), 0f) * transform.rotation;
                    transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
                    guard += dt;
                    if ((transform.position - target).sqrMagnitude < 0.0004f || guard > 12f) break;
                }
                // Klip bitti: denetleyicinin yürüme döngüsüne geç (Walk), dönüş modelde kalır
                if (exitGraph.IsValid() && t >= clip.length)
                {
                    exitGraph.Destroy();
                    animator.applyRootMotion = oldRootMotion;
                }
                yield return null;
            }
            if (exitGraph.IsValid()) exitGraph.Destroy();
            animator.applyRootMotion = oldRootMotion;
            walking = false;
        }

        void OnDestroy()
        {
            if (exitGraph.IsValid()) exitGraph.Destroy();
            if (glass != null) glass.localPosition = glassHome;
        }
    }
}
