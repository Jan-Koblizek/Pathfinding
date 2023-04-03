using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    public GameObject regionIndicator;
    public static Simulator Instance { get; private set; }
    public Target target;
    public InitialUnitPositions unitPositions;
    public UnitMovementManager unitMovementManager;
    public GameObject unitPrefab;
    public MovementMode movementMode;

    private float simulationTime;
    private int unitsReachedTarget;
    private int numberOfUnits;
    public bool simulationStarted = false;
    public bool simulationFinished = false;
    private WaterDecomposition waterDecomposition;
    [HideInInspector]
    public RegionalDecomposition decomposition;
    [HideInInspector]
    public RegionalPathfindingAnalysis regionalPathfinding;

    float secondTimer = 1.0f;

    private void Start()
    {
        Instance = this;
    }

    private void Update()
    {
        if (simulationStarted && !simulationFinished)
        {
            unitMovementManager.MoveUnits();
            simulationTime += Time.deltaTime;
            OneSecondUpdate();
        }
    }

    private void OneSecondUpdate()
    {
        secondTimer -= Time.deltaTime;
        if (secondTimer <= 0)
        {
            secondTimer = 1.0f;
            unitMovementManager.CleanUnitList();
        }
    }

    public void UnitReachedTarget()
    {
        unitsReachedTarget++;
        if (unitsReachedTarget == numberOfUnits)
        {
            Debug.Log($"Simulation Finished: {simulationTime}s");
            simulationFinished = true;
        }
    }

    public void StartSimulation()
    {
        numberOfUnits = unitMovementManager.units.Count;
        simulationStarted = true;
        simulationTime = 0;
        unitMovementManager.StartMovement(movementMode);
    }

    public void StartPreparation()
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        switch (movementMode)
        {
            case MovementMode.SupremeCommanderFlowField:
                MapRegionsSupremeCommander tmp = MapRegionsSupremeCommander.Instance;
                break;
            case MovementMode.RegionalPath:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.tiles, -1);
                List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps = MapRegionPathfinding.CreateFlowMaps(decomposition);
                Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances = MapRegionPathfinding.DistancesBetweenGates(decomposition);
                regionalPathfinding = new RegionalPathfindingAnalysis(decomposition, flowMaps, distances);
                Debug.Log(regionalPathfinding);
                break;
            case MovementMode.FlowGraph:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.tiles, -1);
                break;
            default:
                break;
        }
        stopwatch.Stop();
        Debug.Log($"Preparation time: {stopwatch.Elapsed.TotalMilliseconds} ms");
    }

    public void DecomposeMap()
    {
        waterDecomposition = new WaterDecomposition();
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        decomposition = waterDecomposition.Decompose(Map.instance.tiles, -1);
        //List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps = MapRegionPathfinding.CreateFlowMaps(decomposition);
        //Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances = MapRegionPathfinding.DistancesBetweenGates(decomposition);
        //RegionalPathfindingAnalysis regionalPathfinding = new RegionalPathfindingAnalysis(decomposition, flowMaps, distances);
        stopwatch.Stop();
        Debug.Log($"Decomposition time: {stopwatch.Elapsed.TotalMilliseconds} ms");
        stopwatch.Reset();
        //stopwatch.Start();
        //RegionalPath path = new RegionalPath(regionalPathfinding, unitMovementManager.units[0].position, target.Center);
        //stopwatch.Stop();
        //Debug.Log($"Pathfinding time: {stopwatch.Elapsed.TotalMilliseconds} ms");
        List<Color> colors = new List<Color>();
        for (int i = 0; i < decomposition.numberOfClusters + 1; i++)
        {
            colors.Add(new Color(Random.value, Random.value, Random.value));
        }
        for (int x = 0; x < decomposition.regionMap.GetLongLength(0); x++)
        {
            for (int y = 0; y < decomposition.regionMap.GetLongLength(1); y++)
            {
                if (decomposition.regionMap[x, y] != -1)
                {
                    GameObject go = Instantiate(regionIndicator, new Vector3(x, y, 0.0f), Quaternion.identity, null);
                    float depth = ((float)decomposition.depthMap[x, y]) / 12;
                    //go.GetComponent<SpriteRenderer>().color = new Color(depth, depth, depth);
                    if (decomposition.IsGate(decomposition.regionMap[x, y]))
                    {
                        go.GetComponent<SpriteRenderer>().color = colors[colors.Count - 1];
                    }
                    else
                    {
                        go.GetComponent<SpriteRenderer>().color = colors[decomposition.regionMap[x, y]];
                    }
                }
            }
        }
    }
}

public enum MovementMode
{
    FlowField,
    PathFollowing,
    PathFollowingLowerNumberOfPaths,
    SupremeCommanderFlowField,
    RegionalPath,
    FlowGraph
}