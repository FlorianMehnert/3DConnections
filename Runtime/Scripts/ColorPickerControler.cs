using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class ColorPickerController : MonoBehaviour
{
    public NodeGraphScriptableObject nodeGraph; // List of objects to color
    [SerializeField] private Slider slider;
    public Color col0;
    public Color col1;
    public Color col2;
    public Color col3;
    public Color col4;
    public Color col5;
    public Color col6;
    public Color col7;
    public bool generateColors;
    public bool alternativeColors;

    private void Start()
    {
        slider = GetComponent<Slider>();
        UpdateColor();
        slider.onValueChanged.AddListener(delegate { UpdateColor(); });
    }

    private void OnValidate()
    {
        UpdateColor();
    }

    private void UpdateColor()
    {
        // Apply color to all connections
        var baseColor = new Color(0.2f, 0.6f, 1f);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);

        // Proper hue shifting without clamping incorrectly
        if (slider == null) return;
        h = (h + 1/(slider.maxValue+1) * slider.value) % 1f;
        s = Mathf.Max(0.5f, s); // Ensure some saturation
        v = Mathf.Max(0.5f, v); // Ensure some brightness

        baseColor = Color.HSVToRGB(h, s, v);

        // Generate color palette
        var colors = Colorpalette.GeneratePaletteFromBaseColor(
            baseColor: baseColor,
            prebuiltChannels: (int)slider.value,
            generateColors: generateColors,
            alternativeColors: alternativeColors
        );

        if (NodeConnectionManager.Instance == null || !NodeConnectionManager.Instance.conSo || NodeConnectionManager.Instance.conSo.connections == null)
        {
            return;
        }
        var connections = NodeConnectionManager.Instance.conSo.connections;

        // Store colors for direct access
        col0 = colors[0];
        col1 = colors[1];
        col2 = colors[2];
        col3 = colors[3];
        col4 = colors[4];
        col5 = colors[5];
        col6 = colors[6];

        // Assign colors based on connection types
        foreach (var connection in connections)
        {
            connection.connectionColor = connection.connectionType switch
            {
                "parentChildConnection" => colors[4],
                "componentConnection" => colors[5],
                "referenceConnection" => colors[6],
                _ => Color.white,
            };
            connection.ApplyConnection();
        }

        // Apply colors to nodes
        if (nodeGraph.AllNodes.Count > 0 && nodeGraph.AllNodes[0] == null)
        {
            nodeGraph.AllNodes = SceneHandler.GetNodesUsingTheNodegraphParentObject();
        }
        foreach (var node in nodeGraph.AllNodes)
        {
            var coloredObject = node.GetComponent<ColoredObject>();
            var nodeType = node.GetComponent<NodeType>();

            coloredObject.SetOriginalColor(nodeType.nodeTypeName switch
            {
                NodeTypeName.GameObject => colors[0],
                NodeTypeName.Component => colors[1],
                NodeTypeName.ScriptableObject => colors[2],
                _ => Color.white,
            });

            coloredObject.SetToOriginalColor();
        }
    }

}
