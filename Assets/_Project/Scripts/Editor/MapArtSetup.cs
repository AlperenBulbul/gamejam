using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/15 - Tasarım haritasını bağla"  (önce menü 13 ve 14)
    /// - Greybox haritayı gizler, tasarımcının SM_Map modelini gösterir (iğneler şimdilik gizli)
    /// - Asetatı (SM_MapOverlay: fay + taşkın alanı) haritanın üstüne hizalı koyar; Boşluk ile açılıp kapanır
    /// - Tıklama kutusunu yeni haritanın boyutuna göre ayarlar
    /// - Müşterilerin harita koordinatlarını tasarımcının tablosuna göre yazar (4800 x 2640)
    /// Tekrar çalıştırılabilir.
    public static class MapArtSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string LayoutPath = "Assets/Art/3D/Office/LB_OfficeLayout_Reference.fbx";
        const string OverlayPath = "Assets/Art/3D/Props/SM_MapOverlay.fbx";

        // Tasarımcının teslim notu, bölüm 4 (piksel, sol üst = 0,0)
        static readonly (string id, Vector2 px)[] Pixels =
        {
            ("C1", new Vector2(744, 760)), ("C2", new Vector2(2876, 640)), ("C3", new Vector2(3600, 932)), // 47: fay hattı (1. gün yanlış)
            ("C4", new Vector2(3200, 2184)), ("C5", new Vector2(1240, 1664)), ("C6", new Vector2(2028, 248)), // 3: dere yatağı (2. gün yanlış)
            ("C7", new Vector2(4180, 540)),
        };

        [MenuItem("Load Bearing/15 - Tasarım haritasını bağla", priority = 15)]
        public static void Run()
        {
            // İğneler için harita mesh'inin UV'leri okunabilmeli
            if (AssetImporter.GetAtPath(LayoutPath) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var mapGo = GameObject.Find("Desk/Map");
            var holder = mapGo != null ? mapGo.transform.Find("Art") : null;
            if (holder == null) { Debug.LogError("Desk/Map/Art yok. Önce menü 14'ü çalıştır."); return; }
            holder.gameObject.SetActive(true);
            var smMap = FindDeep(holder, "SM_Map");
            var rend = smMap != null ? smMap.GetComponent<Renderer>() : null;
            if (rend == null) { Debug.LogError("SM_Map modeli bulunamadı."); return; }

            // İğneler: onaylanınca görünecek (Adım 14)
            foreach (Transform c in smMap)
                if (c.name.StartsWith("SM_MapPin")) c.gameObject.SetActive(false);

            // Greybox harita kapat (görsel + eski tıklama kutusu)
            var canvas = mapGo.transform.Find("MapCanvas");
            if (canvas != null) canvas.gameObject.SetActive(false);
            var vis = mapGo.transform.Find("Visual");
            if (vis != null) vis.gameObject.SetActive(false);

            // Asetat: SM_Map ile aynı pivot (tasarımcı notu), çocuğu olarak hizalı
            var oldOv = FindDeep(smMap, "Overlay_Asetat");
            if (oldOv != null) Object.DestroyImmediate(oldOv.gameObject);
            GameObject ov = null;
            var ovPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath);
            if (ovPrefab != null)
            {
                ov = (GameObject)PrefabUtility.InstantiatePrefab(ovPrefab);
                PrefabUtility.UnpackPrefabInstance(ov, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ov.name = "Overlay_Asetat";
                ov.transform.SetParent(smMap, false);
                ov.transform.localPosition = Vector3.zero;
                ov.transform.localRotation = Quaternion.identity;
                ov.transform.localScale = Vector3.one;
                foreach (var r in ov.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ov.SetActive(false);
            }
            else Debug.LogWarning("SM_MapOverlay bulunamadı; asetat eklenmedi.");

            // Tıklama kutusu: yeni harita boyutunda
            var b = rend.bounds;
            var col = mapGo.GetComponent<BoxCollider>();
            if (col == null) col = mapGo.AddComponent<BoxCollider>();
            col.center = mapGo.transform.InverseTransformPoint(b.center);
            col.size = new Vector3(b.size.x, Mathf.Max(0.01f, b.size.y), b.size.z);

            // MapView
            var mv = mapGo.GetComponent<MapView>();
            if (mv == null) mv = mapGo.AddComponent<MapView>();
            mv.mapRenderer = rend;
            mv.mapCanvas = null;
            mv.overlay = ov;
            mv.mapPixelSize = new Vector2(4800f, 2640f);
            EditorUtility.SetDirty(mv);

            // Müşteri koordinatları
            int n = 0;
            foreach (var g in AssetDatabase.FindAssets("t:CustomerData", new[] { "Assets/_Project/Data/Customers" }))
            {
                var c = AssetDatabase.LoadAssetAtPath<CustomerData>(AssetDatabase.GUIDToAssetPath(g));
                if (c == null) continue;
                foreach (var p in Pixels)
                    if (p.id == c.id) { c.mapPixel = p.px; EditorUtility.SetDirty(c); n++; }
            }
            AssetDatabase.SaveAssets();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=lime>[Load Bearing]</color> Tasarım haritası bağlandı. Asetat: {(ov != null ? "var (Boşluk)" : "YOK")}. {n} müşteri koordinatı güncellendi. Harita boyutu: {b.size.x:0.00} x {b.size.z:0.00} m");
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
