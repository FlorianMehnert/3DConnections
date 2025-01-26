using UnityEditor;
using UnityEngine;

public static class AddEntryPointPrefabContext
{
#if UNITY_EDITOR
    [MenuItem("GameObject/3DConnections/Add Entrypoint", false, 0)]
    private static void AddPrefabToScene()
    {
        const string prefabName = "3DConnectionsLoadOverlay";

        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab");

        if (guids.Length == 0)
        {
            Debug.LogWarning($"No prefab found with the name '{prefabName}' in the Assets folder.");
            return;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var prefabToInstantiate = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefabToInstantiate == null)
        {
            Debug.LogError($"Failed to load prefab '{prefabName}'.");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefabToInstantiate) as GameObject;
        if (instance == null) return;
        instance.transform.position = Vector3.zero;
        Selection.activeGameObject = instance;
        Undo.RegisterCreatedObjectUndo(instance, "Add Prefab to Scene");
        Debug.Log($"Added prefab '{instance.name}' to scene at (0,0,0).");
    }
#endif
}