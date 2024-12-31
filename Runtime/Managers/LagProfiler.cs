using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
    public class LagProfiler : MonoBehaviour
    {
        private Node associatedNode;
        public float monitoringDuration = 10f;
        private float startTime;
        private bool isMonitoring = false;
        private float elapsedTime = 0f;

        void Start()
        {
            StartMonitoring();
        }

        void Update()
        {
            if (!isMonitoring) return;

            float timeBefore = Time.realtimeSinceStartup;

            // Perform the operations you're profiling

            float timeAfter = Time.realtimeSinceStartup;

            // Record the time spent
            elapsedTime += (timeAfter - timeBefore);

            // Check if the monitoring period is over
            if (Time.time - startTime > monitoringDuration)
            {
                isMonitoring = false;
                Debug.Log($"{associatedNode.RelatedGameObject.name} consumed {elapsedTime * 1000f:F2} ms during monitoring.");
            }
        }

        public void SetNode(Node node)
        {
            associatedNode = node;
        }

        private void StartMonitoring()
        {
            startTime = Time.time;
            isMonitoring = true;
            elapsedTime = 0f;
            Debug.Log($"Started monitoring {associatedNode.RelatedGameObject.name}...");
        }

        public bool ToggleIsMonitoring()
        {
            var result = isMonitoring;
            isMonitoring = !result;
            return isMonitoring;
        }

    }
}