using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _3DConnections.Runtime.Selection
{
    public class RaycastManager : MonoBehaviour
    {
        [SerializeField] private string targetLayerNodesName = "Nodes";
        [SerializeField] private string targetLayerEdgesName = "Edges";
        [SerializeField] private Camera targetCamera;

        private int _targetLayerNodesMask;
        private int _targetLayerEdgesMask;

        public void Initialize(Camera overlayCamera)
        {
            targetCamera = overlayCamera;
            _targetLayerNodesMask = LayerMask.GetMask(targetLayerNodesName);
            _targetLayerEdgesMask = LayerMask.GetMask(targetLayerEdgesName);

            if (_targetLayerNodesMask == 0)
                Debug.LogError($"Layer '{targetLayerNodesName}' not found!");

            if (_targetLayerEdgesMask == 0)
                Debug.LogError($"Layer '{targetLayerNodesName}' not found!");
        }

        public Vector2 GetMouseWorldPosition(InputAction mousePositionAction)
        {
            if (mousePositionAction == null || !targetCamera)
                return Vector2.zero;

            var screenPos = mousePositionAction.ReadValue<Vector2>();
            var mousePos = new Vector3(screenPos.x, screenPos.y, 0)
            {
                z = -targetCamera.transform.position.z
            };

            var worldPos = targetCamera.ScreenToWorldPoint(mousePos);
            return new Vector2(worldPos.x, worldPos.y);
        }


        public RaycastHit2D RaycastDown(Vector2 position)
        {
            return Physics2D.Raycast(position, Vector2.down, Mathf.Infinity, _targetLayerNodesMask);
        }

        public GameObject GetClosestObjectToMouse(Vector2 mouseWorldPosition)
        {
            var nodeHits = Physics2D.OverlapPointAll(mouseWorldPosition, _targetLayerNodesMask);
            var edgeHits = Physics2D.OverlapPointAll(mouseWorldPosition, _targetLayerEdgesMask);

            var hits = nodeHits.Concat(edgeHits);

            if (!hits.Any()) return null;

            var closestHit = hits.First();
            var closestDistance = Vector2.Distance(mouseWorldPosition, hits.First().transform.position);

            foreach (var hit in hits)
            {
                var distance = Vector2.Distance(mouseWorldPosition, hit.transform.position);
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestHit = hit;
            }

            var closestObject = closestHit.GetComponent<Collider2D>().gameObject;
            return closestObject;
        }
    }
}