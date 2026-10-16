using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/14 - Tasarım modellerini sahneye yerleştir"  (önce menü 13)
    /// - Tasarımcının ofis yerleşimini (LB_OfficeLayout_Reference) "Art_Office" altına kurar; oyuncu gişeye +Z yönünde
    ///   bakacak şekilde gerekiyorsa 180° çevirir (bizim kodumuz bu yönü varsayıyor).
    /// - Kamerayı tasarımcının oyuncu noktasına (CAM_Player) taşır, masaya bakacak şekilde döndürür.
    /// - Oyun objelerimizi (mühürler, monitör, telsiz, gazete, cam, harita, klasör, dosya) tasarımdaki yerlerine taşır.
    ///   Basit nesnelerde greybox görüntüyü gizleyip gerçek modeli objenin içine koyar. Çarpışma kutuları kalır,
    ///   böylece tıklama / vurgulama aynı çalışır.
    /// - Harita, dosya ve kural klasörü şimdilik greybox kalır (sonraki adımlarda modelleri bağlanacak).
    /// Tekrar çalıştırılabilir.
    public static class ArtPlacement
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string LayoutPath = "Assets/Art/3D/Office/LB_OfficeLayout_Reference.fbx";
        const string CamsPath = "Assets/Art/3D/Office/LB_FocusCameras.fbx";

        // Kamera ayarları (tasarımcının CAM_Player noktasına göre)
        const float CameraForward = 0.5f;   // masaya doğru kaç metre yaklaşsın
        const float CameraPitch = 22f;      // başlangıçta kaç derece aşağı baksın (masa + gişe birlikte görünsün)
        const float MinLookUp = 40f;        // gişedeki müşterinin yüzüne bakabilmek için yukarı bakış sınırı

        [MenuItem("Load Bearing/14 - Tasarım modellerini sahneye yerleştir", priority = 14)]
        public static void Run()
        {
            var layoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LayoutPath);
            var camsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CamsPath);
            if (layoutPrefab == null || camsPrefab == null) { Debug.LogError("Tasarım dosyaları bulunamadı. Önce menü 13'ü çalıştır."); return; }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            if (!EditorUtility.DisplayDialog("Tasarım modellerini yerleştir",
                    "Office sahnesi değişecek (greybox oda gizlenir, objeler yeni yerlerine taşınır). Önce commit attıysan devam et.",
                    "Devam", "Vazgeç")) return;

            var log = new List<string>();

            // ---- 1) Daha önce yerleştirilmiş tasarım objelerini temizle (tekrar çalıştırma)
            var oldRoot = GameObject.Find("Art_Office");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            // (objelerin içindeki eski "Art" kopyaları Swap/Park içinde silinir)

            // ---- 2) Yerleşimi kur
            var root = new GameObject("Art_Office").transform;
            var layout = (GameObject)PrefabUtility.InstantiatePrefab(layoutPrefab, root);
            PrefabUtility.UnpackPrefabInstance(layout, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var cams = (GameObject)PrefabUtility.InstantiatePrefab(camsPrefab, root);
            PrefabUtility.UnpackPrefabInstance(cams, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Transform A(string n) => FindDeep(layout.transform, n);
            var camPlayer = FindDeep(cams.transform, "CAM_Player");
            var counterArt = A("SM_CounterGlass");
            if (camPlayer == null || counterArt == null) { Debug.LogError("CAM_Player ya da SM_CounterGlass bulunamadı."); return; }

            // Oyuncu gişeye +Z yönünde bakmalı (müşteri, dosya kayması vb. bu yönü varsayıyor)
            if (counterArt.position.z < camPlayer.position.z)
            {
                root.rotation = Quaternion.Euler(0f, 180f, 0f);
                log.Add("Yerleşim 180° çevrildi (oyuncu +Z'ye bakıyor)");
            }

            Vector3 playerPos = camPlayer.position; // aşağıda masaya doğru yaklaştırılır
            Object.DestroyImmediate(cams); // içindeki kameralar ekrana çizmesin; sadece konum lazımdı

            // ---- 3) Greybox odayı gizle
            Hide("Room"); Hide("Desk/DeskBody"); Hide("Counter/CounterWall"); Hide("Counter/Door");

            // ---- 4) Oyun objelerini tasarımdaki yerlerine taşı
            // Basit nesneler: greybox görüntü gizlenir, gerçek model içine konur
            Swap("Desk/Stamps/StampTool_Approve", A("SM_StampApprove"), log);
            Swap("Desk/Stamps/StampTool_Reject", A("SM_StampReject"), log);
            Swap("Desk/Stamps/InkPad", A("SM_InkPad"), log);
            Swap("Desk/Radio", A("SM_Radio"), log);
            Swap("Desk/Newspaper", A("SM_Newspaper"), log);
            Swap("Desk/Monitor", A("SM_Monitor"), log);
            Swap("Counter/Glass", A("SM_CounterGlass_Glass"), log);

            // Harita / dosya / kural klasörü: şimdilik greybox kalır, tasarım modeli içine gizli olarak konur
            Park("Desk/Map", A("SM_Map"), log);
            Park("Desk/FolderSpot", A("SM_Folder"), log);
            Park("Desk/RuleBinder", A("SM_RuleBinder"), log);

            // ---- 5) Kamera
            var camGo = GameObject.FindWithTag("MainCamera");
            if (camGo != null)
            {
                var deskCenter = Average(new[] { "Desk/Map", "Desk/FolderSpot", "Desk/RuleBinder", "Desk/Monitor", "Desk/Stamps/StampTool_Approve" });
                var fwd = Flat(deskCenter - playerPos).normalized;
                playerPos += fwd * CameraForward; // masaya yaklaş
                camGo.transform.position = playerPos;
                camGo.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(CameraPitch, 0f, 0f);
                log.Add($"Kamera: {playerPos} (masaya {CameraForward} m yaklaştı, {CameraPitch}° aşağı)");

                var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
                if (cfg != null && cfg.lookPitch < MinLookUp)
                {
                    cfg.lookPitch = MinLookUp;
                    EditorUtility.SetDirty(cfg);
                    AssetDatabase.SaveAssets();
                    log.Add($"Yukarı bakış sınırı {MinLookUp}° yapıldı");
                }
            }

            // ---- 6) Yakın bakış noktaları (masadaki objeler taşındı)
            FocusAbove("Desk/Map", "FocusPoint_Map", 0.42f, playerPos);
            FocusAbove("Desk/FolderSpot", "FocusPoint_Folder", 0.33f, playerPos);
            FocusAbove("Desk/RuleBinder", "FocusPoint_Binder", 0.32f, playerPos);
            FocusMonitor(playerPos);

            // ---- 7) Müşteri yolu: gişenin önü ve kapı
            var glassArt = FindDeep(GameObject.Find("Counter/Glass")?.transform, "SM_CounterGlass_Glass") ?? counterArt;
            var doorArt = A("SM_Door");
            var spot = GameObject.Find("Counter/CustomerSpot");
            var door = GameObject.Find("Counter/DoorPoint");
            if (spot != null)
            {
                var away = Flat(glassArt.position - playerPos).normalized;
                var p = glassArt.position + away * 0.45f; p.y = 0f;
                spot.transform.position = p;
                if (door != null && doorArt != null)
                {
                    var dp = doorArt.position + Flat(p - doorArt.position).normalized * 0.6f; dp.y = 0f;
                    door.transform.position = dp;
                }
                log.Add($"Müşteri noktası: {p}");
            }

            // ---- 8) Işık: tavan lambası tasarımdaki lambaya, güneş sağ duvardaki pencereden (not: ~34°)
            var lampArt = A("SM_Office_Lamp");
            var lamp = GameObject.Find("Lighting/CeilingLamp");
            if (lampArt != null && lamp != null) lamp.transform.position = lampArt.position + Vector3.down * 0.3f;
            var sun = GameObject.Find("Lighting/Sun");
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(34f, -90f, 0f);
                var l = sun.GetComponent<Light>();
                if (l != null) { l.color = new Color(1f, 0.976f, 0.914f); l.intensity = 1.2f; }
            }
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.45f);

            // Kontrol: monitör oyuncunun sağında olmalı (GDD)
            var mon = GameObject.Find("Desk/Monitor");
            if (mon != null && camGo != null && Vector3.Dot(mon.transform.position - playerPos, camGo.transform.right) < 0f)
                Debug.LogWarning("[Load Bearing] Monitör oyuncunun SOLUNDA görünüyor. Tasarım dosyasının yönünü kontrol edelim (ekran görüntüsü at).");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Tasarım modelleri yerleştirildi.\n - " + string.Join("\n - ", log));

            // Duvar yazıları / etiketler yüzeyle çakışmasın (menü 16 ile aynı)
            DecalFix.Run();
        }

        // ------------------------------------------------------------------ yardımcılar

        static readonly HashSet<string> KeepVisible = new HashSet<string> { "Page", "MapCanvas", "Content", "Form", "CoverHinge", "AlertFrame", "AlertLight", "Note" };

        /// Oyun objesini modelin yerine taşı, greybox görüntüyü gizle (collider kalır), modeli içine "Art" olarak koy
        static void Swap(string ourPath, Transform art, List<string> log)
        {
            var our = GameObject.Find(ourPath);
            if (our == null || art == null) { log.Add($"ATLANDI: {ourPath} / {(art ? art.name : "model yok")}"); return; }
            RemoveOldArt(our.transform);
            our.transform.position = art.position;
            foreach (var r in our.GetComponentsInChildren<Renderer>(true))
                if (!UnderKeep(r.transform, our.transform)) r.enabled = false;
            var holder = new GameObject("Art").transform;
            holder.SetParent(our.transform, false);
            art.SetParent(holder, true);
            log.Add($"{ourPath} ← {art.name}");
        }

        /// Oyun objesini modelin yerine taşı ama greybox kalsın; model içine gizli konur (sonraki adımda bağlanacak)
        static void Park(string ourPath, Transform art, List<string> log)
        {
            var our = GameObject.Find(ourPath);
            if (our == null || art == null) { log.Add($"ATLANDI: {ourPath}"); return; }
            RemoveOldArt(our.transform);
            our.transform.position = art.position;
            var holder = new GameObject("Art").transform;
            holder.SetParent(our.transform, false);
            art.SetParent(holder, true);
            holder.gameObject.SetActive(false);
            log.Add($"{ourPath} taşındı (greybox kaldı)");
        }

        static void RemoveOldArt(Transform our)
        {
            var old = our.Find("Art");
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        static bool UnderKeep(Transform t, Transform stop)
        {
            for (var p = t; p != null && p != stop; p = p.parent)
                if (KeepVisible.Contains(p.name)) return true;
            return false;
        }

        static void Hide(string path)
        {
            var go = GameObject.Find(path);
            if (go != null) go.SetActive(false);
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

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Vector3 Average(string[] paths)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var p in paths)
            {
                var go = GameObject.Find(p);
                if (go == null) continue;
                sum += go.transform.position; n++;
            }
            return n > 0 ? sum / n : Vector3.zero;
        }

        static void FocusAbove(string objPath, string fpName, float height, Vector3 playerPos)
        {
            var obj = GameObject.Find(objPath);
            var fp = obj != null ? obj.transform.Find(fpName) : null;
            if (fp == null) return;
            var center = obj.transform.position + Vector3.up * 0.02f;
            var toward = Flat(playerPos - center).normalized;
            fp.position = center + Vector3.up * height + toward * 0.05f;
            fp.rotation = Quaternion.LookRotation(center - fp.position, Vector3.up);
        }

        static void FocusMonitor(Vector3 playerPos)
        {
            var mon = GameObject.Find("Desk/Monitor");
            var fp = mon != null ? mon.transform.Find("FocusPoint_Monitor") : null;
            if (fp == null) return;
            var screen = FindDeep(mon.transform, "SM_Monitor_Screen");
            Vector3 c = screen != null && screen.GetComponent<Renderer>() != null
                ? screen.GetComponent<Renderer>().bounds.center
                : mon.transform.position + Vector3.up * 0.2f;
            var toward = Flat(playerPos - c).normalized;
            fp.position = c + toward * 0.48f + Vector3.up * 0.02f;
            fp.rotation = Quaternion.LookRotation(c - fp.position, Vector3.up);
        }
    }
}
