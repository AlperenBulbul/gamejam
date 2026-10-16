using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/12 - Rüşvet zarfını kur"
    /// Dosyanın üst kenarına zarf koyar (sadece rüşvetli müşteride görünür), FolderSpot'a BribeEnvelope ekler.
    public static class BribeSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        [MenuItem("Load Bearing/12 - Rüşvet zarfını kur", priority = 12)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var spot = GameObject.Find("Desk/FolderSpot");
            var content = spot != null ? spot.transform.Find("Content") : null;
            if (spot == null || content == null) { Debug.LogError("Desk/FolderSpot/Content yok. Önce menü 5'i çalıştır."); return; }

            var old = content.Find("Envelope");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Zarf: dosyanın üst kenarında, formun başlığına hafifçe binerek (alanlar açık kalır)
            var env = new GameObject("Envelope");
            env.transform.SetParent(content, false);
            env.transform.localPosition = new Vector3(0.05f, 0.0175f, 0.165f);
            env.transform.localRotation = Quaternion.Euler(0f, -9f, 0f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(env.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            body.transform.localScale = new Vector3(0.16f, 0.004f, 0.095f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("Envelope", new Color(0.80f, 0.68f, 0.48f));

            var flap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flap.name = "Flap";
            flap.transform.SetParent(env.transform, false);
            flap.transform.localPosition = new Vector3(0f, 0.0042f, 0.022f);
            flap.transform.localScale = new Vector3(0.158f, 0.0008f, 0.05f);
            flap.GetComponent<Renderer>().sharedMaterial = Mat("EnvelopeFlap", new Color(0.72f, 0.60f, 0.40f));
            Object.DestroyImmediate(flap.GetComponent<Collider>());

            var seal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            seal.name = "Seal";
            seal.transform.SetParent(env.transform, false);
            seal.transform.localPosition = new Vector3(0f, 0.0048f, 0f);
            seal.transform.localScale = new Vector3(0.018f, 0.0008f, 0.018f);
            seal.GetComponent<Renderer>().sharedMaterial = Mat("Seal", new Color(0.62f, 0.10f, 0.08f));
            Object.DestroyImmediate(seal.GetComponent<Collider>());

            // Üstünde el yazısı gibi küçük not
            var noteGo = new GameObject("Note", typeof(RectTransform), typeof(Canvas));
            var note = (RectTransform)noteGo.transform;
            note.SetParent(env.transform, false);
            note.localPosition = new Vector3(0f, 0.0052f, -0.026f);
            note.localRotation = Quaternion.Euler(90f, 0f, 0f);
            note.localScale = Vector3.one * 0.001f;
            note.sizeDelta = new Vector2(150f, 26f);
            noteGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(note, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.text = "Küçük bir teşekkür";
            t.fontSize = 11f; t.fontStyle = FontStyles.Italic;
            t.color = new Color(0.25f, 0.18f, 0.10f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            var it = env.AddComponent<Interactable>();
            it.prompt = "Zarfı al";
            it.clickableIn = new[] { FocusState.Desk, FocusState.Folder };

            var be = spot.GetComponent<BribeEnvelope>();
            if (be == null) be = spot.AddComponent<BribeEnvelope>();
            be.envelope = env;
            be.envelopeClick = it;
            env.SetActive(false);

            EditorUtility.SetDirty(be);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Rüşvet zarfı kuruldu (sadece C7'de görünür).");
        }

        static Material Mat(string name, Color c)
        {
            string path = $"Assets/_Project/Materials/Greybox/M_GB_{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
