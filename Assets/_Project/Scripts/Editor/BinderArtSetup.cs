using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/19 - Tasarım kural klasörünü bağla"  (önce menü 6, 13, 14)
    /// - Masada kapalı klasör (SM_RuleBinder), yakın bakışta açık klasör (SM_RuleBinder_Open)
    /// - Kural sayfası açık klasörün sayfasının (M_RulePage_Front) tam üstüne oturur
    /// - Turuncu YENİ sekmesi (SM_RuleBinder_NewTab) 2. günden itibaren görünür (BinderView yönetir)
    /// - Beyaz parıltı çerçevesi kapalı modelin boyutuna ve açısına göre yeniden kurulur
    /// - Greybox kutu ve menteşeli greybox kapak gizlenir
    /// Tekrar çalıştırılabilir.
    public static class BinderArtSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string OpenPath = "Assets/Art/3D/Props/SM_RuleBinder_Open.fbx";

        [MenuItem("Load Bearing/19 - Tasarım kural klasörünü bağla", priority = 19)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var binderGo = GameObject.Find("Desk/RuleBinder");
            var view = binderGo != null ? binderGo.GetComponent<BinderView>() : null;
            if (view == null || view.pageRoot == null) { Debug.LogError("Desk/RuleBinder ya da BinderView yok. Önce menü 6'yı çalıştır."); return; }
            var binder = binderGo.transform;
            var page = view.pageRoot.parent as RectTransform;
            if (page == null) { Debug.LogError("Kural sayfası (Page) bulunamadı. Menü 6'yı tekrar çalıştır."); return; }

            // ---- Kapalı klasör: menü 14'ün gizli koyduğu SM_RuleBinder (ya da önceki kurulumdan "Closed" altında)
            var closedRoot = binder.Find("Closed");
            Transform sm = closedRoot != null ? FindDeep(closedRoot, "SM_RuleBinder") : null;
            if (sm == null)
            {
                var art = binder.Find("Art");
                sm = art != null ? FindDeep(art, "SM_RuleBinder") : null;
                if (sm == null) { Debug.LogError("SM_RuleBinder bulunamadı. Önce menü 14'ü çalıştır."); return; }
                if (closedRoot == null)
                {
                    closedRoot = new GameObject("Closed").transform;
                    closedRoot.SetParent(binder, false);
                }
                sm.SetParent(closedRoot, true);
                sm.gameObject.SetActive(true);
            }
            closedRoot.gameObject.SetActive(true);

            // Önceki kurulumdan kalan açık klasör: sayfayı kurtar, sonra sil
            var oldOpen = binder.Find("Open");
            if (oldOpen != null)
            {
                page.SetParent(binder, true);
                Object.DestroyImmediate(oldOpen.gameObject);
            }

            // ---- Greybox: kutu görünmesin, kapak (menteşe) kapansın
            var vis = binder.Find("Visual");
            if (vis != null)
            {
                var r = vis.GetComponent<Renderer>(); if (r != null) r.enabled = false;
                var c = vis.GetComponent<Collider>(); if (c != null) c.enabled = false;
            }
            var hinge = binder.Find("CoverHinge");
            if (hinge != null) hinge.gameObject.SetActive(false);
            view.coverHinge = null;

            // ---- Tıklama kutusu: kapalı modelin boyutunda
            var mf = sm.GetComponent<MeshFilter>();
            var lb = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(new Vector3(0f, 0.03f, 0f), new Vector3(0.297f, 0.06f, 0.32f));
            var col = closedRoot.GetComponent<BoxCollider>();
            if (col != null) Object.DestroyImmediate(col);
            col = sm.GetComponent<BoxCollider>();
            if (col == null) col = sm.gameObject.AddComponent<BoxCollider>();
            col.center = lb.center;
            col.size = lb.size;

            // ---- YENİ sekmesi
            var tab = FindDeep(sm, "SM_RuleBinder_NewTab");
            if (tab != null) tab.gameObject.SetActive(false);
            view.newTab = tab != null ? tab.gameObject : null;

            // ---- Açık klasör
            float yaw = NearYaw(sm.eulerAngles.y);
            var openRoot = new GameObject("Open").transform;
            openRoot.SetParent(binder, false);
            var openPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpenPath);
            Vector3 pageCenter = sm.position + Vector3.up * 0.017f;
            Vector2 pageSize = new Vector2(0.206f, 0.294f);
            if (openPrefab != null)
            {
                var open = (GameObject)PrefabUtility.InstantiatePrefab(openPrefab);
                PrefabUtility.UnpackPrefabInstance(open, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                open.transform.SetParent(openRoot, false);
                open.transform.SetPositionAndRotation(sm.position, sm.rotation);
                if (SubmeshLocal(open, "RulePage", out var c, out var size)) { pageCenter = c; pageSize = size; }
                else Debug.LogWarning("Açık klasörde M_RulePage_Front sayfası bulunamadı; sayfa klasörün ortasına kondu.");
            }
            else Debug.LogWarning("SM_RuleBinder_Open bulunamadı.");

            // ---- Kural sayfası: açık klasörün sayfasının üstüne (sadece açıkken görünür)
            page.SetParent(openRoot, true);
            page.position = pageCenter + Vector3.up * 0.0006f;
            page.rotation = Quaternion.Euler(90f, yaw, 0f);
            float s = Mathf.Min(pageSize.x / page.sizeDelta.x, pageSize.y / page.sizeDelta.y);
            page.localScale = Vector3.one * s / Mathf.Max(0.0001f, openRoot.lossyScale.x);
            page.gameObject.SetActive(true);
            openRoot.gameObject.SetActive(false);

            // ---- Parıltı çerçevesi: kapalı modelin etrafına
            var frame = view.alertFrame != null ? view.alertFrame.transform : null;
            if (frame != null)
            {
                var sc = sm.lossyScale;
                var ext = Vector3.Scale(lb.extents, sc);
                frame.position = sm.TransformPoint(new Vector3(lb.center.x, lb.min.y, lb.center.z));
                frame.rotation = Quaternion.Euler(0f, sm.eulerAngles.y, 0f);
                float gap = 0.004f, t = 0.004f, hgt = ext.y * 2f + 0.004f;
                float ex = Mathf.Abs(ext.x) + gap, ez = Mathf.Abs(ext.z) + gap;
                SetBar(frame, "Left", new Vector3(-ex, hgt * 0.5f, 0f), new Vector3(t, hgt, ez * 2f + t));
                SetBar(frame, "Right", new Vector3(ex, hgt * 0.5f, 0f), new Vector3(t, hgt, ez * 2f + t));
                SetBar(frame, "Front", new Vector3(0f, hgt * 0.5f, -ez), new Vector3(ex * 2f + t, hgt, t));
                SetBar(frame, "Back", new Vector3(0f, hgt * 0.5f, ez), new Vector3(ex * 2f + t, hgt, t));
            }

            view.closedModel = closedRoot.gameObject;
            view.openModel = openRoot.gameObject;
            EditorUtility.SetDirty(view);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=lime>[Load Bearing]</color> Tasarım kural klasörü bağlandı. Sayfa: {pageSize.x * 100f:0.#} x {pageSize.y * 100f:0.#} cm. YENİ sekmesi: {(tab != null ? "var" : "YOK")}");
        }

        static void SetBar(Transform frame, string name, Vector3 pos, Vector3 scale)
        {
            var b = frame.Find(name);
            if (b == null) return;
            b.localPosition = pos;
            b.localRotation = Quaternion.identity;
            b.localScale = scale;
        }

        /// Adı verilen materyali kullanan alt mesh: dünya merkezi ve sayfa düzlemindeki boyutu (genişlik, yükseklik)
        static bool SubmeshLocal(GameObject root, string matContains, out Vector3 worldCenter, out Vector2 size)
        {
            worldCenter = Vector3.zero; size = Vector2.zero;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(matContains)) continue;
                    var b = mf.sharedMesh.GetSubMesh(i).bounds;
                    var sc = mr.transform.lossyScale;
                    var sz = new Vector3(Mathf.Abs(b.size.x * sc.x), Mathf.Abs(b.size.y * sc.y), Mathf.Abs(b.size.z * sc.z));
                    // en ince eksen sayfanın kalınlığı; kalan ikisinden küçüğü genişlik, büyüğü yükseklik
                    float a1, a2;
                    if (sz.y <= sz.x && sz.y <= sz.z) { a1 = sz.x; a2 = sz.z; }
                    else if (sz.x <= sz.z) { a1 = sz.y; a2 = sz.z; }
                    else { a1 = sz.x; a2 = sz.y; }
                    size = new Vector2(Mathf.Min(a1, a2), Mathf.Max(a1, a2));
                    worldCenter = mr.transform.TransformPoint(b.center);
                    worldCenter.y = mr.transform.TransformPoint(b.center + Vector3.up * b.extents.y).y;
                    // dünyada en üst nokta (eksen dönüşümü olsa da)
                    float top = float.NegativeInfinity;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        top = Mathf.Max(top, mr.transform.TransformPoint(corner).y);
                    }
                    worldCenter.y = top;
                    return true;
                }
            }
            return false;
        }

        static float NearYaw(float y)
        {
            y = Mathf.Repeat(y + 180f, 360f) - 180f;
            if (y > 90f) y -= 180f;
            if (y < -90f) y += 180f;
            return y;
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
    }
}
