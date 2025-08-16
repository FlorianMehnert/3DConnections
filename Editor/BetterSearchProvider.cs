namespace _3DConnections.Editor
{
#if UNITY_EDITOR
    using UnityEditor;
    using UnityEditor.Search;
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using Unity.EditorCoroutines.Editor;
    using System.Collections;
    using Runtime.Nodes;

    internal static class NodeOverlaySearchProvider
    {
        private const string ProviderId = "nodeoverlay";
        private const string FilterId = "node:";
        private const string HighlightPrefix = "highlight:";
        private static readonly List<GameObject> HighlightedObjects = new List<GameObject>();

        // =====================
        // Extendable Token System
        // =====================
        private static readonly Dictionary<string, System.Func<GameObject, string, bool>> TokenHandlers =
            new()
            {
                ["name"] = (go, val) => go.name.ToLowerInvariant().Contains(val),
                ["type"] = (go, val) => go.tag.ToLowerInvariant().Contains(val),
                ["end"] = (go, val) =>
                {
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn == null) return false;
                    string ends = string.Join(", ",
                                      conn.inConnections.Where(i => i).Select(i => i.name.ToLowerInvariant()))
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
                },
                ["ref"] = (go, val) =>
                {
                    GameObject target = GameObject.Find(val.Trim('"').Trim());
                    if (target == null)
                        return false;

                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp == null) continue;
                        var so = new SerializedObject(comp);
                        var prop = so.GetIterator();
                        while (prop.NextVisible(true))
                        {
                            if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                                prop.objectReferenceValue == target)
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            };

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
                    string value = part.Substring(sep + 1).Trim().Trim('"').ToLowerInvariant();
                    tokens[key] = value; // keep "ref" intact
                }
                else
                {
                    tokens["any"] = part.ToLowerInvariant();
                }
            }

            return tokens;
        }

        // =====================
        // Search Provider
        // =====================
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
                    // Special case: clear highlights
                    if (context.searchQuery.Equals("clearHighlights", System.StringComparison.OrdinalIgnoreCase))
                    {
                        ClearHighlights();
                        return null;
                    }

                    var parent = GameObject.Find("ParentNodesObject");
                    if (parent == null)
                        return null;

                    bool shouldHighlight = context.searchQuery.StartsWith(HighlightPrefix);
                    string actualQuery = shouldHighlight
                        ? context.searchQuery.Substring(HighlightPrefix.Length).Trim()
                        : context.searchQuery;

                    if (shouldHighlight)
                        ClearHighlights();

                    var tokens = ParseQuery(actualQuery);
                    var matchingObjects = new List<GameObject>();

                    foreach (Transform child in parent.transform)
                    {
                        GameObject go = child.gameObject;
                        var conn = go.GetComponent<LocalNodeConnections>();
                        if (conn == null) continue;

                        if (!MatchesTokens(tokens, go))
                            continue;

                        matchingObjects.Add(go);

                        string label = go.name;
                        string outNames = string.Join(", ", conn.outConnections.Where(o => o).Select(o => o.name));
                        string inNames = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name));
                        string description = $"→ [{outNames}]\n← [{inNames}]";

                        Texture2D thumbnail = GetNodeTypeIcon(go);

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

                    if (shouldHighlight)
                        HighlightObjects(matchingObjects);

                    return null;
                },

                fetchLabel = (item, _) => item.label,
                fetchDescription = (item, _) => item.description,
                fetchThumbnail = (item, _) => item.thumbnail,
                toObject = (item, _) => item.data as GameObject,

                trackSelection = (item, _) =>
                {
                    if (item.data is not GameObject go) return;
                    HighlightSingleObject(go);
                },

                fetchPropositions = (context, _) =>
                {
                    if (string.IsNullOrEmpty(context.searchQuery) || context.searchQuery.StartsWith(HighlightPrefix))
                        return null;

                    return new[]
                    {
                        new SearchProposition(
                            category: "Actions",
                            label: "Highlight Results",
                            replacement: HighlightPrefix + context.searchQuery,
                            help: "Highlight all matching nodes in the scene",
                            priority: 100,
                            icon: EditorGUIUtility.FindTexture("d_winbtn_mac_max")
                        ),
                        new SearchProposition(
                            category: "Actions",
                            label: "Clear Highlights",
                            replacement: "clearHighlights",
                            help: "Clear all highlighted nodes",
                            priority: 99,
                            icon: EditorGUIUtility.FindTexture("d_winbtn_mac_close")
                        )
                    };
                }
            };
        }

        // =====================
        // Icon Lookup
        // =====================
        private static Texture2D GetNodeTypeIcon(GameObject go)
        {
            var nodeTypeComp = go.GetComponent<NodeType>();
            if (nodeTypeComp == null || nodeTypeComp.reference == null)
                return EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;
            var refComponent = nodeTypeComp.reference;
            if (refComponent != null)
                return EditorGUIUtility.ObjectContent(null, refComponent.GetType()).image as Texture2D;
            return EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;
        }

        // =====================
        // Highlight & Camera
        // =====================
        private static void HighlightObjects(List<GameObject> objects)
        {
            ClearHighlights();
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
            if (camObj == null) return;
            var cam = camObj.GetComponent<Camera>();
            if (cam == null) return;
            var targetPos = new Vector3(go.transform.position.x, go.transform.position.y, cam.transform.position.z);
            EditorCoroutineUtility.StartCoroutineOwnerless(AnimateCameraMove(cam, targetPos));
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

            cam.transform.position = targetPosition;
        }

        private static void ClearHighlights()
        {
            foreach (var coloredObject in from go in HighlightedObjects.ToList()
                     where go != null
                     select go.GetComponent<ColoredObject>()
                     into coloredObject
                     where coloredObject != null
                     select coloredObject)
            {
                coloredObject.ManualClearHighlight();
            }

            HighlightedObjects.Clear();
            Debug.Log("Cleared all highlights");
        }
    }
#endif
}