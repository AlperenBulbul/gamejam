using System.Collections.Generic;
using UnityEngine;

namespace LoadBearing
{
    /// Oda modelinin (SM_Office) dört dikey köşesinde duvar parçaları birleşmiyor: arka duvarlar yan duvarların
    /// kalınlığı kadar kısa kalmış, köşede 10 x 10 cm'lik boşluktan dışarısı görünüyordu.
    /// Oyun açılınca her köşeye duvar materyaliyle ince bir dikme, tavan ve zeminin içine de ince levha konur.
    /// Sahnede kurulum gerekmez.
    public static class RoomCornerFix
    {
        const float WallThickness = 0.1f;   // modelde duvar kalınlığı (m)
        const float Overlap = 0.03f;        // dikme komşu duvarlara biraz binsin
        // M_Palette dokusunda duvar boyasının yeri (modelin duvar yüzlerinin UV'si)
        static readonly Vector2 WallUV = new Vector2(0.781f, 0.969f);
        static Mesh fillMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Apply()
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf == null || mf.name != "SM_Office" || mf.sharedMesh == null) continue;
                if (mf.transform.Find("CornerFill_0") != null) continue;
                Fill(mf);
            }
        }

        static void Fill(MeshFilter mf)
        {
            var mesh = mf.sharedMesh;
            var b = mesh.bounds;                       // modelin kendi uzayında: x ±2,3 · z ±2,7 · y 0..2,8
            float u = Mathf.Max(0.01f, b.extents.x / 2.3f); // modelin birimi (içe aktarma ölçeği ne olursa olsun)
            float t = (WallThickness + Overlap * 2f) * u;
            float inset = WallThickness * 0.5f * u;

            var mat = WallMaterial(mf, b, u);
            var cube = FillMesh();
            int i = 0;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "CornerFill_" + i++;
                    Object.Destroy(go.GetComponent<Collider>());
                    go.transform.SetParent(mf.transform, false);
                    float x = sx < 0 ? b.min.x + inset : b.max.x - inset;
                    float z = sz < 0 ? b.min.z + inset : b.max.z - inset;
                    go.transform.localPosition = new Vector3(x, b.center.y, z);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = new Vector3(t, b.size.y, t);
                    go.layer = mf.gameObject.layer;
                    if (cube != null) go.GetComponent<MeshFilter>().sharedMesh = cube;
                    var r = go.GetComponent<MeshRenderer>();
                    if (mat != null) r.sharedMaterial = mat;
                }

            // Tavan ve zemin kenarları: duvar üstleriyle tavan / zemin arasındaki kılcal çatlaklar için
            // tavanın ve zeminin içine gömülü, odanın tamamını kaplayan ince levhalar (içeriden görünmez)
            Slab(mf, mat, cube, new Vector3(b.center.x, b.max.y - 0.025f * u, b.center.z), new Vector3(b.size.x, 0.04f * u, b.size.z), "CornerFill_Top");
            Slab(mf, mat, cube, new Vector3(b.center.x, b.min.y + 0.02f * u, b.center.z), new Vector3(b.size.x, 0.03f * u, b.size.z), "CornerFill_Bottom");
        }

        static void Slab(MeshFilter mf, Material mat, Mesh cube, Vector3 localPos, Vector3 localSize, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(mf.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localSize;
            go.layer = mf.gameObject.layer;
            if (cube != null) go.GetComponent<MeshFilter>().sharedMesh = cube;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// Küp; bütün UV'leri paletteki duvar rengine bakar (tek renk)
        static Mesh FillMesh()
        {
            if (fillMesh != null) return fillMesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = tmp.GetComponent<MeshFilter>().sharedMesh;
            fillMesh = Object.Instantiate(src);
            fillMesh.name = "CornerFill";
            var uv = new Vector2[fillMesh.vertexCount];
            for (int i = 0; i < uv.Length; i++) uv[i] = WallUV;
            fillMesh.uv = uv;
            Object.Destroy(tmp);
            return fillMesh;
        }

        /// Duvar boyası materyali: paletli ana materyal (metal olan değil)
        static Material WallMaterial(MeshFilter mf, Bounds b, float u)
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null) return null;
            var mats = r.sharedMaterials;
            foreach (var m in mats)
                if (m != null && m.name.Contains("Palette") && !m.name.Contains("Metal")) return m;
            var mesh = mf.sharedMesh;
            if (!mesh.isReadable || mesh.subMeshCount <= 1) return mats.Length > 0 ? mats[0] : null;
            var verts = new List<Vector3>();
            mesh.GetVertices(verts);
            var tris = new List<int>();
            for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
            {
                mesh.GetTriangles(tris, s);
                foreach (int idx in tris)
                {
                    var v = verts[idx];
                    if (Mathf.Abs(v.x) > b.max.x - 0.02f * u && v.y > b.min.y + 1f * u && v.y < b.min.y + 2f * u) return mats[s];
                }
            }
            return mats.Length > 0 ? mats[0] : null;
        }
    }
}
