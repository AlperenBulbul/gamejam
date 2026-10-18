using System.Collections;
using UnityEngine;

namespace LoadBearing
{
    /// Rüşvet zarfı (GDD bölüm 3). Bu bileşen her zaman aktif olan FolderSpot'ta durur; zarf objesi dosyanın içindedir.
    ///  - Rüşvetli müşteri (CustomerData.bribe > 0) önce dosyayı verir; 2-3 sn sonra (selamı bitince) zarfı
    ///    dosyanın üstüne bırakır ve bir replik söyler (CustomerData.bribeLine).
    ///  - Zarfa tıklamak onu alır: tutar gün sonu fişine Rüşvet satırı olarak eklenir.
    ///  - Alıp reddetmek +1 ceza, alıp onaylamak kayda geçer (StampController). Almadan reddedersen müşteri geri alır.
    public class BribeEnvelope : MonoBehaviour
    {
        public GameObject envelope;          // dosya içindeki zarf objesi
        public Interactable envelopeClick;   // zarfın Interactable'ı
        public float takeTime = 0.35f;
        [Tooltip("Dosyadan kaç sn sonra zarf uzatılır (selam daha uzunsa o bitince)")]
        public float offerDelay = 2.5f;
        public float offerDelayMax = 3.5f;
        const string DefaultBribeLine = "Bu da... ufak bir teşekkür. Aramızda kalsın.";

        CustomerData current;
        Vector3 homeLocalPos;
        Quaternion homeLocalRot;
        bool taking, offering;
        CustomerQueue queue;

        void Awake()
        {
            if (envelope != null)
            {
                homeLocalPos = envelope.transform.localPosition;
                homeLocalRot = envelope.transform.localRotation;
                envelope.SetActive(false);
            }
            if (envelopeClick != null)
            {
                envelopeClick.Clicked += _ => Take();
                envelopeClick.prompt = "Rüşvet al"; // sadece imleç zarfın üstündeyken: sol tık simgesi + yazı (HoverPromptUI)
            }
            GrowCollider();
        }

        /// Zarfın tıklama kutusu çok ince (kâğıt kalınlığı): üstündeki form / dosya önce vuruluyordu,
        /// imleç zarfa gelince ipucu çıkmıyordu. Kutuyu yukarı doğru biraz kalınlaştır, kenarlardan hafif büyüt.
        void GrowCollider()
        {
            var col = envelopeClick != null ? envelopeClick.GetComponent<BoxCollider>() : null;
            if (col == null && envelope != null) col = envelope.GetComponent<BoxCollider>();
            if (col == null) return;
            var ls = col.transform.lossyScale;
            float minH = 0.025f / Mathf.Max(1e-4f, Mathf.Abs(ls.y));
            var size = col.size;
            var center = col.center;
            if (size.y < minH)
            {
                float bottom = center.y - size.y * 0.5f;
                size.y = minH;
                center.y = bottom + minH * 0.5f;
            }
            size.x *= 1.08f;
            size.z *= 1.08f;
            col.size = size;
            col.center = center;
        }

        void OnEnable()
        {
            GameEvents.CustomerArrived += OnArrived;
            GameEvents.CustomerLeft += OnLeft;
        }

        void OnDisable()
        {
            GameEvents.CustomerArrived -= OnArrived;
            GameEvents.CustomerLeft -= OnLeft;
        }

        void OnArrived(CustomerData c)
        {
            current = c;
            var gm = GameManager.Instance;
            if (gm != null) gm.Run.bribeTakenCurrent = false; // her müşteri için sıfırdan
            StopAllCoroutines();
            taking = false;
            offering = false;
            if (envelope == null) return;
            envelope.transform.localPosition = homeLocalPos;
            envelope.transform.localRotation = homeLocalRot;
            envelope.SetActive(false);
            if (c != null && c.bribe > 0) StartCoroutine(OfferLater(c));
        }

        /// Önce dosya; selam bitince (2-3 sn) zarf dosyanın üstüne kayar, müşteri lafını eder
        IEnumerator OfferLater(CustomerData c)
        {
            offering = true;
            var cfg = GameManager.Instance != null ? GameManager.Instance.Config : null;
            float cps = cfg != null ? Mathf.Max(5f, cfg.speechCps) : 30f;
            float greet = string.IsNullOrEmpty(c.greeting) ? 0f : c.greeting.Length / cps + 0.8f;
            float wait = Mathf.Clamp(Mathf.Max(offerDelay, greet), offerDelay, offerDelayMax);
            yield return new WaitForSeconds(wait); // mola menüsünde (zaman durunca) bekler

            var folder = GetComponent<FolderView>();
            if (current != c || (folder != null && (folder.Decided || folder.Leaving))) { offering = false; yield break; }

            // Zarf hafifçe yukarıdan dosyanın üstüne düşer
            var t = envelope.transform;
            Vector3 from = homeLocalPos + new Vector3(0f, 0.05f, -0.05f);
            t.localPosition = from;
            t.localRotation = homeLocalRot;
            envelope.SetActive(true);
            Sfx.Play("SFX_Envelope_Drop");
            if (queue == null) queue = Object.FindAnyObjectByType<CustomerQueue>();
            var cc = queue != null ? queue.Current : null;
            if (cc != null && cc.Data == c) cc.SayLine(string.IsNullOrEmpty(c.bribeLine) ? DefaultBribeLine : c.bribeLine);

            const float drop = 0.25f;
            for (float e = 0f; e < drop; e += Time.deltaTime)
            {
                float k = e / drop;
                t.localPosition = Vector3.Lerp(from, homeLocalPos, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            t.localPosition = homeLocalPos;
            offering = false;
        }

        // Zarf ekranda görününce (dosya açılıp yerine oturunca) $ işaretini bir kez kur
        void Update()
        {
            if (dollar == null && envelope != null && envelope.activeInHierarchy && !taking && !offering) EnsureDollar();
        }

        // ------------------------------------------------------------------ zarfın üstünde $ işareti

        GameObject dollar;

        /// Zarfın üst yüzüne yatık, yeşil "$" (para olduğu ilk bakışta anlaşılsın)
        void EnsureDollar()
        {
            if (dollar != null || envelope == null) return;
            Renderer best = null;
            float area = 0f;
            foreach (var r in envelope.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                var sz = r.bounds.size;
                if (sz.x * sz.z > area) { area = sz.x * sz.z; best = r; }
            }
            if (best == null) return;
            var b = best.bounds;

            dollar = new GameObject("Dollar", typeof(TMPro.TextMeshPro));
            var tmp = dollar.GetComponent<TMPro.TextMeshPro>();
            var cam = Camera.main;
            Vector3 away = cam != null ? cam.transform.forward : Vector3.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = Vector3.forward;
            dollar.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + 0.0015f, b.center.z),
                                                    Quaternion.AngleAxis(-18f, Vector3.up) * Quaternion.LookRotation(Vector3.down, away.normalized)); // yukarıdan okunur, hafif çapraz
            dollar.transform.SetParent(envelope.transform, true);
            var ls = envelope.transform.lossyScale;
            dollar.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)), 1f / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));

            float side = Mathf.Min(b.size.x, b.size.z);
            tmp.rectTransform.sizeDelta = new Vector2(side * 0.9f, side * 0.9f);
            tmp.text = "$";
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f; tmp.fontSizeMax = 40f;
            tmp.color = new Color(0.12f, 0.45f, 0.22f);
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color32(240, 236, 220, 255);
            var heavy = UiFonts.Heavy;
            if (heavy != null) { tmp.font = heavy; tmp.fontStyle = TMPro.FontStyles.Normal; }
        }

        void OnLeft(CustomerData c, bool angry)
        {
            if (c != current) return;
            current = null;
            var gm = GameManager.Instance;
            if (gm != null) gm.Run.bribeTakenCurrent = false;
            StopAllCoroutines();
            taking = false;
            offering = false;
            if (envelope != null) envelope.SetActive(false);
        }

        void Take()
        {
            var gm = GameManager.Instance;
            var folder = GetComponent<FolderView>();
            if (taking || offering || current == null || current.bribe <= 0 || gm == null) return;
            if (folder != null && (folder.Leaving || folder.Decided)) return; // karar verildiyse / müşteri gidiyorsa alınamaz

            taking = true;
            var run = gm.Run;
            run.bribeTakenCurrent = true;
            run.anyBribe = true;
            run.ledger.bribe += current.bribe;
            GameEvents.RaiseBribeTaken(current.bribe);
            Debug.Log($"[Rüşvet] {current.displayName} zarfı alındı: {current.bribe} TL");
            StartCoroutine(TakeAnim());
        }

        /// Zarf kalkıp oyuncuya doğru (masanın altındaki çekmeceye) kayar ve kaybolur
        IEnumerator TakeAnim()
        {
            var t = envelope.transform;
            var cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 from = t.position;
            Vector3 to = cam != null ? new Vector3(cam.position.x + 0.15f, from.y - 0.05f, cam.position.z + 0.25f) : from + Vector3.back * 0.3f;
            Quaternion r0 = t.rotation, r1 = r0 * Quaternion.Euler(0f, 25f, 0f);
            for (float e = 0f; e < takeTime; e += Time.deltaTime)
            {
                float k = e / takeTime;
                t.position = Vector3.Lerp(from, to, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.05f;
                t.rotation = Quaternion.Slerp(r0, r1, k);
                yield return null;
            }
            envelope.SetActive(false);
            t.localPosition = homeLocalPos;
            t.localRotation = homeLocalRot;
        }
    }
}
