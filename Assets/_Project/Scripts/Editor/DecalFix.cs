using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/16 - Duvar yazılarındaki titremeyi düzelt"
    /// Tasarım modellerinde yüzeye yapışık ince parçalar (poster yazıları, takvim ayı, saat çizgileri, tabela
    /// yazıları, etiketler) altlarındaki yüzeyle aynı düzlemde ya da arkasında kalınca titriyor / parça parça görünüyordu.
    /// Bu menü her ince parçayı (bir ekseni 2 mm'den ince olan çocuk mesh), ebeveyn yüzeyinin o bölgedeki en öndeki
    /// noktasının 1,5 mm önüne taşır. Ayrıca kameranın yakın kesme mesafesini 5 cm yapar (derinlik hassasiyeti).
    /// Tekrar çalıştırılabilir (zaten öndeyse dokunmaz).
    public static class DecalFix
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const float Gap = 0.0015f;      // yüzeyden ne kadar önde dursun
        const float FlatLimit = 0.002f; // bundan ince olan parça "yapışık yazı" sayılır

        // Duvar süs bantlarıyla aynı derinlikte kalan asılı objeler: odaya doğru ne kadar çekilsin (metre)
        static readonly (string name, float push)[] PushOffWall =
        {
            ("SM_Poster_Deprem", 0.015f),
            ("SM_Calendar", 0.010f),
        };
        const string PushedMarker = "_DuvardanCekildi";

        [MenuItem("Load Bearing/16 - Duvar yazılarındaki titremeyi düzelt", priority = 16)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var camGo = GameObject.FindWithTag("MainCamera");
            Vector3 viewer = camGo != null ? camGo.transform.position : Vector3.zero;
            if (camGo != null)
            {
                var cam = camGo.GetComponent<Camera>();
                if (cam != null && cam.nearClipPlane < 0.05f) cam.nearClipPlane = 0.05f;
            }

            int moved = 0, checkedCount = 0;
            var names = new List<string>();

            // 1) Duvardaki objeleri duvardan biraz çek (bir kez; işaret çocuğu ile)
            var artRoot = GameObject.Find("Art_Office");
            if (artRoot != null)
            {
                foreach (var (objName, push) in PushOffWall)
                {
                    var t = FindDeep(artRoot.transform, objName);
                    var r = t != null ? t.GetComponent<Renderer>() : null;
                    if (r == null || t.Find(PushedMarker) != null) continue;
                    var bb = r.bounds;
                    int ax = ThinAxis(bb.size);
                    var dir = Vector3.zero;
                    dir[ax] = Mathf.Sign(viewer[ax] - bb.center[ax]);
                    Undo.RecordObject(t, "Push off wall");
                    t.position += dir * push;
                    new GameObject(PushedMarker).transform.SetParent(t, false);
                    names.Add($"{objName} duvardan {push * 100f:0.#} cm çekildi");
                }
            }

            // 2) Yüzeye yapışık ince parçalar
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var parentMf = mf.transform.parent != null ? mf.transform.parent.GetComponent<MeshFilter>() : null;
                if (parentMf == null || parentMf.sharedMesh == null) continue;
                if (!IsArt(mf.transform)) continue;
                if (mf.name.Contains("Overlay") || mf.name.Contains("Asetat")) continue; // asetat haritaya özel hizalı

                var decalPts = WorldVerts(mf);
                var b = Bounds(decalPts);
                int thin = ThinAxis(b.size);
                if (b.size[thin] > FlatLimit) continue; // ince değil, gerçek bir parça
                checkedCount++;

                // Odaya (izleyiciye) doğru normal
                var n = Vector3.zero;
                n[thin] = Mathf.Sign(viewer[thin] - b.center[thin]);
                if (n == Vector3.zero) n[thin] = 1f;

                // Ebeveynin bu bölgedeki en öndeki noktası
                var parentPts = WorldVerts(parentMf);
                float front = float.NegativeInfinity, frontAll = float.NegativeInfinity;
                int a1 = (thin + 1) % 3, a2 = (thin + 2) % 3;
                foreach (var p in parentPts)
                {
                    float d = Vector3.Dot(p, n);
                    if (d > frontAll) frontAll = d;
                    if (p[a1] >= b.min[a1] - 0.005f && p[a1] <= b.max[a1] + 0.005f &&
                        p[a2] >= b.min[a2] - 0.005f && p[a2] <= b.max[a2] + 0.005f && d > front) front = d;
                }
                if (float.IsNegativeInfinity(front)) front = frontAll;

                float decalBack = float.PositiveInfinity;
                foreach (var q in decalPts) decalBack = Mathf.Min(decalBack, Vector3.Dot(q, n));

                float shift = (front + Gap) - decalBack;
                if (shift > 0.00005f && shift < 0.05f)
                {
                    Undo.RecordObject(mf.transform, "Decal fix");
                    mf.transform.position += n * shift;
                    moved++;
                    names.Add($"{mf.name} (+{shift * 1000f:0.0} mm)");
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=lime>[Load Bearing]</color> {checkedCount} ince parça kontrol edildi, {moved} tanesi öne alındı. Kamera yakın kesme: 5 cm.\n - " +
                      string.Join("\n - ", names));
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static bool IsArt(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                if (p.name == "Art_Office" || p.name == "Art") return true;
            return false;
        }

        static List<Vector3> WorldVerts(MeshFilter mf)
        {
            var list = new List<Vector3>();
            var v = mf.sharedMesh.vertices;
            var m = mf.transform.localToWorldMatrix;
            foreach (var x in v) list.Add(m.MultiplyPoint3x4(x));
            return list;
        }

        static Bounds Bounds(List<Vector3> pts)
        {
            var b = new Bounds(pts.Count > 0 ? pts[0] : Vector3.zero, Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            return b;
        }

        static int ThinAxis(Vector3 s) => s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
    }
}
