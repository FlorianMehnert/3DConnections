using System.Collections.Generic;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor
{
    public class NodeConnectionEditorWindow : EditorWindow
{
    private NodeConnectionsScriptableObject scriptableObject;
    private Vector2 scrollPosition;
    private Dictionary<int, GameObject> tempStartNodes = new Dictionary<int, GameObject>();
    private Dictionary<int, GameObject> tempEndNodes = new Dictionary<int, GameObject>();
    
    [MenuItem("Window/3D Connections/Node Connection Editor")]
    public static void ShowWindow()
    {
        GetWindow<NodeConnectionEditorWindow>("Node Connections");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Node Connection Editor", EditorStyles.boldLabel);
        
        // Field to drag and drop the ScriptableObject
        scriptableObject = (NodeConnectionsScriptableObject)EditorGUILayout.ObjectField(
            "Connections Asset", 
            scriptableObject, 
            typeof(NodeConnectionsScriptableObject), 
            false
        );
        
        if (scriptableObject == null)
        {
            EditorGUILayout.HelpBox("Please assign a NodeConnectionsScriptableObject", MessageType.Info);
            return;
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        for (int i = 0; i < scriptableObject.connections.Count; i++)
        {
            var connection = scriptableObject.connections[i];
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Use temporary dictionaries to store scene references
            if (!tempStartNodes.ContainsKey(i))
                tempStartNodes[i] = connection.startNode;
            if (!tempEndNodes.ContainsKey(i))
                tempEndNodes[i] = connection.endNode;
            
            tempStartNodes[i] = (GameObject)EditorGUILayout.ObjectField(
                "Start Node", 
                tempStartNodes[i], 
                typeof(GameObject), 
                true
            );
            
            tempEndNodes[i] = (GameObject)EditorGUILayout.ObjectField(
                "End Node", 
                tempEndNodes[i], 
                typeof(GameObject), 
                true
            );
            
            // Update at runtime
            if (Application.isPlaying)
            {
                connection.startNode = tempStartNodes[i];
                connection.endNode = tempEndNodes[i];
            }
            
            EditorGUILayout.EndVertical();
        }
        
        EditorGUILayout.EndScrollView();
    }
}

}