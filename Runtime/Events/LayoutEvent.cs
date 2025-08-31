using System;
using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.Events
{
    [CreateAssetMenu(fileName = "LayoutEvent", menuName = "3DConnections/Events/Layout Nodes Event")]
    public class LayoutEvent : ScriptableObject
    {
        public UnityAction<Action> OnEventTriggered;

        public void TriggerEvent(Action afterLayout)
        {
            OnEventTriggered?.Invoke(afterLayout);
        }
    }
}