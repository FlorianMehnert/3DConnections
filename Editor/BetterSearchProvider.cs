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
    using System.Reflection;

    internal static class NodeOverlaySearchProvider
    {
        private const string ProviderId = "nodeoverlay";
        private const string FilterId = "node:";
        private static readonly List<GameObject> HighlightedObjects = new List<GameObject>();

        // =====================
        // Search Actions
        // =====================
        private static readonly Dictionary<string, System.Action<List<GameObject>>> SearchActions = new()
        {
            ["highlight"] = (objects) => HighlightObjects(objects),
            ["select"] = (objects) => Selection.objects = objects.ToArray(),
            ["focus"] = (objects) => 
            {
                if (objects.Count > 0)
                {
                    Selection.activeGameObject = objects[0];
                    SceneView.FrameLastActiveSceneView();
                }
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
                ["type"] = (go, val) => go.tag.ToLowerInvariant().Contains(val),
                ["t"] = (go, val) => // Component type search
                {
                    var components = go.GetComponents<Component>();
                    return components.Any(c => c != null && c.GetType().Name.ToLowerInvariant().Contains(val.ToLowerInvariant()));
                },
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

            // Check for hierarchy prefix
            if (query.StartsWith("h:"))
            {
                result.UseHierarchy = true;
                query = query.Substring(2).Trim();
            }

            // Split by spaces but respect quoted strings
            var parts = System.Text.RegularExpressions.Regex.Matches(query, @"[\""].+?[\""]|[^ ]+")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
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
                    if (key == "ref" || key == "t")
                        result.Tokens[key] = value;
                    else
                        result.Tokens[key] = value.ToLowerInvariant();
                }
                else
                {
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
                    
                    if (fieldInfo != null)
                    {
                        var fieldValue = fieldInfo.GetValue(component);
                        if (CompareValue(fieldValue, filter.Value))
                            return true;
                    }
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
                    GameObject[] searchScope;
                    if (parsedQuery.UseHierarchy)
                    {
                        // Search entire hierarchy
                        searchScope = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);
                    }
                    else
                    {
                        // Search only in ParentNodesObject
                        var parent = GameObject.Find("ParentNodesObject");
                        if (parent == null)
                            return null;
                        searchScope = parent.GetComponentsInChildren<Transform>()
                            .Select(t => t.gameObject)
                            .ToArray();
                    }

                    var matchingObjects = new List<GameObject>();

                    foreach (var go in searchScope)
                    {
                        // Skip if no LocalNodeConnections (unless using hierarchy search)
                        var conn = go.GetComponent<LocalNodeConnections>();
                        if (!parsedQuery.UseHierarchy && conn == null) 
                            continue;

                        if (!MatchesTokens(parsedQuery, go))
                            continue;

                        matchingObjects.Add(go);

                        string label = go.name;
                        string description = "";
                        
                        if (conn != null)
                        {
                            string outNames = string.Join(", ", conn.outConnections.Where(o => o).Select(o => o.name));
                            string inNames = string.Join(", ", conn.inConnections.Where(i => i).Select(i => i.name));
                            description = $"→ [{outNames}]\n← [{inNames}]";
                        }
                        else if (parsedQuery.UseHierarchy)
                        {
                            description = GetHierarchyPath(go);
                        }

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
                        return null;

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

                    // Add hierarchy search proposition
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
