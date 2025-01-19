namespace _3DConnections.Runtime
{
    using UnityEngine;

    public class HighlightConnection : MonoBehaviour
    {
        private LineRenderer _lineRenderer;
        private Color _originalColor;
        private bool _isHighlighting;
        private float _highlightDuration = 1.0f;
        private float _timer;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null && _lineRenderer.material != null)
            {
                _originalColor = _lineRenderer.startColor;
            }
        }

        private void Update()
        {
            if (!_isHighlighting) return;
            _timer += Time.deltaTime;
            if (!(_timer >= _highlightDuration)) return;
            ResetColor();
            _isHighlighting = false;
        }

        /// <summary>
        /// Highlights the LineRenderer by changing its color temporarily.
        /// </summary>
        public void Highlight(Color highlightColor, float duration)
        {
            if (!_lineRenderer || !_lineRenderer.material) return;
            _lineRenderer.startColor = highlightColor;
            _lineRenderer.endColor = highlightColor;
            _highlightDuration = duration;
            _timer = 0f;
            _isHighlighting = true;
        }

        /// <summary>
        /// Resets the LineRenderer color to its original value.
        /// </summary>
        private void ResetColor()
        {
            if (_lineRenderer && _lineRenderer.material)
            {
                _lineRenderer.startColor = _originalColor;
                _lineRenderer.endColor = _originalColor;
            }
        }
    }
}