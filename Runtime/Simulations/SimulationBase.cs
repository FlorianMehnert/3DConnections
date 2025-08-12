using UnityEngine;

public abstract class SimulationBase : MonoBehaviour
{
    [Header("Simulation Timing")]
    public float updateInterval = 0.02f;
    
    [Header("Temperature Settings")]
    public float coolingFactor = 0.95f;
    public float minTemperature = 0.05f;
    public float startTemperature = 1.0f;

    protected float CurrentTemperature { get; set; }
    private float _timer;
    protected bool Activated = true;
    private bool _runningStep;

    public virtual void OnEnable()
    {
        ResetSimulation();
        AdditionalEnableSteps();
    }
    
    /// <summary>
    /// Executed after ResetSimulation in OnEnable of SimulationBase 
    /// </summary>
    protected virtual void AdditionalEnableSteps()
    {
        
    }

    protected virtual void Update()
    {
        if (!Activated || _runningStep) return;

        _timer += Time.deltaTime;
        if (!(_timer >= updateInterval)) return;
        _timer -= updateInterval;
        _runningStep = true;

        RunStep();
        CoolDown();
        _runningStep = false;
    }

    private void ResetSimulation()
    {
        CurrentTemperature = startTemperature;
        _timer = 0f;
    }

    private void CoolDown()
    {
        CurrentTemperature *= coolingFactor;
        if (CurrentTemperature < minTemperature)
            CurrentTemperature = minTemperature;
    }

    /// <summary>
    /// Called at each simulation tick (interval).
    /// </summary>
    protected abstract void RunStep();
}