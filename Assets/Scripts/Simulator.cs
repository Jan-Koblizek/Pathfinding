using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    public SimulationSetup simulationSpecification;
    public GameObject regionIndicator;
    public static Simulator Instance { get; private set; }
    public UnitMovementManager unitMovementManager;
    public GameObject unitPrefab;
    public GameObject unitsParent;
    public GameObject targetPrefab;
    public MovementMode movementMode;

    //[HideInInspector]
    public List<Vector2> warmUpStartPositions;
    //[HideInInspector]
    public List<Vector2> warmUpTargetPositions;

    private float simulationTime;
    private int unitsReachedTarget;
    private int numberOfUnits;
    [HideInInspector]
    public Target target;
    [HideInInspector]
    public bool simulationStarted = false;
    [HideInInspector]
    public bool simulationFinished = false;
    private WaterDecomposition waterDecomposition;
    [HideInInspector]
    public RegionalDecomposition decomposition;
    [HideInInspector]
    public RegionalPathfindingAnalysis regionalPathfinding;
    [HideInInspector]
    public PartialFlowGraph partialFlowGraph;

    float secondTimer = 1.0f;
    private bool halfFinished = false;

    private void Start()
    {
        Instance = this;
        Map.instance.Initialize(simulationSpecification.mapTexture);
        List<Unit> units = new List<Unit>();
        for (int y = 0; y < simulationSpecification.unitStartMap.height; y++)
        {
            for (int x = 0; x < simulationSpecification.unitStartMap.width; x++)
            {
                Color pixel1 = simulationSpecification.unitStartMap.GetPixel(x, y);
                if (pixel1.r > 0.5)
                {
                    GameObject go = Instantiate(unitPrefab, new Vector3(x, y, 0.0f), Quaternion.identity, unitsParent.transform);
                    Unit unit = go.GetComponent<Unit>();
                    units.Add(unit);
                }
            }
        }
        unitMovementManager.SetUnits(units);

        warmUpStartPositions = simulationSpecification.warmUpStartPositions;
        warmUpTargetPositions = simulationSpecification.warmUpGoalPositions;

        target = Target.CreateTarget(simulationSpecification.targetPosition, simulationSpecification.targetSize, targetPrefab);
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
        if (!halfFinished && unitsReachedTarget >= numberOfUnits * 0.9) {
            Debug.Log($"90% the units arrived: {simulationTime}s");
            halfFinished = true;
        }
        if (unitsReachedTarget == numberOfUnits)
        {
            Debug.Log($"Simulation Finished: {simulationTime}s");
            simulationFinished = true;
        }
    }

    public void StartWarmUp()
    {
        for (int i = 0; i < warmUpStartPositions.Count; i++)
        {
            for (int j = 0; j < warmUpTargetPositions.Count; j++)
            {
                unitMovementManager.StartWarmUp(movementMode, warmUpStartPositions[i], warmUpTargetPositions[j]);
            }
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
                break;
            case MovementMode.FlowGraph:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.tiles, -1);
                partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition);
                break;
            case MovementMode.RegionalFlowGraph:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.tiles, -1);
                List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps2 = MapRegionPathfinding.CreateFlowMaps(decomposition);
                Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances2 = MapRegionPathfinding.DistancesBetweenGates(decomposition);
                regionalPathfinding = new RegionalPathfindingAnalysis(decomposition, flowMaps2, distances2);
                partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition);
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
    FlowGraph,
    RegionalFlowGraph
}