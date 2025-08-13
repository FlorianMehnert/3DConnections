#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;

static class NodeOverlaySearchProvider
{
    private const string ProviderId = "nodeoverlay";
    private const string FilterId = "node:";
    private const string HighlightPrefix = "highlight:";

    // Extendable token handlers
    private static readonly Dictionary<string, System.Func<GameObject, string, bool>> TokenHandlers =
        new()
        {
            ["name"] = (go, val) => go.name.ToLowerInvariant().Contains(val),
            ["type"] = (go, val) => go.tag.ToLowerInvariant().Contains(val),
            ["end"] = (go, val) =>
            {
                var conn = go.GetComponent<LocalNodeConnections>();
                if (conn == null) return false;
                string ends = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name.ToLowerInvariant()))
                              + ", " +
                              string.Join(", ",
                                  conn.outConnections.Where(o => o).Select(o => o.name.ToLowerInvariant()));
                return ends.Contains(val);
            },
            ["any"] = (go, val) =>
            {
                string nodeName = go.name.ToLowerInvariant();
                string connType = go.tag.ToLowerInvariant();
                var conn = go.GetComponent<LocalNodeConnections>();
                string endNames = conn != null
                    ? string.Join(", ", conn.inConnections.Concat(conn.outConnections)
                        .Where(x => x).Select(x => x.name.ToLowerInvariant()))
                    : "";
                return (nodeName.Contains(val) || connType.Contains(val) || endNames.Contains(val));
            }
        };

    // Keep track of highlighted objects for cleanup
    private static readonly List<GameObject> HighlightedObjects = new();

    [SearchItemProvider]
    internal static SearchProvider CreateProvider()
    {
        return new SearchProvider(ProviderId, "Node Overlay")
        {
            filterId = FilterId,
            priority = 500,
            isEnabledForContextualSearch = () => true,

            fetchItems = (context, items, provider) =>
            {
                if (context.searchQuery.Equals("clear:highlights", System.StringComparison.OrdinalIgnoreCase))
                {
                    ClearHighlights();
                    return null;
                }

                var parent = GameObject.Find("ParentNodesObject");
                if (parent == null)
                    return null;

                // Check if this is a highlight query
                bool shouldHighlight = context.searchQuery.StartsWith(HighlightPrefix);
                string actualQuery = shouldHighlight
                    ? context.searchQuery.Substring(HighlightPrefix.Length).Trim()
                    : context.searchQuery;

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
                    string connType = go.tag.ToLowerInvariant();
                    string outNames = string.Join(", ",
                        conn.outConnections.Where(o => o).Select(o => o.name.ToLowerInvariant()));
                    string inNames = string.Join(", ",
                        conn.inConnections.Where(i => i).Select(i => i.name.ToLowerInvariant()));

                    // Match query tokens
                    if (!MatchesTokens(tokens, go))
                        continue;

                    // Add to matching objects for highlighting
                    matchingObjects.Add(go);

                    string label = go.name;
                    string description = $"→ [{outNames}]\n← [{inNames}]";

                    Texture2D thumbnail = null;

                    var nodeTypeComp = go.GetComponent<NodeType>();
                    if (nodeTypeComp != null && nodeTypeComp.reference != null)
                    {
                        var refComponent = nodeTypeComp.reference;
                        var refType = refComponent.GetType();
                        thumbnail = EditorGUIUtility.ObjectContent(null, refType).image as Texture2D;
                    }

                    // Fallback, if nothing found
                    if (thumbnail == null)
                        thumbnail = EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;

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

            fetchLabel = (item, _) => item.label,
            fetchDescription = (item, _) => item.description,
            fetchThumbnail = (item, _) => item.thumbnail,
            toObject = (item, _) => item.data as GameObject,

            trackSelection = (item, _) =>
            {
                if (item.data is not GameObject go) return;
                EditorGUIUtility.PingObject(go);
                HighlightSingleObject(go);
            },

            // Add context actions for highlighting
            fetchPropositions = (context, _) =>
            {
                if (string.IsNullOrEmpty(context.searchQuery) || context.searchQuery.StartsWith(HighlightPrefix))
                    return null;

                return new SearchProposition[]
                {
                    new(
                        category: "Actions",
                        label: "Highlight Results",
                        replacement: HighlightPrefix + context.searchQuery,
                        help: "Highlight all matching nodes in the scene",
                        priority: 100,
                        icon: EditorGUIUtility.FindTexture("d_winbtn_mac_max")
                    ),
                    new(
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
                coloredObject.Highlight(Color.red, highlightForever: true, duration: 1);
                HighlightedObjects.Add(go);
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
            coloredObject.Highlight(Color.yellow, -1, highlightForever: true);
            if (!HighlightedObjects.Contains(go))
                HighlightedObjects.Add(go);
        }

        var camObj = GameObject.Find("OverlayCamera");
        if (camObj != null)
        {
            var cam = camObj.GetComponent<Camera>();
            if (cam != null)
            {
                Vector3 targetPos = new Vector3(go.transform.position.x, go.transform.position.y, cam.transform.position.z);
                EditorCoroutineUtility.StartCoroutineOwnerless(AnimateCameraMove(cam, targetPos));
            }
        }
    }

    
    private static IEnumerator AnimateCameraMove(Camera cam, Vector3 targetPosition, float duration = 0.5f)
    {
        Vector3 startPos = cam.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.transform.position = Vector3.Lerp(startPos, targetPosition, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        cam.transform.position = targetPosition; // Ensure exact final position
    }


    
    private static void ClearHighlights()
    {
        foreach (var go in HighlightedObjects.ToList())
        {
            if (go != null)
            {
                var coloredObject = go.GetComponent<ColoredObject>();
                if (coloredObject != null)
                {
                    coloredObject.ManualClearHighlight();
                }
            }
        }
        HighlightedObjects.Clear();
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
                tokens["any"] = part.ToLowerInvariant(); 
            }
        }

        return tokens;
    }


    private static bool MatchesTokens(Dictionary<string, string> tokens, GameObject go)
    {
        foreach (var kvp in tokens)
        {
            if (TokenHandlers.TryGetValue(kvp.Key, out var handler))
            {
                if (!handler(go, kvp.Value))
                    return false;
            }
            else
            {
                Debug.LogWarning($"Unknown search token: {kvp.Key}");
                return false;
            }
        }

        return true;
    }

    private static Texture2D GetTypeIcon(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image as Texture2D;

        var matchingType = System.AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => typeof(Component).IsAssignableFrom(t) &&
                                 t.Name.Equals(typeName, System.StringComparison.OrdinalIgnoreCase));

        if (matchingType != null)
            return EditorGUIUtility.ObjectContent(null, matchingType).image as Texture2D;

        return EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image as Texture2D;
    }
}
#endif