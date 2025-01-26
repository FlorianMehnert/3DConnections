using Unity.Entities;
using Unity.Mathematics;

public struct SpringJoint2DComponent : IComponentData
{
    public float2 Anchor;
    public float2 ConnectedAnchor;
    public float Frequency;
    public float DampingRatio;
}