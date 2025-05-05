using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
    public bool showOverlay = false;
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
        _settingsManager = FindFirstObjectByType<ModularSettingsManager>();
        if (!_settingsManager) return;
        var showOverlaySetting = ScriptableObject.CreateInstance<BoolModularMenuData>();
        showOverlaySetting.settingName = "Show Overlay";
        showOverlaySetting.description = "Show the DebugOverlay (F2)";
        showOverlaySetting.category = "Debug";
        showOverlaySetting.defaultValue = false;
        _settingsManager.RegisterSetting(showOverlaySetting);
        showOverlaySetting.OnValueChanged += (value) =>
        {
            showOverlay = value;
        };
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