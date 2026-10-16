using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/10 - Altyazı ve konuşma sesini kur"
    /// UI altına altyazı şeridini kurar, Systems'e SpeechSystem + AudioSource ekler (tekrar çalıştırılabilir).
    /// Menü 4 (UI) da bunu otomatik çağırır.
    public static class SpeechSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        [MenuItem("Load Bearing/10 - Altyazı ve konuşma sesini kur", priority = 10)]
        public static void Build()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                EditorUtility.DisplayDialog("TextMeshPro eksik", "Önce: Window > TextMeshPro > Import TMP Essential Resources", "Tamam");
                return;
            }
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            var ui = GameObject.Find("UI");
            if (ui == null || ui.GetComponent<Canvas>() == null) { Debug.LogError("UI canvas yok. Önce menü 4'ü çalıştır."); return; }

            BuildSubtitle(ui.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Altyazı ve konuşma sesi kuruldu.");
        }

        public static void BuildSubtitle(Transform uiRoot)
        {
            var old = uiRoot.Find("Subtitle");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Kök: ekranın alt ortası
            var root = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasGroup)).transform as RectTransform;
            root.SetParent(uiRoot, false);
            root.gameObject.layer = LayerMask.NameToLayer("UI");
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 64f);
            root.sizeDelta = new Vector2(1200f, 200f);
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

            // Hover ipucu en üstte kalsın: altyazıyı onun altına koy
            var hover = uiRoot.Find("HoverPrompt");
            if (hover != null) root.SetSiblingIndex(hover.GetSiblingIndex());

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image)).transform as RectTransform;
            bg.SetParent(root, false);
            bg.anchorMin = bg.anchorMax = new Vector2(0.5f, 0f);
            bg.pivot = new Vector2(0.5f, 0f);
            bg.anchoredPosition = Vector2.zero;
            bg.sizeDelta = new Vector2(600f, 60f);
            var img = bg.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;

            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.rectTransform.SetParent(root, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            t.rectTransform.pivot = new Vector2(0.5f, 0f);
            t.rectTransform.anchoredPosition = new Vector2(0f, 11f); // padding.y / 2
            t.rectTransform.sizeDelta = new Vector2(600f, 40f);
            t.text = "";
            t.fontSize = 34f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Bottom;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;

            // Systems: SpeechSystem + 2D ses kaynağı
            var systems = GameObject.Find("Systems");
            if (systems == null) { Debug.LogError("Systems bulunamadı."); return; }
            var sp = systems.GetComponent<SpeechSystem>();
            if (sp == null) sp = systems.AddComponent<SpeechSystem>();
            var src = sp.voice;
            if (src == null)
            {
                var vgo = systems.transform.Find("Voice");
                if (vgo == null) { vgo = new GameObject("Voice").transform; vgo.SetParent(systems.transform, false); }
                src = vgo.GetComponent<AudioSource>();
                if (src == null) src = vgo.gameObject.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            sp.voice = src;
            sp.group = group;
            sp.text = t;
            sp.background = bg;
            EditorUtility.SetDirty(sp);
        }
    }
}
