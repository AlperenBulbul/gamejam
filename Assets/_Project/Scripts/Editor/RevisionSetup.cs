using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/22 - Tasarım revizelerini yükle (masa + harita)"  (önce menü 13, 14, 18, 21)
    /// Tasarımcının OKU_BENI_Kurulum.md, OKU_BENI_Harita_Revize.md ve OKU_BENI_Revize2.md notlarına göre
    /// (Revize altındaki bütün paketler sırayla kopyalanır):
    ///  1. indirilenler/Assets/Revize/.../Assets/Art altındaki her şeyi Assets/Art'a kopyalar (aynı adlılar üzerine)
    ///  2. Menü 13'ü çalıştırır (yeni palet, sıra ekranı / yağmur / gökyüzü materyalleri, yeni FBX ayarları)
    ///  3. Sahne: yeni damgalar, sandalye, sıra ekranı (numara TMP ile), yeni duyuru panosu ve fiş makinesi,
    ///     afiş ve takvimin yeni yeri, yeni şehir (eski şehir parçaları gizlenir), yağmur (kapalı), gökyüzü
    ///  4. Kameranın uzak kesme mesafesi 1200 m (gök kubbesi 650 m); şantiye maketi x = 5000'e taşınır
    /// Konumlar: tasarımcının Blender koordinatı (x, y, z) -> Unity (x, z, y).
    /// Tekrar çalıştırılabilir (eklenenler Art_Office/Revize2 altında; eski parçalar silinmez, gizlenir).
    public static class RevisionSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string Source = "../../indirilenler/Assets/Revize"; // Assets klasörüne göre
        const string Office = "Assets/Art/3D/Office";
        const string Props = "Assets/Art/3D/Props";

        /// Kural klasörü sayfa 1 (harita revizesi: fay tehlike bölgesi asetatta)
        public const string Rule1Body =
            "• FAY HATTI: Kuzey Fayı tehlike bölgesine (asetatta kırmızı taralı alan) değen arsaya yapı yapılamaz.\n\n" +
            "• DERE YATAĞI: Kara Dere taşkın alanının (asetatta mavi taralı alan) içindeki arsaya yapı yapılamaz.\n\n" +
            "<size=85%><i>Haritada Boşluk tuşu: nehir yatağı ve fay hattı katmanını göster / gizle.</i></size>";

        [MenuItem("Load Bearing/22 - Tasarım revizelerini yükle (masa + harita)", priority = 22)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var log = new List<string>();

            // ---- 1) Dosyaları kopyala
            // Revize klasörü sadece dosyaları ilk getiren bilgisayarda var. Dosyalar zaten Assets/Art'ta (git ile geliyor),
            // klasör yoksa kopyalama atlanır ve kurulum projedeki dosyalarla devam eder.
            if (!CopyFiles(log))
                Debug.LogWarning("[Load Bearing] Revize klasörü yok, kopyalama atlandı. Projedeki Assets/Art dosyaları kullanılıyor.\n" + string.Join("\n", log));

            // ---- 2) İçe aktarma ayarları ve materyaller (menü 13)
            ArtImportSetup.Run();

            var artOffice = GameObject.Find("Art_Office");
            var layout = artOffice != null ? FindDeep(artOffice.transform, "LB_OfficeLayout_Reference") : null;
            if (artOffice == null || layout == null) { Debug.LogError("Art_Office / LB_OfficeLayout_Reference yok. Önce menü 14."); return; }

            var old = artOffice.transform.Find("Revize2");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Revize2").transform;
            group.SetParent(artOffice.transform, false);

            // ---- 3a) Damgalar (baret saplı)
            ReplaceStamp("Desk/Stamps/StampTool_Approve", $"{Props}/SM_StampApprove.fbx", log);
            ReplaceStamp("Desk/Stamps/StampTool_Reject", $"{Props}/SM_StampReject.fbx", log);

            // ---- 3b) Sandalye: oyuncunun altında, önü masaya
            Place($"{Office}/SM_Chair.fbx", group, Blender(0f, -0.52f, 0f), log);

            // ---- 3c) Sıra ekranı + numara
            var queue = Place($"{Office}/SM_Cust_QueueDisplay.fbx", group, Blender(0.95f, 3.60f, 1.28f), log);
            if (queue != null) SetupQueueNumber(queue, log);

            // Duyuru panosu: yeni model, yeni yer. Fiş makinesi: yeni model, aynı yer.
            var oldNotice = FindDeep(layout, "SM_Cust_NoticeBox");
            Hide(oldNotice);
            Place($"{Office}/SM_Cust_NoticeBox.fbx", group, Blender(-1.60f, 3.60f, 1.05f), log); // revize 3: vatandaş duyuru panosu, 90 x 68 cm

            // Revize 3: ofis mantar panosu ve duvar saati yenilendi (yerleri aynı)
            ReplaceLayoutChild(layout, group, "SM_CorkBoard", $"{Office}/SM_CorkBoard.fbx", log);
            var clock = ReplaceLayoutChild(layout, group, "SM_WallClock", $"{Office}/SM_WallClock.fbx", log);
            if (clock != null) SetupClock(clock, log);
            var oldTicket = FindDeep(layout, "SM_Cust_TicketMachine");
            if (oldTicket != null)
            {
                var t = Place($"{Office}/SM_Cust_TicketMachine.fbx", group, oldTicket.position, log);
                if (t != null) t.rotation = oldTicket.rotation;
                Hide(oldTicket);
            }

            // ---- 3d) Afiş (harita revizesinde yenilendi: yazılar dokuda) ve takvim yeni yerlerinde (sağ duvar)
            var oldPoster = FindDeep(layout, "SM_Poster_Deprem");
            var poster = Place($"{Office}/SM_Poster_Deprem.fbx", group, Blender(2.176f, 0.58f, 1.15f), log);
            if (poster != null)
            {
                if (oldPoster != null) poster.rotation = oldPoster.rotation;
                Hide(oldPoster);
            }
            else Move(layout, "SM_Poster_Deprem", Blender(2.176f, 0.58f, 1.15f), log);
            Move(layout, "SM_Calendar", Blender(2.184f, -1.05f, 1.35f), log);

            // ---- 3e) Şehir: eski parçalar gizlenir, yenisi ofisle aynı yere
            int hidden = 0;
            foreach (Transform c in layout)
                if (c.name.StartsWith("SM_City_")) { c.gameObject.SetActive(false); hidden++; }
            var city = Place($"{Office}/SM_City_Outside.fbx", group, Vector3.zero, log);
            var skyRenderers = new List<Renderer>();
            if (city != null)
            {
                foreach (var r in city.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (r.sharedMaterials.Any(m => m != null && m.name == "M_Sky")) skyRenderers.Add(r);
                }
                log.Add($"Şehir yenilendi ({hidden} eski parça gizlendi, gök: {skyRenderers.Count} parça)");
            }

            // ---- 3f) Yağmur (varsayılan kapalı)
            var rainGlass = Place($"{Office}/FX_RainGlass.fbx", group, Vector3.zero, log);
            var rainSheet = Place($"{Office}/FX_RainSheet.fbx", group, Vector3.zero, log);
            var rainRoots = new List<GameObject>();
            var streaks = new List<Renderer>();
            var sheets = new List<Renderer>();
            foreach (var root in new[] { rainGlass, rainSheet })
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    if (r.sharedMaterials.Any(m => m != null && m.name == "M_RainGlass_Streaks")) streaks.Add(r);
                    if (r.sharedMaterials.Any(m => m != null && m.name == "M_RainSheet")) sheets.Add(r);
                }
                rainRoots.Add(root.gameObject);
                root.gameObject.SetActive(false);
            }

            // ---- Ortam denetleyicisi (gökyüzü + yağmur)
            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var env = systems.GetComponent<EnvironmentController>();
                if (env == null) env = systems.AddComponent<EnvironmentController>();
                env.skyRenderers = skyRenderers.ToArray();
                env.skyDay1 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/3D/Textures/T_Sky_Day1.png");
                env.skyDay2 = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/3D/Textures/T_Sky_Day2.png");
                env.skyRain = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/3D/Textures/T_Sky_Rain.png");
                env.rainRoots = rainRoots.ToArray();
                env.streakRenderers = streaks.ToArray();
                env.sheetRenderers = sheets.ToArray();
                env.sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                .FirstOrDefault(l => l.type == LightType.Directional);
                EditorUtility.SetDirty(env);
            }

            // ---- Kural sayfaları: tasarlanmış görseller (revize 3)
            string[] ruleIds = { "1_Konum", "2_KatSiniri", "3_Kase", "4_Zemin" };
            for (int i = 0; i < ruleIds.Length; i++)
            {
                var rd = AssetDatabase.LoadAssetAtPath<RuleData>($"Assets/_Project/Data/Rules/Rule_{ruleIds[i]}.asset");
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtImportSetup.RulePage(i + 1));
                if (rd == null || tex == null) continue;
                rd.pageTexture = tex;
                EditorUtility.SetDirty(rd);
            }
            log.Add("Kural sayfaları tasarım görselleriyle değiştirildi");

            // ---- Kural klasörü kapalı modeli (revize 3: kapak dokulu). Menü 19 aşağıda yeniden çalışır.
            var closedRoot = GameObject.Find("Desk/RuleBinder/Closed");
            var binderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Props}/SM_RuleBinder.fbx");
            if (closedRoot != null && binderPrefab != null)
            {
                var oldB = FindDeep(closedRoot.transform, "SM_RuleBinder");
                if (oldB != null)
                {
                    var p = oldB.position; var r = oldB.rotation;
                    Object.DestroyImmediate(oldB.gameObject);
                    var nb = (GameObject)PrefabUtility.InstantiatePrefab(binderPrefab);
                    nb.transform.SetParent(closedRoot.transform, false);
                    nb.transform.SetPositionAndRotation(p, r);
                    log.Add("Kural klasörü kapağı yenilendi");
                }
            }

            // ---- Kural sayfası 1: fay tehlike bölgesi
            var rule1 = AssetDatabase.LoadAssetAtPath<RuleData>("Assets/_Project/Data/Rules/Rule_1_Konum.asset");
            if (rule1 != null && rule1.body != Rule1Body)
            {
                rule1.body = Rule1Body;
                EditorUtility.SetDirty(rule1);
                log.Add("Kural 1 metni güncellendi (fay tehlike bölgesi)");
            }

            // ---- Monitör: yatay model (16:9 ekran, kontroller tek sıra). Sahnedeki eski modelin yerine.
            ReplaceMonitor(log);

            // ---- Gazete: tıklayınca yakın bakış + "GÜNE BAŞLA" düğmesi (eskiden tıklayınca direkt başlıyordu)
            var paper = GameObject.Find("Desk/Newspaper");
            if (paper != null)
            {
                var pit = paper.GetComponent<Interactable>();
                if (pit != null)
                {
                    for (int i = pit.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                        if (pit.onClick.GetPersistentMethodName(i) == "BeginWorking") UnityEventTools.RemovePersistentListener(pit.onClick, i);
                    EditorUtility.SetDirty(pit);
                }
                if (paper.GetComponent<NewspaperView>() == null) paper.AddComponent<NewspaperView>();
                log.Add("Gazete: yakın bakış + GÜNE BAŞLA düğmesi");
            }

            // ---- 4) Kamera uzak kesme + şantiye maketi daha uzağa
            var main = GameObject.FindWithTag("MainCamera");
            if (main != null) { var mc = main.GetComponent<Camera>(); if (mc != null) mc.farClipPlane = 1200f; }
            var feed = GameObject.Find("FeedWorld");
            if (feed != null) feed.transform.position = new Vector3(5000f, 0f, 0f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            // Monitör sistemi yeni modele göre yeniden kurulur (16:9 Render Texture, ekran, düğmeler, ışıklar)
            MonitorSetup.Run();
            // Kural klasörü yeni modellerle yeniden bağlanır (açık model, sayfa, YENİ sekmesi, parıltı)
            BinderArtSetup.Run();
            // Masa düzeni (kural klasörü sağa ve biraz öne, dosya + mühürler öne)
            DeskLayout.Run();

            Debug.Log("<color=lime>[Load Bearing]</color> Tasarım revizeleri yüklendi.\n - " + string.Join("\n - ", log));
        }

        /// Ofis yerleşimindeki eski parçayı gizleyip aynı yere yeni dosyadaki modeli koyar
        static Transform ReplaceLayoutChild(Transform layout, Transform group, string name, string fbx, List<string> log)
        {
            var old = FindDeep(layout, name);
            if (old == null) { log.Add($"BULUNAMADI: {name}"); return null; }
            var t = Place(fbx, group, old.position, log);
            if (t != null) { t.rotation = old.rotation; Hide(old); }
            return t;
        }

        static void SetupClock(Transform clock, List<string> log)
        {
            var wc = clock.GetComponent<WallClock>();
            if (wc == null) wc = clock.gameObject.AddComponent<WallClock>();
            wc.hourHand = FindDeep(clock, "SM_WallClock_HourHand");
            wc.minuteHand = FindDeep(clock, "SM_WallClock_MinuteHand");
            wc.secondHand = FindDeep(clock, "SM_WallClock_SecondHand");
            var glass = FindDeep(clock, "SM_WallClock_Glass");
            if (glass != null) foreach (var r in glass.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Add($"Saat ibreleri: {(wc.hourHand != null && wc.minuteHand != null ? "bağlandı" : "BULUNAMADI")}");
        }

        static void ReplaceMonitor(List<string> log)
        {
            var art = GameObject.Find("Desk/Monitor/Art");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Props}/SM_Monitor.fbx");
            if (art == null || prefab == null) { log.Add("ATLANDI: monitör"); return; }
            var old = FindDeep(art.transform, "SM_Monitor");
            Vector3 pos = old != null ? old.position : art.transform.position;
            Quaternion rot = old != null ? old.rotation : art.transform.rotation;
            bool firstTime = art.transform.Find("_Revize2") == null;
            if (firstTime) pos += Vector3.left * 0.04f; // genişleyen kasa masanın sağ kenarından taşmasın (tasarım notu)
            for (int i = art.transform.childCount - 1; i >= 0; i--)
                if (art.transform.GetChild(i).name != "_Revize2") Object.DestroyImmediate(art.transform.GetChild(i).gameObject);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(art.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);
            if (firstTime) new GameObject("_Revize2").transform.SetParent(art.transform, false);
            log.Add("Monitör yatay modelle değiştirildi");
        }

        // ------------------------------------------------------------------ dosyalar

        static bool CopyFiles(List<string> log)
        {
            string src = Path.GetFullPath(Path.Combine(Application.dataPath, Source));
            if (!Directory.Exists(src)) { log.Add(src); return false; }
            // Revize/<paket>/Assets/Art klasörlerini bul (paket adı değişebilir)
            var artDirs = Directory.GetDirectories(src, "Art", SearchOption.AllDirectories)
                                   .Where(d => Path.GetFileName(Path.GetDirectoryName(d)) == "Assets")
                                   .OrderBy(NewestFile).ThenBy(d => d, System.StringComparer.Ordinal) // eski paket önce, yenisi üzerine yazar
                                   .ToList();
            if (artDirs.Count == 0) { log.Add($"{src} içinde Assets/Art yok"); return false; }

            // Aynı dosya birden fazla pakette varsa EN YENİ tarihli olanı kullanılır (paket sırasına bağlı değil)
            var newest = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var artDir in artDirs)
                foreach (var f in Directory.GetFiles(artDir, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(f);
                    if (name.StartsWith(".") || name.EndsWith(".meta")) continue;
                    string rel = f.Substring(artDir.Length).TrimStart('/', '\\');
                    if (!newest.TryGetValue(rel, out var cur) || File.GetLastWriteTimeUtc(f) > File.GetLastWriteTimeUtc(cur)) newest[rel] = f;
                }

            string dstRoot = Path.Combine(Application.dataPath, "Art");
            int n = 0;
            foreach (var kv in newest)
            {
                string dst = Path.Combine(dstRoot, kv.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(kv.Value, dst, true);
                n++;
            }
            AssetDatabase.Refresh();
            log.Add($"{n} dosya Assets/Art'a kopyalandı");
            return true;
        }

        /// Paketin en yeni dosyasının tarihi (klasör tarihleri açarken değişebildiği için dosyalara bakılır)
        static System.DateTime NewestFile(string dir)
        {
            var t = System.DateTime.MinValue;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                var w = File.GetLastWriteTimeUtc(f);
                if (w > t) t = w;
            }
            return t;
        }

        // ------------------------------------------------------------------ sahne

        /// Blender (x sağ, y gişeye doğru, z yukarı) -> Unity dünya
        static Vector3 Blender(float x, float y, float z) => new Vector3(x, z, y);

        static Transform Place(string path, Transform parent, Vector3 worldPos, List<string> log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { log.Add($"BULUNAMADI: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = parent.rotation; // tasarımda dönüş 0
            log.Add($"{prefab.name} -> {worldPos}");
            return go.transform;
        }

        static void ReplaceStamp(string toolPath, string fbx, List<string> log)
        {
            var tool = GameObject.Find(toolPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (tool == null || prefab == null) { log.Add($"ATLANDI: {toolPath}"); return; }
            var art = tool.transform.Find("Art");
            if (art == null) { art = new GameObject("Art").transform; art.SetParent(tool.transform, false); }
            Quaternion rot = tool.transform.rotation;
            if (art.childCount > 0) rot = art.GetChild(0).rotation;
            for (int i = art.childCount - 1; i >= 0; i--) Object.DestroyImmediate(art.GetChild(i).gameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(art, false);
            go.transform.position = tool.transform.position; // pivot alt orta = mührün taban noktası
            go.transform.rotation = rot;

            // Tıklama kutusu: yeni modelin boyutunda (eski greybox silindirin collider'ı kapanır)
            var vis = tool.transform.Find("Visual");
            if (vis != null) foreach (var c in vis.GetComponents<Collider>()) c.enabled = false;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                var box = tool.GetComponent<BoxCollider>();
                if (box == null) box = tool.AddComponent<BoxCollider>();
                box.center = tool.transform.InverseTransformPoint(b.center);
                box.size = b.size;
            }
            log.Add($"{prefab.name} yenilendi");
        }

        static void SetupQueueNumber(Transform display, List<string> log)
        {
            var num = FindDeep(display, "SM_Cust_QueueDisplay_Number");
            if (num == null) { log.Add("UYARI: SM_Cust_QueueDisplay_Number yok"); return; }
            var nr = num.GetComponent<Renderer>();
            var b = nr != null ? nr.bounds : new Bounds(num.position, new Vector3(0.3f, 0.15f, 0.01f));
            if (nr != null) nr.enabled = false; // basılı "001" yerine kodla yazılan numara

            var go = new GameObject("NumberText", typeof(RectTransform), typeof(TextMeshPro));
            go.transform.SetParent(display, false);
            // Ekranın yüzü oyuncuya (-Z) bakıyor; yazı yüzeyin biraz önünde
            go.transform.position = new Vector3(b.center.x, b.center.y, b.min.z - 0.005f);
            go.transform.rotation = Quaternion.identity;
            var tmp = go.GetComponent<TextMeshPro>();
            tmp.text = "000";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.16f, 0.08f);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f;
            tmp.fontSizeMax = 3f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.characterSpacing = 6f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(Mathf.Max(0.1f, b.size.x * 1.1f), Mathf.Max(0.06f, b.size.y * 1.2f));

            var qd = display.gameObject.AddComponent<QueueDisplay>();
            qd.number = tmp;
            log.Add("Sıra ekranı numarası kodla yazılıyor");
        }

        static void Move(Transform layout, string name, Vector3 world, List<string> log)
        {
            var t = FindDeep(layout, name);
            if (t == null) { log.Add($"BULUNAMADI: {name}"); return; }
            Undo.RecordObject(t, "Move");
            t.position = world;
            log.Add($"{name} taşındı -> {world}");
        }

        static void Hide(Transform t)
        {
            if (t != null) t.gameObject.SetActive(false);
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
