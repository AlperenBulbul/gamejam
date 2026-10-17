using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/18 - Masa düzenini ayarla"  (menü 14 ve 17'den sonra)
    /// Masadaki objeleri tasarımcının yerleşimine göre biraz kaydırır:
    ///  - Kural klasörü sağa
    ///  - Dosya ve iki mühür oyuncuya doğru (masanın ön kenarına)
    ///  - Monitör biraz sağa (yatay model mühürlere giriyordu)
    ///  - Mühürlerin önündeki ıstampa (koyu kare) kaldırılır
    /// İlk çalıştırmada her objenin asıl konumu Desk/_LayoutOrigins altına kaydedilir; sonraki çalıştırmalar
    /// hep o konuma + aşağıdaki kaydırmaya göre yerleştirir. Yani tekrar çalıştırmak üst üste kaydırmaz.
    /// Ayarlamak için aşağıdaki sayıları değiştirip menüyü tekrar çalıştır. (+X = sağ, -Z = oyuncuya doğru, metre)
    public static class DeskLayout
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        static readonly (string path, Vector3 offset)[] Moves =
        {
            ("Desk/RuleBinder",                 new Vector3(0.10f, 0f, -0.04f)), // haritaya binmesin
            ("Desk/FolderSpot",                 new Vector3(-0.01f, 0f, -0.335f)), // gazeteyle aynı hizada
            ("Desk/Stamps/StampTool_Approve",   new Vector3(0.04f, 0f, -0.37f)),   // açık dosya ile gazete arasında
            ("Desk/Stamps/StampTool_Reject",    new Vector3(0.04f, 0f, -0.37f)),
            ("Desk/Monitor",                    new Vector3(0.06f, 0f, 0f)),     // yatay monitör mühürlere girmesin
        };

        const float FolderFocusHeight = 1.5f;

        static readonly string[] HidePaths = { "Desk/Stamps/InkPad" };

        [MenuItem("Load Bearing/18 - Masa düzenini ayarla", priority = 18)]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var desk = GameObject.Find("Desk");
            if (desk == null) { Debug.LogError("Desk bulunamadı."); return; }
            var origins = desk.transform.Find("_LayoutOrigins");
            if (origins == null)
            {
                origins = new GameObject("_LayoutOrigins").transform;
                origins.SetParent(desk.transform, false);
            }

            var log = new System.Text.StringBuilder();
            foreach (var (path, offset) in Moves)
            {
                var go = GameObject.Find(path);
                if (go == null) { log.Append($"\n - ATLANDI: {path}"); continue; }
                string key = path.Replace('/', '_');
                var o = origins.Find(key);
                if (o == null)
                {
                    o = new GameObject(key).transform;
                    o.SetParent(origins, false);
                    o.position = go.transform.position; // asıl konum, bir kez
                }
                Undo.RecordObject(go.transform, "Desk layout");
                go.transform.position = o.position + offset;
                log.Append($"\n - {path}: {offset.x * 100f:+0;-0;0} cm sağ, {-offset.z * 100f:+0;-0;0} cm öne");
            }

            // Dosya yakın bakışı biraz daha geriden (yüksekten)
            var fv = Object.FindAnyObjectByType<FolderView>();
            if (fv != null)
            {
                Undo.RecordObject(fv, "Desk layout");
                fv.focusHeightScale = FolderFocusHeight;
                EditorUtility.SetDirty(fv);
                log.Append($"\n - Dosya yakın bakışı yüksekliği x{FolderFocusHeight}");
            }

            foreach (var path in HidePaths)
            {
                var go = GameObject.Find(path);
                if (go == null) continue;
                Undo.RecordObject(go, "Desk layout");
                go.SetActive(false);
                log.Append($"\n - {path} gizlendi");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Masa düzeni ayarlandı." + log);
        }
    }
}
