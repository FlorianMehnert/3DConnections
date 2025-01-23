
namespace _3DConnections.Runtime
{
    using Unity.Entities;
using UnityEngine;

public class PhysicsEcsConverter : MonoBehaviour
{
    public NodeGraphScriptableObject nodeGraph;  // Assign in Inspector

    private EntityManager _entityManager;

    private void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    public void ConvertNodesToEcs()
    {
        foreach (var node in nodeGraph.AllNodes)
        {
            if (node == null) continue;

            var entity = _entityManager.CreateEntity();

            // Convert Rigidbody2D
            var rb2D = node.GetComponent<Rigidbody2D>();
            if (rb2D != null)
            {
                _entityManager.AddComponentData(entity, new Rigidbody2DComponent
                {
                    Velocity = rb2D.linearVelocity,
                    Mass = rb2D.mass,
                    Drag = rb2D.linearDamping,
                    BodyType = rb2D.bodyType
                });
            }

            // Convert Collider2D (assuming BoxCollider2D for now)
            var boxCollider2D = node.GetComponent<BoxCollider2D>();
            if (boxCollider2D != null)
            {
                _entityManager.AddComponentData(entity, new Collider2DComponent
                {
                    Size = boxCollider2D.size,
                    IsTrigger = boxCollider2D.isTrigger
                });
            }

            // Convert SpringJoint2D
            var springJoint2D = node.GetComponent<SpringJoint2D>();
            if (springJoint2D != null)
            {
                _entityManager.AddComponentData(entity, new SpringJoint2DComponent
                {
                    Anchor = springJoint2D.anchor,
                    ConnectedAnchor = springJoint2D.connectedAnchor,
                    Frequency = springJoint2D.frequency,
                    DampingRatio = springJoint2D.dampingRatio
                });
            }

            Debug.Log($"Converted {node.name} to ECS entity {entity.Index}");
        }
    }
}

}