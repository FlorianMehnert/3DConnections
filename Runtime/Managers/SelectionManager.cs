using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;

public class CubeSelector : ModularSettingsUser
{
    [Header("Layer settings")] [SerializeField] [RegisterModularStringSetting("Target Layer", "The layer against which the selection will try to select objects against", "Selection", "OverlayLayer")]
    private string targetLayerName = "OverlayLayer";

    [SerializeField] private Canvas parentCanvas;


    [SerializeField] private Material highlightMaterial;
    private readonly HashSet<GameObject> _selectedCubes = new();
    private readonly Dictionary<GameObject, Vector3> _selectedCubesStartPositions = new();
    private readonly Dictionary<GameObject, bool> _markUnselect = new();
    private Camera _displayCamera;
    private int _targetLayerMask;
    private Vector3? _dragStart;
    private Vector3? _dragEnd;
    private bool _isDragging;
    private GameObject _currentlyDraggedCube;
    private int _indexOfCurrentlyDraggedCube;
    private GameObject _toBeDeselectedCube;
    private GameObject _currentContextMenu;
    private bool _isActive;

    private readonly RaycastHit2D[] _raycastBuffer = new RaycastHit2D[16];

    [RegisterModularFloatSetting("Double Click Treshold", "How long you can wait for the double click to be recognized", "Selection", 0.3f, 0, 1f)]
    public float doubleClickThreshold = 0.3f; // Time window for detecting a double click

    private float _timer; // Timer to track time between clicks
    private int _clickCount; // Number of clicks

    [RegisterModularBoolSetting("Ping Object in the Editor", "When you select the object should it be pinged while in the editor", "Selection", false)] [SerializeField]
    private bool pingObjectInEditor;

    [Header("Selection Rectangle")] [SerializeField]
    private RectTransform selectionRectangle;

    [SerializeField] private Color selectionRectColor = new(0.3f, 0.5f, 0.8f, 0.3f);
    private Vector2 _selectionStartPos;
    private bool _isDrawingSelectionRect;
    private int _selectedCubesCount;
    [UsedImplicitly] private Rect _selectionRect;

    private void Start()
    {
        _displayCamera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();

        if (!_displayCamera)
        {
            Debug.LogError("No camera found for Display 2!");
            return;
        }

        _targetLayerMask = LayerMask.GetMask(targetLayerName);

        if (_targetLayerMask == 0) Debug.LogError($"Layer '{targetLayerName}' not found!");

        // Initialize selection rectangle
        if (!selectionRectangle) return;
        selectionRectangle.gameObject.SetActive(false);
    }

    private void Awake()
    {
        RegisterModularSettings();
    }

    private void Update()
    {
        if (!ScriptableObjectInventory.Instance.menuState || ScriptableObjectInventory.Instance.menuState.menuOpen)
            return;

        // toggle this whole manager using S
        if (Input.GetKeyDown(KeyCode.S)) _isActive = !_isActive;
        if (!_isActive) return;
        if (Input.GetKeyDown(KeyCode.P)) pingObjectInEditor = !pingObjectInEditor;
        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0) && !Input.GetMouseButtonDown(1) && !_isDragging && !Input.GetKeyDown(KeyCode.M) && !Input.GetKeyDown(KeyCode.I)) return;
        if (_currentlyDraggedCube)
            _currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color =
                ScriptableObjectInventory.Instance.nodeColors.nodeSelectedColor;

        var image = selectionRectangle.GetComponent<Image>();
        if (image)
        {
            image.color = selectionRectColor;
            highlightMaterial.color = new Color(selectionRectColor.r, selectionRectColor.g, selectionRectColor.b, 255);
        }

        HandleMouseInput();
        HandleOtherHotkeys();
    }

    /// <summary>
    /// LCtrl+I, M
    /// </summary>
    private void HandleOtherHotkeys()
    {
        // check for keydown first
        if (Input.GetKeyDown(KeyCode.I) && Input.GetKey(KeyCode.LeftControl))
        {
            var selectedCubes = _selectedCubes.ToList();
            foreach (var outgoingNode in selectedCubes.Select(node => node.GetComponent<LocalNodeConnections>())
                         .Where(connections => connections).Select(connections => connections.outConnections)
                         .SelectMany(outConnections => outConnections))
                SelectCube(outgoingNode, false, false);
        }
        else if (Input.GetKeyDown(KeyCode.M))
        {
#if UNITY_EDITOR
            if (!pingObjectInEditor || _isDrawingSelectionRect || !pingObjectInEditor) return;
            EditorGUIUtility.PingObject(gameObject);
            Selection.activeGameObject = gameObject;
#endif
        }
    }

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
        // Cast without allocations
        var hitCount = Physics2D.RaycastNonAlloc(
            mousePosition,
            Vector2.zero,
            _raycastBuffer,
            Mathf.Infinity,
            _targetLayerMask);

        if (hitCount > 0)
            // Find the closest of the hits that were written into the buffer
            return GetClosestHit(_raycastBuffer, hitCount, mousePosition);

        // No hit stored in buffer – do a single Raycast exactly like before
        return Physics2D.Raycast(
            mousePosition,
            Vector2.zero,
            Mathf.Infinity,
            _targetLayerMask);
    }

    private void HandleMouseInput()
    {
        if (!_displayCamera || _targetLayerMask == 0) return;

        if (_clickCount > 0)
        {
            _timer += Time.deltaTime;

            // Reset click count and timer if threshold exceeded
            if (_timer > doubleClickThreshold)
            {
                _clickCount = 0;
                _timer = 0f;
            }
        }

        Vector2 mousePosition = _displayCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = RaycastForClosest(mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            _selectedCubesStartPositions.Clear();
            _dragStart = null;

            if (hit)
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

                // prepare drag vector estimation
                _dragStart = mousePosition;

                var hitObject = hit.collider.gameObject;
                _currentlyDraggedCube = hitObject;

                // this is used in the camera handler later to focus on this object
                ScriptableObjectInventory.Instance.graph.currentlySelectedGameObject = hitObject;

                SelectCube(hitObject);

                foreach (var cube in _selectedCubes)
                    try
                    {
                        _selectedCubesStartPositions[cube] = cube.transform.position;
                    }
                    finally
                    {
                        Debug.Log("trying to access destroyed gameobject");
                    }
            }
            else
            {
                // Do nothing on a button click
                if (!EventSystem.current.IsPointerOverGameObject())
                {
                    // Start drawing the selection rectangle
                    _isDrawingSelectionRect = true;
                    _selectionStartPos = Input.mousePosition;
                    if (selectionRectangle)
                    {
                        selectionRectangle.gameObject.SetActive(true);
                        UpdateSelectionRectangle();
                    }

                    // Deselect all if shift is not pressed
                    if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                    {
                        DeselectAllCubes();

                        // unset currentlySelectedGO for camera handler to allow for editor selection
                        CloseContextMenu();
                    }
                }
            }
        }

        if (_isDrawingSelectionRect)
        {
            UpdateSelectionRectangle();
            SelectObjectsInRectangle();
        }


        // the mouseDownFunction is only called once to set up the drag afterward we will work with the start values and add the current position
        if (Input.GetMouseButton(0))
            if ((!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && hit) || _isDragging)
            {
                _dragEnd = mousePosition;
                _isDragging = true;
                if (_currentlyDraggedCube)
                {
                    if (_dragStart == null || _dragEnd == null) return;
                    var drag = _dragEnd - _dragStart;
                    foreach (var cube in _selectedCubes.Where(_ => _selectedCubesStartPositions.Count > 1))
                        cube.transform.position = Only2D(_selectedCubesStartPositions[cube] + (Vector3)drag,
                            cube.transform.position.z);

                    foreach (var toBeUnselectedCube in _markUnselect.Keys.ToArray()) _markUnselect[toBeUnselectedCube] = false;

                    _currentlyDraggedCube.transform.position = Only2D(
                        _selectedCubesStartPositions[_currentlyDraggedCube] + (Vector3)drag,
                        _currentlyDraggedCube.transform.position.z);
                }
            }

        foreach (var toBeUnselectedCube in _markUnselect.Keys.Where(toBeUnselectedCube =>
                     _markUnselect[toBeUnselectedCube]))
        {
            // RemoveOutlineCube(toBeUnselectedCube);
            _selectedCubes.Remove(toBeUnselectedCube);
            if (!toBeUnselectedCube || !toBeUnselectedCube.GetComponent<Collider2D>()) continue;
            var selectable = toBeUnselectedCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
            selectable.SetToOriginalColor();
            foreach (Transform child in toBeUnselectedCube.transform) Destroy(child.gameObject);
        }

        if (_markUnselect.Count > 0) CloseContextMenu();

        // Reset dragging when mouse button is released
        if (Input.GetMouseButtonUp(0))
            if (_isDrawingSelectionRect)
            {
                _isDrawingSelectionRect = false;
                if (selectionRectangle) selectionRectangle.gameObject.SetActive(false);

                if (_selectedCubes.Count > 0)
                    ScriptableObjectInventory.Instance.graph.currentlySelectedGameObject = _selectedCubes.ToArray()[0];
                ScriptableObjectInventory.Instance.graph.currentlySelectedBounds = GetSelectionBounds();
            }

        if (!Input.GetMouseButtonUp(0)) return;
        if (_currentlyDraggedCube)
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

    private void UpdateSelectionRectangle()
    {
        if (!selectionRectangle) return;

        Vector2 currentMousePos = Input.mousePosition;
        var center = (_selectionStartPos + currentMousePos) / 2f;
        selectionRectangle.anchoredPosition = center;

        var width = Mathf.Abs(currentMousePos.x - _selectionStartPos.x);
        var height = Mathf.Abs(currentMousePos.y - _selectionStartPos.y);
        selectionRectangle.sizeDelta = new Vector2(width, height);
    }

    private void SelectObjectsInRectangle()
    {
        if (!selectionRectangle) return;

        //DeselectAllCubes();

        var screenHeight = Screen.height;

        var selectionRect = new Rect(
            Mathf.Min(_selectionStartPos.x, Input.mousePosition.x),
            Mathf.Min(screenHeight - _selectionStartPos.y, screenHeight - Input.mousePosition.y),
            Mathf.Abs(Input.mousePosition.x - _selectionStartPos.x),
            Mathf.Abs(Input.mousePosition.y - _selectionStartPos.y)
        );
        _selectionRect = selectionRect;
        var objects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID)
            .Where(obj => obj.layer == LayerMask.NameToLayer(targetLayerName));

        var selectedNodes = 0;

        foreach (var obj in objects)
        {
            // Ensure object has a Collider2D and skip others
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
        // Change the state of the selected object
        DeselectAllCubes();
        CloseContextMenu();
        SelectCube(hitObject);
    }

    private static Vector3 Only2D(Vector3 position, float z)
    {
        return new Vector3(position.x, position.y, z);
    }

    /// <summary>
    /// Marking cubes for deselection that will cause deselection/removal of the outline if not dragged
    /// </summary>
    /// <param name="cube"></param>
    /// <param name="unselect">this parameter is required to be true for cubes to be unselected</param>
    private void SelectCube(GameObject cube, bool pingObject = true, bool unselect = true)
    {
        // If shift is pressed, add to selection without deselecting others
        if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && !_isDrawingSelectionRect &&
            unselect)
            foreach (var highlightedCube in _selectedCubes)
                _markUnselect[highlightedCube] = true;

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
        // CreateOutlineCube(cube);
    }

    private void DeselectAllCubes()
    {
        foreach (var selectable in from cube in _selectedCubes
                 where cube.GetComponent<Collider2D>()
                 where cube.GetComponent<ColoredObject>()
                 select cube.GetComponent<Collider2D>().GetComponent<ColoredObject>())
            selectable.SetToOriginalColor();

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
            if (currentCollider2D) selectionBounds.Encapsulate(currentCollider2D.bounds);
        }

        return selectionBounds;
    }


    private void CloseContextMenu()
    {
        if (_currentContextMenu) Destroy(_currentContextMenu);
    }

    private void SetColorToInvertedSelectionColor(GameObject cube)
    {
        var meshRenderer = cube.GetComponent<MeshRenderer>();
        if (!meshRenderer) return;
        Color.RGBToHSV(selectionRectColor, out var h, out var s, out var v);
        var invertedColor = Color.HSVToRGB((h + .5f) % 1f, 1f, 1f);
        meshRenderer.material.color = invertedColor;
    }

    public int GetSelectionCount()
    {
        return _selectedCubesCount;
    }

    public Rect GetSelectionRectangle()
    {
        return _selectionRect;
    }
}