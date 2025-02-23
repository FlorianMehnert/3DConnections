using TMPro;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

public class NodeGraphWindow : EditorWindow
{
    private TextField _searchField;
    private NodeGraphScriptableObject _nodeGraph;
    [MenuItem("Window/3DConnections/SearchField")]
    public static void ShowWindow()
    {
        var window = GetWindow<NodeGraphWindow>();
        window.titleContent = new GUIContent("Node Graph");
        window.Show();
    }

    private void OnEnable()
    {
        var root = rootVisualElement;
        _searchField = new TextField("Search:");
        _searchField.RegisterValueChangedCallback(evt => _nodeGraph.SearchNodes(evt.newValue));
        root.Add(_searchField);
        var nodeGraphField = new ObjectField("Node Search")
        {
            objectType = typeof(NodeGraphScriptableObject)
        };
        nodeGraphField.RegisterValueChangedCallback(evt => _nodeGraph = evt.newValue as NodeGraphScriptableObject);
        root.Add(nodeGraphField);
    }

}