using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/20 - Hasan Amca'yı müşteri yap"  (önce menü 9 ve 13)
    /// - Animator Controller: Idle (Talking'in ilk karesi, durağan duruş), Talking, Angry (AngryPoint)
    ///   Yürüme / bekleme animasyonu henüz yok; gelince Idle ve yeni bir Walk durumu bağlanır.
    /// - Prefab: Customer_HasanAmca (kök: CustomerController, çocuk "Visual" > model + Animator)
    /// - Doku: T_Customer_C1_Hasan.png -> M_Customer_C1_Hasan (URP Lit), modelin bütün parçalarına
    /// - CustomerQueue.customerPrefab = bu prefab (bütün müşteriler şimdilik Hasan Amca)
    /// Tekrar çalıştırılabilir.
    public static class CharacterSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";
        const string Dir = "Assets/Art/Characters/HasanAmca";
        const string ModelPath = Dir + "/HasanAmca.fbx";
        const string TalkPath = Dir + "/HasanAmca@Talking.fbx";
        const string AngryPath = Dir + "/HasanAmca@Angry Point.fbx";
        const string TexPath = Dir + "/T_Customer_C1_Hasan.png";
        const string OutDir = "Assets/_Project/Characters";
        const string ControllerPath = OutDir + "/HasanAmca.controller";
        const string PrefabPath = OutDir + "/Customer_HasanAmca.prefab";
        const string MatPath = OutDir + "/M_Customer_C1_Hasan.mat";
        const float TargetHeight = 1.72f;
        /// Modelin yüzü hangi yöne bakıyor: 180 = model ters dışa aktarılmış (arka arka yürüyordu)
        const float ModelYaw = 180f;

        [MenuItem("Load Bearing/20 - Hasan Amca'yı müşteri yap", priority = 20)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            var talk = Clip(TalkPath);
            var angry = Clip(AngryPath);
            if (model == null) { Debug.LogError($"{ModelPath} bulunamadı."); return; }
            if (avatar == null || !avatar.isHuman) { Debug.LogError("Hasan Amca'nın Humanoid avatarı yok. Önce menü 13'ü çalıştır."); return; }
            if (talk == null || angry == null) Debug.LogWarning($"Animasyon eksik: Talking={(talk != null)}, AngryPoint={(angry != null)}. Menü 13'ü tekrar çalıştır.");

            if (!AssetDatabase.IsValidFolder(OutDir)) AssetDatabase.CreateFolder("Assets/_Project", "Characters");

            // ---- Animator Controller
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle");
            idle.motion = talk;   // ilk karede durur: T-poz yerine doğal duruş
            idle.speed = 0f;
            var talking = sm.AddState("Talking");
            talking.motion = talk;
            var angryState = sm.AddState("Angry");
            angryState.motion = angry != null ? angry : talk;
            sm.defaultState = idle;
            EditorUtility.SetDirty(ctrl);

            // ---- Prefab
            var root = new GameObject("Customer_HasanAmca");
            root.AddComponent<CustomerController>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(visual, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f); // model -Z'ye bakıyor: yüzü yürüme yönüne

            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.avatar = avatar;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var mat = SkinMaterial();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
                if (mat == null) continue;
                var mats = smr.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                smr.sharedMaterials = mats;
            }
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c); // tıklamayı engellemesin

            // Boy: 1.72 m civarı değilse ölçekle
            float height = 0f;
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                height = b.size.y;
                if (height < 1.4f || height > 2.0f) inst.transform.localScale = Vector3.one * (TargetHeight / Mathf.Max(0.01f, height));
            }

            root.GetComponent<CustomerController>().animator = anim;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // ---- Sıraya bağla
            var systems = GameObject.Find("Systems");
            var q = systems != null ? systems.GetComponent<CustomerQueue>() : null;
            if (q == null) { Debug.LogError("Systems/CustomerQueue yok. Önce menü 9'u çalıştır."); return; }
            q.customerPrefab = prefab;
            EditorUtility.SetDirty(q);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=lime>[Load Bearing]</color> Hasan Amca müşteri oldu. Boy: {height:0.00} m. Animasyonlar: Idle, Talking{(angry != null ? ", Angry" : "")}.");
        }

        /// Hasan Amca'nın renk dosyasıyla URP Lit materyali (yoksa oluşturur, varsa günceller)
        static Material SkinMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (tex == null) { Debug.LogWarning($"{TexPath} bulunamadı; model kendi materyaliyle kalır."); return null; }
            if (AssetImporter.GetAtPath(TexPath) is TextureImporter ti && (ti.maxTextureSize < 2048 || !ti.sRGBTexture))
            {
                ti.maxTextureSize = 2048;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AnimationClip Clip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }
    }
}
