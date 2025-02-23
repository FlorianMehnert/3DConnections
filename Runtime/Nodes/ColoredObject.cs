using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class ColoredObject : MonoBehaviour
{
    private Color _originalColor;
    private Renderer _objectRenderer;
    private LocalNodeConnections _nodeConnections;
    private int _targetLayerMask;

    private bool _isHighlighting;
    private float _highlightDuration = 1.0f;
    private float _timer;
    private UnityAction _actionAfterHighlight;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");


    private void Start()
    {
        _objectRenderer = GetComponent<Renderer>();
        if (_objectRenderer != null)
        {
            _originalColor = _objectRenderer.material.color;
        }

        _nodeConnections = GetComponent<LocalNodeConnections>();
        _targetLayerMask = LayerMask.GetMask("OverlayScene");
    }

    private void Awake()
    {
        _objectRenderer = GetComponent<Renderer>();
        if (_objectRenderer != null && _objectRenderer.material != null)
        {
            _originalColor = _objectRenderer.material.color;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            var ray = Physics2D.Raycast(SceneHandler.GetCameraOfOverlayedScene().ScreenToWorldPoint(Input.mousePosition), Vector2.zero, Mathf.Infinity, _targetLayerMask);

            if (!ray) return;
            if (ray.collider && ray.collider.gameObject && ray.collider.gameObject == gameObject)
            {
                ToggleNodeDeletionMenu();
            }
        }

        if (!_isHighlighting) return;
        _timer += Time.deltaTime;
        if (!(_timer >= _highlightDuration)) return;
        _isHighlighting = false;
        _actionAfterHighlight?.Invoke();
        ResetColor();
    }

    private void ToggleNodeDeletionMenu()
    {
        Debug.Log("toogle node deletion menu");
        if (_nodeConnections.inConnections.Count > 0)
        {
            Debug.Log("Cannot delete - node has incoming connections");
            HighlightIncomingConnections();
        }
        else
        {
            var highlight = gameObject.GetComponent<ColoredObject>();
            if (!highlight) return;
            highlight.Highlight(Color.red, 1f, DeleteNode);
        }
    }

    private void HighlightIncomingConnections()
    {
        foreach (var nodeRenderer in _nodeConnections.inConnections.Select(node => node.GetComponent<Renderer>()).Where(nodeRendererIncoming => nodeRendererIncoming != null))
        {
            var coloredObject = nodeRenderer.gameObject.GetComponent<ColoredObject>();
            coloredObject.Highlight(Color.red, .5f, ResetColor);
        }
    }

    private void DeleteNode()
    {
        foreach (var outNodeConnections in _nodeConnections.outConnections.Select(outNode => outNode.GetComponent<LocalNodeConnections>()).Where(outNodeConnections => outNodeConnections))
        {
            outNodeConnections.inConnections.Remove(gameObject);
        }

        Destroy(gameObject);
    }

    private void ResetColor()
    {
        if (_objectRenderer && _objectRenderer.material)
        {
            _objectRenderer.material.color = _originalColor;
        }

        _objectRenderer.material.DisableKeyword("_EMISSION");
    }

    public void SetColor(Color color)
    {
        if (_objectRenderer && _objectRenderer.material)
        {
            _objectRenderer.material.color = color;
        }
    }

    public void SetToOriginalColor()
    {
        if (_objectRenderer && _objectRenderer.material)
        {
            _objectRenderer.material.color = _originalColor;
        }
    }

    public void SetOriginalColor(Color color)
    {
        _originalColor = color;
    }

    /// <summary>
    /// Highlights the LineRenderer by changing its color temporarily.
    /// </summary>
    public void Highlight(Color highlightColor, float duration, UnityAction actionAfterHighlight = null, Color emissionColor = default)
    {
        if (!_objectRenderer || !_objectRenderer.material) return;
        _objectRenderer.material.color = highlightColor;
        _highlightDuration = duration;
        _timer = 0f;
        _isHighlighting = true;
        _objectRenderer.material.EnableKeyword("_EMISSION");
        if (emissionColor == default)
        {
            emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f;
        }
        _objectRenderer.material.SetColor(EmissionColor, emissionColor);
        _actionAfterHighlight = actionAfterHighlight;
    }
}