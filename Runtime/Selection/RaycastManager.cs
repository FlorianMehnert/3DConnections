using UnityEngine;
using UnityEngine.InputSystem;

namespace _3DConnections.Runtime.Selection
{
    public class RaycastManager : MonoBehaviour
    {
        [SerializeField] private string targetLayerName = "OverlayScene";
        [SerializeField] private Camera targetCamera;

        private int _targetLayerMask;

        public void Initialize(Camera overlayCamera)
        {
            targetCamera = overlayCamera;
            _targetLayerMask = LayerMask.GetMask(targetLayerName);

            if (_targetLayerMask == 0)
                Debug.LogError($"Layer '{targetLayerName}' not found!");
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
            return Physics2D.Raycast(position, Vector2.down, Mathf.Infinity, _targetLayerMask);
        }

        public GameObject GetClosestObjectToMouse(Vector2 mouseWorldPosition)
        {
            var hits = Physics2D.RaycastAll(mouseWorldPosition, Vector2.zero, Mathf.Infinity, _targetLayerMask);

            if (hits.Length == 0) return null;

            var closestHit = hits[0];
            var closestDistance = Vector2.Distance(mouseWorldPosition, hits[0].transform.position);

            foreach (var hit in hits)
            {
                var distance = Vector2.Distance(mouseWorldPosition, hit.transform.position);
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestHit = hit;
            }

            var closestObject = closestHit.collider.gameObject;
            return closestObject;

        }
    }
}