using UnityEngine;
using System.Collections.Generic;
using Runtime;
using Vector3 = UnityEngine.Vector3;

public class CubeSelector : MonoBehaviour
{
    [SerializeField] private float outlineScale = 1.1f;
    [SerializeField] private string targetLayerName = "OverlayLayer";

    private readonly List<GameObject> _selectedCubes = new();
    private readonly List<Vector3> _selectedCubesStartPositions = new();
    private readonly Dictionary<GameObject, GameObject> _outlineCubes = new();
    private Camera _displayCamera;
    private int _targetLayerMask;
    public Vector3 _dragOffset;
    private Vector3? _dragStart = null;
    private Vector3? _dragEnd = null;
    public Vector3 delta = Vector3.zero;
    public int SelectedCubesCount = 0; 
    private bool _isDragging;
    public GameObject _currentlyDraggedCube;
    private int _indexOfCurrentlyDraggedCube;
    private GameObject _toBeDeselectedCube;
    private GameObject _currentContextMenu;
    [SerializeField] private GameObject contextMenuPrefab;  // Prefab for the context menu
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField] private Material highlightMaterial;
    private GameObject pos1;
    private GameObject pos2;
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
        
        pos1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pos2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pos1.transform.localScale = new Vector3(2,2,2);
        pos2.transform.localScale = new Vector3(2,2,2);
        pos1.layer = LayerMask.NameToLayer("OverlayScene");
        pos2.layer = LayerMask.NameToLayer("OverlayScene");
        Destroy(pos1.gameObject.GetComponent<Collider>());
        Destroy(pos2.gameObject.GetComponent<Collider>());
    }

    private void Update()
    {
        // before
        if (_dragStart != null)
        {
            pos1.transform.position = new Vector3(_dragStart.Value.x, _dragStart.Value.y, 12);
            pos1.gameObject.GetComponent<Renderer>().material.color = Color.yellow;
        }
        
        if (_dragEnd != null)
        {
            pos2.transform.position = new Vector3(_dragEnd.Value.x, _dragEnd.Value.y, 12);
            pos2.gameObject.GetComponent<Renderer>().material.color = Color.green;
        }
        
        if (_currentlyDraggedCube)
        {
            _currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color = Color.red;
        }
        
        // actual updates
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
            _selectedCubesStartPositions.Clear();
            _dragStart = null;
            

            if (hit)
            {
                _dragStart = mousePosition;
                pos1.transform.position = new Vector3(_dragStart.Value.x, _dragStart.Value.y, 12);
                var hitObject = hit.collider.gameObject;
                _currentlyDraggedCube = hitObject;
                SelectCube(hitObject);
                // store starting position for each cube that is currently selected
                
                // TODO: Ensure this is not already present in here
                foreach(var cube in _selectedCubes)
                {
                    _selectedCubesStartPositions.Add(cube.transform.position);
                }
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

        
        // the mouseDownFunction is only called once to set up the drag afterward we will work with the start values and add the current position
        if (Input.GetMouseButton(0))
        {
            if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && hit || _isDragging)
            {   
                _dragEnd = mousePosition;
                pos2.transform.position = new Vector3(_dragEnd.Value.x, _dragEnd.Value.y, 12);
                _isDragging = true;
                // Dragging with shift allows moving all selected cubes
                if (_currentlyDraggedCube != null)
                {
                    var drag = _dragEnd - _dragStart;
                    foreach (var cube in _selectedCubes)
                    {
                        // we need to first calculate the offset between starting the motion and ending the motion
                        // starting to drag will first place the dragStart
                        // continuing to drag will place second variable dragEnd
                        // dragEnd - dragStart defines the drag vector
                        if (_selectedCubesStartPositions.Count > 1)
                        {
                            cube.transform.position = Only2D(_selectedCubesStartPositions[0] + (Vector3)drag, cube.transform.position.z);
                        }
                    }
                    _currentlyDraggedCube.transform.position = Only2D(_selectedCubesStartPositions[0] + (Vector3)drag, _currentlyDraggedCube.transform.position.z);
                }
            }
        }

        // Reset dragging when mouse button is released
        if (Input.GetMouseButtonUp(0))
        {
            if (_currentlyDraggedCube != null)
            {
                _currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color = Color.white;
            }
            _currentlyDraggedCube = null;
            _dragOffset = Vector3.zero;
            _dragStart = null;
            _dragEnd = null;
            delta = Vector3.zero;
            _isDragging = false;
        }
    }

    Vector3 Only2D(Vector3 position, float z)
    {
        return new Vector3(position.x, position.y, z);
    }

    void SelectCube(GameObject cube)
    {
        // If shift is pressed, add to selection without deselecting others
        if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        {
            foreach (var highlightedCube in _selectedCubes)
            {
                RemoveOutlineCube(highlightedCube);
            }
            DeselectAllCubes();
        }
        if (_selectedCubes.Contains(cube)) return;
        _selectedCubes.Add(cube);
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