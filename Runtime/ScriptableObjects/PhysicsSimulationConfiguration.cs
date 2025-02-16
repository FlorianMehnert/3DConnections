using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "PhysicsSimConfig", menuName = "3DConnections/ScriptableObjects/Physics Simulation Configuration", order = 1)]
public class PhysicsSimulationConfiguration : ScriptableObject
{
    [SerializeField, DontCreateProperty] private float stiffness;

    [CreateProperty]
    public float Stiffness
    {
        get => ConvertFromLog(stiffness);
        set => stiffness = ConvertToLog(value);
    }

    public float damping = 0.02f;
    public float colliderRadius = 5f;


    [SerializeField, DontCreateProperty] private float collisionResponseStrength;

    [CreateProperty]
    public float CollisionResponseStrength
    {
        get => ConvertFromLog(collisionResponseStrength);
        set => collisionResponseStrength = ConvertToLog(value);
    }


    private const float MinValue = 0.00001f; // Avoid zero (log(0) is undefined)
    private const float MaxValue = 1f;

    private float ConvertToLog(float linearValue)
    {
        return MinValue * Mathf.Pow(MaxValue / MinValue, linearValue);
    }

    private float ConvertFromLog(float logValue)
    {
        return Mathf.Log(logValue / MinValue) / Mathf.Log(MaxValue / MinValue);
    }
}