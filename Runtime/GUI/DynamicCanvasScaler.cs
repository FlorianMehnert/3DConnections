using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class DynamicCanvasScaler : MonoBehaviour
{
    private CanvasScaler _canvasScaler;

    private void Start()
    {
        _canvasScaler = GetComponent<CanvasScaler>();
        UpdateReferenceResolution();
    }

    private void Update()
    {
        UpdateReferenceResolution();
    }

    private void UpdateReferenceResolution()
    {
        if (!_canvasScaler) return;

        var screenWidth = Screen.width;
        var screenHeight = Screen.height;

        _canvasScaler.referenceResolution = new Vector2(screenWidth, screenHeight);
    }
}