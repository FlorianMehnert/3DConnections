namespace _3DConnections.Runtime.GUI
{
    using UnityEngine;
    using ScriptableObjectInventory;

    public class DebugOverlay : MonoBehaviour
    {
        public bool showOverlay;

        public float transparency = 0.7f;

        private MonoBehaviour _settingsManager;

        private void Update()
        {
            // Toggle overlay with F3 key
            if (Input.GetKeyDown(KeyCode.F2))
            {
                showOverlay = !showOverlay;
            }
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
            GUILayout.Label($"There are {ScriptableObjectInventory.Instance.graph.AllNodes.Count} nodes");
            GUILayout.EndArea();
        }
    }
}