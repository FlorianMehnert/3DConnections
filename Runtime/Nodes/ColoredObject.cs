namespace _3DConnections.Runtime.Nodes
{
    using UnityEngine;
    using UnityEngine.Events;
    using Managers.Scene;
    using System.Linq;

    public class ColoredObject : MonoBehaviour
    {
        [SerializeField] private Color originalColor;
        private Renderer _objectRenderer;
        private LocalNodeConnections _nodeConnections;
        private int _targetLayerMask;

        private bool _isHighlighting;
        private float _highlightDuration = 1.0f;
        private bool _highlightForever;
        private float _timer;
        private UnityAction _actionAfterHighlight;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");


        private void Awake()
        {
            _objectRenderer = GetComponent<Renderer>();
            if (_objectRenderer == null || _objectRenderer.material == null) return;
            if (_objectRenderer is LineRenderer lineRenderer)
            {
                originalColor = lineRenderer.startColor;
            }
            else
            {
                originalColor = _objectRenderer.material.color;
            }
        }

        private void Start()
        {
            _objectRenderer = GetComponent<Renderer>();
            if (_objectRenderer == null || _objectRenderer.material == null) return;
            if (_objectRenderer is LineRenderer lineRenderer)
            {
                originalColor = lineRenderer.startColor;
            }
            else
            {
                originalColor = _objectRenderer.material.color;
            }
        }


        private void Update()
        {
            if (!_isHighlighting) return;
            _timer += Time.deltaTime;
            if (_highlightForever) return;
            if (!(_timer >= _highlightDuration)) return;
            _isHighlighting = false;
            _actionAfterHighlight?.Invoke();
            ResetColor();
        }

        public void ManualClearHighlight()
        {
            _isHighlighting = false;
            _highlightForever = false;
            _timer = 0f;
            _actionAfterHighlight?.Invoke();
            ResetColor();
        }

        private void ResetColor()
        {
            if (!_objectRenderer || !_objectRenderer.material) return;
            if (_objectRenderer is LineRenderer lineRenderer)
            {
                lineRenderer.startColor = originalColor;
                lineRenderer.endColor = originalColor;
                lineRenderer.material.DisableKeyword("_EMISSION");
            }
            else
            {
                _objectRenderer.material.color = originalColor;
                _objectRenderer.material.DisableKeyword("_EMISSION");
            }
        }

        public void SetToOriginalColor()
        {
            if (!_objectRenderer || !_objectRenderer.material) return;

            if (_objectRenderer is LineRenderer lineRenderer)
            {
                lineRenderer.startColor = originalColor;
                lineRenderer.endColor = originalColor;
            }
            else
            {
                _objectRenderer.material.color = originalColor;
            }
            _isHighlighting = false;
            _highlightForever = false;
        }

        public void SetOriginalColor(Color color)
        {
            originalColor = color;
        }

        /// <summary>
        /// Highlights the LineRenderer by changing its color temporarily.
        /// </summary>
        public void Highlight(
            Color highlightColor,
            float duration,
            bool highlightForever = false,
            UnityAction actionAfterHighlight = null,
            Color emissionColor = default)
        {
            if (!_objectRenderer || !_objectRenderer.material)
                return;

            // Handle LineRenderer separately
            if (_objectRenderer is LineRenderer lineRenderer)
            {
                lineRenderer.startColor = highlightColor;
                lineRenderer.endColor = highlightColor;

                lineRenderer.material.EnableKeyword("_EMISSION");
                if (emissionColor == default)
                    emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f;

                lineRenderer.material.SetColor(EmissionColor, emissionColor);
            }
            else
            {
                _objectRenderer.material.color = highlightColor;
                _objectRenderer.material.EnableKeyword("_EMISSION");
                if (emissionColor == default)
                    emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f;
                _objectRenderer.material.SetColor(EmissionColor, emissionColor);
            }

            // Common highlight logic
            _highlightDuration = duration;
            _timer = 0f;
            _isHighlighting = true;
            _actionAfterHighlight = actionAfterHighlight;

            if (highlightForever)
                _highlightForever = true;
        }
    }
}