using UnityEngine;

[CreateAssetMenu(fileName = "PhysicsSimConfig", menuName = "3DConnections/ScriptableObjects/Physics Simulation Configuration", order = 1)]
public class PhysicsSimulationConfiguration : ScriptableObject
{
    public float stiffness = 0.1f;
    public float damping = 0.02f;
    public float colliderRadius = 5f;
    public float collisionResponseStrength = 0.1f;
}