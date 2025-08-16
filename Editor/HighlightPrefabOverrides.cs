namespace _3DConnections.Editor
{
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public class HighlightPrefabOverridesContextMenu : Editor
    {
        [MenuItem("Tools/3DConnections/Highlight Prefabs with Overrides %h", false, 20)]
        private static void HighlightPrefabOverrides()
        {
            var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
            var highlightedObjects = allObjects.Where(go =>
                    PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.HasPrefabInstanceAnyOverrides(go, false))
                .ToList();

            if (highlightedObjects.Count > 0)
            {
                Selection.objects = highlightedObjects.ToArray();
                Debug.Log($"Highlighted {highlightedObjects.Count} prefabs with meaningful overrides.");
            }
            else
            {
                Debug.Log("No prefabs with meaningful overrides found in the scene.");
            }
        }

        [MenuItem("GameObject/Highlight Prefabs with Overrides", true)]
        private static bool ValidateHighlightPrefabOverrides()
        {
            return Selection.activeGameObject != null;
        }
    }
}