using Unity.Entities;
using Unity.Transforms;

public partial struct ProcessPhysics2DSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (rb, transform) in
                 SystemAPI.Query<RefRW<Rigidbody2DComponent>, RefRW<LocalTransform>>())
        {
            transform.ValueRW.Position.xy += rb.ValueRW.Velocity * deltaTime;
            rb.ValueRW.Velocity *= (1 - rb.ValueRW.Drag) * deltaTime;
        }
    }
}