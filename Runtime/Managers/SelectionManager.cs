using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace _3DConnections.Runtime.Managers
{
    public class CubeSelector : MonoBehaviour
    {
        [SerializeField] private float outlineScale = 1.1f;
        [SerializeField] private string targetLayerName = "OverlayLayer";
        [SerializeField] private NodeColorsScriptableObject nodeColorsScriptableObject;
        private readonly HashSet<GameObject> _selectedCubes = new();
        private readonly Dictionary<GameObject, Vector3> _selectedCubesStartPositions = new();
        private readonly Dictionary<GameObject, GameObject> _outlineCubes = new();
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
        [SerializeField] private GameObject contextMenuPrefab;
        [SerializeField] private Canvas parentCanvas;
        [SerializeField] private GameObject highlightPrefab;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private OverlaySceneScriptableObject overlay;
        
        [SerializeField] private NodeGraphScriptableObject nodegraph;
        
        public float doubleClickThreshold = 0.3f; // Time window for detecting a double click

        private float _timer; // Timer to track time between clicks
        private int _clickCount; // Number of clicks

        private void Start()
        {
            _displayCamera = overlay.GetCameraOfScene();

            if (_displayCamera == null)
            {
                Debug.LogError("No camera found for Display 2!");
                return;
            }

            _targetLayerMask = LayerMask.GetMask(targetLayerName);

            if (_targetLayerMask == 0)
            {
                Debug.LogError($"Layer '{targetLayerName}' not found!");
            }
        }

        private void Update()
        {
            // highlight currently dragging cube
            if (_currentlyDraggedCube)
            {
                _currentlyDraggedCube.gameObject.GetComponent<MeshRenderer>().sharedMaterial.color = nodeColorsScriptableObject.nodeSelectedColor;
            }

            HandleMouseInput();
        }

        private static RaycastHit2D GetClosestHit(RaycastHit2D[] hits, Vector2 point)
        {
            var closestHit = hits[0];
            var closestDistance = Vector2.Distance(point, hits[0].point);

            for (var i = 1; i < hits.Length; i++)
            {
                var distance = Vector2.Distance(point, hits[i].point);
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestHit = hits[i];
            }

            return closestHit;
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
            
            // Cast a ray from the mouse position
            Vector2 mousePosition = _displayCamera.ScreenToWorldPoint(Input.mousePosition);
            var hits = Physics2D.RaycastAll(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);
            var hit = hits.Length > 0 ? GetClosestHit(hits, mousePosition) : Physics2D.Raycast(mousePosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);

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

                    SelectCube(hitObject);

                    foreach (var cube in _selectedCubes)
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

            if (Input.GetMouseButtonDown(1) && hit.collider)
            {
                ShowContextMenu(hit.collider.gameObject);
            }


            // the mouseDownFunction is only called once to set up the drag afterward we will work with the start values and add the current position
            if (Input.GetMouseButton(0))
            {
                if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && hit || _isDragging)
                {
                    _dragEnd = mousePosition;
                    _isDragging = true;
                    if (_currentlyDraggedCube)
                    {
                        if (_dragStart == null || _dragEnd == null) return;
                        var drag = _dragEnd - _dragStart;
                        foreach (var cube in _selectedCubes.Where(_ => _selectedCubesStartPositions.Count > 1))
                        {
                            cube.transform.position = Only2D(_selectedCubesStartPositions[cube] + (Vector3)drag, cube.transform.position.z);
                        }

                        foreach (var toBeUnselectedCube in _markUnselect.Keys.ToArray())
                        {
                            _markUnselect[toBeUnselectedCube] = false;
                        }

                        _currentlyDraggedCube.transform.position = Only2D(_selectedCubesStartPositions[_currentlyDraggedCube] + (Vector3)drag, _currentlyDraggedCube.transform.position.z);
                    }
                }
            }

            foreach (var toBeUnselectedCube in _markUnselect.Keys.Where(toBeUnselectedCube => _markUnselect[toBeUnselectedCube]))
            {
                RemoveOutlineCube(toBeUnselectedCube);
                _selectedCubes.Remove(toBeUnselectedCube);
                var selectable = toBeUnselectedCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
                selectable.SetToOriginalColor();
                foreach (Transform child in toBeUnselectedCube.transform)
                {
                    Destroy(child.gameObject);
                }
            }

            if (_markUnselect.Count > 0)
            {
                CloseContextMenu();
            }

            // Reset dragging when mouse button is released
            if (!Input.GetMouseButtonUp(0)) return;
            if (_currentlyDraggedCube)
            {
                if (_dragStart != null && _dragEnd != null && (Vector3)_dragEnd - (Vector3)_dragStart == Vector3.zero)
                {
                    RemoveOutlineCube(_currentlyDraggedCube);
                    _selectedCubes.Remove(_currentlyDraggedCube);
                    var selectable = _currentlyDraggedCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
                    selectable.SetToOriginalColor();
                }

                _currentlyDraggedCube = null;
            }

            _markUnselect.Clear();
            _dragStart = null;
            _dragEnd = null;
            _isDragging = false;
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
        private void SelectCube(GameObject cube)
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
#if UNITY_EDITOR
            EditorGUIUtility.PingObject(cube);
            Selection.activeGameObject = cube;
#endif
            CreateOutlineCube(cube);
        }

        private void DeselectAllCubes()
        {
            // Deselect all cubes
            foreach (var cube in _selectedCubes)
            {
                RemoveOutlineCube(cube);
                var selectable = cube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
                selectable.SetToOriginalColor();
            }

            _selectedCubes.Clear();
        }

        private void ShowContextMenu(GameObject hitObject)
        {
            if (_currentContextMenu)
            {
                Destroy(_currentContextMenu);
            }

            _currentContextMenu = Instantiate(contextMenuPrefab, parentCanvas.transform);
            _currentContextMenu.SetActive(true);
            var col = hitObject.GetComponent<Collider>();
            var worldCenter = col
                ? col.bounds.center
                : // Get the exact center of the object (more accurate)
                hitObject.transform.position; // Fallback to the transform's position
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
            if (!_outlineCubes.TryGetValue(originalCube, out var outlineCube)) return;
            Destroy(outlineCube);
            var selectable = originalCube.GetComponent<Collider2D>().GetComponent<ColoredObject>();
            selectable.SetToOriginalColor();
            _outlineCubes.Remove(originalCube);
        }
    }
}