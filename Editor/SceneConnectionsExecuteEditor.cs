using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NodeConnectionManager))]
public class SceneConnectionsExecuteEditor : Editor
{
    private NodeConnectionManager _nodeConnectionManager;
    private Color _highlightColor = Color.red;
    private float _duration = 5f;
    private void OnEnable()
    {
        _nodeConnectionManager = (NodeConnectionManager)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.LabelField("Execute Functions of NodeConnectionsManager", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _highlightColor = EditorGUILayout.ColorField("Highlight Color", _highlightColor);
        _duration = EditorGUILayout.FloatField(value:_duration, label:"Duration");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginVertical();
        if (GUILayout.Button("Highlight Cycles"))
        {
            _nodeConnectionManager.HighlightCycles(_highlightColor, _duration);
        }
        if (GUILayout.Button("Apply Forces to Nodes"))
        {
            _nodeConnectionManager.SeparateCycles();
        }
        EditorGUILayout.EndVertical();
    }
}