using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

    private void Start()
    {
        // Find the camera for Display 2 (index 1)
        _displayCamera = Camera.allCameras.FirstOrDefault(cam => cam.targetDisplay == 1);
        
        if (_displayCamera == null)
        {
            Debug.LogError("No camera found for Display 2!");
            return;
        }

        // Get the layer for raycasting
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

        // Handle mouse down (selection or drag start)
        if (Input.GetMouseButtonDown(0))
        {
            // Perform 2D raycast
            RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);

            bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (hit.collider != null)
            {
                GameObject hitObject = hit.collider.gameObject;

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
                // Clear selections if clicking on empty space and shift is not held
                ClearSelections();
            }
        }

        // Handle dragging
        if (Input.GetMouseButton(0) && _currentlyDraggedCube != null)
        {
            foreach (GameObject cube in _selectedCubes)
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

    private void SelectCube(GameObject cube)
    {
        if (_selectedCubes.Add(cube))
        {
            CreateOutlineCube(cube);
        }
    }

    void DeselectCube(GameObject cube)
    {
        if (_selectedCubes.Contains(cube))
        {
            _selectedCubes.Remove(cube);
            RemoveOutlineCube(cube);
        }
    }

    void ClearSelections()
    {
        foreach (GameObject cube in _selectedCubes.ToArray())
        {
            RemoveOutlineCube(cube);
        }
        _selectedCubes.Clear();
    }

    void CreateOutlineCube(GameObject originalCube)
    {
        // Destroy any existing outline for this cube
        RemoveOutlineCube(originalCube);

        // Create outline cube
        GameObject outlineCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        // Set the parent and reset local position to match the original cube
        outlineCube.transform.SetParent(originalCube.transform, false);
        outlineCube.transform.localPosition = Vector3.zero;
        
        // Scale the outline cube uniformly
        Vector3 uniformScale = Vector3.one * outlineScale;
        outlineCube.transform.localScale = uniformScale;
        
        outlineCube.layer = originalCube.layer;

        // Modify renderer
        Renderer outlineRenderer = outlineCube.GetComponent<Renderer>();
        Material outlineMaterial = new Material(Shader.Find("Standard"));
        
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
        Color transparentOutlineColor = outlineColor;
        transparentOutlineColor.a = 0.8f;
        outlineMaterial.color = transparentOutlineColor;

        outlineRenderer.material = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Store reference to outline cube
        _outlineCubes[originalCube] = outlineCube;
    }

    void RemoveOutlineCube(GameObject originalCube)
    {
        if (_outlineCubes.TryGetValue(originalCube, out GameObject outlineCube))
        {
            Destroy(outlineCube);
            _outlineCubes.Remove(originalCube);
        }
    }
}