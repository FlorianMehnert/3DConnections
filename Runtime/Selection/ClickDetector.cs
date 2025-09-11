using System;
using UnityEngine;

namespace _3DConnections.Runtime.Selection
{
    public class ClickDetector : MonoBehaviour
    {
        [SerializeField] private float doubleClickThreshold = 0.3f;
        
        private float _lastClickTime;
        private int _clickCount;
        
        public event Action<GameObject> OnSingleClick;
        public event Action<GameObject> OnDoubleClick;

        public bool ProcessClick(GameObject clickedObject)
        {
            float currentTime = Time.time;
            
            if (currentTime - _lastClickTime <= doubleClickThreshold)
            {
                _clickCount++;
            }
            else
            {
                _clickCount = 1;
            }
            
            _lastClickTime = currentTime;

            switch (_clickCount)
            {
                case 1:
                    // Start timer for potential double-click
                    Invoke(nameof(ProcessSingleClick), doubleClickThreshold);
                    return false; // Not processed yet
                case 2:
                    // Cancel single click and process double click immediately
                    CancelInvoke(nameof(ProcessSingleClick));
                    _clickCount = 0;
                    OnDoubleClick?.Invoke(clickedObject);
                    return true; // Double click processed
                default:
                    return false;
            }
        }

        private void ProcessSingleClick()
        {
            if (_clickCount == 1)
            {
                _clickCount = 0;
                // Single click will be handled by the selection system
            }
        }

        private void OnDisable()
        {
            CancelInvoke();
        }
    }
}