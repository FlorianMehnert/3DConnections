using Unity.Entities;
using Unity.Mathematics;

namespace _3DConnections.Runtime
{
    public struct Rigidbody2DComponent : IComponentData
    {
        public float2 Velocity;
        public float Mass;
        public float Drag;
        public UnityEngine.RigidbodyType2D BodyType;
    }
}