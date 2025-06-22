using UnityEngine;

[CreateAssetMenu(fileName = "ApplicationState", menuName = "3DConnections/ScriptableObjects/ApplicationState", order = 1)]
public class ApplicationState : ScriptableObject
{
    /// <summary>
    /// state management -> set this variable if any nodes are on the field and only set to false in clear
    /// </summary>
    public bool spawnedNodes;
}