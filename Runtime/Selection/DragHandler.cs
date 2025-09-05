using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _3DConnections.Runtime.Selection
{
    public class DragHandler : MonoBehaviour
    {
        private bool _isDragging;
        private Vector3 _dragStartWorld;
        private GameObject _draggedObject;
        private readonly Dictionary<GameObject, Vector3> _originalPositions = new();
        
        private ISelectionHandler _selectionHandler;

        public bool IsDragging => _isDragging;
        public GameObject DraggedObject => _draggedObject;

        public void Initialize(ISelectionHandler selectionHandler)
        {
            _selectionHandler = selectionHandler;
        }

        public void StartDrag(GameObject obj, Vector3 worldPosition)
        {
            if (!obj) return;

            _isDragging = true;
            _draggedObject = obj;
            _dragStartWorld = worldPosition;
            
            // Store original positions for all selected objects
            _originalPositions.Clear();
            var selectedObjects = _selectionHandler.GetSelectedObjects();
            
            foreach (var selectedObj in selectedObjects)
            {
                if (selectedObj != null)
                {
                    _originalPositions[selectedObj] = selectedObj.transform.position;
                }
            }
        }

        public void UpdateDrag(Vector3 currentWorldPosition)
        {
            if (!_isDragging || !_draggedObject) return;

            var dragDelta = currentWorldPosition - _dragStartWorld;
            
            // Update positions for all selected objects
            foreach (var (obj, originalPos) in _originalPositions.ToArray())
            {
                if (obj)
                {
                    obj.transform.position = Only2D(originalPos + dragDelta, obj.transform.position.z);
                }
                else
                {
                    // Clean up destroyed objects
                    _originalPositions.Remove(obj);
                }
            }
        }

        public bool EndDrag(Vector3 currentWorldPosition)
        {
            if (!_isDragging) return false;

            bool wasDragged = Vector3.Distance(_dragStartWorld, currentWorldPosition) > 0.01f;
            
            _isDragging = false;
            _draggedObject = null;
            _originalPositions.Clear();
            
            return wasDragged;
        }

        public void CancelDrag()
        {
            if (!_isDragging) return;

            // Restore original positions
            foreach (var kvp in _originalPositions)
            {
                var obj = kvp.Key;
                var originalPos = kvp.Value;
                
                if (obj != null)
                {
                    obj.transform.position = originalPos;
                }
            }

            EndDrag(_dragStartWorld);
        }

        private static Vector3 Only2D(Vector3 position, float z)
        {
            return new Vector3(position.x, position.y, z);
        }
    }
}