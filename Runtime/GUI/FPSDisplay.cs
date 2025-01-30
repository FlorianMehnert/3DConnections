using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class FPSDisplay : MonoBehaviour
{
    private float _deltaTime;
    private readonly List<float> _fpsBuffer = new();
    private const int BufferSize = 60;
    private bool _showFPS = true;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
            _showFPS = !_showFPS;

        if (!_showFPS) return;
        _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
        var currentFPS = 1.0f / _deltaTime;
        _fpsBuffer.Add(currentFPS);
        if (_fpsBuffer.Count > BufferSize)
            _fpsBuffer.RemoveAt(0);
    }

    private void OnGUI()
    {
        if (!_showFPS) return;
        int w = Screen.width, h = Screen.height;
        var style = new GUIStyle();
        style.alignment = TextAnchor.LowerRight; // Anchor text to the bottom-right
        style.fontSize = h / 60;
        style.normal.textColor = Color.white;
        var averageFPS = CalculateAverageFPS();
        var rect = new Rect(w - 130, h - 40, 120, 30); // Adjust position and size
        var text = $"FPS: {averageFPS:F1}";
        GUI.Label(rect, text, style);
    }

    private float CalculateAverageFPS()
    {
        if (_fpsBuffer.Count == 0)
            return 0;
        var sum = _fpsBuffer.Sum();
        return sum / _fpsBuffer.Count;
    }
}