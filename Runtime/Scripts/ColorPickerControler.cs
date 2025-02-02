using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class ColorPickerController : MonoBehaviour
{
    public NodeGraphScriptableObject nodeGraph; // List of objects to color
    private Slider _slider;
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
        _slider = GetComponent<Slider>();
        UpdateColor();
        _slider.onValueChanged.AddListener(delegate { UpdateColor(); });
    }

    private void OnValidate()
    {
        var baseColor = new Color(0.2f, 0.6f, 1f);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);
        if (_slider == null) return;
        h = (h + 0.33f * _slider.value) % 1f;
        baseColor = Color.HSVToRGB(h, s, v);
        var colors = Colorpalette.GeneratePaletteFromBaseColor(baseColor: baseColor, prebuiltChannels: (int)_slider.value, generateColors:generateColors, alternativeColors:alternativeColors);
        col0 = Color.HSVToRGB(h, s, v);
        col1 = colors[0];
        col2 = colors[1];
        col3 = colors[2];
        col4 = colors[3];
        col5 = colors[4];
        col6 = colors[5];
        col7 = colors[6];
    }

    private void UpdateColor()
    {
        // Apply color to all connections
        var baseColor = new Color(0.2f, 0.6f, 1f);
        Color.RGBToHSV(baseColor, out var h, out var s, out var v);

        // Proper hue shifting without clamping incorrectly
        h = (h + 0.33f * _slider.value) % 1f;
        s = Mathf.Max(0.5f, s); // Ensure some saturation
        v = Mathf.Max(0.5f, v); // Ensure some brightness

        baseColor = Color.HSVToRGB(h, s, v);

        // Generate color palette
        var colors = Colorpalette.GeneratePaletteFromBaseColor(
            baseColor: baseColor,
            prebuiltChannels: (int)_slider.value,
            generateColors: generateColors,
            alternativeColors: alternativeColors
        );

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
        foreach (var node in nodeGraph.AllNodes)
        {
            var coloredObject = node.GetComponent<ColoredObject>();
            var nodeType = node.GetComponent<NodeType>();

            coloredObject.SetOriginalColor(nodeType.nodeTypeName switch
            {
                "GameObject" => colors[4],
                "Component" => colors[5],
                "ScriptableObject" => colors[6],
                _ => Color.white,
            });

            coloredObject.SetToOriginalColor();
        }
    }

}
