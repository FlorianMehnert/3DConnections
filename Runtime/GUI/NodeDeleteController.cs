using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _3DConnections.Runtime.Nodes;
using _3DConnections.Runtime.Managers;
using soi = _3DConnections.Runtime.ScriptableObjectInventory.ScriptableObjectInventory;

namespace _3DConnections.Runtime.GUI
{
    public class NodeDeleteController : MonoBehaviour
    {
        [System.Serializable]
        public class DeleteUndoData
        {
            public GameObject node;
            public string nodeName;
            public Vector3 position;
            public List<ConnectionData> connections = new();

            [System.Serializable]
            public class ConnectionData
            {
                public GameObject otherNode;
                public bool isStart;
                public Color color;
                public float width;
                public string type;
                public bool dashed;
                public CodeReference codeRef;
            }
        }

        private DeleteUndoData _lastDeleted;
        private float _undoTimeRemaining;
        private bool _showingDialog;

        // Simple inline dialog
        private GameObject _dialogObject;

        void Update()
        {
            HandleDeleteInput();
            UpdateUndoTimer();
        }

        private void HandleDeleteInput()
        {
            if (_showingDialog) return;

            if (!Input.GetKeyDown(KeyCode.Delete)) return;
            var selected = soi.Instance?.graph?.currentlySelectedGameObject;
            if (!selected) return;

            var safetyState = selected.GetComponent<NodeSafetyState>();
            if (!safetyState)
            {
                // No safety state = treat as orphan, delete immediately
                DeleteNode(selected);
                return;
            }

            if (safetyState.InboundSubscriptionCount > 0)
            {
                // Show dialog for subscribers
                ShowSubscriberDialog(selected);
            }
            else if (safetyState.InboundReferenceCount > 0)
            {
                // Optional: confirm for hard links
                // DeleteNode(selected); // Or show a simpler confirmation
                ShowSubscriberDialog(selected);
            }
            else
            {
                // Orphan - delete immediately
                // DeleteNode(selected);
                ShowSubscriberDialog(selected);
            }
        }

        private void ShowSubscriberDialog(GameObject node)
        {
            _showingDialog = true;

            var subscribers = SafeDeleteService.Instance.GetInboundSubscribers(node);

            // Create simple dialog (you'd replace this with proper UI)
            _dialogObject = new GameObject("DeleteDialog");
            var canvas = _dialogObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            _dialogObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_dialogObject.transform);
            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400, 300);
            rect.anchoredPosition = Vector2.zero;

            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            // Add text showing subscribers
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(panel.transform);
            var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = $"Warning: {subscribers.Count} event subscribers will break:\n\n";
            foreach (var (source, info) in subscribers.Take(5))
            {
                text.text += $"• {source.name}";
                if (!string.IsNullOrEmpty(info)) text.text += $" ({info})";
                text.text += "\n";
            }

            if (subscribers.Count > 5) text.text += $"... and {subscribers.Count - 5} more";

            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            var textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(380, 200);
            textRect.anchoredPosition = new Vector2(0, 30);

            // Add buttons
            CreateDialogButton("Cancel", new Vector2(-80, -100), CloseDialog);
            CreateDialogButton("Delete Anyway", new Vector2(80, -100), () =>
            {
                CloseDialog();
                DeleteNode(node);
            });
        }

        private void CreateDialogButton(string label, Vector2 position, System.Action onClick)
        {
            // Ensure EventSystem exists
            if (!FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                Debug.LogError("no event system found");
            }
    
            var buttonObj = new GameObject($"Button_{label}");
            buttonObj.transform.SetParent(_dialogObject.transform.GetChild(0));
    
            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            image.raycastTarget = true; // Enable raycast
    
            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
    
            var rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 30);
            rect.anchoredPosition = position;
    
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            var text = textObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.raycastTarget = false; // Don't block button
            var textRect = text.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(120, 30);
            textRect.anchoredPosition = Vector2.zero;
    
            button.onClick.AddListener(() => {
                Debug.Log($"Dialog button {label} clicked!");
                onClick?.Invoke();
            });
        }


        private void CloseDialog()
        {
            if (_dialogObject) Destroy(_dialogObject);
            _showingDialog = false;
        }

        private void DeleteNode(GameObject node)
        {
            // Capture undo data
            _lastDeleted = new DeleteUndoData
            {
                node = node,
                nodeName = node.name,
                position = node.transform.position
            };

            // Capture all connections
            var connections = soi.Instance.conSo.connections;
            foreach (var conn in connections)
            {
                if (conn.startNode != node && conn.endNode != node) continue;
                _lastDeleted.connections.Add(new DeleteUndoData.ConnectionData
                {
                    otherNode = conn.startNode == node ? conn.endNode : conn.startNode,
                    isStart = conn.startNode == node,
                    color = conn.connectionColor,
                    width = conn.lineWidth,
                    type = conn.connectionType,
                    dashed = conn.dashed,
                    codeRef = conn.codeReference
                });

                // Destroy the edge
                if (conn.lineRenderer) Destroy(conn.lineRenderer.gameObject);
            }

            // Remove from connection list
            soi.Instance.conSo.connections.RemoveAll(c => c.startNode == node || c.endNode == node);

            // Remove from node list
            soi.Instance.graph.AllNodes = soi.Instance.graph.AllNodes.Where(n => n != node).ToList();

            // Destroy the node
            node.SetActive(false);

            // Show undo snackbar
            _undoTimeRemaining = 5f;
            ShowUndoSnackbar();

            // Trigger recompute
            SafeDeleteService.OnConnectionsChanged?.Invoke();
        }

        private GameObject _snackbar;

        private void ShowUndoSnackbar()
        {
            if (_snackbar) Destroy(_snackbar);
            
            _snackbar = new GameObject("UndoSnackbar");
            var canvas = _snackbar.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
    
            _snackbar.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_snackbar.transform);
            var rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(300, 50);
            rect.anchorMin = new Vector2(0.5f, 0);
            rect.anchorMax = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 100);

            var image = panel.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var text = new GameObject("Text");
            text.transform.SetParent(panel.transform);
            var tmp = text.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = $"Deleted {_lastDeleted.nodeName}";
            tmp.fontSize = 14;
            tmp.color = Color.white;
            tmp.alignment = TMPro.TextAlignmentOptions.Left;
            var textRect = tmp.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200, 50);
            textRect.anchoredPosition = new Vector2(-40, 0);

            CreateSnackbarButton("UNDO", new Vector2(100, 0), UndoDelete);
        }

        private void CreateSnackbarButton(string label, Vector2 position, System.Action onClick)
        {
            var buttonObj = new GameObject($"Button_{label}");
            buttonObj.transform.SetParent(_snackbar.transform.GetChild(0));

            // Add Image first for raycast target
            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.5f, 0.8f, 0.8f);
            image.raycastTarget = true;

            var button = buttonObj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image; 
            var rect = buttonObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60, 30);
            rect.anchoredPosition = position;
            var text = new GameObject("Text");
            text.transform.SetParent(buttonObj.transform);
            var tmp = text.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.color = new Color(0.4f, 0.8f, 1f, 1f);
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            var textRect = tmp.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(60, 30);
            textRect.anchoredPosition = Vector2.zero;

            // Add the listener
            button.onClick.AddListener(() =>
            {
                onClick?.Invoke();
            });
            
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            button.colors = colors;
        }


        private void UndoDelete()
        {
            if (_lastDeleted == null) return;

            // Restore node
            _lastDeleted.node.SetActive(true);
            soi.Instance.graph.AllNodes = soi.Instance.graph.AllNodes.Append(_lastDeleted.node).ToList();

            // Restore connections
            var connManager = NodeConnectionManager.Instance;
            foreach (var conn in _lastDeleted.connections)
            {
                if (conn.isStart)
                {
                    connManager.AddConnection(
                        _lastDeleted.node, conn.otherNode,
                        conn.color, conn.width, 1f,
                        conn.type, conn.dashed, conn.codeRef
                    );
                }
                else
                {
                    connManager.AddConnection(
                        conn.otherNode, _lastDeleted.node,
                        conn.color, conn.width, 1f,
                        conn.type, conn.dashed, conn.codeRef
                    );
                }
            }

            // Clear undo data
            _lastDeleted = null;
            _undoTimeRemaining = 0;
            if (_snackbar) Destroy(_snackbar);

            // Trigger recompute
            SafeDeleteService.OnConnectionsChanged?.Invoke();
        }

        private void UpdateUndoTimer()
        {
            if (!(_undoTimeRemaining > 0)) return;
            _undoTimeRemaining -= Time.deltaTime;
            if (!(_undoTimeRemaining <= 0)) return;
            // Time expired - permanently delete
            if (_lastDeleted?.node) Destroy(_lastDeleted.node);
            _lastDeleted = null;
            if (_snackbar) Destroy(_snackbar);
        }
    }
}