using UnityEngine;
using UnityEngine.InputSystem;

namespace _3DConnections.Runtime.Selection
{
    public class RaycastManager : MonoBehaviour
    {
        [SerializeField] private string targetLayerName = "OverlayLayer";
        [SerializeField] private Camera targetCamera;
        
        private int _targetLayerMask;
        private readonly RaycastHit2D[] _raycastBuffer = new RaycastHit2D[16];

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            _targetLayerMask = LayerMask.GetMask(targetLayerName);
            
            if (_targetLayerMask == 0)
                Debug.LogError($"Layer '{targetLayerName}' not found!");
        }

        public Vector2 GetMouseWorldPosition(InputAction mousePositionAction)
        {
            if (mousePositionAction == null || targetCamera == null) 
                return Vector2.zero;
                
            Vector2 screenPos = mousePositionAction.ReadValue<Vector2>();
            return targetCamera.ScreenToWorldPoint(screenPos);
        }

        public RaycastHit2D RaycastAtMousePosition(Vector2 mouseWorldPosition)
        {
            var hitCount = Physics2D.RaycastNonAlloc(
                mouseWorldPosition,
                Vector2.zero,
                _raycastBuffer,
                Mathf.Infinity,
                _targetLayerMask);

            if (hitCount > 0)
                return GetClosestHit(_raycastBuffer, hitCount, mouseWorldPosition);

            return Physics2D.Raycast(
                mouseWorldPosition,
                Vector2.zero,
                Mathf.Infinity,
                _targetLayerMask);
        }

        public RaycastHit2D RaycastDown(Vector2 position)
        {
            return Physics2D.Raycast(position, Vector2.down, Mathf.Infinity, _targetLayerMask);
        }

        private static RaycastHit2D GetClosestHit(RaycastHit2D[] hits, int hitCount, Vector2 origin)
        {
            var closest = hits[0];
            var minSqr = (hits[0].point - origin).sqrMagnitude;

            for (var i = 1; i < hitCount; i++)
            {
                var sqr = (hits[i].point - origin).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    closest = hits[i];
                }
            }

            return closest;
        }

        public bool IsTargetLayer(GameObject obj)
        {
            return obj != null && ((1 << obj.layer) & _targetLayerMask) != 0;
        }
    }
}