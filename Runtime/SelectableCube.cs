using UnityEngine;

/// <summary>
/// Add this MonoHBehaviour to each Cube that should be selectable
/// </summary>
public class SelectableCube : MonoBehaviour
{
    private Renderer _renderer;
    private static readonly Color DefaultColor = Color.white;
    private static readonly Color HighlightColor = Color.yellow;

    public bool IsSelected { get; private set; } = false;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetHighlight(false);
    }

    public void SetHighlight(bool highlight)
    {
        _renderer.material.color = highlight ? HighlightColor : DefaultColor;
    }

    public void Select()
    {
        IsSelected = true;
        SetHighlight(true);
    }

    public void Deselect()
    {
        IsSelected = false;
        SetHighlight(false);
    }
}