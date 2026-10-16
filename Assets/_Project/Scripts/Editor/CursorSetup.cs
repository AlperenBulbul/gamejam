using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/11 - Crosshair ve fare bakışını kur"
    /// Ekran ortasına crosshair kurar, Main Camera'ya CursorController ekler (tekrar çalıştırılabilir).
    /// Menü 4 (UI) da bunu otomatik çağırır.
    public static class CursorSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        [MenuItem("Load Bearing/11 - Crosshair ve fare bakışını kur", priority = 11)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var ui = GameObject.Find("UI");
            if (ui == null || ui.GetComponent<Canvas>() == null) { Debug.LogError("UI canvas yok. Önce menü 4'ü çalıştır."); return; }

            BuildCrosshair(ui.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Crosshair kuruldu. Masada fare kamerayı döndürür.");
        }

        public static void BuildCrosshair(Transform uiRoot)
        {
            var old = uiRoot.Find("Crosshair");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var root = new GameObject("Crosshair", typeof(RectTransform)).transform as RectTransform;
            root.SetParent(uiRoot, false);
            root.gameObject.layer = LayerMask.NameToLayer("UI");
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(10f, 10f);

            // İmleç ipucu crosshair'in üstünde çizilsin
            var hover = uiRoot.Find("HoverPrompt");
            if (hover != null) root.SetSiblingIndex(hover.GetSiblingIndex());

            Image Circle(string name, float size, Color c)
            {
                var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).transform as RectTransform;
                rt.SetParent(root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                var img = rt.GetComponent<Image>();
                img.sprite = knob; img.color = c; img.raycastTarget = false;
                return img;
            }
            Circle("Outline", 10f, new Color(0f, 0f, 0f, 0.55f)); // her zeminde görünsün
            var dot = Circle("Dot", 6f, Color.white);
            root.gameObject.SetActive(false); // CursorController açar

            var camGo = GameObject.FindWithTag("MainCamera");
            if (camGo == null) { Debug.LogError("Main Camera bulunamadı."); return; }
            var cc = camGo.GetComponent<CursorController>();
            if (cc == null) cc = camGo.AddComponent<CursorController>();
            cc.crosshair = root;
            cc.dot = dot;
            EditorUtility.SetDirty(cc);
        }
    }
}
