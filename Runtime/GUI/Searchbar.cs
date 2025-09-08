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
            // Setup Input Actions
            // searchFocusAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/f", modifiers: "ctrl");
            // clearSearchAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/escape");
        }

        private void OnEnable()
        {
            SetupUI();

            searchFocusAction.performed += OnSearchFocus;
            searchFocusAction.Enable();

            clearSearchAction.performed += OnClearSearch;
            clearSearchAction.Enable();
        }

        private void OnDisable()
        {
            searchFocusAction.performed -= OnSearchFocus;
            searchFocusAction.Disable();

            clearSearchAction.performed -= OnClearSearch;
            clearSearchAction.Disable();

            if (searchInputField != null)
            {
                searchInputField.onValueChanged.RemoveListener(OnSearchValueChanged);
                searchInputField.onEndEdit.RemoveListener(OnSearchEndEdit);
                searchInputField.onDeselect.RemoveListener(OnSearchDefocus);
            }

            if (clearButton != null)
            {
                clearButton.onClick.RemoveListener(ClearSearch);
            }
        }

        private void SetupUI()
        {
            if (searchInputField != null)
            {
                searchInputField.onValueChanged.AddListener(OnSearchValueChanged);
                searchInputField.onEndEdit.AddListener(OnSearchEndEdit);
                searchInputField.onDeselect.AddListener(OnSearchDefocus);
                searchInputField.placeholder.GetComponent<TextMeshProUGUI>().text = "Search nodes...";
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(ClearSearch);
            }

            UpdateResultsCount(0, NodeGraph?.NodeCount ?? 0);
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

            if (Time.time - _lastSearchTime >= searchDelay - 0.01f)
            {
                PerformSearch(searchText);
            }
        }

        private void PerformSearch(string searchText)
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

        private void ClearSearch()
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
        private void OnSearchDefocus(string _)
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
        private static void OnSearchEndEdit(string _) { }

        private void FocusSearchInput()
        {
            if (!searchInputField) return;
            searchInputField.Select();
            searchInputField.ActivateInputField();
        }

        // Input System callbacks
        private void OnSearchFocus(InputAction.CallbackContext ctx)
        {
            FocusSearchInput();
        }

        private void OnClearSearch(InputAction.CallbackContext ctx)
        {
            if (!string.IsNullOrEmpty(_lastSearchTerm))
            {
                ClearSearch();
            }
        }
    }
}
