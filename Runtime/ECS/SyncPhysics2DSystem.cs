namespace _3DConnections.Runtime
{
    using Unity.Entities;
    using UnityEngine;

    public partial class SyncPhysics2DSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((Rigidbody2D rb2D, ref Rigidbody2DComponent rbComponent) =>
            {
                rb2D.linearVelocity = rbComponent.Velocity;
                rb2D.linearDamping = rbComponent.Drag;
                rb2D.bodyType = rbComponent.BodyType;
            }).WithoutBurst().Run();
        }
    }

}