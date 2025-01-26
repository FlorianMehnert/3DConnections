using Unity.Entities;
using Unity.Transforms;

public partial struct Physics2DSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, rb2d) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<Rigidbody2DComponent>>())
        {
            // Simple position update based on velocity
            transform.ValueRW.Position.xy += rb2d.ValueRW.Velocity * deltaTime;

            // Apply basic drag to velocity
            rb2d.ValueRW.Velocity *= (1 - rb2d.ValueRW.Drag * deltaTime);
        }
    }
}