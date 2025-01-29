using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    private float _deltaTime;

    private void Update()
    {
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        int w = Screen.width, h = Screen.height;

        var style = new GUIStyle();
        var rect = new Rect(w - 130, 10, 90, 30); // Adjust position

        style.alignment = TextAnchor.UpperRight;
        style.fontSize = h / 30;
        style.normal.textColor = Color.white;

        var fps = 1.0f / _deltaTime;
        var text = $"FPS: {fps:F1}";

        GUI.Label(rect, text, style);
    }
}