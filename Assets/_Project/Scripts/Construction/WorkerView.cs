using UnityEngine;

namespace LoadBearing
{
    /// Monitördeki tek bir işçi (küçük figür). MonitorFeed oluşturur.
    /// Düzgün işçi çekiç sallar. Hatalı işçinin elinde / önünde hatasını gösteren bir şey belirir (GDD bölüm 5):
    ///  F1 betona su katıyor: mavi hortum + gri sulu birikinti
    ///  F2 deniz kumu: mavi etiketli çuval
    ///  F3 eksik demir: önünde seyrek demir ızgara
    ///  F4 nervürsüz demir: kırmızı işaretli düz çubuk
    ///  F5 kolonu kesiyor: taşlama makinesi + kıvılcım
    public class WorkerView : MonoBehaviour
    {
        public int index;
        public int FaultType { get; private set; }

        Transform body, armR;
        GameObject[] props = new GameObject[6];
        Transform sparks;
        Material lit, unlit;
        float phase;
        Vector3 bodyHome;

        static readonly Color Skin = new Color(0.90f, 0.72f, 0.58f);
        static readonly Color Shirt = new Color(0.52f, 0.54f, 0.58f);
        static readonly Color Pants = new Color(0.20f, 0.21f, 0.24f);
        static readonly Color Helmet = new Color(0.96f, 0.60f, 0.12f);
        static readonly Color Blue = new Color(0.20f, 0.45f, 0.90f);
        static readonly Color Water = new Color(0.55f, 0.62f, 0.68f);
        static readonly Color Rebar = new Color(0.42f, 0.27f, 0.16f);
        static readonly Color Red = new Color(0.90f, 0.15f, 0.12f);
        static readonly Color Steel = new Color(0.62f, 0.64f, 0.66f);
        static readonly Color Spark = new Color(1f, 0.85f, 0.30f);

        public void Build(int idx, Material litMat, Material unlitMat)
        {
            index = idx;
            lit = litMat; unlit = unlitMat;
            phase = Random.value * 10f;

            // Ölçüler: figür ~0.3 birim boyunda
            Box("LegL", transform, new Vector3(-0.025f, 0.05f, 0f), new Vector3(0.035f, 0.10f, 0.04f), Pants);
            Box("LegR", transform, new Vector3(0.025f, 0.05f, 0f), new Vector3(0.035f, 0.10f, 0.04f), Pants);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localPosition = new Vector3(0f, 0.10f, 0f);
            bodyHome = body.localPosition;
            Box("Torso", body, new Vector3(0f, 0.065f, 0f), new Vector3(0.10f, 0.13f, 0.06f), Shirt);
            Ball("Head", body, new Vector3(0f, 0.165f, 0f), 0.065f, Skin);
            Box("Helmet", body, new Vector3(0f, 0.195f, 0f), new Vector3(0.08f, 0.035f, 0.08f), Helmet);
            armR = new GameObject("ArmR").transform;
            armR.SetParent(body, false);
            armR.localPosition = new Vector3(0.065f, 0.12f, 0f);
            Box("Arm", armR, new Vector3(0f, -0.045f, 0.01f), new Vector3(0.03f, 0.09f, 0.03f), Shirt);
            Box("Hammer", armR, new Vector3(0f, -0.09f, 0.035f), new Vector3(0.025f, 0.025f, 0.06f), Steel);

            // Tıklama kutusu (adım 13)
            var col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.15f, 0f);
            col.size = new Vector3(0.16f, 0.32f, 0.16f);
        }

        public void SetFault(int type)
        {
            if (type == FaultType) return;
            FaultType = type;
            for (int i = 1; i < props.Length; i++) if (props[i] != null) props[i].SetActive(i == type);
            if (type > 0 && type < props.Length && props[type] == null) props[type] = BuildProp(type);
            if (armR != null) armR.gameObject.SetActive(type == 0 || type == 3); // hatalıyken çekiç yok
        }

        GameObject BuildProp(int type)
        {
            var root = new GameObject($"F{type}").transform;
            root.SetParent(transform, false);
            switch (type)
            {
                case 1: // hortum + sulu birikinti
                    Cyl("Puddle", root, new Vector3(0.10f, 0.004f, 0.08f), new Vector3(0.18f, 0.004f, 0.14f), Water);
                    Box("Hose", root, new Vector3(0.07f, 0.14f, 0.05f), new Vector3(0.02f, 0.02f, 0.12f), Blue, Quaternion.Euler(35f, 20f, 0f));
                    Box("Hose2", root, new Vector3(0.10f, 0.06f, 0.10f), new Vector3(0.02f, 0.11f, 0.02f), Blue);
                    break;
                case 2: // mavi etiketli kum çuvalı (kucakta)
                    Box("Sack", root, new Vector3(0f, 0.17f, 0.07f), new Vector3(0.11f, 0.08f, 0.06f), new Color(0.85f, 0.82f, 0.72f));
                    Box("Label", root, new Vector3(0f, 0.17f, 0.101f), new Vector3(0.08f, 0.05f, 0.004f), Blue);
                    break;
                case 3: // seyrek demir ızgara (yerde, önünde)
                    for (int i = 0; i < 2; i++)
                        Box($"BarX{i}", root, new Vector3(0f, 0.006f, 0.08f + i * 0.10f), new Vector3(0.22f, 0.012f, 0.012f), Rebar);
                    for (int i = 0; i < 2; i++)
                        Box($"BarZ{i}", root, new Vector3(-0.08f + i * 0.16f, 0.006f, 0.13f), new Vector3(0.012f, 0.012f, 0.16f), Rebar);
                    break;
                case 4: // kırmızı işaretli düz çubuk (omuzda)
                    Box("Bar", root, new Vector3(0.02f, 0.24f, 0f), new Vector3(0.36f, 0.014f, 0.014f), Steel, Quaternion.Euler(0f, 0f, -12f));
                    Box("Mark", root, new Vector3(0.17f, 0.21f, 0f), new Vector3(0.05f, 0.02f, 0.02f), Red, Quaternion.Euler(0f, 0f, -12f));
                    break;
                case 5: // taşlama + kıvılcım
                    Box("Column", root, new Vector3(0f, 0.16f, 0.12f), new Vector3(0.06f, 0.32f, 0.06f), new Color(0.70f, 0.71f, 0.72f));
                    Box("Grinder", root, new Vector3(0f, 0.15f, 0.07f), new Vector3(0.05f, 0.03f, 0.04f), new Color(0.15f, 0.35f, 0.75f));
                    sparks = new GameObject("Sparks").transform;
                    sparks.SetParent(root, false);
                    sparks.localPosition = new Vector3(0f, 0.15f, 0.09f);
                    for (int i = 0; i < 6; i++)
                        Box($"S{i}", sparks, Vector3.zero, Vector3.one * 0.012f, Spark, Quaternion.identity, unlit);
                    break;
            }
            return root.gameObject;
        }

        void Update()
        {
            float t = Time.time + phase;
            if (body != null)
            {
                // Çalışma hareketi: hafif öne eğilip doğrulma
                float bob = FaultType == 0 ? Mathf.Abs(Mathf.Sin(t * 3.2f)) : Mathf.Abs(Mathf.Sin(t * 1.6f)) * 0.5f;
                body.localRotation = Quaternion.Euler(bob * 14f, 0f, 0f);
                body.localPosition = bodyHome;
            }
            if (armR != null && armR.gameObject.activeSelf)
                armR.localRotation = Quaternion.Euler(-60f + Mathf.Abs(Mathf.Sin(t * 3.2f)) * 100f, 0f, 0f);

            if (FaultType == 5 && sparks != null)
            {
                for (int i = 0; i < sparks.childCount; i++)
                {
                    var s = sparks.GetChild(i);
                    float k = Mathf.Repeat(t * 3f + i * 0.17f, 1f);
                    s.localPosition = new Vector3(Mathf.Sin(i * 2.1f) * 0.08f * k, 0.05f * Mathf.Sin(k * Mathf.PI) - 0.04f * k, 0.03f + Mathf.Cos(i * 1.7f) * 0.06f * k);
                    s.gameObject.SetActive(k < 0.8f);
                }
            }
        }

        // ------------------------------------------------------------------ küçük şekiller

        Transform Box(string n, Transform p, Vector3 pos, Vector3 size, Color c) => Box(n, p, pos, size, c, Quaternion.identity, null);

        Transform Box(string n, Transform p, Vector3 pos, Vector3 size, Color c, Quaternion rot, Material mat = null)
            => MonitorFeed.Shape(PrimitiveType.Cube, n, p, pos, size, c, mat != null ? mat : lit, rot);

        Transform Ball(string n, Transform p, Vector3 pos, float d, Color c)
            => MonitorFeed.Shape(PrimitiveType.Sphere, n, p, pos, Vector3.one * d, c, lit, Quaternion.identity);

        Transform Cyl(string n, Transform p, Vector3 pos, Vector3 size, Color c)
            => MonitorFeed.Shape(PrimitiveType.Cylinder, n, p, pos, size, c, lit, Quaternion.identity);
    }
}
