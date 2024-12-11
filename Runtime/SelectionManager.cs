using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Runtime;

public class CubeSelector : MonoBehaviour
{
    private static readonly int Mode = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineScale = 1.1f;
    [SerializeField] private string targetLayerName = "OverlayLayer";

    private readonly HashSet<GameObject> _selectedCubes = new();
    private readonly Dictionary<GameObject, GameObject> _outlineCubes = new();
    private Camera _displayCamera;
    private int _targetLayerMask;
    private Vector3 _dragOffset;
    private GameObject _currentlyDraggedCube;
    private GameObject _currentContextMenu;
    public GameObject contextMenuPrefab;  // Prefab for the context menu
    public Canvas parentCanvas;

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
        if (!_displayCamera || _targetLayerMask == 0) return;

        // Convert mouse position to world position
        Vector2 mousePosition = _displayCamera.ScreenToWorldPoint(Input.mousePosition);
        
        // Perform 2D raycast
        var hit = Physics2D.Raycast(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);
        // Handle mouse down (selection or drag start)
        if (Input.GetMouseButtonDown(0))
        {

            var isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (hit.collider)
            {
                var hitObject = hit.collider.gameObject;

                // Start drag preparation
                _dragOffset = hitObject.transform.position - (Vector3)mousePosition;

                if (!isShiftHeld)
                {
                    // Clear previous selections if shift is not held
                    ClearSelections();
                }

                // Toggle selection
                if (_selectedCubes.Contains(hitObject))
                {
                    DeselectCube(hitObject);
                }
                else
                {
                    SelectCube(hitObject);
                    _currentlyDraggedCube = hitObject;
                }
            }
            else if (!isShiftHeld)
            {
                ClearSelections();
                
                if (!hit)
                {
                    CloseContextMenu();
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            if (!hit || !hit.collider) return;
            var hitObject = hit.collider.gameObject;
            ShowContextMenu(hitObject, mousePosition);
            return;
        }

        // Handle dragging
        var isShiftHeldWhileDragging = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetMouseButton(0) && _currentlyDraggedCube && !isShiftHeldWhileDragging)
        {
            foreach (var cube in _selectedCubes)
            {
                // Calculate new position with offset
                var drag = new Vector2(mousePosition.x, mousePosition.y) + new Vector2(_dragOffset.x, _dragOffset.y);
                var newPosition = new Vector3(drag.x, drag.y, cube.transform.position.z);
                cube.transform.position = newPosition;
            }
        }

        // Reset dragging
        if (Input.GetMouseButtonUp(0))
        {
            _currentlyDraggedCube = null;
        }
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

    private void SelectCube(GameObject cube)
    {
        if (_selectedCubes.Add(cube))
        {
            CreateOutlineCube(cube);
        }
    }

    private void DeselectCube(GameObject cube)
    {
        if (!_selectedCubes.Contains(cube)) return;
        _selectedCubes.Remove(cube);
        RemoveOutlineCube(cube);
    }

    private void ClearSelections()
    {
        foreach (var cube in _selectedCubes.ToArray())
        {
            RemoveOutlineCube(cube);
        }
        _selectedCubes.Clear();
    }

    private void CreateOutlineCube(GameObject originalCube)
    {
        // Destroy any existing outline for this cube
        RemoveOutlineCube(originalCube);

        // Create outline cube
        var outlineCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        // Set the parent and reset local position to match the original cube
        outlineCube.transform.SetParent(originalCube.transform, false);
        outlineCube.transform.localPosition = Vector3.zero;
        
        // Scale the outline cube uniformly
        var uniformScale = Vector2.one * outlineScale;
        outlineCube.transform.localScale = new Vector3(uniformScale.x, uniformScale.y, .5f);
        
        outlineCube.layer = originalCube.layer;

        // Modify renderer
        var outlineRenderer = outlineCube.GetComponent<Renderer>();
        var outlineMaterial = new Material(Shader.Find("Standard"));
        
        // Configure material for wireframe-like appearance
        outlineMaterial.SetFloat(Mode, 2); // Fade mode
        outlineMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        outlineMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        outlineMaterial.SetInt(ZWrite, 0);
        outlineMaterial.DisableKeyword("_ALPHATEST_ON");
        outlineMaterial.EnableKeyword("_ALPHABLEND_ON");
        outlineMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        outlineMaterial.renderQueue = 3000;

        // Set color with transparency
        var transparentOutlineColor = outlineColor;
        transparentOutlineColor.a = 0.8f;
        outlineMaterial.color = transparentOutlineColor;

        outlineRenderer.material = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Store reference to outline cube
        _outlineCubes[originalCube] = outlineCube;
    }

    private void RemoveOutlineCube(GameObject originalCube)
    {
        if (!_outlineCubes.TryGetValue(originalCube, out GameObject outlineCube)) return;
        Destroy(outlineCube);
        _outlineCubes.Remove(originalCube);
    }
}