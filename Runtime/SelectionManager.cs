using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using Vector3 = UnityEngine.Vector3;

public class CubeSelector : MonoBehaviour
{
    [SerializeField] private float outlineScale = 1.1f;
    [SerializeField] private string targetLayerName = "OverlayLayer";

    public readonly HashSet<GameObject> _selectedCubes = new();
    private readonly Dictionary<GameObject, GameObject> _outlineCubes = new();
    private Camera _displayCamera;
    private int _targetLayerMask;
    public Vector3 _dragOffset;
    public Vector3? _dragStartPosition = null;
    public int SelectedCubesCount = 0; 
    private bool _isDragging;
    public GameObject _currentlyDraggedCube;
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
        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (!_displayCamera || _targetLayerMask == 0) return;
        
        // only for debugging
        SelectedCubesCount = _selectedCubes.Count;
        
        // Cast a ray from the mouse position
        Vector2 mousePosition = _displayCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);

        // Left mouse button down
        if (Input.GetMouseButtonDown(0))
        {
            if (hit)
            {
                // Cube is hit
                var hitObject = hit.collider.gameObject;

                // If shift is not pressed, deselect all cubes first
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) && _selectedCubes.Count < 2)
                {
                    DeselectAllCubes();
                }

                if (_selectedCubes.Count < 2)
                {
                    // Select the cube
                    SelectCube(hitObject);
                }
                // Prepare for dragging
                _dragOffset = hitObject.transform.position - (Vector3)hit.point;
                _currentlyDraggedCube = hitObject;
            }
            else
            {
                // No cube hit, deselect all if shift is not pressed
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    DeselectAllCubes();
                }
            }
        }

        
        // Dragging logic
        if (Input.GetMouseButton(0))
        {
            if (hit)
            {
                // Dragging with shift allows moving all selected cubes
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (_selectedCubes == null) return;
                    foreach (var cube in _selectedCubes.Where(cube => !cube))
                    {
                        MoveCube(cube, hit.point);
                    }
                }
                // Otherwise, only move the initially selected cube
                else if (_currentlyDraggedCube != null)
                {
                    MoveCube(_currentlyDraggedCube, hit.point);
                }else if (_currentlyDraggedCube != null && _selectedCubes.Count > 1)
                {
                    foreach (var cube in _selectedCubes.Where(cube => !cube))
                    {
                        MoveCube(cube, hit.point);
                    }
                }
            }
        }

        // Reset dragging when mouse button is released
        if (Input.GetMouseButtonUp(0))
        {
            _currentlyDraggedCube = null;
            _dragOffset = Vector3.zero;
            _dragStartPosition = null;
        }
    }

    void MoveCube(GameObject cube, Vector3 hitPoint)
    {
        // Move the cube to the new position, maintaining the original offset
        cube.transform.position = hitPoint + _dragOffset;
    }

    void SelectCube(GameObject cube)
    {
        // If shift is pressed, add to selection without deselecting others
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (_selectedCubes.Add(cube))
            {
                CreateOutlineCube(cube);
            }
        }
        // Normal selection
        else {
            // If cube is not already selected, select it
            if (!_selectedCubes.Contains(cube))
            {
                // Deselect all first
                DeselectAllCubes();
                
                _selectedCubes.Add(cube);
                // Optional: Add visual feedback for selection
                CreateOutlineCube(cube);
            }
        }
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
        _currentlyDraggedCube = null;
    }

    private static void MoveSelectedNodes(HashSet<GameObject> selectedNodes, Vector3 drag)
    {
        foreach (var node in selectedNodes)
            node.transform.position += drag;
    }

    private void ShowContextMenu(GameObject hitObject, Vector3 hitPosition)
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

    private void ClearSelections()
    {
        foreach (var cube in _selectedCubes.ToArray())
        {
            RemoveOutlineCube(cube);
        }
        _selectedCubes.Clear();
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