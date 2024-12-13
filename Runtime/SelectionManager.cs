using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using Vector3 = UnityEngine.Vector3;

public class CubeSelector : MonoBehaviour
{
    [SerializeField] private float outlineScale = 1.1f;
    [SerializeField] private string targetLayerName = "OverlayLayer";

    private readonly HashSet<GameObject> _selectedCubes = new();
    private readonly Dictionary<GameObject, Vector3> _selectedCubesStartPositions = new();
    private readonly Dictionary<GameObject, GameObject> _outlineCubes = new();
    private readonly Dictionary<GameObject, bool> _markUnselect = new();
    private Camera _displayCamera;
    private int _targetLayerMask;
    private Vector3? _dragStart;
    private Vector3? _dragEnd;
    private bool _isDragging;
    public GameObject currentlyDraggedCube;
    private int _indexOfCurrentlyDraggedCube;
    private GameObject _toBeDeselectedCube;
    private GameObject _currentContextMenu;
    [SerializeField] private GameObject contextMenuPrefab;  // Prefab for the context menu
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField] private Material highlightMaterial;
    private void Start()
    {
        // Find the camera for Display 2 (index 1)
        _displayCamera = SceneHandler.GetCameraOfScene("NewScene");
        
        if (_displayCamera == null)
        {
            Debug.LogError("No camera found for Display 2!");
            return;
        }

        // Get the layer for ray casting
        _targetLayerMask = LayerMask.GetMask(targetLayerName);
        
        if (_targetLayerMask == 0)
        {
            Debug.LogError($"Layer '{targetLayerName}' not found!");
        }
    }

    private void Update()
    {
        // before
        if (currentlyDraggedCube)
        {
            currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color = Color.red;
        }
        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (!_displayCamera || _targetLayerMask == 0) return;
        
        // Cast a ray from the mouse position
        Vector2 mousePosition = _displayCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);

        // Left mouse button down
        if (Input.GetMouseButtonDown(0))
        {
            _selectedCubesStartPositions.Clear();
            _dragStart = null;
            
            if (hit)
            {
                // prepare drag vector estimation
                _dragStart = mousePosition;
                
                // debug
                var hitObject = hit.collider.gameObject;
                currentlyDraggedCube = hitObject;
                
                // In left click DO NOT REMOVE here only mark as being removed after drag
                // assuming we hit a highlighted object: move the object
                SelectCube(hitObject);

                foreach(var cube in _selectedCubes)
                {
                    _selectedCubesStartPositions[cube] = cube.transform.position;
                }
            }
            else
            {
                // No cube hit, deselect all if shift is not pressed
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    DeselectAllCubes();
                    CloseContextMenu();
                }
            }
        }

        if (Input.GetMouseButtonDown(1) && hit.collider != null)
        {
            ShowContextMenu(hit.collider.gameObject);
        }

        
        // the mouseDownFunction is only called once to set up the drag afterward we will work with the start values and add the current position
        if (Input.GetMouseButton(0))
        {
            // IsSelected might never trigger - potential remove
            if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && hit || _isDragging )
            {   
                _dragEnd = mousePosition;
                _isDragging = true;
                // Dragging with shift allows moving all selected cubes
                if (currentlyDraggedCube != null)
                {
                    if (_dragStart == null || _dragEnd == null) return;
                    var drag = _dragEnd - _dragStart;
                    foreach (var cube in _selectedCubes)
                    {
                        // we need to first calculate the offset between starting the motion and ending the motion
                        // starting to drag will first place the dragStart
                        // continuing to drag will place second variable dragEnd
                        // dragEnd - dragStart defines the drag vector
                        if (_selectedCubesStartPositions.Count > 1)
                        {
                            cube.transform.position = Only2D(_selectedCubesStartPositions[cube] + (Vector3)drag, cube.transform.position.z);
                        }
                    }

                    foreach (var toBeUnselectedCube in _markUnselect.Keys.ToArray())
                    {
                        _markUnselect[toBeUnselectedCube] = false;
                    }
                    currentlyDraggedCube.transform.position = Only2D(_selectedCubesStartPositions[currentlyDraggedCube] + (Vector3)drag, currentlyDraggedCube.transform.position.z);
                }
            }
        }

        foreach (var toBeUnselectedCube in _markUnselect.Keys)
        {
            if (_markUnselect[toBeUnselectedCube])
            {
                RemoveOutlineCube(toBeUnselectedCube);
                _selectedCubes.Remove(toBeUnselectedCube);
            }
        }

        if (_markUnselect.Count > 0)
        {
            CloseContextMenu();
        }

        // Reset dragging when mouse button is released
        if (Input.GetMouseButtonUp(0))
        {
            if (currentlyDraggedCube != null)
            {
                if (_dragStart != null && _dragEnd != null && (Vector3)_dragEnd - (Vector3)_dragStart == Vector3.zero)
                {
                    RemoveOutlineCube(currentlyDraggedCube);
                    _selectedCubes.Remove(currentlyDraggedCube);
                }
                currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color = Color.white;
                currentlyDraggedCube = null;
            }
            _markUnselect.Clear();
            _dragStart = null;
            _dragEnd = null;
            _isDragging = false;
        }
    }

    Vector3 Only2D(Vector3 position, float z)
    {
        return new Vector3(position.x, position.y, z);
    }

    /// <summary>
    /// Marking cubes for deselection that will cause deselection/removal of the outline if not dragged
    /// </summary>
    /// <param name="cube"></param>
    void SelectCube(GameObject cube)
    {
        // If shift is pressed, add to selection without deselecting others
        if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            foreach (var highlightedCube in _selectedCubes)
            {
                _markUnselect[highlightedCube] = true;
            }
        }
        if (!_selectedCubes.Add(cube)) return;
        CreateOutlineCube(cube);
    }

    void DeselectAllCubes()
    {
        // Deselect all cubes
        foreach (GameObject cube in _selectedCubes)
        {
            // Optional: Remove visual feedback
            RemoveOutlineCube(cube);
        }
        _selectedCubes.Clear();
    }

    private void ShowContextMenu(GameObject hitObject)
    {
        // Destroy the current context menu if it exists
        if (_currentContextMenu)
        {
            Destroy(_currentContextMenu);
        }

        // Instantiate the context menu as a child of the canvas
        _currentContextMenu = Instantiate(contextMenuPrefab, parentCanvas.transform);
        _currentContextMenu.SetActive(true);
        var col = hitObject.GetComponent<Collider>();
        var worldCenter = col ? col.bounds.center : // Get the exact center of the object (more accurate)
            hitObject.transform.position; // Fallback to the transform's position

        // Set the position of the context menu in the world
        var rectTransform = _currentContextMenu.GetComponent<RectTransform>();
        rectTransform.position = worldCenter;
        var pos = _currentContextMenu.transform.localPosition;
        _currentContextMenu.GetComponent<RectTransform>().localPosition = new Vector3(pos.x, pos.y, 1);
    }


    private void CloseContextMenu()
    {
        if (_currentContextMenu)
        {
            Destroy(_currentContextMenu);
        }
    }

    /// <summary>
    /// Spawn Highlight prefab and scale to fit the cube currently selected
    /// </summary>
    /// <param name="originalCube"></param>
    private void CreateOutlineCube(GameObject originalCube)
    {
        // Destroy any existing outline for this cube
        RemoveOutlineCube(originalCube);

        var highlight = Instantiate(highlightPrefab, originalCube.transform);
        DestroyImmediate(highlight.gameObject.GetComponent<Collider2D>());
        highlight.transform.SetParent(originalCube.transform, false);
        highlight.transform.localScale = new Vector3(outlineScale, outlineScale, .8f);
        highlight.GetComponent<MeshRenderer>().sharedMaterial = highlightMaterial;
        highlight.transform.localPosition = Vector3.zero;
        
        highlight.layer = originalCube.layer;

        _outlineCubes[originalCube] = highlight;
    }

    private void RemoveOutlineCube(GameObject originalCube)
    {
        if (!_outlineCubes.TryGetValue(originalCube, out GameObject outlineCube)) return;
        Destroy(outlineCube);
        _outlineCubes.Remove(originalCube);
    }
}