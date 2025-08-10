#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

static class NodeOverlaySearchProvider
{
    const string providerId = "nodeoverlay";
    const string filterId = "node:";
    const string highlightPrefix = "highlight:";
    
    // Keep track of highlighted objects for cleanup
    private static List<GameObject> highlightedObjects = new List<GameObject>();

    [SearchItemProvider]
    internal static SearchProvider CreateProvider()
    {
        return new SearchProvider(providerId, "Node Overlay")
        {
            filterId = filterId,
            priority = 500,
            isEnabledForContextualSearch = () => true,

            fetchItems = (context, items, provider) =>
            {
                var parent = GameObject.Find("ParentNodesObject");
                if (parent == null)
                    return null;

                // Check if this is a highlight query
                bool shouldHighlight = context.searchQuery.StartsWith(highlightPrefix);
                string actualQuery = shouldHighlight ? 
                    context.searchQuery.Substring(highlightPrefix.Length).Trim() : 
                    context.searchQuery;

                // Clear previous highlights if this is a new search
                if (shouldHighlight)
                {
                    ClearHighlights();
                }

                // Parse query tokens like "name:Foo", "type:Trigger", etc.
                var tokens = ParseQuery(actualQuery);
                var matchingObjects = new List<GameObject>();

                foreach (Transform child in parent.transform)
                {
                    GameObject go = child.gameObject;
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn == null) continue;

                    string nodeName = go.name.ToLowerInvariant();
                    string connType = go.tag.ToLowerInvariant(); // or from component/metadata
                    string outNames = string.Join(", ", conn.outConnections.Where(o => o).Select(o => o.name.ToLowerInvariant()));
                    string inNames = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name.ToLowerInvariant()));

                    // Match query tokens
                    if (!MatchesTokens(tokens, nodeName, connType, inNames + "," + outNames))
                        continue;

                    // Add to matching objects for highlighting
                    matchingObjects.Add(go);

                    string label = go.name;
                    string description = $"→ [{outNames}]\n← [{inNames}]";

                    var thumbnail = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;

                    var item = provider.CreateItem(
                        id: go.GetInstanceID().ToString(),
                        score: 0,
                        label: label,
                        description: description,
                        thumbnail: thumbnail,
                        data: go
                    );

                    items.Add(item);
                }

                // Highlight all matching objects if highlight prefix was used
                if (shouldHighlight)
                {
                    HighlightObjects(matchingObjects);
                }

                return null;
            },

            fetchLabel = (item, context) => item.label,
            fetchDescription = (item, context) => item.description,
            fetchThumbnail = (item, context) => item.thumbnail,
            toObject = (item, type) => item.data as GameObject,
            
            trackSelection = (item, context) =>
            {
                if (item.data is not GameObject go) return;
                EditorGUIUtility.PingObject(go);
                Selection.activeGameObject = go;
            },

            // Add context actions for highlighting
            fetchPropositions = (context, options) =>
            {
                if (string.IsNullOrEmpty(context.searchQuery) || context.searchQuery.StartsWith(highlightPrefix))
                    return null;

                return new SearchProposition[]
                {
                    new SearchProposition(
                        category: "Actions",
                        label: "Highlight Results",
                        replacement: highlightPrefix + context.searchQuery,
                        help: "Highlight all matching nodes in the scene",
                        priority: 100,
                        icon: EditorGUIUtility.FindTexture("d_winbtn_mac_max")
                    ),
                    new SearchProposition(
                        category: "Actions", 
                        label: "Clear Highlights",
                        replacement: "clear:highlights",
                        help: "Clear all highlighted nodes",
                        priority: 99,
                        icon: EditorGUIUtility.FindTexture("d_winbtn_mac_close")
                    )
                };
            },

            // Handle special commands
            startDrag = (item, context) =>
            {
                // Handle clear highlights command
                if (context.searchQuery == "clear:highlights")
                {
                    ClearHighlights();
                    return;
                }

                // Handle individual item highlighting on drag start
                if (item.data is GameObject go)
                {
                    HighlightSingleObject(go);
                }
            }
        };
    }

    // Highlight all matching objects
    private static void HighlightObjects(List<GameObject> objects)
    {
        ClearHighlights(); // Clear previous highlights first
        
        foreach (var go in objects)
        {
            var coloredObject = go.GetComponent<ColoredObject>();
            if (coloredObject != null)
            {
                coloredObject.Highlight(Color.red, highlightForever:true, duration:1);
                highlightedObjects.Add(go);
            }
        }
        
        Debug.Log($"Highlighted {objects.Count} matching nodes");
    }

    // Highlight a single object (for context actions)
    private static void HighlightSingleObject(GameObject go)
    {
        var coloredObject = go.GetComponent<ColoredObject>();
        if (coloredObject != null)
        {
            coloredObject.Highlight(Color.yellow, -1, highlightForever:true);
            if (!highlightedObjects.Contains(go))
                highlightedObjects.Add(go);
        }
    }

    // Clear all highlights
    private static void ClearHighlights()
    {
        foreach (var go in highlightedObjects)
        {
            if (go != null)
            {
                var coloredObject = go.GetComponent<ColoredObject>();
                if (coloredObject != null)
                {
                    coloredObject.ManualClearHighlight(); // Assuming this method exists
                }
            }
        }
        highlightedObjects.Clear();
        Debug.Log("Cleared all highlights");
    }

    // Utility: Parse query like "name:foo type:bar end:baz" into key-value pairs
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var tokens = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(query))
            return tokens;

        var parts = query.Split(' ');
        foreach (var part in parts)
        {
            int sep = part.IndexOf(':');
            if (sep > 0 && sep < part.Length - 1)
            {
                string key = part.Substring(0, sep).ToLowerInvariant();
                string value = part.Substring(sep + 1).ToLowerInvariant();
                tokens[key] = value;
            }
            else
            {
                tokens["any"] = part.ToLowerInvariant(); // fallback
            }
        }
        return tokens;
    }

    // Match node against the search tokens
    private static bool MatchesTokens(Dictionary<string, string> tokens, string nodeName, string connType, string endNames)
    {
        foreach (var kvp in tokens)
        {
            string key = kvp.Key;
            string val = kvp.Value;

            switch (key)
            {
                case "name":
                    if (!nodeName.Contains(val))
                        return false;
                    break;
                case "type":
                    if (!connType.Contains(val))
                        return false;
                    break;
                case "end":
                    if (!endNames.Contains(val))
                        return false;
                    break;
                case "any":
                    if (!(nodeName.Contains(val) || connType.Contains(val) || endNames.Contains(val)))
                        return false;
                    break;
            }
        }
        return true;
    }
}
#endif