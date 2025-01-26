using Unity.Entities;
using Unity.Mathematics;

    public struct Collider2DComponent : IComponentData
    {
        public float2 Size;
        public bool IsTrigger;
    }
