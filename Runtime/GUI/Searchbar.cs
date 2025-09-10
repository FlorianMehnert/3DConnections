using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using _3DConnections.Runtime.Nodes;
using _3DConnections.Runtime.Nodes.Connection;

namespace _3DConnections.Runtime.GUI
{
    public class Searchbar : MonoBehaviour
    {
        [Header("UI References")]
        public TMP_InputField searchInputField;
        public Button clearButton;
        public TextMeshProUGUI resultsCountText;

        [Header("Search Settings")]
        public float searchDelay = 0.3f;
        [Tooltip("Dim non-matching nodes and edges to make matches stand out")]
        public bool dimNonMatches = true;
        [Range(0.1f, 1f)]
        [Tooltip("How much to dim non-matching objects (lower = darker)")]
        public float dimIntensity = 0.3f;
        [Tooltip("Color for dimmed objects")]
        public Color dimColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [Tooltip("Color for highlighted matching objects")]
        public Color highlightColor = Color.yellow;

        private float _lastSearchTime;
        private string _lastSearchTerm;
        private Coroutine _searchCoroutine;

        // Input Actions
        private InputAction searchFocusAction;
        private InputAction clearSearchAction;
        
        public InputActionAsset inputActions;
        private InputActionMap gameplayMap;

        [Header("Performance Settings")]
        public int maxSearchResults = 100;
        private List<SearchableNode> _cachedNodes;
        private List<SearchableEdge> _cachedEdges;
        private bool _cacheValid = false;

        // Store original line widths for edges
        private Dictionary<LineRenderer, float> _originalLineWidths = new Dictionary<LineRenderer, float>();

        // Cached searchable data structures
        private struct SearchableNode
        {
            public GameObject gameObject;
            public string name;
            public LocalNodeConnections connections;
            public ColoredObject coloredObject;
            public Renderer renderer;
        }

        private struct SearchableEdge
        {
            public GameObject gameObject;
            public string name;
            public EdgeType edgeType;
            public ColoredObject coloredObject;
            public LineRenderer lineRenderer;
        }

        private void Awake()
        {
            // Setup Input Actions with proper keyboard events
            searchFocusAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/f");
            searchFocusAction.AddCompositeBinding("ButtonWithOneModifier")
                .With("Modifier", "<Keyboard>/ctrl")
                .With("Button", "<Keyboard>/f");

            clearSearchAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/escape");
            
            
        }

        private void OnEnable()
        {
            SetupUI();
            
            // Enable and bind Input Actions
            searchFocusAction.Enable();
            clearSearchAction.Enable();
            
            searchFocusAction.performed += OnSearchFocus;
            clearSearchAction.performed += OnClearSearch;
            
            if (searchInputField)
            {
                searchInputField.onSelect.AddListener(OnInputFieldFocused);
                searchInputField.onDeselect.AddListener(OnInputFieldDefocused);
                if (inputActions != null)
                {
                    gameplayMap = inputActions.FindActionMap("NodeInteractivity");
                    gameplayMap = inputActions.FindActionMap("UI");
                }
            }

            // Invalidate cache when enabled
            _cacheValid = false;
        }

        private void OnDisable()
        {
            // Disable and unbind Input Actions
            searchFocusAction.performed -= OnSearchFocus;
            clearSearchAction.performed -= OnClearSearch;
            
            if (searchInputField)
            {
                searchInputField.onSelect.RemoveListener(OnInputFieldFocused);
                searchInputField.onDeselect.RemoveListener(OnInputFieldDefocused);
            }
            
            searchFocusAction.Disable();
            clearSearchAction.Disable();
        }
        
        private void OnInputFieldFocused(string text)
        {
            clearSearchAction.Disable();
            gameplayMap.Disable();
            // Keep searchFocusAction enabled since it uses Ctrl modifier
        }

        private void OnInputFieldDefocused(string text)
        {
            clearSearchAction.Enable();
            gameplayMap.Enable();
        }

        private void OnDestroy()
        {
            // Dispose of Input Actions
            searchFocusAction?.Dispose();
            clearSearchAction?.Dispose();
        }

        private void SetupUI()
        {
            BuildCache();
            UpdateResultsCount(0, GetTotalSearchableCount());
            
            // Connect UI events
            if (searchInputField)
            {
                searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
                searchInputField.onEndEdit.AddListener(OnSearchEndEdit);
            }
            
            if (clearButton)
            {
                clearButton.onClick.AddListener(ClearSearch);
            }
        }

        /// <summary>
        /// Build cache using the same method as BetterSearchProvider's GetSearchScope
        /// </summary>
        private void BuildCache()
        {
            if (_cacheValid && _cachedNodes != null && _cachedEdges != null) return;

            // Initialize or clear lists
            if (_cachedNodes == null) _cachedNodes = new List<SearchableNode>();
            else _cachedNodes.Clear();

            if (_cachedEdges == null) _cachedEdges = new List<SearchableEdge>();
            else _cachedEdges.Clear();

            _originalLineWidths.Clear();

            // Search for nodes using the same method as BetterSearchProvider
            var parentNodes = GameObject.Find("ParentNodesObject");
            if (parentNodes != null)
            {
                var nodeTransforms = parentNodes.GetComponentsInChildren<Transform>()
                    .Where(t => t != parentNodes.transform); // Exclude parent itself

                foreach (var nodeTransform in nodeTransforms)
                {
                    if (nodeTransform == null) continue;
                    var go = nodeTransform.gameObject;
                    
                    _cachedNodes.Add(new SearchableNode
                    {
                        gameObject = go,
                        name = go.name.ToLowerInvariant(),
                        connections = go.GetComponent<LocalNodeConnections>(),
                        coloredObject = go.GetComponent<ColoredObject>(),
                        renderer = go.GetComponent<Renderer>()
                    });
                }
            }

            // Search for edges using EdgeType component
            var parentEdges = GameObject.Find("ParentEdgesObject");
            if (parentEdges != null)
            {
                var edgeTransforms = parentEdges.GetComponentsInChildren<Transform>()
                    .Where(t => t != parentEdges.transform); // Exclude parent itself

                foreach (var edgeTransform in edgeTransforms)
                {
                    if (edgeTransform == null) continue;
                    var go = edgeTransform.gameObject;
                    var lineRenderer = go.GetComponent<LineRenderer>();
                    
                    // Store original line width
                    if (lineRenderer != null && !_originalLineWidths.ContainsKey(lineRenderer))
                    {
                        _originalLineWidths[lineRenderer] = lineRenderer.startWidth;
                    }
                    
                    _cachedEdges.Add(new SearchableEdge
                    {
                        gameObject = go,
                        name = go.name.ToLowerInvariant(),
                        edgeType = go.GetComponent<EdgeType>(),
                        coloredObject = go.GetComponent<ColoredObject>(),
                        lineRenderer = lineRenderer
                    });
                }
            }

            _cacheValid = true;
        }

        public void InvalidateCache()
        {
            _cacheValid = false;
        }

        public void OnSearchValueChanged()
        {
            OnSearchValueChanged(searchInputField.text);
        }

        private void OnSearchValueChanged(string searchText)
        {
            _lastSearchTime = Time.time;

            if (_searchCoroutine != null)
                StopCoroutine(_searchCoroutine);

            _searchCoroutine = StartCoroutine(DelayedSearch(searchText));
        }

        private System.Collections.IEnumerator DelayedSearch(string searchText)
        {
            yield return new WaitForSeconds(searchDelay);
    
            if (Time.time - _lastSearchTime >= searchDelay - 0.01f && searchText == searchInputField.text)
            {
                PerformSearch(searchText);
            }
        }

        private async void PerformSearch(string searchText)
        {
            _lastSearchTerm = searchText;
            BuildCache();

            if (string.IsNullOrEmpty(searchText))
            {
                ClearAllHighlights();
                UpdateResultsCount(0, GetTotalSearchableCount());
                return;
            }

            // Perform search on background thread for better performance
            var (nodeMatches, edgeMatches) = await Task.Run(() => SearchNodesAndEdges(searchText));
    
            // Return to main thread for UI updates
            HighlightAndDimObjects(nodeMatches, edgeMatches);
            UpdateResultsCount(nodeMatches.Count + edgeMatches.Count, GetTotalSearchableCount());
        }

        private (List<SearchableNode> nodeMatches, List<SearchableEdge> edgeMatches) SearchNodesAndEdges(string searchText)
        {
            string lowerSearchText = searchText.ToLowerInvariant();
    
            // Search nodes by name
            var nodeMatches = _cachedNodes
                .Where(node => node.name.Contains(lowerSearchText))
                .Take(maxSearchResults / 2)
                .ToList();
    
            // Search edges by name and connection type
            var edgeMatches = _cachedEdges
                .Where(edge => 
                    edge.name.Contains(lowerSearchText) ||
                    (edge.edgeType != null && edge.edgeType.connectionType != null && 
                     edge.edgeType.connectionType.ToLowerInvariant().Contains(lowerSearchText)))
                .Take(maxSearchResults / 2)
                .ToList();

            return (nodeMatches, edgeMatches);
        }

        /// <summary>
        /// Highlight matching objects and optionally dim non-matching ones
        /// </summary>
        private void HighlightAndDimObjects(List<SearchableNode> nodeMatches, List<SearchableEdge> edgeMatches)
        {
            // Clear previous highlights
            ClearAllHighlights();

            // Create HashSets for fast lookup
            var matchingNodeSet = new HashSet<SearchableNode>(nodeMatches);
            var matchingEdgeSet = new HashSet<SearchableEdge>(edgeMatches);

            // Process all nodes
            foreach (var node in _cachedNodes)
            {
                if (node.coloredObject != null)
                {
                    if (matchingNodeSet.Contains(node))
                    {
                        // Highlight matching nodes
                        node.coloredObject.Highlight(highlightColor, float.MaxValue, highlightForever: true);
                    }
                    else if (dimNonMatches)
                    {
                        // Dim non-matching nodes
                        ApplyDimming(node.coloredObject, node.renderer);
                    }
                }
            }

            // Process all edges
            foreach (var edge in _cachedEdges)
            {
                if (matchingEdgeSet.Contains(edge))
                {
                    // Highlight matching edges
                    if (edge.coloredObject != null)
                    {
                        edge.coloredObject.Highlight(highlightColor, float.MaxValue, highlightForever: true);
                    }
                    
                    // Make matching edge lines thicker
                    if (edge.lineRenderer != null && _originalLineWidths.TryGetValue(edge.lineRenderer, out float originalWidth))
                    {
                        edge.lineRenderer.startWidth = originalWidth * 2f;
                        edge.lineRenderer.endWidth = originalWidth * 2f;
                    }
                }
                else if (dimNonMatches)
                {
                    // Dim non-matching edges
                    if (edge.coloredObject != null)
                    {
                        ApplyDimming(edge.coloredObject, null);
                    }
                    
                    // Optionally make non-matching edge lines thinner
                    if (edge.lineRenderer != null && _originalLineWidths.TryGetValue(edge.lineRenderer, out float originalWidth))
                    {
                        edge.lineRenderer.startWidth = originalWidth * 0.5f;
                        edge.lineRenderer.endWidth = originalWidth * 0.5f;
                        
                        // Also dim the line renderer color
                        var material = edge.lineRenderer.material;
                        if (material != null)
                        {
                            var currentColor = material.color;
                            material.color = Color.Lerp(currentColor, dimColor, 1f - dimIntensity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Apply dimming effect to a ColoredObject
        /// </summary>
        private void ApplyDimming(ColoredObject coloredObject, Renderer renderer)
        {
            if (coloredObject == null) return;

            // Use the ColoredObject's highlight method with dim color
            coloredObject.Highlight(dimColor, float.MaxValue, highlightForever: true);
            
            // Additionally, if there's a renderer, we can adjust its material directly
            if (renderer != null && renderer.material != null)
            {
                var material = renderer.material;
                if (material.HasProperty("_Color"))
                {
                    var originalColor = material.GetColor("_Color");
                    material.SetColor("_Color", Color.Lerp(originalColor, dimColor, 1f - dimIntensity));
                }
            }
        }

        private void ClearAllHighlights()
        {
            // Clear node highlights and dimming
            foreach (var node in _cachedNodes)
            {
                if (node.coloredObject != null)
                {
                    node.coloredObject.SetToOriginalColor();
                }
            }

            // Clear edge highlights and restore line widths
            foreach (var edge in _cachedEdges)
            {
                if (edge.coloredObject != null)
                {
                    edge.coloredObject.ManualClearHighlight();
                }
                
                // Restore original line width and color
                if (edge.lineRenderer != null)
                {
                    if (_originalLineWidths.TryGetValue(edge.lineRenderer, out float originalWidth))
                    {
                        edge.lineRenderer.startWidth = originalWidth;
                        edge.lineRenderer.endWidth = originalWidth;
                    }
                    
                    // Restore line renderer material color
                    var material = edge.lineRenderer.material;
                    if (material != null)
                    {
                        material.color = Color.white;
                    }
                }
            }
        }

        private int GetTotalSearchableCount()
        {
            BuildCache();
            return _cachedNodes.Count + _cachedEdges.Count;
        }

        private void UpdateResultsCount(int matches, int total)
        {
            if (!resultsCountText) return;
            
            if (string.IsNullOrEmpty(_lastSearchTerm))
            {
                resultsCountText.text = $"Total: {_cachedNodes.Count} nodes, {_cachedEdges.Count} edges";
            }
            else
            {
                resultsCountText.text = $"Found: {matches} / {total}";
                if (dimNonMatches)
                {
                    resultsCountText.text += " (dimming enabled)";
                }
            }
        }

        public void ClearSearch()
        {
            if (searchInputField)
            {
                searchInputField.text = "";
            }

            ClearAllHighlights();
            UpdateResultsCount(0, GetTotalSearchableCount());
            _lastSearchTerm = "";
        }

        public void OnSearchDefocus(string _)
        {
            ClearAllHighlights();
            UpdateResultsCount(0, GetTotalSearchableCount());
            _lastSearchTerm = "";
        }

        private void OnSearchEndEdit(string searchText)
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                PerformSearch(searchText);
            }
        }

        private void FocusSearchInput()
        {
            if (!searchInputField) return;
            searchInputField.Select();
            searchInputField.ActivateInputField();
        }

        private void OnSearchFocus(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && !searchInputField.isFocused)
            {
                FocusSearchInput();
            }
        }

        private void OnClearSearch(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && !searchInputField.isFocused && !string.IsNullOrEmpty(_lastSearchTerm))
            {
                ClearSearch();
            }
        }

        public void RefreshCache()
        {
            InvalidateCache();
            BuildCache();
        }

        /// <summary>
        /// Toggle dimming of non-matching objects
        /// </summary>
        public void ToggleDimming()
        {
            dimNonMatches = !dimNonMatches;
            
            // Re-run the search if there's an active search term
            if (!string.IsNullOrEmpty(_lastSearchTerm))
            {
                PerformSearch(_lastSearchTerm);
            }
        }

        /// <summary>
        /// Set dimming intensity
        /// </summary>
        public void SetDimIntensity(float intensity)
        {
            dimIntensity = Mathf.Clamp01(intensity);
            
            // Re-run the search if there's an active search term
            if (!string.IsNullOrEmpty(_lastSearchTerm))
            {
                PerformSearch(_lastSearchTerm);
            }
        }
    }
}
