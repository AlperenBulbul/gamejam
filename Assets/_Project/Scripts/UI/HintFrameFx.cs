using UnityEngine;

namespace LoadBearing
{
    /// Turuncu gösterge çerçevelerini masadan ayırmak için ek görünüm:
    ///  - Her çubuğun arkasında koyu bir kenar (yazıdaki dış çizgi gibi): ahşap masaya karışmaz
    ///  - Çerçevenin tamamı boyunca hafif turuncu hale: bütün şekil birlikte yanıp söner (nokta ışık yok)
    /// Çerçeve kapanınca (SetActive false) ışık ve kenar da kendiliğinden kapanır.
    public class HintFrameFx : MonoBehaviour
    {
        public float pulsePerSecond = 1.4f;
        public float outline = 0.0035f;         // koyu kenarın her yandan taşma payı (m)
        public float halo = 0.010f;             // hafif turuncu halenin her yandan taşma payı (m)
        public float haloMinAlpha = 0f, haloMaxAlpha = 0.16f;
        public float backMinAlpha = 0f, backMaxAlpha = 0.80f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Material backMat, haloMat;

        Renderer[] halos;
        Renderer[] backs;
        MaterialPropertyBlock mpb;

        /// frame: çubukları (küp) içeren çerçeve. normal: çerçevenin baktığı yön (ışık o tarafa konur).
        public static HintFrameFx Attach(GameObject frame, Material glowMat, float pulsePerSecond, Vector3 normal)
        {
            if (frame == null) return null;
            var fx = frame.GetComponent<HintFrameFx>();
            if (fx != null) return fx;
            fx = frame.AddComponent<HintFrameFx>();
            fx.pulsePerSecond = pulsePerSecond;
            fx.Build(glowMat, normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up);
            return fx;
        }

        void Build(Material glowMat, Vector3 normal)
        {
            mpb = new MaterialPropertyBlock();
            var bars = GetComponentsInChildren<MeshRenderer>(true);

            // Koyu kenar: aynı çubuğun biraz büyüğü, turuncudan önce çizilir (render sırası bir eksik)
            if (glowMat != null && backMat == null)
            {
                backMat = new Material(glowMat) { name = "M_HintOutline (runtime)" };
                backMat.SetColor(BaseColorId, new Color(0f, 0f, 0f, 0.65f));
                backMat.renderQueue = glowMat.renderQueue - 1;
                haloMat = new Material(glowMat) { name = "M_HintHalo (runtime)" };
                haloMat.renderQueue = glowMat.renderQueue - 2; // en arkada: önce hale, sonra koyu kenar, en üstte turuncu çizgi
            }

            var holder = new GameObject("Outline").transform;
            holder.SetParent(transform, false);
            var list = new System.Collections.Generic.List<Renderer>();
            var haloList = new System.Collections.Generic.List<Renderer>();
            foreach (var bar in bars)
            {
                var mf = bar.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (backMat == null) continue;

                list.Add(Clone(bar, holder, "Back", outline, backMat));
                haloList.Add(Clone(bar, holder, "Halo", halo, haloMat));
            }
            backs = list.ToArray();
            halos = haloList.ToArray();
        }

        static Renderer Clone(MeshRenderer bar, Transform holder, string name, float grow, Material mat)
        {
            var t = bar.transform;
            var b = Instantiate(bar.gameObject, holder);
            b.name = name;
            foreach (var extra in b.GetComponents<Component>())
                if (!(extra is Transform) && !(extra is MeshFilter) && !(extra is MeshRenderer)) Destroy(extra);
            b.transform.SetPositionAndRotation(t.position, t.rotation);
            var ls = t.lossyScale;
            var ps = holder.lossyScale;
            float g = grow * 2f;
            b.transform.localScale = new Vector3(
                (Mathf.Abs(ls.x) + g) / Mathf.Max(1e-5f, Mathf.Abs(ps.x)),
                (Mathf.Abs(ls.y) + g) / Mathf.Max(1e-5f, Mathf.Abs(ps.y)),
                (Mathf.Abs(ls.z) + g) / Mathf.Max(1e-5f, Mathf.Abs(ps.z)));
            var r = b.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.SetPropertyBlock(null);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void Update()
        {
            float s = HintColors.Steady ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulsePerSecond * 2f * Mathf.PI);
            var hc = HintColors.Orange; hc.a = Mathf.Lerp(haloMinAlpha, haloMaxAlpha, s);
            if (halos != null)
                foreach (var r in halos)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, hc);
                    r.SetPropertyBlock(mpb);
                }
            if (backs == null) return;
            var c = new Color(0f, 0f, 0f, Mathf.Lerp(backMinAlpha, backMaxAlpha, s));
            foreach (var r in backs)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
