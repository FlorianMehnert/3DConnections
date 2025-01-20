using Unity.Entities;
using Unity.Mathematics;

namespace _3DConnections.Runtime
{
    public struct SpringJoint2DComponent : IComponentData
    {
        public float2 Anchor;
        public float2 ConnectedAnchor;
        public float Frequency;
        public float DampingRatio;
    }
}