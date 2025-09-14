using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(NodeConnectionsScriptableObject))]
    public class NodeConnectionsEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<NodeConnection, (GameObject start, GameObject end)> EditorNodeReferences = new();

        private NodeConnectionsScriptableObject _nodeConnections;

        private void OnEnable()
        {
            _nodeConnections = (NodeConnectionsScriptableObject)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Node Connections", EditorStyles.boldLabel);

            _nodeConnections.parentChildReferencesActive = EditorGUILayout.Toggle("Parent/Child References",
                _nodeConnections.parentChildReferencesActive);
            _nodeConnections.componentReferencesActive =
                EditorGUILayout.Toggle("Component References", _nodeConnections.componentReferencesActive);
            _nodeConnections.fieldReferencesActive =
                EditorGUILayout.Toggle("Field References", _nodeConnections.fieldReferencesActive);
            _nodeConnections.dynamicReferencesActive =
                EditorGUILayout.Toggle("Dynamic References", _nodeConnections.dynamicReferencesActive);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Connection Count: {_nodeConnections.connections.Count}", EditorStyles.helpBox);

            for (int i = 0; i < _nodeConnections.connections.Count; i++)
            {
                var connection = _nodeConnections.connections[i];

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Connection {i}", EditorStyles.boldLabel);

                // Get or create editor references
                if (!EditorNodeReferences.ContainsKey(connection))
                {
                    EditorNodeReferences[connection] = (null, null);
                }

                var refs = EditorNodeReferences[connection];

                // Display object fields that accept scene objects
                EditorGUI.BeginChangeCheck();
                refs.start =
                    (GameObject)EditorGUILayout.ObjectField("Start Node", refs.start, typeof(GameObject), true);
                refs.end = (GameObject)EditorGUILayout.ObjectField("End Node", refs.end, typeof(GameObject), true);

                if (EditorGUI.EndChangeCheck())
                {
                    EditorNodeReferences[connection] = refs;

                    // Optional: Update the connection if in Play mode
                    if (Application.isPlaying)
                    {
                        connection.startNode = refs.start;
                        connection.endNode = refs.end;
                    }
                }

                // Display connection properties
                connection.connectionColor = EditorGUILayout.ColorField("Color", connection.connectionColor);
                connection.lineWidth = EditorGUILayout.FloatField("Line Width", connection.lineWidth);
                connection.connectionType = EditorGUILayout.TextField("Type", connection.connectionType);
                connection.dashed = EditorGUILayout.Toggle("Dashed", connection.dashed);

                if (connection.lineRenderer)
                {
                    EditorGUILayout.ObjectField("Line Renderer", connection.lineRenderer, typeof(LineRenderer), true);
                }

                if (connection.codeReference != null)
                {
                    EditorGUILayout.LabelField("Code Reference", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    connection.codeReference.className =
                        EditorGUILayout.TextField("Class", connection.codeReference.className);
                    connection.codeReference.methodName =
                        EditorGUILayout.TextField("Method", connection.codeReference.methodName);
                    connection.codeReference.sourceFile =
                        EditorGUILayout.TextField("Source File", connection.codeReference.sourceFile);
                    connection.codeReference.lineNumber =
                        EditorGUILayout.IntField("Line", connection.codeReference.lineNumber);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Connection", GUILayout.Height(20)))
                {
                    connection.ApplyConnection();
                }

                if (GUILayout.Button("Remove", GUILayout.Height(20)))
                {
                    _nodeConnections.connections.RemoveAt(i);
                    EditorNodeReferences.Remove(connection);
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (GUILayout.Button("Add New Connection", GUILayout.Height(30)))
            {
                var newConnection = new NodeConnection
                {
                    codeReference = new CodeReference()
                };
                _nodeConnections.connections.Add(newConnection);
            }
            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_nodeConnections);
            }
        }
    }
}