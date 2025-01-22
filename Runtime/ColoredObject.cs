using UnityEngine;

namespace _3DConnections.Runtime
{
    public class ColoredObject : MonoBehaviour
    {
        private Color _originalColor;
        private Renderer _objectRenderer;

        private void Start()
        {
            _objectRenderer = GetComponent<Renderer>();
            if (_objectRenderer != null)
            {
                _originalColor = _objectRenderer.material.color;
            }
        }

        public void SetToOriginalColor()
        {
            if (_objectRenderer  != null && _objectRenderer.material != null)
            {
                _objectRenderer.material.color = _originalColor;
            }
        }

        public void SetOriginalColor(Color color)
        {
            _originalColor = color;
        }
    }
}