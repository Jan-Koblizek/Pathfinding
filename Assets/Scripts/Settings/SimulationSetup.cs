using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/SimulationSetup",
                 fileName = "SimulationSetup")]
public class SimulationSetup : ScriptableObject
{
    public Texture2D mapTexture;
    public Texture2D unitStartMap;
    public Vector2 targetPosition;
    public float targetSize;
    public List<Vector2> warmUpGoalPositions;
    public List<Vector2> warmUpStartPositions;
}
