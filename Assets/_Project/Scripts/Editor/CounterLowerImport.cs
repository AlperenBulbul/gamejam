using UnityEngine;
using UnityEditor;

namespace LoadBearing.EditorTools
{
    /// Gişe penceresinin alt kenarı alçaltıldı (müşteri daha çok görünsün).
    /// Tasarımcının modelleri içe aktarılırken (sahnede değil, modelin kendisinde) şu parçalar Drop kadar aşağı iner:
    ///  - SM_Office: pencerenin altındaki duvar ve dolap kapakları kısalır, pencere dikmeleri aşağı uzar
    ///  - SM_CounterGlass: tezgâh tablası (üstteki başlık kirişi yerinde kalır), SM_CounterGlass_Tray: evrak tepsisi
    ///  - SM_CounterGlass_Glass ve çizgileri: camın alt kenarı aşağı uzar (üst kenar aynı)
    /// Hem yerleşim dosyasındaki (LB_OfficeLayout_Reference) hem tek tek dosyalardaki kopyalara uygulanır.
    /// Yeni / değişen postprocessor Unity'de ilgili modelleri kendiliğinden yeniden içe aktarır (kimsenin menü çalıştırması gerekmez).
    public class CounterLowerImport : AssetPostprocessor
    {
        const float Drop = 0.15f; // metre

        public override uint GetVersion() => 1;

        static readonly string[] Files =
        {
            "LB_OfficeLayout_Reference.fbx", "SM_Office.fbx", "SM_CounterGlass.fbx", "SM_CounterGlass_Glass.fbx",
        };

        void OnPostprocessModel(GameObject root)
        {
            string p = assetPath.Replace('\\', '/');
            bool ours = false;
            foreach (var f in Files) if (p.EndsWith("/" + f)) { ours = true; break; }
            if (!ours) return;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                string n = mf.gameObject.name;
                if (n == "SM_Office") Office(mesh);
                else if (n == "SM_CounterGlass") Counter(mesh);
                else if (n == "SM_CounterGlass_Tray") Shift(mesh, 0.016f, _ => true);
                else if (n == "SM_CounterGlass_Glass" || n.StartsWith("SM_CounterGlass_Streak")) Shift(mesh, 0.98f, y => y < 0.49f);
            }
        }

        /// Ölçek: model birimi metre değilse (cm vb.) mesh yüksekliğinden bul
        static float Scale(Mesh m, float expectedHeight) =>
            expectedHeight > 0f && m.bounds.size.y > 1e-5f ? m.bounds.size.y / expectedHeight : 1f;

        /// Pencerenin altındaki duvar (|x| < 0.95, z 0..0.12) ve dikmeler: üst / alt uçları y 1.0 – 1.08 arasındaki köşeler
        static void Office(Mesh m)
        {
            float s = Scale(m, 2.8f);
            float baseY = m.bounds.min.y;
            var v = m.vertices;
            int moved = 0;
            for (int i = 0; i < v.Length; i++)
            {
                float x = Mathf.Abs(v[i].x) / s, z = Mathf.Abs(v[i].z) / s, y = (v[i].y - baseY) / s;
                if (x <= 0.99f && z <= 0.13f && y >= 1.0f && y <= 1.08f) { v[i].y -= Drop * s; moved++; }
            }
            Apply(m, v, moved);
        }

        /// Tezgâh: alttaki tabla ve dudak (y < 0.5) iner, üstteki başlık kirişi kalır
        static void Counter(Mesh m) => Shift(m, 1.042f, y => y < 0.5f);

        static void Shift(Mesh m, float expectedHeight, System.Func<float, bool> pick)
        {
            float s = Scale(m, expectedHeight);
            float baseY = m.bounds.min.y;
            var v = m.vertices;
            int moved = 0;
            for (int i = 0; i < v.Length; i++)
                if (pick((v[i].y - baseY) / s)) { v[i].y -= Drop * s; moved++; }
            Apply(m, v, moved);
        }

        static void Apply(Mesh m, Vector3[] v, int moved)
        {
            if (moved == 0) return;
            m.vertices = v;
            m.RecalculateBounds();
        }
    }
}
