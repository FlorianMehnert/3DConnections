using UnityEngine;

[CreateAssetMenu(fileName = "NodeGraphLayoutParameters", menuName = "3DConnections/ScriptableObjects/LayoutParameters", order = 1)]
public class LayoutParameters : ScriptableObject
{
    public int layoutType = (int)LayoutType.Radial;
    public float minDistance = 2f;
    public float startRadius = 3f;
    public float radiusInc = 4f;
    public float rootSpacing = 10f;
    
    public float levelSpacing = 10f;
    public float nodeSpacing = 2f;
    public float subtreeSpacing = 2f;
}