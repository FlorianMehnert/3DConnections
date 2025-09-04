using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Runtime.Managers
{
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using System.Collections.Generic;
    using System.Linq;
    using JetBrains.Annotations;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;
    using ScriptableObjectInventory;
    using Nodes;
    public class SelectionManager : MonoBehaviour
    {
        [Header("Layer settings")]
        [SerializeField] private string targetLayerName = "OverlayLayer";
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private Material highlightMaterial;
        
        [Header("Input Settings")]
        [SerializeField] private InputActionAsset inputActions;
        
        // Input Actions - these will be set up in the InputActionAsset
        private InputAction _selectAction;
        private InputAction _contextMenuAction;
        private InputAction _toggleActiveAction;
        private InputAction _togglePingAction;
        private InputAction _selectOutgoingAction;
        private InputAction _pingEditorAction;
        private InputAction _shiftModifier;
        private InputAction _mousePosition;

        private readonly HashSet<GameObject> _selectedCubes = new();
        private readonly Dictionary<GameObject, Vector3> _selectedCubesStartPositions = new();
        private readonly Dictionary<GameObject, bool> _markUnselect = new();
        private Camera _displayCamera;
        private int _targetLayerMask;
        private Vector3? _dragStart;
        private Vector3? _dragEnd;
        private bool _isDragging;
        private GameObject _currentlyDraggedCube;
        private GameObject _currentContextMenu;
        private bool _isActive;

        private readonly RaycastHit2D[] _raycastBuffer = new RaycastHit2D[16];

        public float doubleClickThreshold = 0.3f;
        private float _timer;
        private int _clickCount;

        [SerializeField] private bool pingObjectInEditor;

        [Header("Selection Rectangle")]
        [SerializeField] private RectTransform selectionRectangle;
        [SerializeField] private Color selectionRectColor = new(0.3f, 0.5f, 0.8f, 0.3f);
        
        private Vector2 _selectionStartPos;
        private bool _isDrawingSelectionRect;
        private int _selectedCubesCount;
        [UsedImplicitly] private Rect _selectionRect;

        private void SetupInputActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("InputActionAsset is not assigned!");
                return;
            }

            // Get references to input actions
            _selectAction = inputActions.FindAction("Select");
            _contextMenuAction = inputActions.FindAction("ContextMenu");
            _toggleActiveAction = inputActions.FindAction("ToggleActive");
            _togglePingAction = inputActions.FindAction("TogglePing");
            _selectOutgoingAction = inputActions.FindAction("SelectOutgoing");
            _pingEditorAction = inputActions.FindAction("PingEditor");
            _shiftModifier = inputActions.FindAction("ShiftModifier");
            _mousePosition = inputActions.FindAction("MousePosition");

            // Subscribe to events
            if (_selectAction != null)
            {
                _selectAction.started += OnSelectStarted;
                _selectAction.performed += OnSelectPerformed;
                _selectAction.canceled += OnSelectCanceled;
            }

            if (_contextMenuAction != null)
                _contextMenuAction.performed += OnContextMenu;

            if (_toggleActiveAction != null)
                _toggleActiveAction.performed += OnToggleActive;

            if (_togglePingAction != null)
                _togglePingAction.performed += OnTogglePing;

            if (_selectOutgoingAction != null)
                _selectOutgoingAction.performed += OnSelectOutgoing;

            if (_pingEditorAction != null)
                _pingEditorAction.performed += OnPingEditor;
        }

        private void Start()
        {
            _displayCamera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();

            if (!_displayCamera)
            {
                Debug.LogError("No camera found for Display 2!");
                return;
            }

            _targetLayerMask = LayerMask.GetMask(targetLayerName);

            if (_targetLayerMask == 0) 
                Debug.LogError($"Layer '{targetLayerName}' not found!");

            // Initialize selection rectangle
            if (selectionRectangle != null)
                selectionRectangle.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            inputActions?.Enable();
            SetupInputActions();
            ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.AddListener(HandleEvent);
        }

        private void OnDisable()
        {
            inputActions?.Disable();
            if (ScriptableObjectInventory.Instance == null) return;
            ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.RemoveListener(HandleEvent);
        }

        private void HandleEvent()
        {
            _selectedCubes.Clear();
            _selectedCubesStartPositions.Clear();
            _markUnselect.Clear();
            _isDrawingSelectionRect = false;
            _isDragging = false;
            _currentlyDraggedCube = null;
            _dragStart = null;
            _dragEnd = null;
            _clickCount = 0;
            _timer = 0f;
            _selectedCubesCount = 0;
        }

        private void Update()
        {
            if (!ShouldProcessInput()) return;

            // Handle double-click timing
            if (_clickCount > 0)
            {
                _timer += Time.deltaTime;
                if (_timer > doubleClickThreshold)
                {
                    _clickCount = 0;
                    _timer = 0f;
                }
            }

            // Handle ongoing drag operations - this is the key fix
            if (_selectAction != null && _selectAction.IsPressed())
            {
                Vector2 mousePosition = GetMouseWorldPosition();
                RaycastForClosest(mousePosition);
        
                // Start dragging if we haven't already
                if (!_isDragging && _currentlyDraggedCube && _dragStart != null)
                {
                    _isDragging = true;
                }
        
                // Continue dragging
                if (_isDragging)
                {
                    HandleDragUpdate();
                }
            }

            // Handle selection rectangle drawing
            if (_isDrawingSelectionRect)
            {
                UpdateSelectionRectangle();
                SelectObjectsInRectangle();
            }

            // Handle deselection cleanup
            HandleDeselectCleanup();

            // Update selection rectangle color
            UpdateSelectionRectangleColor();
        }

        #region Input Event Handlers

        private void OnSelectStarted(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput()) return;

            Vector2 mousePosition = GetMouseWorldPosition();
            var hit = RaycastForClosest(mousePosition);

            _selectedCubesStartPositions.Clear();
            _dragStart = null;

            if (hit)
            {
                HandleObjectClick(hit, mousePosition);
            }
            else
            {
                HandleEmptySpaceClick();
            }
        }

        private void OnSelectPerformed(InputAction.CallbackContext context)
        {
            // This event is triggered only when the button is initially pressed, not held
            // continuous dragging is handled in update()
        }

        private void OnSelectCanceled(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput()) return;
            HandleSelectRelease();
        }

        private void OnContextMenu(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput()) return;
            // Handle a right-click context menu
        }

        private void OnToggleActive(InputAction.CallbackContext context)
        {
            _isActive = !_isActive;
        }

        private void OnTogglePing(InputAction.CallbackContext context)
        {
            if (!_isActive) return;
            pingObjectInEditor = !pingObjectInEditor;
        }

        private void OnSelectOutgoing(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput()) return;
    
            var selectedCubes = _selectedCubes.ToList();
            foreach (var cube in selectedCubes)
            {
                if (cube == null) continue;
        
                var connections = cube.GetComponent<LocalNodeConnections>();
                if (connections == null || connections.outConnections == null) continue;
        
                foreach (var outgoingNode in connections.outConnections.Where(outgoingNode => outgoingNode != null))
                {
                    SelectCube(outgoingNode, false, false);
                }
            }
        }


        private void OnPingEditor(InputAction.CallbackContext context)
        {
            if (!ShouldProcessInput()) return;
            
#if UNITY_EDITOR
            if (!pingObjectInEditor || _isDrawingSelectionRect) return;
            EditorGUIUtility.PingObject(gameObject);
            Selection.activeGameObject = gameObject;
#endif
        }

        /// <summary>
        /// Invoked by F event (Focus on Node)
        /// </summary>
        [UsedImplicitly]
        public void FocusOnNode()
        {
            var hit = Physics2D.Raycast(GetMouseWorldPosition(), Vector2.down, Mathf.Infinity, _targetLayerMask);
            if (hit == false)
            {
                // Clear highlights first
                NodeGraphScriptableObject.ClearAllHighlights();
        
                // Then refresh LOD colors to use original colors
                var lodManager = FindFirstObjectByType<GraphLODManager>();
                if (lodManager != null)
                {
                    lodManager.RefreshAggregatedEdgeColors();
                }
                return;
            }
            ScriptableObjectInventory.Instance.graph.HighlightNodeConnections(hit.transform.gameObject, 1);
        }


        #endregion

        #region Input Handling Logic

        private bool ShouldProcessInput()
        {
            if (!ScriptableObjectInventory.Instance.menuState || ScriptableObjectInventory.Instance.menuState.menuOpen)
                return false;
            
            return _isActive;
        }

        private Vector2 GetMouseWorldPosition()
        {
            if (_mousePosition == null) return Vector2.zero;
            Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
            return _displayCamera.ScreenToWorldPoint(screenPos);
        }

        private bool IsShiftPressed()
        {
            return _shiftModifier?.IsPressed() ?? false;
        }

        private void HandleObjectClick(RaycastHit2D hit, Vector2 mousePosition)
        {
            _clickCount++;

            switch (_clickCount)
            {
                case 1:
                    _timer = 0f;
                    break;
                case 2:
                    HandleDoubleClick(hit.collider.gameObject);
                    _clickCount = 0;
                    return;
            }

            // Prepare drag
            _dragStart = mousePosition;
            var hitObject = hit.collider.gameObject;
            _currentlyDraggedCube = hitObject;

            ScriptableObjectInventory.Instance.graph.currentlySelectedGameObject = hitObject;
            SelectCube(hitObject);

            foreach (var cube in _selectedCubes)
            {
                try
                {
                    _selectedCubesStartPositions[cube] = cube.transform.position;
                }
                catch
                {
                    Debug.Log("trying to access destroyed gameobject");
                }
            }
        }

        private void HandleEmptySpaceClick()
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;
            // Start drawing selection rectangle
            _isDrawingSelectionRect = true;
            _selectionStartPos = _mousePosition.ReadValue<Vector2>();
                
            if (selectionRectangle != null)
            {
                selectionRectangle.gameObject.SetActive(true);
                UpdateSelectionRectangle();
            }

            // Deselect all if shift is not pressed
            if (IsShiftPressed()) return;
            DeselectAllCubes();
            CloseContextMenu();
        }

        private void HandleDragUpdate()
        {
            if (!_currentlyDraggedCube || _dragStart == null) 
                return;

            // Update drag end position in real time
            Vector2 currentMousePosition = GetMouseWorldPosition();
            _dragEnd = currentMousePosition;

            if (_dragEnd != null)
            {
                var drag = (Vector3)_dragEnd - (Vector3)_dragStart;
    
                // Update positions for all selected cubes
                foreach (var cube in _selectedCubes.Where(cube => _selectedCubesStartPositions.ContainsKey(cube)))
                {
                    cube.transform.position = Only2D(_selectedCubesStartPositions[cube] + drag,
                        cube.transform.position.z);
                }
            }

            // Clear unselect marks during drag
            foreach (var toBeUnselectedCube in _markUnselect.Keys.ToArray())
                _markUnselect[toBeUnselectedCube] = false;
        }


        private void HandleSelectRelease()
        {
            if (_isDrawingSelectionRect)
            {
                _isDrawingSelectionRect = false;
                if (selectionRectangle != null) 
                    selectionRectangle.gameObject.SetActive(false);

                if (_selectedCubes.Count > 0)
                    ScriptableObjectInventory.Instance.graph.currentlySelectedGameObject = _selectedCubes.ToArray()[0];
                
                ScriptableObjectInventory.Instance.graph.currentlySelectedBounds = GetSelectionBounds();
            }

            if (_currentlyDraggedCube != null)
            {
                if (_dragStart != null && _dragEnd != null && (Vector3)_dragEnd - (Vector3)_dragStart == Vector3.zero)
                {
                    _selectedCubes.Remove(_currentlyDraggedCube);
                    if (_currentlyDraggedCube.GetComponent<Collider2D>())
                    {
                        var selectable = _currentlyDraggedCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
                        selectable.SetToOriginalColor();
                    }
                }

                _currentlyDraggedCube = null;
            }

            _markUnselect.Clear();
            _dragStart = null;
            _dragEnd = null;
            _isDragging = false;
        }

        private void HandleDeselectCleanup()
        {
            foreach (var toBeUnselectedCube in _markUnselect.Keys.Where(toBeUnselectedCube =>
                         _markUnselect[toBeUnselectedCube]))
            {
                _selectedCubes.Remove(toBeUnselectedCube);
                if (!toBeUnselectedCube || !toBeUnselectedCube.GetComponent<Collider2D>()) continue;
                var selectable = toBeUnselectedCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
                selectable.SetToOriginalColor();
                foreach (Transform child in toBeUnselectedCube.transform) 
                    Destroy(child.gameObject);
            }

            if (_markUnselect.Count > 0) CloseContextMenu();
        }

        private void UpdateSelectionRectangleColor()
        {
            if (!selectionRectangle) return;
            
            var image = selectionRectangle.GetComponent<Image>();
            if (!image) return;
            image.color = selectionRectColor;
            if (highlightMaterial)
            {
                highlightMaterial.color = new Color(selectionRectColor.r, selectionRectColor.g, selectionRectColor.b, 255);
            }
        }

        #endregion

        #region Misc

        private static RaycastHit2D GetClosestHit(RaycastHit2D[] hits, int hitCount, Vector2 origin)
        {
            var closest = hits[0];
            var minSqr = (hits[0].point - origin).sqrMagnitude;

            for (var i = 1; i < hitCount; i++)
            {
                var sqr = (hits[i].point - origin).sqrMagnitude;
                if (!(sqr < minSqr)) continue;
                minSqr = sqr;
                closest = hits[i];
            }

            return closest;
        }

        private RaycastHit2D RaycastForClosest(Vector2 mousePosition)
        {
            var hitCount = Physics2D.RaycastNonAlloc(
                mousePosition,
                Vector2.zero,
                _raycastBuffer,
                Mathf.Infinity,
                _targetLayerMask);

            if (hitCount > 0)
                return GetClosestHit(_raycastBuffer, hitCount, mousePosition);

            return Physics2D.Raycast(
                mousePosition,
                Vector2.zero,
                Mathf.Infinity,
                _targetLayerMask);
        }

        private void UpdateSelectionRectangle()
        {
            if (!selectionRectangle) return;

            Vector2 currentMousePos = _mousePosition.ReadValue<Vector2>();
            var center = (_selectionStartPos + currentMousePos) / 2f;
            selectionRectangle.anchoredPosition = center;

            var width = Mathf.Abs(currentMousePos.x - _selectionStartPos.x);
            var height = Mathf.Abs(currentMousePos.y - _selectionStartPos.y);
            selectionRectangle.sizeDelta = new Vector2(width, height);
        }

        private void SelectObjectsInRectangle()
        {
            if (!selectionRectangle) return;

            var screenHeight = Screen.height;
            var currentMousePos = _mousePosition.ReadValue<Vector2>();

            var selectionRect = new Rect(
                Mathf.Min(_selectionStartPos.x, currentMousePos.x),
                Mathf.Min(screenHeight - _selectionStartPos.y, screenHeight - currentMousePos.y),
                Mathf.Abs(currentMousePos.x - _selectionStartPos.x),
                Mathf.Abs(currentMousePos.y - _selectionStartPos.y)
            );
            
            _selectionRect = selectionRect;
            var objects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID)
                .Where(obj => obj.layer == LayerMask.NameToLayer(targetLayerName));

            var selectedNodes = 0;

            foreach (var obj in objects)
            {
                var objectsCollider = obj.GetComponent<Collider2D>();
                if (!objectsCollider) continue;

                var worldPosition = objectsCollider.bounds.center;
                var screenPosition = _displayCamera.WorldToScreenPoint(worldPosition);
                var rectAdjustedPosition = new Vector2(screenPosition.x, screenHeight - screenPosition.y);
                
                if (!selectionRect.Contains(rectAdjustedPosition)) continue;
                
                SelectCube(obj);
                selectedNodes++;
            }

            _selectedCubesCount = selectedNodes;
        }

        private void HandleDoubleClick(GameObject hitObject)
        {
            if (!hitObject) return;
            DeselectAllCubes();
            CloseContextMenu();
            SelectCube(hitObject);
        }

        private static Vector3 Only2D(Vector3 position, float z)
        {
            return new Vector3(position.x, position.y, z);
        }

        private void SelectCube(GameObject cube, bool pingObject = true, bool unselect = true)
        {
            if (!cube) return;
            
            if (!IsShiftPressed() && !_isDrawingSelectionRect && unselect)
            {
                foreach (var highlightedCube in _selectedCubes)
                    _markUnselect[highlightedCube] = true;
            }

            if (!_selectedCubes.Add(cube)) return;

#if UNITY_EDITOR
            if (pingObjectInEditor && !_isDrawingSelectionRect && pingObject)
            {
                EditorGUIUtility.PingObject(cube);
                Selection.activeGameObject = cube;
            }
#endif

            var coRenderer = cube.GetComponent<Renderer>();
            if (coRenderer)
            {
                var coloredObject = cube.GetComponent<ColoredObject>();
                if (!coloredObject)
                {
                    Debug.Log("has no coloredObject");
                    var coColoredObject = cube.AddComponent<ColoredObject>();
                    coColoredObject.SetOriginalColor(coRenderer.sharedMaterial.color);
                }
            }

            SetColorToInvertedSelectionColor(cube);
        }

        private void DeselectAllCubes()
        {
            foreach (var selectable in from cube in _selectedCubes
                     where cube != null
                     let collider = cube.GetComponent<Collider2D>()
                     where collider != null
                     let coloredObject = collider.GetComponent<ColoredObject>()
                     where coloredObject != null
                     select coloredObject)
            {
                selectable.SetToOriginalColor();
            }

            _selectedCubes.Clear();
        }

        private Bounds GetSelectionBounds()
        {
            if (_selectedCubes.Count == 0) return new Bounds();
            
            var selectedCubesArray = _selectedCubes.ToArray();
            var selectionBounds = selectedCubesArray[0].GetComponent<Collider2D>().bounds;

            for (var i = 1; i < _selectedCubes.Count; i++)
            {
                var currentCollider2D = selectedCubesArray[i].GetComponent<Collider2D>();
                if (currentCollider2D) 
                    selectionBounds.Encapsulate(currentCollider2D.bounds);
            }

            return selectionBounds;
        }

        private void CloseContextMenu()
        {
            if (_currentContextMenu) 
                Destroy(_currentContextMenu);
        }

        private void SetColorToInvertedSelectionColor(GameObject cube)
        {
            var meshRenderer = cube.GetComponent<ColoredObject>();
            if (!meshRenderer) return;
            
            Color.RGBToHSV(selectionRectColor, out var h, out _, out _);
            var invertedColor = Color.HSVToRGB((h + .5f) % 1f, 1f, 1f);
            meshRenderer.Highlight(invertedColor, -1f, true);
        }

        public int GetSelectionCount()
        {
            return _selectedCubesCount;
        }

        public Rect GetSelectionRectangle()
        {
            return _selectionRect;
        }

        #endregion
    }
}