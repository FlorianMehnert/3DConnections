using TMPro;

namespace _3DConnections.Runtime.GUI
{
    using UnityEngine;
    using UnityEngine.UI;

    public class RadiusNodeScalingIndicator : MonoBehaviour
    {
        [SerializeField] private Image circle;
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float sensitivity = 50f;

        private bool _active;

        public void Toggle()
        {
            _active = !_active;
            circle.gameObject.SetActive(_active);
            text.gameObject.SetActive(_active);

            if (_active)
            {
                UpdateCircleVisual();
            }
        }

        public void Confirm()
        {
            if (!_active) return;

            ScriptableObjectInventory.ScriptableObjectInventory.Instance.uiSettings.Radius = GetCurrentRadius();
            Toggle();
        }

        private void Update()
        {
            if (!_active) return;
            var center = RectTransformUtility.WorldToScreenPoint(null, circle.rectTransform.position);
            Vector2 mouse = Input.mousePosition;
            var dist = Vector2.Distance(mouse, center);
            var radius = Mathf.Clamp(dist / sensitivity, 1f, 1000f);
            ScriptableObjectInventory.ScriptableObjectInventory.Instance.uiSettings.Radius = radius;
            UpdateCircleVisual();
        }

        private void UpdateCircleVisual()
        {
            var radius = ScriptableObjectInventory.ScriptableObjectInventory.Instance.uiSettings.Radius;
            var diameterInPixels = radius * sensitivity * 2f;
            text.text = radius.ToString(radius > 1f ? "F0" : "F1");

            circle.rectTransform.sizeDelta = new Vector2(diameterInPixels, diameterInPixels);
        }

        private static float GetCurrentRadius()
        {
            return ScriptableObjectInventory.ScriptableObjectInventory.Instance.uiSettings.Radius;
        }
    }
}
