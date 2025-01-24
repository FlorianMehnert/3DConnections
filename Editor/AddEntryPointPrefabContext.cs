using UnityEditor;
using UnityEngine;
using System.Linq;

public class AddEntryPointPrefabContext
{
    [MenuItem("GameObject/Add 3DConnections Entrypoint", false, 0)]
    private static void AddPrefabToScene()
    {
        string prefabName = "3DConnectionsLoadOverlay";  // Specify the name of the prefab you want to find
        
        // Search for the prefab by name in the "Assets" folder
        string[] guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");  // "t:Prefab" ensures we are looking for prefabs
        if (guids.Length == 0)
        {
            Debug.LogWarning($"No prefab found with the name '{prefabName}' in the Assets folder.");
            return;
        }

        // Get the first prefab found (you can modify this to select a specific prefab if multiple matches)
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject prefabToInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefabToInstantiate == null)
        {
            Debug.LogError($"Failed to load prefab '{prefabName}'.");
            return;
        }

        // Instantiate the prefab in the scene at (0, 0, 0)
        GameObject instance = PrefabUtility.InstantiatePrefab(prefabToInstantiate) as GameObject;
        if (instance != null)
        {
            instance.transform.position = Vector3.zero;
            Selection.activeGameObject = instance;
            Undo.RegisterCreatedObjectUndo(instance, "Add Prefab to Scene");
            Debug.Log($"Added prefab '{instance.name}' to scene at (0,0,0).");
        }
    }
}