using UnityEngine;

public class DebugOverlay : ModularSettingsUser
{
    [RegisterModularBoolSetting("Show overlay", "Show the DebugOverlay (F2)", "Debug", false)]
    public bool showOverlay;
    public float transparency = 0.7f;
    
    private ModularSettingsManager _settingsManager;

    // Example variables to display
    public NodeColorsScriptableObject nodeColors;
    public NodeConnectionsScriptableObject nodeConnections;
    public NodeGraphScriptableObject nodeGraph;
    public OverlaySceneScriptableObject overlayScene;

    private void Update()
    {
        // Toggle overlay with F3 key
        if (Input.GetKeyDown(KeyCode.F2))
        {
            showOverlay = !showOverlay;
        }
    }

    private void Awake()
    {
        RegisterModularSettings();
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

        // Set background color with transparency
        Color overlayColor = new Color(255, 255, 255, transparency);
        GUI.backgroundColor = overlayColor;

        // Create a box for the overlay
        GUILayout.BeginArea(new Rect(10, 10, 250, 150), GUI.skin.box);
        GUILayout.Label("Debug Overlay");
        GUILayout.Space(10);

        // Display variables
        GUILayout.Label($"There are {nodeGraph.AllNodes.Count} nodes");
        GUILayout.EndArea();
    }
}