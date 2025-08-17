using System;

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
    using Runtime.Nodes.Connection;
    using Runtime.Nodes.Extensions;
    using System.Reflection;

    internal static class NodeOverlaySearchProvider
    {
        private const string ProviderId = "nodeoverlay";
        private const string FilterId = "node:";
        private static readonly List<GameObject> HighlightedObjects = new();

        // =====================
        // Search Actions
        // =====================
        private static readonly Dictionary<string, System.Action<List<GameObject>>> SearchActions = new()
        {
            ["highlight"] = HighlightObjects,
            ["select"] = (objects) => Selection.objects = objects.ToArray(),
            ["focus"] = (objects) =>
            {
                if (objects.Count <= 0) return;
                Selection.activeGameObject = objects[0];
                SceneView.FrameLastActiveSceneView();
            },
            ["disable"] = (objects) => 
            {
                foreach (var go in objects)
                    go.SetActive(false);
            },
            ["enable"] = (objects) => 
            {
                foreach (var go in objects)
                    go.SetActive(true);
            },
            ["log"] = (objects) => 
            {
                Debug.Log($"Found {objects.Count} objects:");
                foreach (var go in objects)
                    Debug.Log($"  - {go.name} ({go.GetInstanceID()})");
            }
        };

        // =====================
        // Extendable Token System
        // =====================
        private static readonly Dictionary<string, System.Func<GameObject, string, bool>> TokenHandlers =
            new()
            {
                ["name"] = (go, val) => go.name.ToLowerInvariant().Contains(val),
                ["tag"] = (go, val) => go.tag.ToLowerInvariant().Contains(val),
                ["type"] = (go, val) => go.tag.ToLowerInvariant().Contains(val), // alias for tag
                ["layer"] = (go, val) => LayerMask.LayerToName(go.layer).ToLowerInvariant().Contains(val),
                ["active"] = (go, val) => go.activeInHierarchy.ToString().ToLowerInvariant() == val,
                ["t"] = (go, val) => // Component type search
                {
                    var components = go.GetComponents<Component>();
                    return components.Any(c => c != null && c.GetType().Name.ToLowerInvariant().Contains(val.ToLowerInvariant()));
                },
                ["comp"] = (go, val) => // Component type search (alias)
                {
                    var components = go.GetComponents<Component>();
                    return components.Any(c => c != null && c.GetType().Name.ToLowerInvariant().Contains(val.ToLowerInvariant()));
                },
                ["nodetype"] = (go, val) => // Search by NodeType.nodeTypeName
                {
                    var nodeType = go.GetComponent<NodeType>();
                    return nodeType != null && nodeType.nodeTypeName.ToString().ToLowerInvariant().Contains(val);
                },
                ["edgetype"] = (go, val) => // Search by EdgeType.connectionType
                {
                    var edgeType = go.GetComponent<EdgeType>();
                    return edgeType != null && edgeType.connectionType.ToLowerInvariant().Contains(val);
                },
                ["end"] = (go, val) => // Connection endpoints
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
                ["in"] = (go, val) => // Incoming connections
                {
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn == null) return false;
                    return conn.inConnections.Where(i => i).Any(i => i.name.ToLowerInvariant().Contains(val));
                },
                ["out"] = (go, val) => // Outgoing connections
                {
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn == null) return false;
                    return conn.outConnections.Where(o => o).Any(o => o.name.ToLowerInvariant().Contains(val));
                },
                ["hasref"] = (go, val) => // Has reference object
                {
                    var nodeType = go.GetComponent<NodeType>();
                    return nodeType != null && nodeType.reference != null && 
                           (val == "true" ? nodeType.reference != null : nodeType.reference == null);
                },
                ["artificial"] = (go, val) => // Is artificial game object
                {
                    var isArtificial = go.GetComponent<ArtificialGameObject>() != null;
                    return val == "true" ? isArtificial : !isArtificial;
                },
                ["colored"] = (go, val) => // Has ColoredObject component
                {
                    var isColored = go.GetComponent<ColoredObject>() != null;
                    return val == "true" ? isColored : !isColored;
                },
                ["any"] = (go, val) => // Search anywhere
                {
                    string nodeName = go.name.ToLowerInvariant();
                    string connType = go.tag.ToLowerInvariant();
                    
                    // Check basic properties
                    if (nodeName.Contains(val) || connType.Contains(val))
                        return true;
                    
                    // Check connections
                    var conn = go.GetComponent<LocalNodeConnections>();
                    if (conn != null)
                    {
                        string endNames = string.Join(", ", conn.inConnections.Concat(conn.outConnections)
                            .Where(x => x).Select(x => x.name.ToLowerInvariant()));
                        if (endNames.Contains(val))
                            return true;
                    }
                    
                    // Check NodeType
                    var nodeType = go.GetComponent<NodeType>();
                    if (nodeType != null && nodeType.nodeTypeName.ToString().ToLowerInvariant().Contains(val))
                        return true;
                    
                    // Check EdgeType
                    var edgeType = go.GetComponent<EdgeType>();
                    if (edgeType != null && edgeType.connectionType.ToLowerInvariant().Contains(val))
                        return true;
                    
                    return false;
                },
                ["ref"] = (go, val) => // References specific object
                {
                    GameObject target = GameObject.Find(val.Trim('"').Trim());
                    if (target == null)
                        return false;

                    // Check NodeType reference
                    var nodeType = go.GetComponent<NodeType>();
                    if (nodeType != null && nodeType.reference == target)
                        return true;

                    // Check all component references
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

        private class PropertyFilter
        {
            public string ComponentType { get; set; }
            public string PropertyName { get; set; }
            public string Value { get; set; }
        }

        private class ParsedQuery
        {
            public Dictionary<string, string> Tokens { get; set; } = new();
            public List<PropertyFilter> PropertyFilters { get; set; } = new();
            public string Action { get; set; }
            public bool UseHierarchy { get; set; }
            public bool SearchNodes { get; set; } = true;
            public bool SearchEdges { get; set; } = false;
        }

        private static ParsedQuery ParseQuery(string query)
        {
            var result = new ParsedQuery();
            
            if (string.IsNullOrWhiteSpace(query))
                return result;

            // Check for action prefix (e.g., "highlight{...}" or "select{...}")
            var actionMatch = System.Text.RegularExpressions.Regex.Match(query, @"^(\w+)\{(.+)\}$");
            if (actionMatch.Success)
            {
                result.Action = actionMatch.Groups[1].Value.ToLowerInvariant();
                query = actionMatch.Groups[2].Value;
            }

            // Check for scope prefixes
            if (query.StartsWith("h:"))
            {
                result.UseHierarchy = true;
                query = query.Substring(2).Trim();
            }
            else if (query.StartsWith("edges:"))
            {
                result.SearchNodes = false;
                result.SearchEdges = true;
                query = query.Substring(6).Trim();
            }
            else if (query.StartsWith("all:"))
            {
                result.SearchNodes = true;
                result.SearchEdges = true;
                query = query.Substring(4).Trim();
            }

            // Split by spaces but respect quoted strings
            var parts = System.Text.RegularExpressions.Regex.Matches(query, @"[\""].+?[\""]|[^ ]+")
                .Select(m => m.Value)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            foreach (var part in parts)
            {
                // Check for property filter pattern: #ComponentType.property:"value"
                var propertyMatch = System.Text.RegularExpressions.Regex.Match(part, 
                    @"#(\w+)\.(\w+):""?([^""]+)""?");
                
                if (propertyMatch.Success)
                {
                    result.PropertyFilters.Add(new PropertyFilter
                    {
                        ComponentType = propertyMatch.Groups[1].Value,
                        PropertyName = propertyMatch.Groups[2].Value,
                        Value = propertyMatch.Groups[3].Value.Trim('"')
                    });
                    continue;
                }

                // Regular token parsing
                int sep = part.IndexOf(':');
                if (sep > 0 && sep < part.Length - 1)
                {
                    string key = part.Substring(0, sep).ToLowerInvariant();
                    string value = part.Substring(sep + 1).Trim().Trim('"');
                    
                    // Don't lowercase the value for certain tokens
                    if (key == "ref" || key == "t" || key == "comp")
                        result.Tokens[key] = value;
                    else
                        result.Tokens[key] = value.ToLowerInvariant();
                }
                else
                {
                    // Default to 'any' search for plain terms
                    result.Tokens["any"] = part.ToLowerInvariant();
                }
            }

            return result;
        }

        private static bool MatchesTokens(ParsedQuery query, GameObject go)
        {
            // First check regular tokens
            foreach (var kvp in query.Tokens)
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

            // Then check property filters
            foreach (var filter in query.PropertyFilters)
            {
                if (!CheckPropertyFilter(go, filter))
                    return false;
            }

            return true;
        }

        private static bool CheckPropertyFilter(GameObject go, PropertyFilter filter)
        {
            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;
                
                var componentType = component.GetType();
                if (!componentType.Name.Equals(filter.ComponentType, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                // Try to get the property value
                var propertyInfo = componentType.GetProperty(filter.PropertyName, 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (propertyInfo == null)
                {
                    // Try field if property not found
                    var fieldInfo = componentType.GetField(filter.PropertyName, 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo == null) continue;
                    var fieldValue = fieldInfo.GetValue(component);
                    if (CompareValue(fieldValue, filter.Value))
                        return true;
                }
                else
                {
                    var propValue = propertyInfo.GetValue(component);
                    if (CompareValue(propValue, filter.Value))
                        return true;
                }
            }

            return false;
        }

        private static bool CompareValue(object actualValue, string expectedValue)
        {
            if (actualValue == null)
                return string.IsNullOrEmpty(expectedValue);

            string actualStr = actualValue.ToString().ToLowerInvariant();
            string expectedStr = expectedValue.ToLowerInvariant();
            
            // Support partial matching for strings
            if (actualValue is string)
                return actualStr.Contains(expectedStr);
            
            // Exact match for other types
            return actualStr.Equals(expectedStr);
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

                    var parsedQuery = ParseQuery(context.searchQuery);
                    
                    // Determine search scope
                    GameObject[] searchScope = GetSearchScope(parsedQuery);
                    if (searchScope == null || searchScope.Length == 0)
                        return null;

                    var matchingObjects = new List<GameObject>();

                    foreach (var go in searchScope)
                    {
                        if (!MatchesTokens(parsedQuery, go))
                            continue;

                        matchingObjects.Add(go);

                        string label = go.name;
                        string description = GetObjectDescription(go, parsedQuery);
                        Texture2D thumbnail = GetNodeTypeIcon(go);

                        var item = provider.CreateItem(
                            id: go.GetInstanceID().ToString(),
                            score: GetSearchScore(go, parsedQuery),
                            label: label,
                            description: description,
                            thumbnail: thumbnail,
                            data: go
                        );

                        items.Add(item);
                    }

                    // Execute action if specified
                    if (!string.IsNullOrEmpty(parsedQuery.Action) && 
                        SearchActions.TryGetValue(parsedQuery.Action, out var action))
                    {
                        action(matchingObjects);
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
                    HighlightSingleObject(go);
                },

                fetchPropositions = (context, _) =>
                {
                    if (string.IsNullOrEmpty(context.searchQuery))
                        return GetDefaultPropositions();

                    var propositions = new List<SearchProposition>();

                    // Add action propositions
                    foreach (var action in SearchActions.Keys)
                    {
                        propositions.Add(new SearchProposition(
                            category: "Actions",
                            label: $"{char.ToUpper(action[0])}{action.Substring(1)} Results",
                            replacement: $"{action}{{{context.searchQuery}}}",
                            help: $"Execute {action} on all matching objects",
                            priority: 100,
                            icon: GetActionIcon(action)
                        ));
                    }

                    // Add scope propositions
                    if (!context.searchQuery.StartsWith("h:"))
                    {
                        propositions.Add(new SearchProposition(
                            category: "Scope",
                            label: "Search in Hierarchy",
                            replacement: $"h: {context.searchQuery}",
                            help: "Search in entire scene hierarchy",
                            priority: 90,
                            icon: EditorGUIUtility.FindTexture("d_UnityEditor.SceneHierarchyWindow")
                        ));
                    }

                    if (!context.searchQuery.StartsWith("edges:"))
                    {
                        propositions.Add(new SearchProposition(
                            category: "Scope",
                            label: "Search Edges",
                            replacement: $"edges: {context.searchQuery}",
                            help: "Search only in edge objects",
                            priority: 85,
                            icon: EditorGUIUtility.FindTexture("d_winbtn_graph")
                        ));
                    }

                    if (!context.searchQuery.StartsWith("all:"))
                    {
                        propositions.Add(new SearchProposition(
                            category: "Scope",
                            label: "Search All (Nodes + Edges)",
                            replacement: $"all: {context.searchQuery}",
                            help: "Search both nodes and edges",
                            priority: 82,
                            icon: EditorGUIUtility.FindTexture("d_UnityEditor.HierarchyWindow")
                        ));
                    }

                    propositions.Add(new SearchProposition(
                        category: "Actions",
                        label: "Clear Highlights",
                        replacement: "clearHighlights",
                        help: "Clear all highlighted nodes",
                        priority: 80,
                        icon: EditorGUIUtility.FindTexture("d_winbtn_mac_close")
                    ));

                    return propositions;
                }
            };
        }

        private static GameObject[] GetSearchScope(ParsedQuery query)
        {
            if (query.UseHierarchy)
            {
                // Search entire hierarchy
                return Object.FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
            }

            var searchScope = new List<GameObject>();

            // Search in ParentNodesObject if nodes are requested
            if (query.SearchNodes)
            {
                var parentNodes = GameObject.Find("ParentNodesObject");
                if (parentNodes != null)
                {
                    var nodeObjects = parentNodes.GetComponentsInChildren<Transform>()
                        .Where(t => t != parentNodes.transform) // Exclude parent itself
                        .Select(t => t.gameObject)
                        .ToArray();
                    searchScope.AddRange(nodeObjects);
                }
            }

            // Search in ParentEdgesObject if edges are requested
            if (query.SearchEdges)
            {
                var parentEdges = GameObject.Find("ParentEdgesObject");
                if (parentEdges != null)
                {
                    var edgeObjects = parentEdges.GetComponentsInChildren<Transform>()
                        .Where(t => t != parentEdges.transform) // Exclude parent itself
                        .Select(t => t.gameObject)
                        .ToArray();
                    searchScope.AddRange(edgeObjects);
                }
            }

            return searchScope.ToArray();
        }

        private static string GetObjectDescription(GameObject go, ParsedQuery query)
        {
            var descriptionParts = new List<string>();

            // Add path if hierarchy search
            if (query.UseHierarchy)
            {
                descriptionParts.Add(GetHierarchyPath(go));
            }

            // Add connection info for nodes
            var conn = go.GetComponent<LocalNodeConnections>();
            if (conn != null)
            {
                string outNames = string.Join(", ", conn.outConnections.Where(o => o).Select(o => o.name));
                string inNames = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name));
                descriptionParts.Add($"→ [{outNames}]\n← [{inNames}]");
            }

            // Add node type info
            var nodeType = go.GetComponent<NodeType>();
            if (nodeType != null)
            {
                descriptionParts.Add($"Type: {nodeType.nodeTypeName}");
                if (nodeType.reference != null)
                {
                    descriptionParts.Add($"Ref: {nodeType.reference.name}");
                }
            }

            // Add edge type info
            var edgeType = go.GetComponent<EdgeType>();
            if (edgeType != null)
            {
                descriptionParts.Add($"Edge: {edgeType.connectionType}");
            }

            // Add component info
            var componentNames = go.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name)
                .Take(3); // Limit to avoid clutter
            
            if (componentNames.Any())
            {
                descriptionParts.Add($"Components: {string.Join(", ", componentNames)}");
            }

            return string.Join(" | ", descriptionParts);
        }

        private static int GetSearchScore(GameObject go, ParsedQuery query)
        {
            int score = 0;

            // Boost exact name matches
            foreach (var token in query.Tokens)
            {
                if (token.Key == "name" && go.name.Equals(token.Value, System.StringComparison.OrdinalIgnoreCase))
                    score += 100;
                else if (token.Key == "any" && go.name.ToLowerInvariant().Contains(token.Value))
                    score += 50;
            }

            // Boost objects with more relevant components
            if (go.GetComponent<NodeType>() != null) score += 10;
            if (go.GetComponent<EdgeType>() != null) score += 10;
            if (go.GetComponent<LocalNodeConnections>() != null) score += 5;

            return score;
        }

        private static List<SearchProposition> GetDefaultPropositions()
        {
            return new List<SearchProposition>
            {
                new SearchProposition(
                    category: "Examples",
                    label: "Search by name",
                    replacement: "name:MyNode",
                    help: "Find objects by name",
                    priority: 100
                ),
                new SearchProposition(
                    category: "Examples",
                    label: "Search by node type",
                    replacement: "nodetype:Component",
                    help: "Find nodes by their type",
                    priority: 95
                ),
                new SearchProposition(
                    category: "Examples",
                    label: "Search edges",
                    replacement: "edges: connectiontype:default",
                    help: "Search edge objects",
                    priority: 90
                ),
                new SearchProposition(
                    category: "Examples",
                    label: "Search by component",
                    replacement: "t:ColoredObject",
                    help: "Find objects with specific components",
                    priority: 85
                )
            };
        }

        private static string GetHierarchyPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static Texture2D GetActionIcon(string action)
        {
            return action switch
            {
                "highlight" => EditorGUIUtility.FindTexture("d_winbtn_mac_max"),
                "select" => EditorGUIUtility.FindTexture("d_rectselection"),
                "focus" => EditorGUIUtility.FindTexture("d_ViewToolZoom"),
                "disable" => EditorGUIUtility.FindTexture("d_scenevis_hidden_hover"),
                "enable" => EditorGUIUtility.FindTexture("d_scenevis_visible_hover"),
                "log" => EditorGUIUtility.FindTexture("d_console.infoicon"),
                _ => null
            };
        }

        // =====================
        // Icon Lookup
        // =====================
        private static Texture2D GetNodeTypeIcon(GameObject go)
        {
            // Check for EdgeType first
            var edgeType = go.GetComponent<EdgeType>();
            if (edgeType != null)
            {
                return EditorGUIUtility.FindTexture("d_winbtn_graph") ?? 
                       EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;
            }

            // Check for NodeType
            var nodeTypeComp = go.GetComponent<NodeType>();
            if (nodeTypeComp?.reference != null)
            {
                return EditorGUIUtility.ObjectContent(null, nodeTypeComp.reference.GetType()).image as Texture2D;
            }

            // Default icon
            return EditorGUIUtility.ObjectContent(go, typeof(GameObject)).image as Texture2D;
        }

        // =====================
        // Highlight & Camera
        // =====================
        private static void HighlightObjects(List<GameObject> objects)
        {
            ClearHighlights();

            var parentNodes = GameObject.Find("ParentNodesObject");
            var parentEdges = GameObject.Find("ParentEdgesObject");

            var allObjects = new List<GameObject>();

            // Get all nodes
            if (parentNodes != null)
            {
                allObjects.AddRange(from Transform child in parentNodes.transform select child.gameObject);
            }
            
            // Get all edges
            if (parentEdges != null)
            {
                allObjects.AddRange(from Transform child in parentEdges.transform select child.gameObject);
            }

            // Objects that should be faded out (all objects minus the ones we're highlighting)
            var fadeOutObjects = allObjects.Except(objects).ToList();

            // Highlight selected objects in bright red
            foreach (var go in objects)
            {
                var coloredObject = go.GetComponent<ColoredObject>();
                if (coloredObject == null) continue;

                coloredObject.Highlight(Color.red, highlightForever: true, duration: 1);
                HighlightedObjects.Add(go);
            }

            // Fade out all others with a dimmed color
            foreach (var go in fadeOutObjects)
            {
                var coloredObject = go.GetComponent<ColoredObject>();
                if (coloredObject == null) continue;

                var dimColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
                coloredObject.Highlight(dimColor, highlightForever: true, duration: 1);
                HighlightedObjects.Add(go);
            }

            Debug.Log($"Highlighted {objects.Count} matching objects, faded out {fadeOutObjects.Count} others");
        }

        private static void HighlightSingleObject(GameObject go)
        {
            var coloredObject = go.GetComponent<ColoredObject>();
            if (coloredObject != null)
            {
                coloredObject.Highlight(Color.yellow, 2, highlightForever: false);
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
            // Create a copy of the list to avoid modification during iteration
            var objectsToProcess = new List<GameObject>(HighlightedObjects);
            
            foreach (ColoredObject coloredObject in from go in objectsToProcess where go != null select go.GetComponent<ColoredObject>() into coloredObject where coloredObject != null select coloredObject)
            {
                coloredObject.ManualClearHighlight();
            }

            HighlightedObjects.Clear();
            Debug.Log("Cleared all highlights");
        }
    }
#endif
}