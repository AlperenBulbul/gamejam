using System.Collections;
using UnityEngine;

namespace LoadBearing
{
    /// Rüşvet zarfı (GDD bölüm 3). Bu bileşen her zaman aktif olan FolderSpot'ta durur; zarf objesi dosyanın içindedir.
    ///  - Rüşvetli müşteri (CustomerData.bribe > 0) gelince zarf dosyanın üstünde görünür.
    ///  - Zarfa tıklamak onu alır: tutar gün sonu fişine Rüşvet satırı olarak eklenir.
    ///  - Alıp reddetmek +1 ceza, alıp onaylamak kayda geçer (StampController). Almadan reddedersen müşteri geri alır.
    public class BribeEnvelope : MonoBehaviour
    {
        public GameObject envelope;          // dosya içindeki zarf objesi
        public Interactable envelopeClick;   // zarfın Interactable'ı
        public float takeTime = 0.35f;

        CustomerData current;
        Vector3 homeLocalPos;
        Quaternion homeLocalRot;
        bool taking;

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
                envelopeClick.prompt = "Rüşveti kabul et"; // imlecin yanında sol tık simgesiyle
            }
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
            if (envelope == null) return;
            envelope.transform.localPosition = homeLocalPos;
            envelope.transform.localRotation = homeLocalRot;
            envelope.SetActive(c != null && c.bribe > 0);
        }

        // Zarf ekranda görününce (dosya açılıp yerine oturunca) $ işaretini bir kez kur
        void Update()
        {
            if (dollar == null && envelope != null && envelope.activeInHierarchy && !taking) EnsureDollar();
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
                                                    Quaternion.LookRotation(Vector3.down, away.normalized)); // yukarıdan okunur
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
            if (envelope != null) envelope.SetActive(false);
        }

        void Take()
        {
            var gm = GameManager.Instance;
            var folder = GetComponent<FolderView>();
            if (taking || current == null || current.bribe <= 0 || gm == null) return;
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
