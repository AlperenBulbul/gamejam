using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoadBearing.EditorTools
{
    /// Üst menü: "Load Bearing"
    ///  1 - Klasörleri ve verileri oluştur  (D-01 + D-02)
    ///  2 - Greybox Office sahnesini kur     (D-01 + D-03/04 test sahnesi)
    ///  3 - Validator testi                   (D-02 bitti sayılır: 7/7)
    /// Var olan veri varlıklarının değerlerine DOKUNMAZ; sadece eksik olanları oluşturur.
    public static class LoadBearingSetup
    {
        const string Root = "Assets/_Project";
        const string Data = Root + "/Data";
        const string ScenePath = Root + "/Scenes/Office.unity";

        // =====================================================================
        // 1) KLASÖRLER + VERİ
        // =====================================================================

        [MenuItem("Load Bearing/1 - Klasörleri ve verileri oluştur", priority = 1)]
        public static void CreateFoldersAndData()
        {
            CreateFolders();
            var cfg = CreateConfig();
            var rules = CreateRules();
            var customers = CreateCustomers();
            CreateDays(customers, rules);
            CreateDialogue();
            CreateHeadlines();
            CreateEndings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=lime>[Load Bearing]</color> Klasörler ve veri varlıkları hazır.");
            RunValidatorTest();
        }

        static void CreateFolders()
        {
            string[] folders =
            {
                Root + "/Scenes", Root + "/Prefabs", Root + "/RenderTextures", Root + "/Materials",
                Root + "/Materials/Greybox",
                Data, Data + "/Customers", Data + "/Days", Data + "/Rules", Data + "/ShopItems",
                Data + "/Headlines", Data + "/Endings", Data + "/Dialogue",
                Root + "/Scripts/Speech", Root + "/Scripts/Customers", Root + "/Scripts/Flow", Root + "/Scripts/Audio",
                "Assets/Art", "Assets/Audio",
            };
            foreach (var f in folders) EnsureFolder(f);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        /// Varsa yükler (created=false), yoksa oluşturur (created=true)
        static T GetOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            created = a == null;
            if (created)
            {
                a = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(a, path);
            }
            return a;
        }

        static GameConfig CreateConfig()
        {
            var cfg = GetOrCreate<GameConfig>(Data + "/GameConfig.asset", out _);
            return cfg; // varsayılan değerler GameConfig.cs içinde dokümanla birebir
        }

        static List<RuleData> CreateRules()
        {
            var list = new List<RuleData>();
            RuleData R(string id, int day, string title, string body)
            {
                var r = GetOrCreate<RuleData>($"{Data}/Rules/Rule_{id}.asset", out bool created);
                if (created)
                {
                    r.id = id; r.dayIntroduced = day; r.title = title; r.body = body;
                    EditorUtility.SetDirty(r);
                }
                list.Add(r);
                return r;
            }

            R("1_Konum", 1, "Yasak bölgeler",
                RevisionSetup.Rule1Body);
            R("2_KatSiniri", 1, "Kat sınırı",
                "Arsa alanına göre en fazla kat:\n\n" +
                "• 200 m² altı: en fazla 2 kat\n• 200 – 399 m²: en fazla 4 kat\n• 400 m² ve üstü: en fazla 6 kat");
            R("3_Kase", 2, "Mühendis kaşesi",
                "Başvurudaki ruhsat numarası mühendis sicilinde BİREBİR bulunmalıdır.\n\n" +
                "MÜHENDİS SİCİLİ\nİMO-1047\nİMO-2213\nİMO-3390\nİMO-4521\nİMO-5876\nİMO-6102");
            R("4_Zemin", 2, "Zemin etüdü",
                "5 ve üzeri katlı yapılar için zemin etüdü zorunludur.\n\n" +
                "• Zemin etüdü yoksa: RET\n• Zemin YUMUŞAK ise: RET\n• Zemin SAĞLAM ise: kurala uygun");
            return list;
        }

        static List<CustomerData> CreateCustomers()
        {
            var list = new List<CustomerData>();
            void C(string id, int day, string name, string role, int ada, int parsel, int area, int floors, string use,
                   string lic, SoilType soil, bool flood, bool fault, int bribe, float patience, float pitch, BodyType body,
                   string greeting, string approve, string reject, bool expected)
            {
                var c = GetOrCreate<CustomerData>($"{Data}/Customers/{id}.asset", out bool created);
                if (created)
                {
                    c.id = id; c.day = day; c.displayName = name; c.role = role;
                    c.ada = ada; c.parsel = parsel; c.areaM2 = area; c.floors = floors; c.use = use;
                    c.licenseNo = lic; c.soil = soil; c.inFloodZone = flood; c.onFaultLine = fault;
                    c.bribe = bribe; c.patienceSeconds = patience; c.voicePitch = pitch; c.body = body;
                    c.greeting = greeting; c.approveLine = approve; c.rejectLine = reject; c.expectedValid = expected;
                    EditorUtility.SetDirty(c);
                }
                list.Add(c);
            }

            //  id    gün  isim                       rol                              ada parsel m²  kat kullanım         ruhsat      zemin            taşkın fay   rüşvet sabır perde
            C("C1", 1, "Hasan Yıldız", "Emekli", 12, 4, 150, 2, "Konut", "İMO-1047", SoilType.None, false, false, 0, 120, 0.85f, BodyType.Male,
                "Merhaba evladım. Torunlar gelince sığalım diye küçük bir ev.", "Allah razı olsun evladım!", "Ama... bütün birikimimizi koyduk.", true);
            C("C2", 1, "Mahmut Aydın", "Küçük müteahhit", 7, 19, 320, 4, "Konut", "İMO-2213", SoilType.None, false, false, 0, 90, 1.0f, BodyType.Male,
                "Merhaba. Dereye yakın ama merak etme, her şey hesaplı.", "Teşekkürler, ekip yarın sahada.", "Ölçüleri kendin kontrol et istersen. Pekâlâ.", true);
            C("C3", 1, "Selin Kaya", "Aceleci yatırımcı", 3, 8, 260, 3, "Dükkân + konut", "İMO-3390", SoilType.None, true, false, 0, 60, 1.25f, BodyType.Female,
                "Acelem var. Damga, imza, bitti.", "Harika. Zaman paradır.", "Ciddi misin. Bu arsaya ne kadar para verdim biliyor musun.", false);
            C("C4", 2, "Murat Demir", "Kalabalık bir ailenin babası", 15, 2, 180, 4, "Konut", "İMO-2213", SoilType.None, false, false, 0, 90, 0.95f, BodyType.Male,
                "Beş çocuk, bir babaanne. Dört kat lazım bize.", "Sağ ol kardeşim, sağ ol!", "Peki biz nereye sığacağız...", false);
            C("C5", 2, "Fatma Çelik", "Yalnız yaşayan yaşlı kadın", 9, 11, 140, 1, "Konut", "İMO-4512", SoilType.None, false, false, 0, 110, 1.35f, BodyType.Female,
                "Evladım, tek katlı küçük bir ev. Mühendis bey her şeyi hazırladı.", "Çok teşekkür ederim evladım.", "Mühendis bey öyle dememişti ama...", false);
            C("C6", 2, "Okan Yalın", "Yalın Yapı A.Ş. temsilcisi", 21, 6, 420, 5, "Konut", "İMO-3390", SoilType.Solid, false, false, 0, 80, 1.05f, BodyType.Male,
                "Yalın Yapı. Zemin etüdü ve kaşe ekte. Her şey tam.", "Güzel. İşimizi biliriz.", "Bunu itiraz yoluyla çözeriz.", true);
            C("C7", 2, "İsmail Zirve", "Zirve Holding sahibi", 1, 1, 450, 6, "Rezidans", "İMO-5876", SoilType.Solid, false, true, 1500, 100, 0.8f, BodyType.Male,
                "İsmail Zirve. Dosyam burada, bir bakıverin.", "Akıllı adamsın. Seni unutmam.", "Bu şehirde kimin sözü geçer, göreceksin.", false);
            return list;
        }

        static void CreateDays(List<CustomerData> customers, List<RuleData> rules)
        {
            var d1 = GetOrCreate<DayData>($"{Data}/Days/Day1.asset", out bool c1);
            if (c1)
            {
                d1.dayNumber = 1;
                d1.customers = customers.Where(c => c.day == 1).OrderBy(c => c.id).ToArray();
                d1.newRulePages = rules.Where(r => r.dayIntroduced == 1).ToArray();
                d1.endEvent = DayEndEvent.Rain;
                d1.lightingPreset = "Day1";
                d1.openingHeadline = "Karaova'da yeni yapı denetim ofisi açıldı"; // YER TUTUCU: tasarım ekibi değiştirebilir
                EditorUtility.SetDirty(d1);
            }
            var d2 = GetOrCreate<DayData>($"{Data}/Days/Day2.asset", out bool c2);
            if (c2)
            {
                d2.dayNumber = 2;
                d2.customers = customers.Where(c => c.day == 2).OrderBy(c => c.id).ToArray();
                d2.newRulePages = rules.Where(r => r.dayIntroduced == 2).ToArray();
                d2.endEvent = DayEndEvent.Quake;
                d2.lightingPreset = "Day2";
                EditorUtility.SetDirty(d2);
            }
        }

        static void CreateDialogue()
        {
            var d = GetOrCreate<DialogueSet>($"{Data}/Dialogue/CommonDialogue.asset", out bool created);
            if (!created) return;
            d.impatient = new[] { "Hıh... Daha ne kadar sürecek bu.", "Saatim ilerliyor ama.", "Bir imza için bu kadar bekleme olmaz." };
            d.angry = new[] { "Hrrm. Müdürünüze anlatacağım bunu.", "Çay ocağı mı burası, ofis mi.", "Ben vergimi bunun için mi ödüyorum." };
            d.rage = new[] { "YETER ARTIK!", "Bir saattir buradayım!", "BU NE REZİLLİK!" };
            d.leave = new[] { "Başka kapı da var. Hoşça kalın!" };
            EditorUtility.SetDirty(d);
        }

        static void CreateHeadlines()
        {
            void H(string key, string text, int priority)
            {
                var h = GetOrCreate<HeadlineData>($"{Data}/Headlines/HL_{key}.asset", out bool created);
                if (!created) return;
                h.key = key; h.text = text; h.priority = priority;
                EditorUtility.SetDirty(h);
            }
            H("CorrectApprove", "Emekli çift yeni evine taşındı", 10);
            H("CorrectReject", "Belediye denetçisi riskli projeyi geri çevirdi", 10);
            H("WrongfulReject", "Belediye denetçisi her şeye hayır diyor", 30);
            H("Flood", "Sağanak dere kenarındaki yeni binayı su altında bıraktı", 40);
            H("CustomerLeft", "Vatandaş denetim ofisinden şikâyetçi", 20);
            H("FaultCollapse", "Depremde yıkılan rezidans: denetim ofisine soru işaretleri", 50);
            H("DefectCollapse", "Yeni bina depremde ağır hasar aldı: malzeme mi kötüydü", 50);
            H("AllStanding", "Karaova'da deprem: yeni binalar ayakta kaldı", 35);
        }

        static void CreateEndings()
        {
            void E(string id, int priority, string title, string body)
            {
                var e = GetOrCreate<EndingData>($"{Data}/Endings/End_{priority}_{id}.asset", out bool created);
                if (!created) return;
                e.id = id; e.priority = priority; e.title = title; e.body = body;
                EditorUtility.SetDirty(e);
            }
            E("JailBribe", 1, "Hapis — Rüşvet Ortaya Çıktı", "Manşette senin adın ve zarfın fotoğrafı.");
            E("Jail", 2, "Hapis", "Fay hattındaki binaya onay verdin. Depremde çöktü.");
            E("FiredCollapse", 3, "Kovuldun — Çöküş", "Onayladığın bina depremde çöktü.");
            E("Fired", 4, "Kovuldun", "Masandaki eşyalar bir koliye kondu. Kapı arkandan kapandı.");
            E("InspectorOfYear", 5, "Yılın Denetçisi", "Deprem sonrası tüm binalar ayakta. Masanda bir plaket var.");
            E("RichButNervous", 6, "Zengin ama Tedirgin", "Çekmecede bir zarf. Telefon çalıyor.");
            E("StillHere", 7, "Hâlâ İşte", "Müdür: \"Bir daha olmasın.\"");
        }

        // =====================================================================
        // 3) VALIDATOR TESTİ
        // =====================================================================

        [MenuItem("Load Bearing/3 - Validator testi (7/7)", priority = 3)]
        public static void RunValidatorTest()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Data + "/GameConfig.asset");
            if (cfg == null) { Debug.LogError("GameConfig yok. Önce menüden 1'i çalıştır."); return; }

            var guids = AssetDatabase.FindAssets("t:CustomerData", new[] { Data + "/Customers" });
            var customers = guids.Select(g => AssetDatabase.LoadAssetAtPath<CustomerData>(AssetDatabase.GUIDToAssetPath(g)))
                                 .OrderBy(c => c.id).ToList();
            int pass = 0;
            foreach (var c in customers)
            {
                string reason = Validator.Explain(c, c.day, cfg);
                bool valid = reason == null;
                bool ok = valid == c.expectedValid;
                if (ok) pass++;
                Debug.Log($"{(ok ? "<color=lime>GEÇTİ</color>" : "<color=red>KALDI</color>")}  {c.id} {c.displayName}: " +
                          $"{(valid ? "ONAY" : "RET — " + reason)}  (beklenen: {(c.expectedValid ? "ONAY" : "RET")})");
            }
            var msg = $"Validator testi: {pass}/{customers.Count}";
            if (pass == customers.Count && customers.Count > 0) Debug.Log($"<color=lime><b>{msg} ✔</b></color>");
            else Debug.LogError(msg);
        }

        // =====================================================================
        // 2) GREYBOX SAHNE
        // =====================================================================

        [MenuItem("Load Bearing/2 - Greybox Office sahnesini kur", priority = 2)]
        public static void BuildGreyboxScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(Data + "/GameConfig.asset") == null)
                CreateFoldersAndData();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog("Office sahnesi zaten var",
                    "Office.unity baştan kurulsun mu? (Sahnedeki değişiklikler silinir.)", "Baştan kur", "Vazgeç"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Malzemeler
            var mFloor = Mat("Floor", new Color(0.32f, 0.30f, 0.28f));
            var mWall = Mat("Wall", new Color(0.62f, 0.64f, 0.60f));
            var mDesk = Mat("Desk", new Color(0.45f, 0.32f, 0.22f));
            var mPaper = Mat("Paper", new Color(0.93f, 0.91f, 0.84f));
            var mMap = Mat("Map", new Color(0.80f, 0.86f, 0.74f));
            var mBinder = Mat("Binder", new Color(0.18f, 0.30f, 0.55f));
            var mFolder = Mat("Folder", new Color(0.85f, 0.70f, 0.40f));
            var mDark = Mat("Dark", new Color(0.12f, 0.12f, 0.13f));
            var mScreen = Mat("Screen", new Color(0.10f, 0.25f, 0.18f));
            var mGreen = Mat("StampApprove", new Color(0.20f, 0.60f, 0.25f));
            var mRed = Mat("StampReject", new Color(0.75f, 0.18f, 0.15f));
            var mCustomer = Mat("Customer", new Color(0.55f, 0.55f, 0.75f));
            var mGlass = GlassMat();

            // ---- Systems
            var systems = new GameObject("Systems");
            var gm = systems.AddComponent<GameManager>();
            var gmSo = new SerializedObject(gm);
            gmSo.FindProperty("config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameConfig>(Data + "/GameConfig.asset");
            var daysProp = gmSo.FindProperty("days");
            daysProp.arraySize = 2;
            daysProp.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<DayData>(Data + "/Days/Day1.asset");
            daysProp.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<DayData>(Data + "/Days/Day2.asset");
            gmSo.ApplyModifiedPropertiesWithoutUndo();
            systems.AddComponent<DebugKeys>();

            // ---- PlayerRig + kamera
            var rig = new GameObject("PlayerRig");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(rig.transform, false);
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 1.30f, -0.70f), Quaternion.Euler(15f, 0f, 0f));
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.03f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<DeskLook>();
            camGo.AddComponent<FocusController>();
            camGo.AddComponent<DeskInteractor>();

            // ---- Oda
            var room = new GameObject("Room");
            Box("Floor", room.transform, new Vector3(0, -0.05f, 0.5f), new Vector3(6, 0.1f, 6), mFloor);
            Box("BackWall", room.transform, new Vector3(0, 1.5f, 2.3f), new Vector3(6, 3, 0.1f), mWall);
            Box("LeftWall", room.transform, new Vector3(-2.2f, 1.5f, 0.5f), new Vector3(0.1f, 3, 6), mWall);
            Box("RightWall", room.transform, new Vector3(2.2f, 1.5f, 0.5f), new Vector3(0.1f, 3, 6), mWall);

            // ---- Masa
            var desk = new GameObject("Desk");
            Box("DeskBody", desk.transform, new Vector3(0, 0.375f, -0.025f), new Vector3(1.7f, 0.75f, 0.8f), mDesk);
            const float top = 0.75f;

            // Harita
            var map = Group("Map", desk.transform, new Vector3(-0.30f, top, 0.12f));
            Box("Visual", map.transform, new Vector3(0, 0.0025f, 0), new Vector3(0.6f, 0.005f, 0.4f), mMap);
            MakeFocusable(map, "FocusPoint_Map", FocusState.Map, new Vector3(-0.30f, 1.12f, 0.00f));

            // Kural klasörü
            var binder = Group("RuleBinder", desk.transform, new Vector3(-0.62f, top, -0.20f));
            Box("Visual", binder.transform, new Vector3(0, 0.025f, 0), new Vector3(0.22f, 0.05f, 0.30f), mBinder);
            MakeFocusable(binder, "FocusPoint_Binder", FocusState.Binder, new Vector3(-0.62f, 1.08f, -0.30f));

            // Dosya yeri
            var folder = Group("FolderSpot", desk.transform, new Vector3(0.05f, top, -0.12f));
            Box("Visual", folder.transform, new Vector3(0, 0.0075f, 0), new Vector3(0.24f, 0.015f, 0.32f), mFolder);
            MakeFocusable(folder, "FocusPoint_Folder", FocusState.Folder, new Vector3(0.05f, 1.08f, -0.22f));

            // Mühürler
            var stamps = new GameObject("Stamps");
            stamps.transform.SetParent(desk.transform, false);
            var approve = Group("StampTool_Approve", stamps.transform, new Vector3(0.32f, top, -0.24f));
            Box("Visual", approve.transform, new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.035f, 0.05f), mGreen, PrimitiveType.Cylinder);
            approve.AddComponent<Interactable>();
            var reject = Group("StampTool_Reject", stamps.transform, new Vector3(0.42f, top, -0.24f));
            Box("Visual", reject.transform, new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.035f, 0.05f), mRed, PrimitiveType.Cylinder);
            reject.AddComponent<Interactable>();
            Box("InkPad", stamps.transform, new Vector3(0.37f, top + 0.005f, -0.13f), new Vector3(0.12f, 0.01f, 0.08f), mDark);

            // Güvenlik monitörü (ön yüzü kameraya dönük)
            var monitor = Group("Monitor", desk.transform, new Vector3(0.62f, top, 0.12f));
            monitor.transform.rotation = Quaternion.Euler(0f, 36f, 0f);
            Box("Body", monitor.transform, new Vector3(0, 0.20f, 0.0f), new Vector3(0.42f, 0.32f, 0.06f), mDark);
            var screen = Box("Screen", monitor.transform, new Vector3(0, 0.20f, -0.031f), new Vector3(0.38f, 0.28f, 1f), mScreen, PrimitiveType.Quad);
            Box("Stand", monitor.transform, new Vector3(0, 0.02f, 0.0f), new Vector3(0.18f, 0.04f, 0.12f), mDark);
            var screenCenter = screen.transform.position;
            var toCam = (camGo.transform.position - screenCenter); toCam.y = 0; toCam.Normalize();
            MakeFocusable(monitor, "FocusPoint_Monitor", FocusState.Monitor, screenCenter + toCam * 0.48f + Vector3.up * 0.02f, screenCenter);

            // Telsiz
            var radio = Group("Radio", desk.transform, new Vector3(0.76f, top, -0.22f));
            Box("Visual", radio.transform, new Vector3(0, 0.04f, 0), new Vector3(0.12f, 0.08f, 0.06f), mDark);
            var radioLight = new GameObject("AlertLight");
            radioLight.transform.SetParent(radio.transform, false);
            radioLight.transform.localPosition = new Vector3(0, 0.1f, 0);
            var rl = radioLight.AddComponent<Light>(); rl.type = LightType.Point; rl.range = 0.3f; rl.color = new Color(1f, 0.75f, 0.1f); rl.enabled = false;
            radio.AddComponent<AudioSource>().playOnAwake = false;

            // Gazete
            var paper = Group("Newspaper", desk.transform, new Vector3(0.20f, top, 0.16f));
            Box("Visual", paper.transform, new Vector3(0, 0.003f, 0), new Vector3(0.30f, 0.006f, 0.22f), mPaper);
            paper.AddComponent<Interactable>();

            // ItemSlots (dükkân: kesilebilir, yuvalar şimdilik boş)
            var slots = new GameObject("ItemSlots"); slots.transform.SetParent(desk.transform, false);
            Vector3[] slotPos = { new(-0.75f, top, -0.35f), new(0.75f, top, -0.35f), new(-0.05f, top, 0.30f),
                                  new(0f, 1.6f, 2.24f), new(-1.2f, 1.4f, 2.15f), new(1.1f, 0f, -0.2f) };
            for (int i = 0; i < slotPos.Length; i++) Group($"Slot_{i + 1}", slots.transform, slotPos[i]);

            // ---- Gişe
            var counter = new GameObject("Counter");
            Box("CounterWall", counter.transform, new Vector3(0, 0.4f, 0.50f), new Vector3(1.7f, 0.8f, 0.06f), mWall);
            var glass = Box("Glass", counter.transform, new Vector3(0, 1.25f, 0.50f), new Vector3(1.7f, 0.9f, 0.01f), mGlass);
            Object.DestroyImmediate(glass.GetComponent<Collider>()); // camın arkasına tıklanabilsin
            Group("CustomerSpot", counter.transform, new Vector3(0, 0, 0.90f));
            Group("DoorPoint", counter.transform, new Vector3(1.6f, 0, 1.2f));
            Box("Door", counter.transform, new Vector3(1.6f, 1.0f, 1.9f), new Vector3(0.9f, 2.0f, 0.05f), mDesk);
            var dummy = Box("_FramingDummy (müşteri gelince sil)", counter.transform, new Vector3(0, 0.85f, 0.90f), new Vector3(0.45f, 0.85f, 0.45f), mCustomer, PrimitiveType.Capsule);
            Object.DestroyImmediate(dummy.GetComponent<Collider>());

            // ---- Monitör dünyası (D-12'de doldurulacak)
            var feed = new GameObject("FeedWorld");
            feed.transform.position = new Vector3(1000f, 0f, 0f);
            Group("SiteRoot", feed.transform, new Vector3(1000f, 0f, 0f));

            // ---- UI (D-21'de doldurulacak)
            new GameObject("UI");

            // ---- Işık
            var lighting = new GameObject("Lighting");
            var sun = Group("Sun", lighting.transform, new Vector3(0, 3, 0));
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sunL = sun.AddComponent<Light>(); sunL.type = LightType.Directional; sunL.intensity = 0.9f; sunL.shadows = LightShadows.Soft;
            var lamp = Group("CeilingLamp", lighting.transform, new Vector3(0, 2.5f, 0.1f));
            var lampL = lamp.AddComponent<Light>(); lampL.type = LightType.Point; lampL.range = 5f; lampL.intensity = 1.6f;
            lampL.color = new Color(1f, 0.92f, 0.78f); lampL.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.32f, 0.36f);

            // ---- Kaydet + Build Settings
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("<color=lime>[Load Bearing]</color> Office.unity kuruldu ve Build Settings'e eklendi. Play'e bas: " +
                      "kenara git = etrafa bak, harita/klasör/dosya/monitör = yakın bakış, sağ tık/Esc = geri, F8 = akış.");
        }

        // ---------------- yardımcılar ----------------

        static GameObject Group(string name, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m,
                              PrimitiveType type = PrimitiveType.Cube)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static void MakeFocusable(GameObject obj, string fpName, FocusState state, Vector3 fpWorld, Vector3? lookAt = null)
        {
            var fp = new GameObject(fpName).transform;
            fp.SetParent(obj.transform, true);
            fp.position = fpWorld;
            var target = lookAt ?? obj.transform.position;
            fp.rotation = Quaternion.LookRotation(target - fpWorld, Vector3.up);
            var it = obj.AddComponent<Interactable>();
            it.focusPoint = fp;
            it.focusState = state;
        }

        static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) Debug.LogWarning("URP Lit bulunamadı. Proje 'Universal 3D' şablonuyla mı açıldı?");
            return s != null ? s : Shader.Find("Standard");
        }

        static Material Mat(string name, Color c)
        {
            string path = $"{Root}/Materials/Greybox/M_GB_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(LitShader());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material GlassMat()
        {
            string path = $"{Root}/Materials/Greybox/M_GB_Glass.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(LitShader());
            m.SetColor("_BaseColor", new Color(0.75f, 0.88f, 0.95f, 0.12f));
            m.SetFloat("_Surface", 1f);   // Transparent
            m.SetFloat("_Blend", 0f);     // Alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Smoothness", 0.9f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
