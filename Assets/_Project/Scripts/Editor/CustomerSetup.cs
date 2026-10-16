using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoadBearing.EditorTools
{
    /// "Load Bearing/9 - Müşteri sırasını kur"
    /// Systems'e CustomerQueue ekler, kapı ve gişe noktalarını bağlar, çerçeveleme kuklasını siler.
    public static class CustomerSetup
    {
        const string ScenePath = "Assets/_Project/Scenes/Office.unity";

        [MenuItem("Load Bearing/9 - Müşteri sırasını kur", priority = 9)]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var systems = GameObject.Find("Systems");
            var door = GameObject.Find("Counter/DoorPoint");
            var spot = GameObject.Find("Counter/CustomerSpot");
            if (systems == null || door == null || spot == null)
            {
                Debug.LogError("Systems, Counter/DoorPoint ya da Counter/CustomerSpot bulunamadı. Önce menü 2'yi çalıştır.");
                return;
            }

            var q = systems.GetComponent<CustomerQueue>();
            if (q == null) q = systems.AddComponent<CustomerQueue>();
            q.doorPoint = door.transform;
            q.customerSpot = spot.transform;
            q.greyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Greybox/M_GB_Customer.mat");
            q.dialogue = AssetDatabase.LoadAssetAtPath<DialogueSet>("Assets/_Project/Data/Dialogue/CommonDialogue.asset");
            if (q.dialogue == null) Debug.LogWarning("CommonDialogue bulunamadı; menü 1'i çalıştır. Müşteriler 'Hrrm...' der.");

            // Sahneyi çerçevelemek için konmuş kapsül artık gereksiz
            foreach (Transform t in GameObject.Find("Counter").transform)
            {
                if (t.name.StartsWith("_FramingDummy")) { Object.DestroyImmediate(t.gameObject); break; }
            }

            EditorUtility.SetDirty(q);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("<color=lime>[Load Bearing]</color> Müşteri sırası kuruldu. Gazeteye tıklayınca müşteriler gelmeye başlar.");
        }
    }
}
