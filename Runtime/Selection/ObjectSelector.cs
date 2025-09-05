using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.Nodes;
using UnityEngine;

namespace _3DConnections.Runtime.Selection
{
    public class ObjectSelector : MonoBehaviour, ISelectionHandler
    {
        [SerializeField] private Color selectionColor = Color.yellow;
        [SerializeField] private bool enableEditorPing = true;
        
        private readonly HashSet<GameObject> _selectedObjects = new();
        private readonly Dictionary<GameObject, bool> _pendingDeselection = new();
        
        private InputValidator _inputValidator;

        public int SelectionCount => _selectedObjects.Count;

        public void Initialize(InputValidator inputValidator)
        {
            _inputValidator = inputValidator;
        }

        /// <summary>
        /// Select node object, can be used by single click and multi select
        /// </summary>
        /// <param name="obj">object that will be selected</param>
        /// <param name="addToSelection"></param>
        /// <param name="disallowPingInEditorAnyways">Flag to distinguish between single select object using ping object and rectangle selection</param>
        public void SelectObject(GameObject obj, bool addToSelection = false, bool disallowPingInEditorAnyways = false)
        {
            if (!obj) return;

            // If not adding to selection, mark current selection for deselection
            if (!addToSelection)
            {
                foreach (var selectedObj in _selectedObjects)
                {
                    _pendingDeselection[selectedObj] = true;
                }
            }

            // Add to selection
            if (_selectedObjects.Add(obj))
            {
                ApplySelectionVisual(obj);
                
#if UNITY_EDITOR
                if (!disallowPingInEditorAnyways && enableEditorPing && _inputValidator != null)
                {
                    _inputValidator.PingInEditor(obj);
                }
#endif
            }
        }

        public void DeselectObject(GameObject obj)
        {
            if (!obj || !_selectedObjects.Contains(obj)) return;

            _selectedObjects.Remove(obj);
            RemoveSelectionVisual(obj);
        }

        public void DeselectAll()
        {
            var objectsToDeselect = _selectedObjects.ToArray();
            
            foreach (var obj in objectsToDeselect)
            {
                DeselectObject(obj);
            }
            
            _selectedObjects.Clear();
            _pendingDeselection.Clear();
        }

        public void ToggleSelection(GameObject obj)
        {
            if (IsSelected(obj))
                DeselectObject(obj);
            else
                SelectObject(obj, true, true);
        }

        public bool IsSelected(GameObject obj)
        {
            return _selectedObjects.Contains(obj);
        }

        public GameObject[] GetSelectedObjects()
        {
            return _selectedObjects.Where(obj => obj != null).ToArray();
        }

        public Bounds GetSelectionBounds()
        {
            var validObjects = GetSelectedObjects();
            
            if (validObjects.Length == 0) 
                return new Bounds();

            var firstCollider = validObjects[0].GetComponent<Collider2D>();
            if (!firstCollider) 
                return new Bounds();

            var bounds = firstCollider.bounds;

            for (int i = 1; i < validObjects.Length; i++)
            {
                var collider = validObjects[i].GetComponent<Collider2D>();
                if (collider != null)
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return bounds;
        }

        public void ProcessPendingDeselections()
        {
            var toDeselect = _pendingDeselection.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();
            
            foreach (var obj in toDeselect)
            {
                if (obj != null)
                {
                    DeselectObject(obj);
                }
            }
            
            _pendingDeselection.Clear();
        }

        public void ClearPendingDeselections()
        {
            foreach (var key in _pendingDeselection.Keys.ToArray())
            {
                _pendingDeselection[key] = false;
            }
        }

        public void SelectOutgoingConnections()
        {
            var currentSelection = _selectedObjects.ToList();
            
            foreach (var cube in currentSelection)
            {
                if (!cube) continue;
                
                var connections = cube.GetComponent<LocalNodeConnections>();
                if (connections?.outConnections == null) continue;
                
                foreach (var outgoingNode in connections.outConnections.Where(node => node != null))
                {
                    SelectObject(outgoingNode, true, true);
                }
            }
        }

        private void ApplySelectionVisual(GameObject obj)
        {
            var nodeObjectRenderer = obj.GetComponent<Renderer>();
            if (!nodeObjectRenderer) return;

            var coloredObject = obj.GetComponent<ColoredObject>();
            if (!coloredObject)
            {
                coloredObject = obj.AddComponent<ColoredObject>();
                coloredObject.SetOriginalColor(nodeObjectRenderer.sharedMaterial.color);
            }

            // Create inverted selection color
            Color.RGBToHSV(selectionColor, out var h, out _, out _);
            var invertedColor = Color.HSVToRGB((h + 0.5f) % 1f, 1f, 1f);
            coloredObject.Highlight(invertedColor, -1f, true);
        }

        private void RemoveSelectionVisual(GameObject obj)
        {
            if (!obj) return;
            
            var coloredObject = obj.GetComponent<ColoredObject>();
            if (coloredObject != null)
            {
                coloredObject.SetToOriginalColor();
            }

            // Clean up child objects (like highlights)
            foreach (Transform child in obj.transform)
            {
                if (child.gameObject.name.Contains("Highlight") || 
                    child.gameObject.name.Contains("Selection"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private void Update()
        {
            // Clean up any destroyed objects from selection
            _selectedObjects.RemoveWhere(obj => obj == null);
        }
    }
}