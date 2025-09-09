using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using _3DConnections.Runtime.ScriptableObjects;
using _3DConnections.Runtime.ScriptableObjectInventory;

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

        private NodeGraphScriptableObject NodeGraph =>
            ScriptableObjectInventory.ScriptableObjectInventory.Instance ? ScriptableObjectInventory.ScriptableObjectInventory.Instance.graph : null;

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
            UpdateResultsCount(0, NodeGraph?.NodeCount ?? 0);
            
            // Connect UI events
            if (searchInputField)
            {
                searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
                searchInputField.onEndEdit.AddListener(OnSearchEndEdit);
                searchInputField.onDeselect.AddListener(OnSearchDefocus);
            }
            
            if (clearButton)
            {
                clearButton.onClick.AddListener(ClearSearch);
            }
        }

        public void OnSearchValueChanged()
        {
            OnSearchValueChanged(searchInputField.text);
        }

        public void OnSearchValueChanged(string searchText)
        {
            _lastSearchTime = Time.time;

            if (_searchCoroutine != null)
                StopCoroutine(_searchCoroutine);

            _searchCoroutine = StartCoroutine(DelayedSearch(searchText));
        }

        private System.Collections.IEnumerator DelayedSearch(string searchText)
        {
            yield return new WaitForSeconds(searchDelay);

            if (Time.time - _lastSearchTime >= searchDelay - 0.01f)
            {
                PerformSearch(searchText);
            }
        }

        public void PerformSearch(string searchText)
        {
            var nodeGraph = NodeGraph;
            if (!nodeGraph)
            {
                Debug.LogWarning("NodeGraph reference is null!");
                return;
            }

            _lastSearchTerm = searchText;

            if (string.IsNullOrEmpty(searchText))
            {
                nodeGraph.ClearSearchHighlights();
                UpdateResultsCount(0, nodeGraph.NodeCount);
            }
            else
            {
                nodeGraph.SearchNodes(searchText);
                var matchCount = CountMatches(searchText);
                UpdateResultsCount(matchCount, nodeGraph.NodeCount);
            }
        }

        private int CountMatches(string searchText)
        {
            var nodeGraph = NodeGraph;
            return nodeGraph?.AllNodes?.Count(node => node && node.name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase)) ?? 0;
        }

        private void UpdateResultsCount(int matches, int total)
        {
            if (!resultsCountText) return;
            resultsCountText.text = string.IsNullOrEmpty(_lastSearchTerm) ? $"Total nodes: {total}" : $"Found: {matches} / {total}";
        }

        public void ClearSearch()
        {
            if (searchInputField)
            {
                searchInputField.text = "";
            }

            var nodeGraph = NodeGraph;
            if (nodeGraph)
            {
                nodeGraph.ClearSearchHighlights();
            }

            UpdateResultsCount(0, nodeGraph?.NodeCount ?? 0);
            _lastSearchTerm = "";
        }

        // Called when the input field loses focus (defocus)
        public void OnSearchDefocus(string _)
        {
            var nodeGraph = NodeGraph;
            if (nodeGraph != null)
            {
                nodeGraph.ClearSearchHighlights();
            }
            UpdateResultsCount(0, nodeGraph?.NodeCount ?? 0);
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
    }
}
