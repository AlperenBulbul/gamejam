using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/20 - Müşteri karakterlerini kur"  (önce menü 9 ve 13)
    /// Hasan Amca (C1) ve Selin (C3) için Animator Controller + prefab kurar ve müşterilere bağlar.
    /// - Selin dosyaları Assets/Art/Characters/Selin altında olmalı (dışarıdan kopyalama yok)
    /// - Animasyon durumları: Idle, Impatient, Angry, Rage, Talking, Happy, Sad, Walk (karakterde olanlar)
    ///   CustomerController elinde olmayanın yerine en yakınını oynar.
    /// - Modelin yüzü kollarının konumundan bulunur ve prefabda yürüme yönüne çevrilir
    /// - Varsayılanlar: erkek müşteriler Hasan Amca, kadın müşteriler Selin (her müşteriye ayrı model gelene kadar)
    /// Tekrar çalıştırılabilir.
    public static class CharacterSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string OutDir = "Assets/_Project/Characters";
        const float TargetHeight = 1.72f;

        const string HasanDir = "Assets/Art/Characters/HasanAmca";
        const string SelinDir = "Assets/Art/Characters/Selin";
        const string MahmutDir = "Assets/Art/Characters/Mahmut";
        const string EmreDir = "Assets/Art/Characters/Emre";
        const string NaciyeDir = "Assets/Art/Characters/Naciye";
        const string ZeynepDir = "Assets/Art/Characters/Zeynep";

        [MenuItem("Load Bearing/20 - Müşteri karakterlerini kur", priority = 20)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Project", "Characters");

            var log = new System.Text.StringBuilder();

            // ---- Hasan Amca (menü 13 içe aktarmayı ayarladı)
            GameObject hasan = null;
            {
                var talk = Clip($"{HasanDir}/HasanAmca@Talking.fbx");
                var angry = Clip($"{HasanDir}/HasanAmca@Angry Point.fbx");
                // Beklerken kızgın animasyon yok (geçişleri kopuk duruyordu); reddedilince "Sad" = kızgın hareket
                var states = new List<(string, Motion, float)>
                {
                    ("Idle", talk, 0f),          // Talking'in ilk karesi: T-poz yerine doğal duruş
                    ("Talking", talk, 1f),
                    ("Sad", angry != null ? angry : talk, 1f),
                };
                hasan = BuildCharacter("HasanAmca", $"{HasanDir}/HasanAmca.fbx", $"{HasanDir}/T_Customer_C1_Hasan.png", states, 180f, log,
                                       cc => { cc.rejectSeconds = 3f; });
            }

            // ---- Mahmut (C2): kendi modeli + dokusu; konuşma / kızgın animasyonları Hasan Amca'nınki (Humanoid, her modele uyar)
            GameObject mahmut = null;
            {
                string model = FindAsset(MahmutDir, "SK_Customer_C2_Mahmut");
                if (model != null)
                {
                    SetupModelImporter(model);
                    var talk = Clip($"{HasanDir}/HasanAmca@Talking.fbx");
                    var angry = Clip($"{HasanDir}/HasanAmca@Angry Point.fbx");
                    AnimationClip dance = null;
                    foreach (var p in AssetDatabase.FindAssets("t:Model", new[] { MahmutDir }).Select(AssetDatabase.GUIDToAssetPath))
                    {
                        if (!p.Contains("@")) continue;
                        SetupClipImporter(p, "Dance", true); // Mahmut'un kendi animasyonu (döngü)
                        dance = Clip(p);
                    }
                    // Kendi animasyonu (dans) sadece onay alınca 5 sn; yürüme Selin'in yürüyüşü (Humanoid, her modele uyar)
                    var walkPath = FindAsset(SelinDir, "A_Customer_Selin_Walk");
                    var walk = walkPath != null ? Clip(walkPath) : null;
                    var states = new List<(string, Motion, float)>
                    {
                        ("Idle", talk, 0f),
                        ("Talking", talk, 1f),
                        ("Sad", angry != null ? angry : talk, 1f),   // reddedilince kızgın hareket
                    };
                    if (dance != null) states.Add(("Happy", dance, 1f));
                    if (walk != null) states.Add(("Walk", walk, 1f));
                    mahmut = BuildCharacter("Mahmut", model, FindAsset(MahmutDir, "T_Customer_C2_Mahmut"), states, float.NaN, log,
                                            cc => { cc.standBack = 0.35f; cc.happySeconds = 5f; cc.rejectSeconds = 3f; }); // biraz geride durur, onayda 5 sn dans
                }
                else log.Append($"\n - UYARI: Mahmut modeli yok ({MahmutDir}/SK_Customer_C2_Mahmut.fbx)");
            }

            // ---- 2. gün karakterleri (kendi modeli + dokusu + konuşma / kızgın animasyonları)
            var emre = BuildDay2("Emre", EmreDir, "SK_Customer_C8_Emre", "T_Customer_C8_Emre", "Standing Arguing", "Kızgın", log);
            var naciye = BuildDay2("Naciye", NaciyeDir, "SK_Customer_C5_Naciye", "T_Customer_C5_Naciye", "Talking", null, log);
            var zeynep = BuildDay2("Zeynep", ZeynepDir, "SK_Customer_C10_Zeynep", "T_Customer_C10_Zeynep", "Talking", "Standing Arguing", log);

            // ---- Selin
            GameObject selin = null;
            if (HasSelin(log))
            {
                string model = FindAsset(SelinDir, "SK_Customer_Selin");
                if (model != null)
                {
                    SetupModelImporter(model);
                    var clips = new Dictionary<string, AnimationClip>();
                    foreach (var p in AssetDatabase.FindAssets("A_Customer_Selin_ t:Model", new[] { SelinDir }).Select(AssetDatabase.GUIDToAssetPath))
                    {
                        string key = Path.GetFileNameWithoutExtension(p).Replace("A_Customer_Selin_", "");
                        bool loop = !key.StartsWith("React");
                        SetupClipImporter(p, key, loop);
                        var c = Clip(p);
                        if (c != null) clips[key] = c;
                    }
                    var states = new List<(string, Motion, float)>();
                    void Add(string state, string clip) { if (clips.TryGetValue(clip, out var c)) states.Add((state, c, 1f)); }
                    Add("Idle", "IdleCalm");
                    Add("Impatient", "IdleImpatient");
                    Add("Angry", "IdleAngry");
                    Add("Rage", "Rage");
                    Add("Talking", "Talk");
                    Add("Happy", "ReactHappy");
                    Add("Sad", "ReactSad");
                    Add("Walk", "Walk");
                    log.Append($"\n - Selin animasyonları: {string.Join(", ", clips.Keys)}");
                    selin = BuildCharacter("Selin", model, FindAsset(SelinDir, "T_Customer_Selin"), states, float.NaN, log);
                }
                else log.Append("\n - UYARI: SK_Customer_Selin bulunamadı.");
            }

            // ---- Müşterilere bağla
            var q = Object.FindAnyObjectByType<CustomerQueue>();
            if (q != null)
            {
                if (hasan != null) q.customerPrefab = hasan;
                q.femalePrefab = selin;
                EditorUtility.SetDirty(q);
            }
            else log.Append("\n - UYARI: CustomerQueue yok (menü 9).");

            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.Days != null)
            {
                foreach (var d in gm.Days)
                {
                    if (d == null || d.customers == null) continue;
                    foreach (var c in d.customers)
                    {
                        if (c == null) continue;
                        GameObject want = c.id == "C1" ? hasan : c.id == "C2" ? mahmut : c.id == "C3" ? selin
                                        : c.id == "C4" ? emre : c.id == "C5" ? naciye : c.id == "C6" ? zeynep : null;
                        // C6 artık Zeynep (Yalın Yapı temsilcisi, kadın)
                        if (c.id == "C6" && zeynep != null && c.displayName != "Zeynep Yalın")
                        {
                            c.displayName = "Zeynep Yalın"; c.body = BodyType.Female; c.voicePitch = 1.2f; EditorUtility.SetDirty(c);
                        }
                        if (c.characterPrefab != want) { c.characterPrefab = want; EditorUtility.SetDirty(c); }
                        // C2 artık Mahmut (ikinci Hasan Amca yok)
                        if (c.id == "C2" && mahmut != null && c.displayName != "Mahmut Aydın") { c.displayName = "Mahmut Aydın"; EditorUtility.SetDirty(c); }
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=lime>[Load Bearing]</color> Müşteri karakterleri kuruldu." + log);
        }

        // ------------------------------------------------------------------ 2. gün karakterleri

        /// Model + doku + '@' animasyonları: talkKey içeren dosya konuşma, angryKey içeren kızgın.
        /// Bekleme = konuşmanın ilk karesi, yürüme = Selin'in yürüyüşü (Humanoid, her modele uyar).
        static GameObject BuildDay2(string name, string dir, string modelName, string texName, string talkKey, string angryKey, System.Text.StringBuilder log)
        {
            string model = FindAsset(dir, modelName);
            if (model == null) { log.Append($"\n - UYARI: {name} modeli yok ({dir}/{modelName}.fbx)"); return null; }
            SetupModelImporter(model);
            AnimationClip talk = null, angry = null;
            foreach (var p in AssetDatabase.FindAssets("t:Model", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (!p.Contains("@")) continue;
                string anim = Path.GetFileNameWithoutExtension(p);
                anim = anim.Substring(anim.IndexOf('@') + 1);
                if (talk == null && anim.Contains(talkKey)) { SetupClipImporter(p, "Talking", true); talk = Clip(p); }
                else if (angryKey != null && angry == null && anim.Contains(angryKey)) { SetupClipImporter(p, "Angry", true); angry = Clip(p); }
            }
            if (talk == null) talk = Clip($"{HasanDir}/HasanAmca@Talking.fbx");
            if (angry == null) angry = Clip($"{HasanDir}/HasanAmca@Angry Point.fbx");
            var walkPath = FindAsset(SelinDir, "A_Customer_Selin_Walk");
            var walk = walkPath != null ? Clip(walkPath) : null;
            var states = new List<(string, Motion, float)>
            {
                ("Idle", talk, 0f),
                ("Talking", talk, 1f),
                ("Sad", angry != null ? angry : talk, 1f),   // reddedilince: tartışma / kızma animasyonu (beklerken oynamaz)
            };
            if (walk != null) states.Add(("Walk", walk, 1f));
            // Konuşma uzun sürsün; reddedilince kendi tepkisini 3,5 sn oynasın, sonra çıksın
            return BuildCharacter(name, model, FindAsset(dir, texName), states, float.NaN, log,
                                  cc => { cc.talkHold = 2.5f; cc.rejectSeconds = 3.5f; });
        }

        // ------------------------------------------------------------------ Selin dosyaları

        static bool HasSelin(System.Text.StringBuilder log)
        {
            if (FindAsset(SelinDir, "SK_Customer_Selin") != null) return true;
            log.Append($"\n - UYARI: Selin modeli yok ({SelinDir}/SK_Customer_Selin.fbx)");
            return false;
        }

        static void SetupModelImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            mi.bakeAxisConversion = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.addCollider = false;
            mi.SaveAndReimport();
        }

        static void SetupClipImporter(string path, string clipName, bool loop)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter ai)) return;
            ai.bakeAxisConversion = true;
            ai.animationType = ModelImporterAnimationType.Human;
            ai.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; // Humanoid klipler her avatara uyar
            ai.importAnimation = true;
            ai.importCameras = false;
            ai.importLights = false;
            ai.SaveAndReimport(); // varsayılan klipler okunabilsin

            var defs = ai.defaultClipAnimations;
            if (defs.Length == 0) return;
            var c = defs[0];
            c.name = clipName;
            c.loopTime = loop;
            c.lockRootPositionXZ = true;      // olduğu yerde oynasın (yürürken de; konumu kod taşır)
            c.keepOriginalPositionXZ = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = true;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            ai.clipAnimations = new[] { c };
            ai.SaveAndReimport();
        }

        // ------------------------------------------------------------------ prefab

        /// states: (durum adı, klip, hız). defaultYaw: model yönü bulunamazsa kullanılacak açı (NaN = 0)
        static GameObject BuildCharacter(string name, string modelPath, string texPath, List<(string state, Motion motion, float speed)> states, float defaultYaw, System.Text.StringBuilder log,
                                         System.Action<CustomerController> configure = null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (model == null) { log.Append($"\n - UYARI: {modelPath} yok."); return null; }
            if (avatar == null || !avatar.isHuman) { log.Append($"\n - UYARI: {name} için Humanoid avatar yok (menü 13 / rig)."); return null; }

            // Animator Controller
            // Var olan controller silinmez, içi boşaltılıp yeniden doldurulur: prefablardaki bağlantı kopmasın
            // (silip yeniden oluşturunca açık olan prefablar "kayıp controller" ile kalıp T-pozda geziyordu)
            string ctrlPath = $"{OutDir}/{name}.controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var cs in sm.states.ToArray()) sm.RemoveState(cs.state);
            AnimatorState first = null;
            foreach (var (state, motion, speed) in states)
            {
                if (motion == null) continue;
                var st = sm.AddState(state);
                st.motion = motion;
                st.speed = speed;
                if (first == null || state == "Idle") first = state == "Idle" || first == null ? st : first;
            }
            if (first != null) sm.defaultState = first;
            EditorUtility.SetDirty(ctrl);

            // Prefab: kök (CustomerController) > Visual > model
            var root = new GameObject($"Customer_{name}");
            var cc = root.AddComponent<CustomerController>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(visual, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.avatar = avatar;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Yüz yönü: sağ kol solun sağındaysa ileri = Cross(sağ, yukarı). +Z'ye bakmıyorsa 180° çevir.
            float yaw = float.IsNaN(defaultYaw) ? 0f : defaultYaw;
            var l = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var r = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (l != null && r != null)
            {
                var right = r.position - l.position; right.y = 0f;
                var fwd = Vector3.Cross(right, Vector3.up);
                if (fwd.sqrMagnitude > 1e-6f) yaw = Mathf.Round(Vector3.SignedAngle(fwd, Vector3.forward, Vector3.up) / 90f) * 90f;
            }
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var mat = SkinMaterial(name, texPath);
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                if (mat == null) continue;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                smr.sharedMaterials = mats;
            }
            foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            float height = 0f;
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var rr in rends) b.Encapsulate(rr.bounds);
                height = b.size.y;
                if (height < 1.4f || height > 2.0f) inst.transform.localScale = Vector3.one * (TargetHeight / Mathf.Max(0.01f, height));
            }

            cc.animator = anim;
            configure?.Invoke(cc);
            AssetDatabase.SaveAssets();
            string prefabPath = $"{OutDir}/Customer_{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate); // bellekteki prefab da tazelensin
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            log.Append($"\n - {name}: boy {height:0.00} m, yön {yaw:0}°, durumlar: {string.Join(", ", states.Where(s => s.motion != null).Select(s => s.state))}{(mat == null ? " (doku yok)" : "")}");
            return prefab;
        }

        static Material SkinMaterial(string name, string texPath)
        {
            var tex = texPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) : null;
            if (tex == null) return null;
            if (AssetImporter.GetAtPath(texPath) is TextureImporter ti && (ti.maxTextureSize != 2048 || !ti.sRGBTexture))
            {
                ti.maxTextureSize = 2048;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            string matPath = $"{OutDir}/M_Customer_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string FindAsset(string dir, string fileNameNoExt)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var g in AssetDatabase.FindAssets(fileNameNoExt, new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) == fileNameNoExt) return p;
            }
            return null;
        }

        static AnimationClip Clip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
    }
}
