using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [Tooltip("Settings Object for the UI")]
    [CreateAssetMenu(fileName = "UI Settings",
        menuName = "3DConnections/ScriptableObjects/UI Settings", order = 1)]
    public class UISettings : ScriptableObject
    {
        public UnityEvent<float> onRadiusChanged = new UnityEvent<float>();

        [SerializeField, Min(0f)] 
        private float nodeScalingInfluenceRadius = 10.0f;

        public float Radius
        {
            get => nodeScalingInfluenceRadius;
            set
            {
                if (Mathf.Approximately(nodeScalingInfluenceRadius, value)) return;
                nodeScalingInfluenceRadius = value;
                onRadiusChanged?.Invoke(nodeScalingInfluenceRadius);
            }
        }
    
        private void OnValidate()
        {
            onRadiusChanged?.Invoke(nodeScalingInfluenceRadius);
        }
    }
}