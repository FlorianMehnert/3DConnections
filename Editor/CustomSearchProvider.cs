#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

internal static class CustomSearchProvider
{
    [SearchItemProvider]
    public static SearchProvider CreateProvider()
    {
        return new SearchProvider("tmp-parent", "TMP Parents")
        {
            priority = 999,
            filterId = "ptmp:",
            isExplicitProvider = true,
            showDetails = true,

            // Fetch items from scene
            fetchItems = (context, items, provider) =>
            {
                var query = context.searchQuery;
                var matches = FindTMPParentsByText(query);
                foreach (var go in matches)
                {
                    var id = go.GetInstanceID().ToString();
                    var item = provider.CreateItem(context, id);
                    item.label = go.name;
                    item.description = $"Parent of TMP with text: \"{query}\"";
                    item.data = go; // GameObject reference
                    item.thumbnail = AssetPreview.GetMiniThumbnail(go);
                    items.Add(item);
                }

                return null;
            },

            // Convert search item to GameObject for selection
            toObject = (item, type) => item.data as GameObject,

            // Handle selection when item is activated (double-click, Enter, etc.)
            trackSelection = (item, context) =>
            {
                var gameObject = item.data as GameObject;
                if (gameObject == null) return;
                // Select the GameObject in the hierarchy
                Selection.activeGameObject = gameObject;

                // Optionally ping it in the hierarchy window
                EditorGUIUtility.PingObject(gameObject);
            },

            // Handle what happens when the item is selected (single click)
            startDrag = (item, context) =>
            {
                var gameObject = item.data as GameObject;
                if (gameObject == null) return;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { gameObject };
                DragAndDrop.StartDrag(gameObject.name);
            }
        };
    }

    private static IEnumerable<GameObject> FindTMPParentsByText(string targetText)
    {
        var results = new List<GameObject>();

        // Find all TextMeshPro components in the scene
        var tmpComponents = Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.InstanceID);

        foreach (var tmp in tmpComponents)
            // Check if the text contains our search query (case-insensitive)
            if (!string.IsNullOrEmpty(tmp.text) &&
                tmp.text.IndexOf(targetText, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var parent = tmp.transform.parent;
                if (parent != null && !results.Contains(parent.gameObject)) results.Add(parent.gameObject);
            }

        return results;
    }
}
#endif