using _3DConnections.Runtime.Managers.Scene;
using _3DConnections.Runtime.ScriptableObjects;
using System.Collections.Generic;
using System.Linq;

namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;
    using Runtime.Nodes;
    using Runtime.Managers;

    [CustomEditor(typeof(LocalNodeConnections))]
    public class LocalNodeConnectionsEditor : Editor
    {
        private SerializedProperty _inConnectionsProp;
        private SerializedProperty _outConnectionsProp;

        private Vector2 _inScrollPos;
        private Vector2 _outScrollPos;

        private const float ScrollViewHeight = 200f;

        // Caching variables
        private static readonly Dictionary<string, string> ConnectionTypeCache = new();
        private static readonly Dictionary<string, NodeConnectionsScriptableObject> NodeConnectionsSoCache = new();
        private static bool _cacheInitialized;
        private static double _lastCacheUpdateTime;
        private const double CacheRefreshInterval = 10.0;
        
        private static readonly Dictionary<string, int> EdgePingIndexCache = new();

        private void OnEnable()
        {
            _inConnectionsProp = serializedObject.FindProperty("inConnections");
            _outConnectionsProp = serializedObject.FindProperty("outConnections");
            
            InitializeCache();
        }

        private void OnDisable()
        {
            // Clear cache when editor is disabled to prevent memory leaks
            ClearCache();
        }

        public override void OnInspectorGUI()
        {
            // Check if cache needs refreshing
            if (EditorApplication.timeSinceStartup - _lastCacheUpdateTime > CacheRefreshInterval)
            {
                RefreshCache();
            }

            serializedObject.Update();

            DrawConnectionList(_inConnectionsProp, "In Connections", "In", ref _inScrollPos);
            EditorGUILayout.Space();
            DrawConnectionList(_outConnectionsProp, "Out Connections", "Out", ref _outScrollPos);

            EditorGUILayout.Space();
            if (GUILayout.Button("Add In Connection"))
            {
                AddConnection(_inConnectionsProp);
            }
            if (GUILayout.Button("Add Out Connection"))
            {
                AddConnection(_outConnectionsProp);
            }

            // Add button to manually refresh cache
            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh Connection Cache"))
            {
                RefreshCache();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void InitializeCache()
        {
            if (_cacheInitialized) return;
            RefreshCache();
            _cacheInitialized = true;
        }

        private static void RefreshCache()
        {
            ConnectionTypeCache.Clear();
            NodeConnectionsSoCache.Clear();

            // Load all NodeConnectionsScriptableObject assets
            string[] guids = AssetDatabase.FindAssets("t:NodeConnectionsScriptableObject");
            
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                NodeConnectionsScriptableObject nodeConnectionsSo = AssetDatabase.LoadAssetAtPath<NodeConnectionsScriptableObject>(path);
                
                if (nodeConnectionsSo?.connections == null) continue;
                
                NodeConnectionsSoCache[guid] = nodeConnectionsSo;
                
                foreach (var connection in nodeConnectionsSo.connections)
                {
                    if (connection?.startNode == null || connection.endNode == null) continue;
                    
                    var connectionKey1 = GetConnectionKey(connection.startNode, connection.endNode);
                    var connectionKey2 = GetConnectionKey(connection.endNode, connection.startNode);
                    var connectionType = string.IsNullOrEmpty(connection.connectionType) ? "Default" : connection.connectionType;
                    
                    ConnectionTypeCache[connectionKey1] = connectionType;
                    ConnectionTypeCache[connectionKey2] = connectionType;
                }
            }
            
            _lastCacheUpdateTime = EditorApplication.timeSinceStartup;
            Debug.Log($"Connection cache refreshed. Found {ConnectionTypeCache.Count / 2} unique connections.");
        }

        private static void ClearCache()
        {
            ConnectionTypeCache.Clear();
            NodeConnectionsSoCache.Clear();
            EdgePingIndexCache.Clear();
            _cacheInitialized = false;
        }

        private static string GetConnectionKey(GameObject node1, GameObject node2)
        {
            if (node1 == null || node2 == null) return "";
            return $"{node1.GetInstanceID()}_{node2.GetInstanceID()}";
        }

        private void DrawConnectionList(SerializedProperty listProp, string label, string prefix, ref Vector2 scrollPos)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            if (listProp.arraySize > 0)
            {
                Rect scrollRect = GUILayoutUtility.GetRect(
                    0,
                    float.MaxValue,
                    0,
                    ScrollViewHeight
                );

                // Begin scroll view
                scrollPos = GUI.BeginScrollView(scrollRect, scrollPos,
                    new Rect(0, 0, scrollRect.width - 16f, listProp.arraySize * EditorGUIUtility.singleLineHeight));

                for (var i = 0; i < listProp.arraySize; i++)
                {
                    var lineRect = new Rect(
                        0,
                        i * EditorGUIUtility.singleLineHeight,
                        scrollRect.width - 16f, // subtract scrollbar width
                        EditorGUIUtility.singleLineHeight
                    );

                    var element = listProp.GetArrayElementAtIndex(i);
            
                    // Get connection type for display using cached lookup
                    string connectionType = "";
                    if (element.objectReferenceValue != null)
                    {
                        var targetNode = element.objectReferenceValue as GameObject;
                        connectionType = GetConnectionTypeCached(target as LocalNodeConnections, targetNode);
                        connectionType = $" ({connectionType})";
                    }
            
                    element.objectReferenceValue = EditorGUI.ObjectField(
                        lineRect,
                        $"{prefix} {i}{connectionType}",
                        element.objectReferenceValue,
                        typeof(GameObject),
                        true
                    );

                    HandleContextMenu(element, lineRect, target as LocalNodeConnections);
                }

                GUI.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox($"No {label} defined.", MessageType.Info);
            }
        }

        private static string GetConnectionTypeCached(LocalNodeConnections localNodeConnections, GameObject targetNode)
        {
            if (localNodeConnections == null || targetNode == null)
                return "Unknown";

            GameObject currentNode = localNodeConnections.gameObject;
            var connectionKey = GetConnectionKey(currentNode, targetNode);
            
            // Try to get from cache first
            if (ConnectionTypeCache.TryGetValue(connectionKey, out string cachedType))
            {
                return cachedType;
            }
            
            // If not in cache, fall back to original method and cache the result
            var connectionType = GetConnectionTypeOriginal(localNodeConnections, targetNode);
            ConnectionTypeCache[connectionKey] = connectionType;
            
            return connectionType;
        }

        private static string GetConnectionTypeOriginal(LocalNodeConnections localNodeConnections, GameObject targetNode)
        {
            if (localNodeConnections == null || targetNode == null)
                return "Unknown";

            var currentNode = localNodeConnections.gameObject;
            
            foreach (var connection in NodeConnectionsSoCache.Select(kvp => kvp.Value).Where(nodeConnectionsSo => nodeConnectionsSo?.connections != null).SelectMany(nodeConnectionsSo => from connection in nodeConnectionsSo.connections where connection != null let isMatchingConnection = (connection.startNode == currentNode && connection.endNode == targetNode) ||
                         (connection.startNode == targetNode && connection.endNode == currentNode) where isMatchingConnection select connection))
            {
                return string.IsNullOrEmpty(connection.connectionType) ? "Default" : connection.connectionType;
            }
            
            return "Not Found";
        }

        private static void HandleContextMenu(SerializedProperty element, Rect fieldRect, LocalNodeConnections localNodeConnections)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick || !fieldRect.Contains(e.mousePosition)) return;
            
            var menu = new GenericMenu();
            
            if (element.objectReferenceValue != null)
            {
                var go = element.objectReferenceValue as GameObject;
                var currentNode = localNodeConnections.gameObject;
                
                menu.AddItem(new GUIContent("Focus on this node"), false, () =>
                {
                    if (!go) return;
                    CameraController.AdjustCameraToViewObjects(SceneHandler.GetCameraOfOverlayedScene(), new [] {go});
                });
                
                menu.AddItem(new GUIContent("Ping Edge"), false, () =>
                {
                    PingEdgeConnection(currentNode, go);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Focus on this node"));
                menu.AddDisabledItem(new GUIContent("Select this connection"));
                menu.AddDisabledItem(new GUIContent("Ping Edge"));
            }

            menu.ShowAsContext();
            e.Use();
        }

        private static void PingEdgeConnection(GameObject startNode, GameObject endNode)
        {
            var connectionKey = GetConnectionKey(startNode, endNode);
    
            var matchingConnections = NodeConnectionsSoCache.Select(kvp => kvp.Value)
                .Where(nodeConnectionsSo => nodeConnectionsSo?.connections != null)
                .SelectMany(nodeConnectionsSo => nodeConnectionsSo.connections)
                .Where(connection => connection != null && connection.lineRenderer)
                .Where(connection => 
                    (connection.startNode == startNode && connection.endNode == endNode) ||
                    (connection.startNode == endNode && connection.endNode == startNode))
                .ToList();
    
            if (matchingConnections.Count == 0)
            {
                Debug.LogWarning($"No edge connection found between {startNode.name} and {endNode.name}");
                return;
            }
    
            int currentIndex = EdgePingIndexCache.GetValueOrDefault(connectionKey, 0);
    
            // Ensure index is within bounds
            currentIndex %= matchingConnections.Count;
    
            var connectionToPing = matchingConnections[currentIndex];
    
            EditorGUIUtility.PingObject(connectionToPing.lineRenderer.gameObject);
    
            Selection.activeGameObject = connectionToPing.lineRenderer.gameObject;
    
            // Increment index for next ping
            EdgePingIndexCache[connectionKey] = (currentIndex + 1) % matchingConnections.Count;
        }


        private static void AddConnection(SerializedProperty listProp)
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = null;
        }
    }
}
