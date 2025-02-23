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
        _searchField.RegisterValueChangedCallback(evt => SearchNodes(evt.newValue));
        root.Add(_searchField);
        var nodeGraphField = new ObjectField("Node Search")
        {
            objectType = typeof(NodeGraphScriptableObject)
        };
        nodeGraphField.RegisterValueChangedCallback(evt => _nodeGraph = evt.newValue as NodeGraphScriptableObject);
        root.Add(nodeGraphField);
    }

    private void SearchNodes(string searchString)
    {
        if (_nodeGraph == null || _nodeGraph.AllNodes == null)
            return;
        foreach (var nodeObj in _nodeGraph.AllNodes)
        {
            var node = nodeObj.GetComponent<ColoredObject>();
            if (node == null) continue;
            if (string.IsNullOrEmpty(searchString) || nodeObj.name.Contains(searchString, System.StringComparison.OrdinalIgnoreCase))
            {
                node.SetColor(Color.white);
                ChangeTextSize(nodeObj, 30f);
            }
            else
            {
                node.SetToOriginalColor();
                ChangeTextSize(nodeObj, 1.5f);
            }
        }
    }

    private static void ChangeTextSize(GameObject node, float size)
    {
        var textComponent = node.transform.GetComponentInChildren<TextMeshPro>();
        if (textComponent != null)
        {
            textComponent.fontSize = size;
        }
    }

}