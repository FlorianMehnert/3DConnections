using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using _3DConnections.Runtime.ScriptableObjects;
using _3DConnections.Runtime.Nodes;

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

        private float _lastSearchTime;
        private string _lastSearchTerm;
        private Coroutine _searchCoroutine;

        // Input Actions
        private InputAction searchFocusAction;
        private InputAction clearSearchAction;

        [Header("Performance Settings")]
        public int maxSearchResults = 100;
        private List<SearchableNode> _cachedNodes;
        private List<SearchableEdge> _cachedEdges;
        private bool _cacheValid = false;

        private NodeGraphScriptableObject NodeGraph =>
            ScriptableObjectInventory.ScriptableObjectInventory.Instance ? ScriptableObjectInventory.ScriptableObjectInventory.Instance.graph : null;

        private NodeConnectionsScriptableObject ConnectionsGraph =>
            ScriptableObjectInventory.ScriptableObjectInventory.Instance ? ScriptableObjectInventory.ScriptableObjectInventory.Instance.conSo : null;

        // Cached searchable data structures
        private struct SearchableNode
        {
            public GameObject gameObject;
            public string name;
        }

        private struct SearchableEdge
        {
            public NodeConnection connection;
            public string name;
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

            // Invalidate cache when enabled
            _cacheValid = false;
        }

        private void OnDisable()
        {
            // Disable and unbind Input Actions
            searchFocusAction.performed -= OnSearchFocus;
            clearSearchAction.performed -= OnClearSearch;
            
            searchFocusAction.Disable();
            clearSearchAction.Disable();
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

        private void BuildCache()
        {
            if (_cacheValid && _cachedNodes != null && _cachedEdges != null) return;

            // Use object pooling for better memory management
            if (_cachedNodes == null) _cachedNodes = new List<SearchableNode>();
            else _cachedNodes.Clear();
    
            if (_cachedEdges == null) _cachedEdges = new List<SearchableEdge>();
            else _cachedEdges.Clear();

            // Cache with capacity pre-allocation for better performance
            var nodeGraph = NodeGraph;
            if (nodeGraph?.AllNodes != null)
            {
                _cachedNodes.Capacity = nodeGraph.AllNodes.Count;
                foreach (var node in nodeGraph.AllNodes)
                {
                    if (node)
                    {
                        _cachedNodes.Add(new SearchableNode
                        {
                            gameObject = node,
                            name = node.name.ToLowerInvariant()
                        });
                    }
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

            // Use a more aggressive debounce for better performance
            _searchCoroutine = StartCoroutine(DelayedSearch(searchText));
        }

        private System.Collections.IEnumerator DelayedSearch(string searchText)
        {
            // Wait for the debounce period
            yield return new WaitForSeconds(searchDelay);
    
            // Only proceed if this is still the most recent search
            if (Time.time - _lastSearchTime >= searchDelay - 0.01f && searchText == searchInputField.text)
            {
                PerformSearch(searchText);
            }
        }

        private async void PerformSearch(string searchText)
        {
            var nodeGraph = NodeGraph;
            if (!nodeGraph) return;

            _lastSearchTerm = searchText;
            BuildCache();

            if (string.IsNullOrEmpty(searchText))
            {
                ClearAllHighlights();
                UpdateResultsCount(0, GetTotalSearchableCount());
                return;
            }

            // Perform search on background thread
            var (nodeMatches, edgeMatches) = await Task.Run(() => SearchNodesAndEdges(searchText));
    
            // Return to main thread for UI updates
            HighlightMatches(nodeMatches, edgeMatches);
            UpdateResultsCount(nodeMatches.Count + edgeMatches.Count, GetTotalSearchableCount());
        }


        

        private (List<SearchableNode> nodeMatches, List<SearchableEdge> edgeMatches) SearchNodesAndEdges(string searchText)
        {
            string lowerSearchText = searchText.ToLowerInvariant();
    
            var nodeMatches = _cachedNodes
                .Where(node => node.name.IndexOf(lowerSearchText, StringComparison.Ordinal) >= 0)
                .Take(maxSearchResults / 2)
                .ToList();
    
            var edgeMatches = _cachedEdges
                .Where(edge => edge.name.IndexOf(lowerSearchText, StringComparison.Ordinal) >= 0)
                .Take(maxSearchResults / 2)
                .ToList();

            return (nodeMatches, edgeMatches);
        }


        private void HighlightMatches(List<SearchableNode> nodeMatches, List<SearchableEdge> edgeMatches)
        {
            var nodeGraph = NodeGraph;
            
            // Clear previous highlights
            ClearAllHighlights();

            // Highlight matching nodes using existing NodeGraph method
            if (nodeMatches.Count > 0)
            {
                var matchingNodeObjects = nodeMatches.Select(n => n.gameObject).ToList();
                nodeGraph.SearchNodes(_lastSearchTerm); // This should highlight the nodes
            }

            // Highlight matching edges
            foreach (var edgeMatch in edgeMatches)
            {
                HighlightEdge(edgeMatch.connection, true);
            }
        }

        private void HighlightEdge(NodeConnection connection, bool highlight)
        {
            if (connection?.lineRenderer == null) return;

            var coloredObject = connection.lineRenderer.GetComponent<ColoredObject>();
            if (coloredObject == null)
            {
                coloredObject = connection.lineRenderer.gameObject.AddComponent<ColoredObject>();
                coloredObject.SetOriginalColor(connection.connectionColor);
            }

            if (highlight)
            {
                // Highlight with a bright color (e.g., yellow)
                Color highlightColor = Color.yellow;
                highlightColor.a = 0.8f;
                coloredObject.Highlight(highlightColor, float.MaxValue); // Persistent highlight
                
                // Make the line thicker when highlighted
                connection.lineRenderer.startWidth = connection.lineWidth * 2f;
                connection.lineRenderer.endWidth = connection.lineWidth * 2f;
            }
            else
            {
                // Remove highlight
                coloredObject.SetToOriginalColor();
                
                // Reset line width
                connection.lineRenderer.startWidth = connection.lineWidth;
                connection.lineRenderer.endWidth = connection.lineWidth;
            }
        }

        private void ClearAllHighlights()
        {
            var nodeGraph = NodeGraph;
            var connectionsGraph = ConnectionsGraph;

            // Clear node highlights
            if (nodeGraph)
            {
                nodeGraph.ClearSearchHighlights();
            }

            // Clear edge highlights
            if (connectionsGraph?.connections != null)
            {
                foreach (var connection in connectionsGraph.connections)
                {
                    HighlightEdge(connection, false);
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

        // Called when the input field loses focus (defocus)
        public void OnSearchDefocus(string _)
        {
            ClearAllHighlights();
            UpdateResultsCount(0, GetTotalSearchableCount());
            _lastSearchTerm = "";
        }

        // Called when editing ends (e.g., user presses Enter or clicks away)
        private void OnSearchEndEdit(string searchText)
        {
            // Perform final search when user finishes editing
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

        // Input System callbacks - now properly connected to keyboard events
        private void OnSearchFocus(InputAction.CallbackContext ctx)
        {
            if (ctx.performed) // Only trigger on key press, not release
            {
                FocusSearchInput();
            }
        }

        private void OnClearSearch(InputAction.CallbackContext ctx)
        {
            if (ctx.performed && !string.IsNullOrEmpty(_lastSearchTerm))
            {
                ClearSearch();
            }
        }

        public void RefreshCache()
        {
            InvalidateCache();
            BuildCache();
        }
    }
}
