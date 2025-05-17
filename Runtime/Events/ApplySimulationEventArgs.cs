using System;
using UnityEngine;

public enum ActionType { PlaySound, SpawnVFX, ApplyDamage }

public class ApplySimulationEventArgs : EventArgs
{
    public ActionType What;
    public GameObject Target;
    public float Amount;
}