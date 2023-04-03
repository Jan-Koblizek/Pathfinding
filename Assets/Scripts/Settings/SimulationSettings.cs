using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationSettings : MonoBehaviour
{
    [HideInInspector]
    public const float UnitRadius = 0.4f;
    public float UnitSpeed;
    public float MaxForce;
    public static SimulationSettings instance;
    private void Start()
    {
        instance = this;
    }
}
