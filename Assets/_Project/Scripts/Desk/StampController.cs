using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LoadBearing
{
    /// Mühür alma, imleçle taşıma, dosyaya basma ve KARAR mantığı (Geliştirme dokümanı 6.2).
    ///  - Mühüre tıkla  -> HoldingStamp, mühür imleci takip eder
    ///  - Dosyaya tıkla -> mühür iner, KARAR kutusuna basılır, karar RunState'e yazılır
    ///  - Boş yere tıkla / sağ tık -> mühür yerine döner
    /// DeskInteractor'dan SONRA çalışır (aynı karedeki tıklamayı doğru yorumlamak için).
    [DefaultExecutionOrder(50)]
    public class StampController : MonoBehaviour
    {
        public static StampController Instance { get; private set; }

        [Header("Bağlantılar")]
        public StampTool approveStamp;
        public StampTool rejectStamp;
        public FolderView folder;

        [Header("Hareket")]
        public float hoverHeight = 0.07f;
        public float followSmooth = 22f;
        public float pressTime = 0.07f;
        public float returnTime = 0.2f;

        StampTool held;
        bool animating;
        int pickFrame = -1;
        Camera cam;
        DeskInteractor interactor;
        Interactable folderInteractable;
        float deskY;

        void Awake() => Instance = this;

        void Start()
        {
            cam = Camera.main;
            interactor = cam != null ? cam.GetComponent<DeskInteractor>() : null;
            folderInteractable = folder != null ? folder.GetComponent<Interactable>() : null;
            deskY = approveStamp != null ? approveStamp.HomePos.y : 0.75f;

            Hook(approveStamp);
            Hook(rejectStamp);
        }

        void Hook(StampTool s)
        {
            if (s == null) return;
            var it = s.GetComponent<Interactable>();
            if (it != null) it.Clicked += _ => Pick(s);
        }

        void OnEnable()
        {
            GameEvents.FocusChanged += OnFocusChanged;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.FocusChanged -= OnFocusChanged;
            GameEvents.StateChanged -= OnStateChanged;
        }

        /// Gün başı / menü / son: mühürler her zaman masadaki yerlerine döner
        void OnStateChanged(GameState s)
        {
            if (s == GameState.DayIntro || s == GameState.MainMenu || s == GameState.Ending) ResetStamps();
        }

        void ResetStamps()
        {
            StopAllCoroutines();
            animating = false;
            held = null;
            foreach (var st in new[] { approveStamp, rejectStamp })
            {
                if (st == null) continue;
                st.transform.SetPositionAndRotation(st.HomePos, st.HomeRot);
                st.gameObject.SetActive(true);
                SetColliders(st, true);
            }
        }

        void OnFocusChanged(FocusState f)
        {
            // Sağ tık ya da başka bir sebeple mühür bırakıldıysa yerine götür
            if (f != FocusState.HoldingStamp && held != null && !animating) ReturnHeld();
        }

        void Pick(StampTool s)
        {
            var fc = FocusController.Instance;
            if (animating || fc == null || fc.State != FocusState.Desk || held != null) return;
            held = s;
            pickFrame = Time.frameCount;
            SetColliders(s, false); // eldeki mühür imlecin altındaki dosyayı örtmesin
            fc.SetHoldingStamp(true);
        }

        static void SetColliders(StampTool s, bool on)
        {
            foreach (var c in s.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        void Update()
        {
            if (held == null || animating) return;
            var fc = FocusController.Instance;
            if (fc == null || fc.State != FocusState.HoldingStamp) return;

            var mouse = Mouse.current;
            if (mouse == null || cam == null) return;

            // Mühür imlecin masadaki noktasının üstünde süzülür
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, deskY, 0f));
            if (plane.Raycast(ray, out float d))
            {
                var target = ray.GetPoint(d) + Vector3.up * hoverHeight;
                float k = 1f - Mathf.Exp(-followSmooth * Time.unscaledDeltaTime);
                held.transform.position = Vector3.Lerp(held.transform.position, target, k);
            }

            if (mouse.leftButton.wasPressedThisFrame && Time.frameCount != pickFrame)
            {
                bool onFolder = interactor != null && folderInteractable != null && interactor.Hovered == folderInteractable;
                if (onFolder && folder.Current != null && !folder.Decided) StartCoroutine(StampRoutine(held));
                else fc.SetHoldingStamp(false); // boş yere tıklandı: bırak
            }
        }

        IEnumerator StampRoutine(StampTool s)
        {
            animating = true;
            Vector3 contact = folder.StampAreaWorld;
            Vector3 above = contact + Vector3.up * 0.05f;

            yield return Move(s.transform, s.transform.position, above, pressTime);
            yield return Move(s.transform, above, contact, pressTime * 0.7f);

            Decide(s.type == StampType.Approve);          // tam değdiği an

            yield return new WaitForSecondsRealtime(0.06f);
            yield return Move(s.transform, contact, above, pressTime);

            animating = false;
            FocusController.Instance.SetHoldingStamp(false); // -> OnFocusChanged -> ReturnHeld
        }

        void ReturnHeld()
        {
            var s = held;
            held = null;
            if (s != null) StartCoroutine(ReturnRoutine(s));
        }

        IEnumerator ReturnRoutine(StampTool s)
        {
            yield return Move(s.transform, s.transform.position, s.HomePos, returnTime);
            SetColliders(s, true);
        }

        static IEnumerator Move(Transform t, Vector3 from, Vector3 to, float time)
        {
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / time));
                yield return null;
            }
            t.position = to;
        }

        // ------------------------------------------------------------------ KARAR

        void Decide(bool approved)
        {
            var gm = GameManager.Instance;
            var c = folder.Current;
            if (gm == null || c == null || folder.Decided || folder.Leaving) return; // müşteri o an gittiyse sayılmaz
            var run = gm.Run;
            var cfg = gm.Config;
            bool valid = Validator.IsValid(c, run.day, cfg);

            if (approved)
            {
                run.ledger.fees += c.floors * cfg.feePerFloor;
                if (run.bribeTakenCurrent) run.bribeOnApproved = c.id;
                if (valid) AddHeadline(run, "CorrectApprove");
            }
            else
            {
                if (valid) { run.AddWrongfulReject(cfg); AddHeadline(run, "WrongfulReject"); }
                else AddHeadline(run, "CorrectReject");
                if (run.bribeTakenCurrent) run.AddPenalty("BribeReject", cfg);
            }

            run.decisions.Add(new Decision { customerId = c.id, approved = approved, valid = valid });
            folder.MarkDecided(approved);

            string verdict = approved == valid ? "<color=lime>DOĞRU</color>" : "<color=red>YANLIŞ</color>";
            string why = Validator.Explain(c, run.day, cfg) ?? "kurallara uygun";
            Debug.Log($"[Karar] {c.id} {(approved ? "ONAY" : "RET")} → {verdict}  ({why})");

            GameEvents.RaiseStamped(c, approved);
        }

        static void AddHeadline(RunState run, string key)
        {
            if (!run.todayHeadlineKeys.Contains(key)) run.todayHeadlineKeys.Add(key);
        }

        /// F1: şu anki dosyaya doğru mührü animasyonsuz bas
        public void DebugAutoStamp()
        {
            var gm = GameManager.Instance;
            if (gm == null || folder == null || folder.Current == null || folder.Decided) return;
            Decide(Validator.IsValid(folder.Current, gm.Run.day, gm.Config));
        }
    }
}
