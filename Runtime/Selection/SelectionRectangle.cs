using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _3DConnections.Runtime.Selection
{
    public class SelectionRectangle : MonoBehaviour
    {
        [SerializeField] private RectTransform selectionRectangle;
        [SerializeField] private Color selectionRectColor = new(0.3f, 0.5f, 0.8f, 0.3f);
        [SerializeField] private string targetLayerNodesName = "Nodes";
        [SerializeField] private string targetLayerEdgesName = "Edges";

        private Vector2 _selectionStartPos;
        private bool _isDrawing;
        private Camera _targetCamera;
        private Rect _currentRect;
        private InputAction _mousePositionAction;

        public bool IsDrawing => _isDrawing;
        public Rect CurrentRect => _currentRect;

        public void Initialize(Camera camera)
        {
            _targetCamera = camera;

            if (selectionRectangle != null)
            {
                selectionRectangle.gameObject.SetActive(false);
                UpdateColor();
            }
            else
            {
                Debug.LogError("SelectionRectangle RectTransform is not assigned!");
            }
        }

        public void SetMousePositionAction(InputAction mousePositionAction)
        {
            _mousePositionAction = mousePositionAction;
        }

        public void StartSelection(Vector2 screenPosition)
        {
            if (!selectionRectangle)
            {
                return;
            }

            _isDrawing = true;
            _selectionStartPos = screenPosition;

            selectionRectangle.gameObject.SetActive(true);
            UpdateRectangle(screenPosition);
        }

        public void UpdateSelection(Vector2 currentScreenPosition)
        {
            if (!_isDrawing)
            {
                return;
            }

            UpdateRectangle(currentScreenPosition);
        }

        public void EndSelection()
        {
            _isDrawing = false;

            if (selectionRectangle != null)
                selectionRectangle.gameObject.SetActive(false);
        }

        public List<GameObject> GetObjectsInSelection()
        {
            if (!_isDrawing || !_targetCamera) return new List<GameObject>();
            Vector2 currentMousePos = _mousePositionAction?.ReadValue<Vector2>() ?? Input.mousePosition;
            var screenHeight = Screen.height;
            var selectionRect = new Rect(Mathf.Min(_selectionStartPos.x, currentMousePos.x),
                Mathf.Min(screenHeight - _selectionStartPos.y, screenHeight - currentMousePos.y),
                Mathf.Abs(currentMousePos.x - _selectionStartPos.x),
                Mathf.Abs(currentMousePos.y - _selectionStartPos.y));
            _currentRect = selectionRect;
            var objects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID).Where(obj =>
                obj.layer == LayerMask.NameToLayer(targetLayerNodesName) ||
                obj.layer == LayerMask.NameToLayer(targetLayerEdgesName));

            return (from obj in objects
                let objectCollider = obj.GetComponent<Collider2D>()
                where objectCollider
                let worldPosition = objectCollider.bounds.center
                let screenPosition = _targetCamera.WorldToScreenPoint(worldPosition)
                let rectAdjustedPosition = new Vector2(screenPosition.x, screenHeight - screenPosition.y)
                where selectionRect.Contains(rectAdjustedPosition)
                select obj).ToList();
        }


        private void UpdateRectangle(Vector2 currentMousePos)
        {
            if (!selectionRectangle) return;

            var center = (_selectionStartPos + currentMousePos) / 2f;
            selectionRectangle.anchoredPosition = center;

            var width = Mathf.Abs(currentMousePos.x - _selectionStartPos.x);
            var height = Mathf.Abs(currentMousePos.y - _selectionStartPos.y);
            selectionRectangle.sizeDelta = new Vector2(width, height);
        }

        private void UpdateColor()
        {
            if (!selectionRectangle) return;

            var image = selectionRectangle.GetComponent<Image>();
            if (image != null)
            {
                image.color = selectionRectColor;
            }
        }

        public void SetColor(Color color)
        {
            selectionRectColor = color;
            UpdateColor();
        }
    }
}