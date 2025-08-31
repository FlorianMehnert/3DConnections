using System;
using UnityEngine;

namespace _3DConnections.Runtime.Events
{
    [CreateAssetMenu(fileName = "AnalyzeEvent", menuName = "3DConnections/Events/Analyze Scene Event")]
    public class AnalyzeEvent : ScriptableObject
    {
        // only this class can raise it
        private event Action OnEventTriggered;

        /// <summary>
        /// Raises the event, notifying all listeners
        /// </summary>
        public void Raise()
        {
            OnEventTriggered?.Invoke();
        }

        /// <summary>
        /// Subscribe a listener to this event
        /// </summary>
        public void AddListener(Action listener)
        {
            OnEventTriggered += listener;
        }

        /// <summary>
        /// Unsubscribe a listener from this event
        /// </summary>
        public void RemoveListener(Action listener)
        {
            OnEventTriggered -= listener;
        }

        /// <summary>
        /// Clears all listeners (useful when domain reload is disabled)
        /// </summary>
        public void Clear()
        {
            OnEventTriggered = null;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            // Clear listeners when entering play mode to avoid duplicates
            // when domain reload is disabled
            if (!Application.isPlaying)
            {
                OnEventTriggered = null;
            }
#endif
        }
    }
}