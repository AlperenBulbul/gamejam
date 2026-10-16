using System.Collections;
using UnityEngine;

namespace LoadBearing
{
    /// Günün müşterilerini sırayla gişeye getirir (Geliştirme dokümanı 6.4).
    /// Working başlayınca ilk müşteri gelir; her müşteri çıktıktan nextCustomerDelay sn sonra sıradaki gelir.
    /// Son müşteri çıkınca GameManager.OnLastCustomerLeft(). Kovulma bekliyorsa müşteri çıkınca oyun biter.
    public class CustomerQueue : MonoBehaviour
    {
        public static CustomerQueue Instance { get; private set; }

        [Header("Sahne noktaları")]
        public Transform doorPoint;
        public Transform customerSpot;

        [Header("Görsel")]
        [Tooltip("Boşsa basit kapsül gövde oluşturulur. Karakter modelleri gelince buraya prefab konur.")]
        public GameObject customerPrefab;
        public Material greyboxMaterial;
        [Tooltip("Tüm müşterilerin ortak sabır replikleri (Data/Dialogue/CommonDialogue)")]
        public DialogueSet dialogue;
        public float firstCustomerDelay = 1.0f;

        int index;
        CustomerController current;
        Renderer lastBody;      // greybox gövdesi (sinirlenince kızarır)
        Color lastBodyColor;
        Coroutine routine;

        public CustomerController Current => current;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.CustomerLeft += OnCustomerLeft;
        }

        void OnDisable()
        {
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.CustomerLeft -= OnCustomerLeft;
        }

        void OnStateChanged(GameState s)
        {
            if (s == GameState.Working)
            {
                ClearCurrent();
                index = 0;
                Schedule(firstCustomerDelay);
            }
            else
            {
                ClearCurrent(); // son vardiya, gün sonu, menü vb.: gişe boşalır
            }
        }

        void OnCustomerLeft(CustomerData c, bool angry)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Working) return;
            current = null;
            if (gm.CheckPendingFired()) return;   // 3. ceza / 3. haksız ret: müşteri çıktı, oyun biter

            var day = gm.CurrentDay;
            if (index >= day.customers.Length) gm.OnLastCustomerLeft();
            else Schedule(gm.Config.nextCustomerDelay);
        }

        void Schedule(float delay)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(SpawnAfter(delay));
        }

        IEnumerator SpawnAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            routine = null;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Working) yield break;
            var day = gm.CurrentDay;
            if (day == null || day.customers == null || index >= day.customers.Length) { gm.OnLastCustomerLeft(); yield break; }

            var data = day.customers[index++];
            current = Spawn(data);
            current.Begin(data, doorPoint.position, customerSpot.position, gm.Config.customerWalkSeconds);
        }

        void ClearCurrent()
        {
            if (routine != null) { StopCoroutine(routine); routine = null; }
            if (current != null) Destroy(current.gameObject);
            current = null;
        }

        CustomerController Spawn(CustomerData data)
        {
            GameObject go;
            Transform visual;
            if (customerPrefab != null)
            {
                go = Instantiate(customerPrefab);
                visual = go.transform.childCount > 0 ? go.transform.GetChild(0) : go.transform;
            }
            else
            {
                go = BuildGreybox(data, out visual);
            }
            go.name = $"Customer_{data.id}";
            var cc = go.GetComponent<CustomerController>();
            if (cc == null) cc = go.AddComponent<CustomerController>();
            cc.visual = visual;
            cc.dialogue = dialogue;
            if (lastBody != null) { cc.tintRenderer = lastBody; cc.baseColor = lastBodyColor; }
            lastBody = null;
            return cc;
        }

        /// Geçici gövde: kapsül + kafa, her müşteriye farklı renk
        GameObject BuildGreybox(CustomerData data, out Transform visual)
        {
            var root = new GameObject();
            var vis = new GameObject("Visual").transform;
            vis.SetParent(root.transform, false);
            visual = vis;

            bool female = data.body == BodyType.Female;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(vis, false);
            body.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            body.transform.localScale = female ? new Vector3(0.40f, 0.76f, 0.34f) : new Vector3(0.46f, 0.78f, 0.38f);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.transform.SetParent(vis, false);
            head.transform.localPosition = new Vector3(0f, 1.66f, 0f);
            head.transform.localScale = Vector3.one * 0.24f;

            // Burun: hangi yöne baktığı belli olsun
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(vis, false);
            nose.transform.localPosition = new Vector3(0f, 1.66f, 0.12f);
            nose.transform.localScale = new Vector3(0.04f, 0.04f, 0.06f);

            // Renk: id'den sabit bir ton
            int seed = 0;
            foreach (char ch in data.id) seed = seed * 31 + ch;
            float hue = Mathf.Abs(seed * 0.618034f) % 1f;
            var bodyColor = Color.HSVToRGB(hue, female ? 0.45f : 0.35f, 0.75f);
            var skin = new Color(0.86f, 0.70f, 0.58f);
            Paint(body, bodyColor);
            lastBody = body.GetComponent<Renderer>();
            lastBodyColor = bodyColor;
            Paint(head, skin);
            Paint(nose, skin * 0.9f);

            foreach (var col in root.GetComponentsInChildren<Collider>()) Destroy(col); // tıklamayı engellemesin
            return root;
        }

        void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (greyboxMaterial != null) r.sharedMaterial = greyboxMaterial;
            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }

        /// F4: kalan müşterileri atla, gün sonuna geç
        public void DebugSkipDay()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Working) return;
            if (current != null) GameEvents.RaiseCustomerLeaving(current.Data); // dosya masadan kalksın
            ClearCurrent();
            index = gm.CurrentDay.customers.Length;
            gm.OnLastCustomerLeft();
        }
    }
}
