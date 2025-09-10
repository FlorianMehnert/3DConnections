using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.Nodes.Connection;
using _3DConnections.Runtime.ScriptableObjects;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using JetBrains.Annotations;

namespace _3DConnections.Runtime.Selection
{
    public class SelectionManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private InputValidator inputValidator;
        [SerializeField] private RaycastManager raycastManager;
        [SerializeField] private SelectionRectangle selectionRectangle;
        [SerializeField] private ObjectSelector objectSelector;
        [SerializeField] private DragHandler dragHandler;
        [SerializeField] private ClickDetector clickDetector;

        [Header("Input Settings")]
        [SerializeField] private InputActionAsset inputActions;
        
        [Header("Canvas Settings")]
        [SerializeField] private Material highlightMaterial;

        // Input Actions
        private InputAction _selectAction;
        private InputAction _contextMenuAction;
        private InputAction _toggleActiveAction;
        private InputAction _togglePingAction;
        private InputAction _selectOutgoingAction;
        private InputAction _pingEditorAction;
        private InputAction _shiftModifier;
        private InputAction _mousePosition;

        private Camera _displayCamera;
        private GameObject _currentContextMenu;

        private void Start()
        {
            InitializeCamera();
            InitializeComponents();
        }

        private void InitializeCamera()
        {
            _displayCamera = soi.Instance.overlay.GetCameraOfScene();

            if (!_displayCamera)
            {
                Debug.LogError("No camera found for Display 2!");
            }
        }

        private void InitializeComponents()
        {
            if (!inputValidator)
                inputValidator = GetComponentInChildren<InputValidator>();
            if (!inputValidator)
            {
                Debug.LogError("InputValidator component not found in children!");
                return;
            }

            // Initialize raycast manager
            if (!raycastManager)
                raycastManager = GetComponentInChildren<RaycastManager>();
            if (!raycastManager)
            {
                Debug.LogError("RaycastManager component not found in children!");
                return;
            }
            raycastManager.Initialize(_displayCamera);

            // Initialize selection rectangle
            if (!selectionRectangle)
                selectionRectangle = GetComponentInChildren<SelectionRectangle>();
            if (!selectionRectangle)
            {
                Debug.LogError("SelectionRectangle component not found in children!");
                return;
            }
            selectionRectangle.Initialize(_displayCamera);

            // Initialize object selector
            if (!objectSelector)
                objectSelector = GetComponentInChildren<ObjectSelector>();
            if (!objectSelector)
            {
                Debug.LogError("ObjectSelector component not found in children!");
                return;
            }
            objectSelector.Initialize(inputValidator);

            // Initialize drag handler
            if (!dragHandler)
                dragHandler = GetComponentInChildren<DragHandler>();
            if (!dragHandler)
            {
                Debug.LogError("DragHandler component not found in children!");
                return;
            }
            dragHandler.Initialize(objectSelector);

            // Initialize click detector
            if (!clickDetector)
                clickDetector = GetComponentInChildren<ClickDetector>();
            if (!clickDetector)
            {
                Debug.LogError("ClickDetector component not found in children!");
                return;
            }
            clickDetector.OnDoubleClick += HandleDoubleClick;
        }

        private void OnEnable()
        {
            inputActions?.Enable();
            SetupInputActions();
            
            if (soi.Instance != null)
            {
                soi.Instance.clearEvent.onEventTriggered.AddListener(HandleClearEvent);
            }
        }

        private void OnDisable()
        {
            inputActions?.Disable();
            
            if (soi.Instance != null)
            {
                soi.Instance.clearEvent.onEventTriggered.RemoveListener(HandleClearEvent);
            }
        }

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

        private void Update()
        {
            if (!inputValidator.ShouldProcessInput()) return;

            // Handle ongoing drag operations
            if (_selectAction != null && _selectAction.IsPressed())
            {
                Vector2 mouseWorldPos = raycastManager.GetMouseWorldPosition(_mousePosition);
                
                if (dragHandler.IsDragging)
                {
                    dragHandler.UpdateDrag(mouseWorldPos);
                    objectSelector.ClearPendingDeselections(); // Don't deselect while dragging
                }
                else if (selectionRectangle.IsDrawing)
                {
                    selectionRectangle.UpdateSelection(_mousePosition.ReadValue<Vector2>());
                    
                    // Select objects in rectangle
                    var objectsInRect = selectionRectangle.GetObjectsInSelection();
                    foreach (var obj in objectsInRect)
                    {
                        objectSelector.SelectObject(obj, true);
                    }
                }
            }

            // Process pending deselections
            objectSelector.ProcessPendingDeselections();
        }

        #region Input Event Handlers

        private void OnSelectStarted(InputAction.CallbackContext context)
        {
            if (!inputValidator.ShouldProcessInput()) return;

            Vector2 mouseWorldPos = raycastManager.GetMouseWorldPosition(_mousePosition);
            var closestObj = raycastManager.GetClosestObjectToMouse(mouseWorldPos);
            
            if (closestObj != null)
            {
                if (closestObj.GetComponent<EdgeType>()) Debug.Log($"hit an edge {closestObj.name}");
                HandleObjectClick(closestObj, mouseWorldPos);
            }
            else
            {
                HandleEmptySpaceClick();
            }
        }

        private void OnSelectCanceled(InputAction.CallbackContext context)
        {
            if (!inputValidator.ShouldProcessInput()) return;
            HandleSelectRelease();
        }

        private void OnContextMenu(InputAction.CallbackContext context)
        {
            if (!inputValidator.ShouldProcessInput()) return;
            // Handle right-click context menu
        }

        private void OnToggleActive(InputAction.CallbackContext context)
        {
            inputValidator.IsActive = !inputValidator.IsActive;
        }

        private void OnTogglePing(InputAction.CallbackContext context)
        {
            if (!inputValidator.IsActive) return;
            if (objectSelector == null) return;
            objectSelector.enableEditorPing = !objectSelector.enableEditorPing;
        }

        private void OnSelectOutgoing(InputAction.CallbackContext context)
        {
            if (!inputValidator.ShouldProcessInput()) return;
            objectSelector.SelectOutgoingConnections();
        }

        private void OnPingEditor(InputAction.CallbackContext context)
        {
            if (!inputValidator.ShouldProcessInput()) return;
            
#if UNITY_EDITOR
            if (selectionRectangle.IsDrawing) return;
            inputValidator.PingInEditor(gameObject);
#endif
        }

        #endregion

        #region Event Handlers

        private void HandleObjectClick(GameObject hitObject, Vector2 mouseWorldPos)
        {
            // Handle click detection
            bool isDoubleClick = clickDetector.ProcessClick(hitObject);
            if (isDoubleClick) return; // Double click will be handled separately

            // Set current selection in graph
            if (soi.Instance?.graph != null)
            {
                soi.Instance.graph.currentlySelectedGameObject = hitObject;
            }

            // Select the object
            bool addToSelection = IsShiftPressed();
            objectSelector.SelectObject(hitObject, addToSelection);

            // Start drag operation
            dragHandler.StartDrag(hitObject, mouseWorldPos);
        }

        private void HandleEmptySpaceClick()
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            // Start drawing selection rectangle
            Vector2 screenPos = _mousePosition.ReadValue<Vector2>();
            selectionRectangle.StartSelection(screenPos);

            // Deselect all if shift is not pressed
            if (!IsShiftPressed())
            {
                objectSelector.DeselectAll();
                CloseContextMenu();
            }
        }

        private void HandleSelectRelease()
        {
            Vector2 mouseWorldPos = raycastManager.GetMouseWorldPosition(_mousePosition);
            
            if (selectionRectangle.IsDrawing)
            {
                selectionRectangle.EndSelection();
                
                // Update graph selection bounds
                if (soi.Instance?.graph != null)
                {
                    var selectedObjects = objectSelector.GetSelectedObjects();
                    if (selectedObjects.Length > 0)
                    {
                        soi.Instance.graph.currentlySelectedGameObject = selectedObjects[0];
                        soi.Instance.graph.currentlySelectedBounds = objectSelector.GetSelectionBounds();
                    }
                }
            }

            if (dragHandler.IsDragging)
            {
                bool wasDragged = dragHandler.EndDrag(mouseWorldPos);
                
                // If object wasn't actually dragged, it was just a click
                if (!wasDragged && dragHandler.DraggedObject != null)
                {
                    // Handle as a simple click - object was already selected in HandleObjectClick
                }
            }
        }

        private void HandleDoubleClick(GameObject hitObject)
        {
            if (!hitObject) return;
            
            objectSelector.DeselectAll();
            CloseContextMenu();
            objectSelector.enableEditorPing = true;
            objectSelector.SelectObject(hitObject, false);
            objectSelector.enableEditorPing = false;
        }

        private void HandleClearEvent()
        {
            objectSelector.DeselectAll();
            selectionRectangle.EndSelection();
            dragHandler.CancelDrag();
            CloseContextMenu();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Invoked by F event (Focus on Node)
        /// </summary>
        [UsedImplicitly]
        public void FocusOnNode()
        {
            Vector2 mouseWorldPos = raycastManager.GetMouseWorldPosition(_mousePosition);
            var hit = raycastManager.RaycastDown(mouseWorldPos);
            
            if (!hit.collider)
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
            
            if (soi.Instance?.graph != null)
            {
                soi.Instance.graph.HighlightNodeConnections(hit.transform.gameObject, 1);
            }
        }

        public int GetSelectionCount()
        {
            return objectSelector.SelectionCount;
        }

        public Rect GetSelectionRectangle()
        {
            return selectionRectangle.CurrentRect;
        }

        public GameObject[] GetSelectedObjects()
        {
            return objectSelector.GetSelectedObjects();
        }

        public Bounds GetSelectionBounds()
        {
            return objectSelector.GetSelectionBounds();
        }

        #endregion

        #region Helper Methods

        private bool IsShiftPressed()
        {
            return _shiftModifier?.IsPressed() ?? false;
        }

        private void CloseContextMenu()
        {
            if (_currentContextMenu != null) 
            {
                Destroy(_currentContextMenu);
                _currentContextMenu = null;
            }
        }

        #endregion

        #region Editor Integration

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Selection Manager/Toggle Active")]
        private static void ToggleSelectionManager()
        {
            var selectionManager = FindFirstObjectByType<SelectionManager>();
            if (selectionManager != null)
            {
                selectionManager.inputValidator.IsActive = !selectionManager.inputValidator.IsActive;
                Debug.Log($"Selection Manager Active: {selectionManager.inputValidator.IsActive}");
            }
        }

        [UnityEditor.MenuItem("Tools/Selection Manager/Deselect All")]
        private static void DeselectAll()
        {
            var selectionManager = FindFirstObjectByType<SelectionManager>();
            if (selectionManager != null)
            {
                selectionManager.objectSelector.DeselectAll();
            }
        }
#endif

        #endregion
    }
}