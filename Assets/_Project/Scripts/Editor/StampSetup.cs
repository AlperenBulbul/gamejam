using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/8 - Mühürleri kur"
    /// Desk/Stamps altındaki iki mühüre StampTool, Stamps'e StampController ekler;
    /// dosyayı mühür elindeyken de tıklanabilir yapar (tekrar çalıştırılabilir).
    public static class StampSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        [MenuItem("Load Bearing/8 - Mühürleri kur", priority = 8)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var stamps = GameObject.Find("Desk/Stamps");
            var approve = GameObject.Find("Desk/Stamps/StampTool_Approve");
            var reject = GameObject.Find("Desk/Stamps/StampTool_Reject");
            var folderGo = GameObject.Find("Desk/FolderSpot");
            if (stamps == null || approve == null || reject == null || folderGo == null)
            {
                Debug.LogError("Desk/Stamps, mühürler ya da Desk/FolderSpot bulunamadı. Önce menü 2 ve 5'i çalıştır.");
                return;
            }
            var folder = folderGo.GetComponent<FolderView>();
            if (folder == null) { Debug.LogError("FolderSpot'ta FolderView yok. Önce menü 5'i çalıştır."); return; }

            var a = Tool(approve, StampType.Approve);
            var r = Tool(reject, StampType.Reject);

            var ctrl = stamps.GetComponent<StampController>();
            if (ctrl == null) ctrl = stamps.AddComponent<StampController>();
            ctrl.approveStamp = a;
            ctrl.rejectStamp = r;
            ctrl.folder = folder;

            // Mühür elindeyken dosyanın üstüne gelince vurgulansın ve "Mühür bas" yazsın
            var fi = folderGo.GetComponent<Interactable>();
            if (fi != null)
            {
                fi.clickableIn = new[] { FocusState.Desk, FocusState.HoldingStamp };
                EditorUtility.SetDirty(fi);
            }

            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Mühürler kuruldu. Mühüre tıkla, sonra dosyaya tıkla.");
        }

        static StampTool Tool(GameObject go, StampType type)
        {
            var t = go.GetComponent<StampTool>();
            if (t == null) t = go.AddComponent<StampTool>();
            t.type = type;
            var it = go.GetComponent<Interactable>();
            if (it == null) it = go.AddComponent<Interactable>();
            it.prompt = type == StampType.Approve ? "Onay mührünü al" : "Ret mührünü al";
            EditorUtility.SetDirty(t);
            EditorUtility.SetDirty(it);
            return t;
        }
    }
}
