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
        [SerializeField] private string targetLayerName = "OverlayLayer";
        
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
                Debug.Log($"SelectionRectangle initialized with UI element: {selectionRectangle.name}");
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
            Debug.Log("EndSelection called");
            _isDrawing = false;
            
            if (selectionRectangle != null)
                selectionRectangle.gameObject.SetActive(false);
        }

        public List<GameObject> GetObjectsInSelection()
        {
            if (!_isDrawing || !_targetCamera) 
                return new List<GameObject>();

            Vector2 currentMousePos = _mousePositionAction?.ReadValue<Vector2>() ?? Input.mousePosition;
            var screenHeight = Screen.height;

            var selectionRect = new Rect(
                Mathf.Min(_selectionStartPos.x, currentMousePos.x),
                Mathf.Min(screenHeight - _selectionStartPos.y, screenHeight - currentMousePos.y),
                Mathf.Abs(currentMousePos.x - _selectionStartPos.x),
                Mathf.Abs(currentMousePos.y - _selectionStartPos.y)
            );
            
            _currentRect = selectionRect;

            var objects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID)
                .Where(obj => obj.layer == LayerMask.NameToLayer(targetLayerName));

            var selectedObjects = new List<GameObject>();

            foreach (var obj in objects)
            {
                var objectCollider = obj.GetComponent<Collider2D>();
                if (!objectCollider) continue;

                var worldPosition = objectCollider.bounds.center;
                var screenPosition = _targetCamera.WorldToScreenPoint(worldPosition);
                var rectAdjustedPosition = new Vector2(screenPosition.x, screenHeight - screenPosition.y);
                
                if (selectionRect.Contains(rectAdjustedPosition))
                {
                    selectedObjects.Add(obj);
                }
            }

            return selectedObjects;
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