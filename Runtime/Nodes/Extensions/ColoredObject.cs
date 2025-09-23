namespace _3DConnections.Runtime.Nodes
{
    using UnityEngine;
    using UnityEngine.Events;

    public class ColoredObject : MonoBehaviour
    {
        [SerializeField] private Color originalColor = Color.white;
        private Renderer _objectRenderer;
        private Material _materialInstance;

        private bool _isHighlighting;
        private float _highlightDuration = 1.0f;
        private bool _highlightForever;
        private float _timer;
        private UnityAction _actionAfterHighlight;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _objectRenderer = GetComponent<Renderer>();
            if (_objectRenderer == null) return;

            // Create unique material instance
            _materialInstance = _objectRenderer.material;

            if (_objectRenderer is LineRenderer)
            {
                if (_materialInstance.HasProperty(ColorProperty))
                    originalColor = _materialInstance.GetColor(ColorProperty);
            }
            else
            {
                originalColor = _materialInstance.color;
            }
        }

        private void Update()
        {
            if (!_isHighlighting) return;

            _timer += Time.deltaTime;
            if (_highlightForever) return;

            if (_timer >= _highlightDuration)
            {
                _isHighlighting = false;
                _actionAfterHighlight?.Invoke();
                ResetColor();
            }
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
            if (!_materialInstance) return;

            if (_objectRenderer is LineRenderer)
            {
                _materialInstance.SetColor(ColorProperty, originalColor);
            }
            else
            {
                _materialInstance.color = originalColor;
                _materialInstance.DisableKeyword("_EMISSION");
            }
        }

        public void SetToOriginalColor()
        {
            if (!_materialInstance) return;

            if (_objectRenderer is LineRenderer)
            {
                _materialInstance.SetColor(ColorProperty, originalColor);
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
            if (!_materialInstance) return;

            if (_objectRenderer is LineRenderer)
                _materialInstance.SetColor(ColorProperty, originalColor);
            else
                _materialInstance.color = originalColor;
        }

        public Color GetOriginalColor() => originalColor;

        public void Highlight(
            Color highlightColor,
            float duration,
            bool highlightForever = false,
            UnityAction actionAfterHighlight = null,
            Color emissionColor = default)
        {
            if (!_materialInstance)
                return;

            if (_objectRenderer is LineRenderer)
            {
                _materialInstance.SetColor(ColorProperty, highlightColor);
            }
            else
            {
                _materialInstance.color = highlightColor;
                _materialInstance.EnableKeyword("_EMISSION");
                if (emissionColor == default)
                    emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f;
                _materialInstance.SetColor(EmissionColor, emissionColor);
            }

            _highlightDuration = duration;
            _timer = 0f;
            _isHighlighting = true;
            _actionAfterHighlight = actionAfterHighlight;

            if (highlightForever)
                _highlightForever = true;
        }
    }
}
