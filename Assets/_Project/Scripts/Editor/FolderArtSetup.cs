using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/17 - Tasarım dosyasını bağla"  (önce menü 13, 14 ve 5, 12)
    /// - Masada kapalı dosya (SM_Folder), yakın bakışta açık dosya (SM_Folder_Open)
    /// - Başvuru formu açık dosyanın form sayfasının (M_FormPage_Front) tam üstüne oturur
    /// - Mühür kapağın ortasına basılır: tasarımcının T_StampMark görselleri, 12 x 6 cm
    /// - Rüşvet zarfı tasarımcının SM_Envelope modeli olur, kapağın üst kenarında durur
    /// Tekrar çalıştırılabilir.
    public static class FolderArtSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string OpenPath = "Assets/Art/3D/Props/SM_Folder_Open.fbx";
        const string EnvelopePath = "Assets/Art/3D/Props/SM_Envelope.fbx";
        const string ApprovePath = "Assets/Art/UI/Desk/T_StampMark_Approve.png";
        const string RejectPath = "Assets/Art/UI/Desk/T_StampMark_Reject.png";

        [MenuItem("Load Bearing/17 - Tasarım dosyasını bağla", priority = 17)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var spotGo = GameObject.Find("Desk/FolderSpot");
            var spot = spotGo != null ? spotGo.transform : null;
            var content = spot != null ? spot.Find("Content") : null;
            var view = spotGo != null ? spotGo.GetComponent<FolderView>() : null;
            if (content == null || view == null) { Debug.LogError("Desk/FolderSpot/Content ya da FolderView yok. Önce menü 5'i çalıştır."); return; }

            // Kapalı dosya: menü 14'ün gizli koyduğu SM_Folder (ya da önceki kurulumdan "Closed" altında)
            var closedRoot = content.Find("Closed");
            Transform smFolder = closedRoot != null ? FindDeep(closedRoot, "SM_Folder") : null;
            if (smFolder == null)
            {
                var art = spot.Find("Art");
                smFolder = art != null ? FindDeep(art, "SM_Folder") : null;
                if (smFolder == null) { Debug.LogError("SM_Folder bulunamadı. Önce menü 14'ü çalıştır."); return; }
                if (closedRoot == null)
                {
                    closedRoot = new GameObject("Closed").transform;
                    closedRoot.SetParent(content, false);
                }
                smFolder.SetParent(closedRoot, true);
                smFolder.gameObject.SetActive(true);
            }
            // Önceki kurulum artıkları
            DestroyChild(closedRoot, "CoverStamp");
            DestroyChild(content, "Open");

            // Greybox dosya kutusu: görüntü kapalı, tıklama kutusu kalsın
            var vis = content.Find("Visual");
            if (vis != null) { var r = vis.GetComponent<Renderer>(); if (r != null) r.enabled = false; }

            float yaw = NearYaw(smFolder.eulerAngles.y);
            var closedRend = smFolder.GetComponent<Renderer>();
            var cb = closedRend != null ? closedRend.bounds : new Bounds(smFolder.position, new Vector3(0.25f, 0.011f, 0.36f));

            // ---- Açık dosya
            var openRoot = new GameObject("Open").transform;
            openRoot.SetParent(content, false);
            var openPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpenPath);
            Bounds page = new Bounds(cb.center + Vector3.up * 0.0055f, new Vector3(0.206f, 0f, 0.293f));
            if (openPrefab != null)
            {
                var open = (GameObject)PrefabUtility.InstantiatePrefab(openPrefab);
                PrefabUtility.UnpackPrefabInstance(open, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                open.transform.SetParent(openRoot, false);
                open.transform.SetPositionAndRotation(smFolder.position, smFolder.rotation);
                page = SubmeshBounds(open, "FormPage") ?? page;
            }
            else Debug.LogWarning("SM_Folder_Open bulunamadı.");
            openRoot.gameObject.SetActive(false);

            // ---- Form: sayfanın üstüne
            var form = content.Find("Form") as RectTransform;
            if (form != null)
            {
                form.position = new Vector3(page.center.x, page.max.y + 0.0006f, page.center.z);
                form.rotation = Quaternion.Euler(90f, yaw, 0f);
                float s = Mathf.Min(page.size.x / 0.22f, page.size.z / 0.30f) * 0.001f;
                form.localScale = Vector3.one * s;
            }

            // ---- Kapak mührü
            var stampGo = new GameObject("CoverStamp", typeof(RectTransform), typeof(Canvas));
            var stampRt = (RectTransform)stampGo.transform;
            stampRt.SetParent(closedRoot, false);
            stampRt.position = new Vector3(cb.center.x, cb.max.y + 0.0006f, cb.center.z);
            stampRt.rotation = Quaternion.Euler(90f, yaw, 0f);
            stampRt.localScale = Vector3.one * 0.001f;
            stampRt.sizeDelta = new Vector2(120f, 60f); // 12 x 6 cm
            stampGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var imgGo = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            var imgRt = (RectTransform)imgGo.transform;
            imgRt.SetParent(stampRt, false);
            imgRt.anchorMin = Vector2.zero; imgRt.anchorMax = Vector2.one;
            imgRt.offsetMin = imgRt.offsetMax = Vector2.zero;
            var img = imgGo.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            imgGo.SetActive(false);

            view.closedModel = closedRoot.gameObject;
            view.openModel = openRoot.gameObject;
            view.formRoot = form != null ? form.gameObject : null;
            view.coverStamp = img;
            view.approveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ApprovePath);
            view.rejectSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RejectPath);
            if (view.approveSprite == null) Debug.LogWarning("Mühür izi görselleri sprite değil; menü 13'ü tekrar çalıştır.");
            if (form != null) form.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);

            // ---- Zarf: tasarımcının modeli, kapağın üst kenarında
            var env = content.Find("Envelope");
            var envPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvelopePath);
            if (env != null && envPrefab != null)
            {
                DestroyChild(env, "SM_Envelope");
                foreach (Transform c in env)
                    if (c.name == "Body" || c.name == "Flap" || c.name == "Seal" || c.name == "Note") c.gameObject.SetActive(false);

                var away = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; // gişe tarafı
                env.position = new Vector3(cb.center.x, cb.max.y + 0.0004f, cb.center.z) + away * (cb.extents.z - 0.035f);
                env.rotation = Quaternion.Euler(0f, yaw - 6f, 0f);
                var e = (GameObject)PrefabUtility.InstantiatePrefab(envPrefab);
                PrefabUtility.UnpackPrefabInstance(e, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                e.transform.SetParent(env, false);
                e.transform.localPosition = Vector3.zero;
                e.transform.localRotation = Quaternion.identity;

                var er = e.GetComponentInChildren<Renderer>();
                var col = env.GetComponent<BoxCollider>();
                if (col == null) col = env.gameObject.AddComponent<BoxCollider>();
                if (er != null)
                {
                    var eb = er.bounds;
                    col.center = env.InverseTransformPoint(eb.center);
                    col.size = new Vector3(eb.size.x / Mathf.Max(0.0001f, env.lossyScale.x), Mathf.Max(0.006f, eb.size.y), eb.size.z);
                }
                // BribeEnvelope ilk konumu Awake'te kaydeder; sahnede yeni konum kalıcı
            }

            // ---- Yakın bakış: form sayfasının tepesinden
            var fp = spot.Find("FocusPoint_Folder");
            var camGo = GameObject.FindWithTag("MainCamera");
            if (fp != null)
            {
                var toward = camGo != null ? camGo.transform.position - page.center : Vector3.back;
                toward.y = 0f; toward.Normalize();
                float h = Mathf.Max(page.size.z, page.size.x / 1.7f) * 0.5f / Mathf.Tan(35f * Mathf.Deg2Rad) * 1.12f;
                fp.position = page.center + Vector3.up * h + toward * 0.03f;
                fp.rotation = Quaternion.LookRotation(page.center - fp.position, Vector3.up);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=lime>[Load Bearing]</color> Tasarım dosyası bağlandı. Form sayfası: {page.size.x * 100f:0.#} x {page.size.z * 100f:0.#} cm. Mühür izi: {(view.approveSprite != null ? "görsel" : "renk")}");
        }

        /// Adı verilen materyali kullanan alt mesh'in dünya sınırları
        static Bounds? SubmeshBounds(GameObject root, string matContains)
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = mr.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(matContains)) continue;
                    var lb = mf.sharedMesh.GetSubMesh(i).bounds;
                    var w = new Bounds(mr.transform.TransformPoint(lb.center), Vector3.zero);
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        w.Encapsulate(mr.transform.TransformPoint(corner));
                    }
                    return w;
                }
            }
            return null;
        }

        /// Açıyı oyuncuya göre okunur kalacak şekilde -90..90 aralığına getir
        static float NearYaw(float y)
        {
            y = Mathf.Repeat(y + 180f, 360f) - 180f;
            if (y > 90f) y -= 180f;
            if (y < -90f) y += 180f;
            return y;
        }

        static void DestroyChild(Transform parent, string name)
        {
            if (parent == null) return;
            var c = parent.Find(name);
            if (c != null) Object.DestroyImmediate(c.gameObject);
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
