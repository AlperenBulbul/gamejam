using UnityEngine;

namespace LoadBearing
{
    /// Gösterge renkleri (oyuncuya "buraya bak" demek için tek renk: turuncu)
    public static class HintColors
    {
        public static readonly Color Orange = new Color(1f, 0.40f, 0.06f); // kırmızıya biraz kaymış turuncu
    }

    /// Bir nesnenin etrafında parlayan / yanıp sönen turuncu çerçeve (oyuncuya ne yapacağını göstermek için).
    /// Çerçeve hedef modelin kendi kutusuna göre kurulur: düz nesnelerde (harita, gazete) üst yüzde,
    /// dik nesnelerde (monitör) oyuncuya bakan ön yüzde.
    public class HintGlow : MonoBehaviour
    {
        public Renderer target;
        public float pulsePerSecond = 1.4f;
        [Range(0f, 1f)] public float minAlpha = 0f;
        [Range(0f, 1f)] public float maxAlpha = 1f;
        public float margin = 0.008f;
        public float thickness = 0.007f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        GameObject frame;
        Renderer[] bars;
        MaterialPropertyBlock mpb;
        bool on;

        public bool IsOn => on;

        /// owner altında hedefin çevresine çerçeve kurar. Materyal: kural klasörünün parıltı materyali.
        public static HintGlow Create(Transform owner, Renderer target, string name = "HintGlow", bool ringAtBase = false)
        {
            if (owner == null || target == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(owner, false);
            var h = go.AddComponent<HintGlow>();
            h.target = target;
            h.ringAtBase = ringAtBase;
            h.Build();
            return h;
        }

        /// true: çerçeve nesnenin tabanında, masaya yatay (mühür gibi dik nesneler için)
        public bool ringAtBase;

        public static Material GlowMaterial()
        {
            var binder = FindAnyObjectByType<BinderView>();
            if (binder != null && binder.alertFrame != null)
            {
                var r = binder.alertFrame.GetComponentInChildren<Renderer>(true);
                if (r != null) return r.sharedMaterial;
            }
            return null;
        }

        void Build()
        {
            mpb = new MaterialPropertyBlock();
            var mat = GlowMaterial();
            var mf = target != null ? target.GetComponent<MeshFilter>() : null;
            if (mat == null || mf == null || mf.sharedMesh == null) return;

            var t = target.transform;
            var b = mf.sharedMesh.bounds;
            var size = Vector3.Scale(b.size, t.lossyScale);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

            // En ince eksen = yüzün normali (tabanda halka istenirse yukarı ekseni)
            int thin = ringAtBase ? 1 : (size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2));
            Vector3 n = thin == 0 ? t.right : (thin == 1 ? t.up : t.forward);
            var cam = Camera.main;
            Vector3 center = t.TransformPoint(b.center);
            if (thin == 1) { if (n.y < 0f) n = -n; }                               // düz nesne: üst yüz
            else if (cam != null && Vector3.Dot(n, cam.transform.position - center) < 0f) n = -n; // dik nesne: oyuncuya bakan yüz
            n.Normalize();

            int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
            Vector3 ax1 = a1 == 0 ? t.right : (a1 == 1 ? t.up : t.forward);
            Vector3 ax2 = a2 == 0 ? t.right : (a2 == 1 ? t.up : t.forward);
            float e1 = size[a1] * 0.5f + margin, e2 = size[a2] * 0.5f + margin;
            float faceOffset = ringAtBase ? -size[thin] * 0.5f + 0.003f : size[thin] * 0.5f + 0.002f; // taban ya da ön/üst yüz

            frame = new GameObject("Frame");
            frame.transform.SetParent(transform, false);
            frame.transform.position = center + n * faceOffset;
            frame.transform.rotation = Quaternion.LookRotation(n, ax2) ; // z = normal, y = ax2, x = ax1 (yönü önemli değil)
            // Yerel eksenler: x ~ ax1, y ~ ax2
            float th = thickness, depth = 0.004f;
            Bar(new Vector3(-e1, 0f, 0f), new Vector3(th, e2 * 2f + th, depth), mat);
            Bar(new Vector3(e1, 0f, 0f), new Vector3(th, e2 * 2f + th, depth), mat);
            Bar(new Vector3(0f, -e2, 0f), new Vector3(e1 * 2f + th, th, depth), mat);
            Bar(new Vector3(0f, e2, 0f), new Vector3(e1 * 2f + th, th, depth), mat);
            bars = frame.GetComponentsInChildren<Renderer>(true);
            HintFrameFx.Attach(frame, mat, pulsePerSecond, n); // koyu kenar + turuncu yansıma ışığı
            frame.SetActive(false);
        }

        void Bar(Vector3 pos, Vector3 size, Material m)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(b.GetComponent<Collider>()); // tıklamayı engellemesin
            b.transform.SetParent(frame.transform, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            var r = b.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Show(bool show)
        {
            on = show;
            if (frame != null) frame.SetActive(show);
        }

        void Update()
        {
            if (!on || bars == null) return;
            float s = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsePerSecond * 2f * Mathf.PI);
            var c = HintColors.Orange;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, s);
            foreach (var r in bars)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
