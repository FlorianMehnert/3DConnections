using Unity.Entities;
using Unity.Mathematics;

namespace _3DConnections.Runtime
{
    public struct Collider2DComponent : IComponentData
    {
        public float2 Size;
        public bool IsTrigger;
    }
}