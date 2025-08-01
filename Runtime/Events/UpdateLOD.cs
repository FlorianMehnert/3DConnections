using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "UpdateLOD", menuName = "3DConnections/Events/UpdateLOD")]
public class UpdateLOD : ScriptableObject
{
    public UnityAction OnEventTriggered;

    public void TriggerEvent()
    {
        OnEventTriggered?.Invoke();
    }
}