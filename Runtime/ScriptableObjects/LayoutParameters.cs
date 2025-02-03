using UnityEngine;

[CreateAssetMenu(fileName = "NodeGraphLayoutParameters", menuName = "3DConnections/ScriptableObjects/LayoutParameters", order = 1)]
public class LayoutParameters : ScriptableObject
{
    public float minDistance = 2f;
    public float startRadius = 3f;
    public float radiusInc = 4f;
    public float rootSpacing = 10f;
}