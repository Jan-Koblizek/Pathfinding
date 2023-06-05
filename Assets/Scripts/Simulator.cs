using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulator : MonoBehaviour
{
    public bool writeResults;
    public bool automaticSimulation;
    public int numberOfSimulations;
    public List<MovementMode> movementModesTestedAutomatic;
    public int internalMovementCycles;
    public int outerMovementCycles;
    public bool drawUnits = true;
    public List<SimulationSetup> simulationSpecifications;
    public static Simulator Instance { get; private set; }
    public UnitMovementManager unitMovementManager;
    public GameObject unitPrefab;
    public GameObject unitsParent;
    public GameObject targetPrefab;
    public MovementMode movementMode;

    private float simulationTime;
    private float executionComputationTime;
    private int unitsReachedTarget;
    private int numberOfUnits;
    [HideInInspector]
    public Target target;
    [HideInInspector]
    public List<Vector2> warmUpGoalPositions;
    [HideInInspector]
    public List<Vector2> warmUpStartPositions;


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
    public RegionalPathfindingPathsAnalysis regionalPathfindingPaths;
    [HideInInspector]
    public PartialFlowGraph partialFlowGraph;
    private bool halfFinished = false;
    private int simulationID;
    private bool failed = false;

    [HideInInspector]
    public ExperimentResults experimentResults;

    private void Start()
    {
        Instance = this;
        if (!automaticSimulation)
        {
            simulationID = 0;
            Map.instance.Initialize(simulationSpecifications[0].mapTexture);
            InitializeUnits();

            target = Target.CreateTarget(simulationSpecifications[0].GetTargetPosition(), simulationSpecifications[0].targetSize, targetPrefab);
            warmUpStartPositions = simulationSpecifications[0].GetWarmUpStartPositions();
            warmUpGoalPositions = simulationSpecifications[0].GetWarmUpGoalPositions();
        }
        if (automaticSimulation)
        {
            StartCoroutine(SimulateAutomatic());
        }
    }

    IEnumerator SimulateAutomatic()
    {
        for (int i = 0; i < simulationSpecifications.Count; i++)
        {
            simulationID = i;
            Map.instance.Initialize(simulationSpecifications[i].mapTexture);
            if (target != null) Destroy(target.gameObject);
            target = Target.CreateTarget(simulationSpecifications[i].GetTargetPosition(), simulationSpecifications[i].targetSize, targetPrefab);
            warmUpStartPositions = simulationSpecifications[i].GetWarmUpStartPositions();
            warmUpGoalPositions = simulationSpecifications[i].GetWarmUpGoalPositions();
            yield return StartCoroutine(SimulateAutomaticOne());
        }
        Debug.Log("Quitting");
        Application.Quit();
    }

    IEnumerator SimulateAutomaticOne()
    {
        decomposition = null;
        experimentResults = new ExperimentResults();
        foreach (MovementMode movementMode in movementModesTestedAutomatic)
        {
            InitializeUnits(true);
            Debug.Log($"Pathfinding method: {movementMode} --------------------------------------------------------------");
            this.movementMode = movementMode;
            yield return new WaitForSeconds(0.5f);
            StartPreparation();
            yield return null;
            yield return new WaitForSeconds(0.5f);
            StartWarmUp();
            yield return null;
            yield return new WaitForSeconds(0.5f);

            yield return null;
            yield return new WaitForSeconds(0.5f);

            int i = 0;
            while (i < numberOfSimulations)
            {
                failed = false;
                //Debug.Log(unitMovementManager.units.Count);
                experimentResults.runNumber = i;
                experimentResults.experimentName = simulationSpecifications[simulationID].name;
                experimentResults.movementModeName = movementMode.ToString();
                Debug.Log("Before Start Simulation");
                StartSimulation();
                yield return null;
                while (!simulationFinished) yield return new WaitForSeconds(0.2f);
                if (writeResults) experimentResults.WriteExperimentResultsToFile($"Results/{simulationSpecifications[simulationID].name}/{movementMode}-{i}.txt", failed);
                yield return new WaitForSeconds(1.0f);
                if (i != numberOfSimulations - 1)
                {
                    InitializeUnits();
                }
                i++;
                yield return new WaitForSeconds(1.0f);
            }
            yield return new WaitForSeconds(1.0f);
        }
        if (writeResults)
        {
            experimentResults.WriteGateAnalysisToFile($"Results/{simulationSpecifications[simulationID].name}/GateAnalysis.txt");
            experimentResults.decomposition = decomposition.regionMap;
            experimentResults.WriteDecompositionToFile($"Results/{simulationSpecifications[simulationID].name}/Decomposition.txt");
        }
    }

    private void InitializeUnits(bool forceNew = false)
    {
        Debug.Log("Initialize Units");
        List<Unit> units = new List<Unit>();
        List<UnitVisualization> unitVisualizations = new List<UnitVisualization>();
        for (int y = 0; y < simulationSpecifications[simulationID].unitStartMap.height; y++)
        {
            for (int x = 0; x < simulationSpecifications[simulationID].unitStartMap.width; x++)
            {
                Color pixel1 = simulationSpecifications[simulationID].unitStartMap.GetPixel(x, y);
                if (pixel1.r > 0.5)
                {
                    Unit unit = new Unit(units.Count);
                    unit.Initialize(new Vector2(x + 0.5f, y + 0.5f));
                    units.Add(unit);
                    if (drawUnits)
                    {
                        GameObject go = Instantiate(unitPrefab, new Vector3(x + 0.5f, y + 0.5f, 0.0f), Quaternion.identity, unitsParent.transform);
                        UnitVisualization unitVisualization = go.GetComponent<UnitVisualization>();
                        unitVisualizations.Add(unitVisualization);
                    }
                }
            }
        }
        //Debug.Log(units.Count);
        if (drawUnits)
            unitMovementManager.SetUnits(units, unitVisualizations, forceNew);
        else
            unitMovementManager.SetUnits(units, forceNew);
    }

    private void Update()
    {
        if (simulationTime > 10000 && !simulationFinished)
        {
            simulationFinished = true;
            failed = true;
            for (int i = unitMovementManager.units.Count - 1; i >= 0; i--)
            {
                unitMovementManager.RemoveUnit(unitMovementManager.units[i]);
                unitMovementManager.units[i].currentCoord.UnitsAtTile().Remove(unitMovementManager.units[i]);
                foreach (Unit deletedUnit in unitMovementManager.unitsToDelete)
                {
                    unitMovementManager.units.Remove(deletedUnit);
                }
                if (unitMovementManager.unitsToDelete.Count > 0 && unitMovementManager.unitsECS != null)
                {
                    unitMovementManager.unitsECS.UnitsRemoved(unitMovementManager.units);
                }
            }
        }
        if (simulationStarted && !simulationFinished)
        {
            for (int i = 0; i < outerMovementCycles; i++)
            {
                float deltaTime = 0.02f;
                unitMovementManager.MoveUnits(deltaTime, internalMovementCycles);
                simulationTime += deltaTime * internalMovementCycles;
            }
        }
        executionComputationTime += Time.unscaledDeltaTime;
    }
    /*
    private void OneSecondUpdate()
    {
        secondTimer -= Time.deltaTime;
        if (secondTimer <= 0)
        {
            secondTimer = 1.0f;
            unitMovementManager.CleanUnitList();
        }
    }
    */

    public void UnitReachedTarget(Unit unit)
    {
        unitsReachedTarget++;
        experimentResults.unitPathsData.unitPaths[unit.ID] = unit.pathPositions;
        experimentResults.unitPathsData.finishTimes[unit.ID] = simulationTime;
        experimentResults.unitPathsData.repaths += unit.repaths;
        experimentResults.unitPathsData.softRepaths += unit.softRepaths;
        experimentResults.gatesData.unitGatePaths[unit.ID] = unit.gatePathMovement;
        if (!halfFinished && unitsReachedTarget >= numberOfUnits * 0.9) {
            experimentResults.finishedTime90 = simulationTime;
            Debug.Log($"90% the units arrived: {simulationTime}s");
            halfFinished = true;
        }
        if (unitsReachedTarget == numberOfUnits)
        {
            experimentResults.finishedTime = simulationTime;
            Debug.Log($"Simulation Finished: {simulationTime}s, Execution Time: {executionComputationTime}");
            simulationFinished = true;
            experimentResults.unitPathsData.ComputeResults(simulationID);
            experimentResults.gatesData.GatesDataFromUnitPaths();
        }
        unitMovementManager.RemoveUnit(unit);
    }

    public void StartWarmUp()
    {
        for (int i = 0; i < warmUpStartPositions.Count; i++)
        {
            for (int j = 1; j < warmUpGoalPositions.Count; j++)
            {
                //int random = UnityEngine.Random.Range(0, warmUpGoalPositions.Count);
                //Debug.Log($"Start Position {warmUpStartPositions[i]}, Target Position {warmUpGoalPositions[j]}");
                unitMovementManager.StartWarmUp(movementMode, warmUpStartPositions[i], warmUpGoalPositions[j]);
            }
        }
    }

    public void StartSimulation()
    {
        experimentResults.gatesData.unitGatePaths = new Dictionary<int, List<(int gateID, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition))>>();
        experimentResults.unitPathsData = new UnitPathsData();
        numberOfUnits = unitMovementManager.units.Count;
        unitsReachedTarget = 0;
        simulationFinished = false;
        halfFinished = false;
        simulationStarted = true;
        //Debug.Log("Before Start Movement");
        unitMovementManager.StartMovement(movementMode);
        simulationTime = 0;
        executionComputationTime = 0;
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
                decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
                List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps = MapRegionPathfinding.CreateFlowMaps(decomposition);
                Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances = MapRegionPathfinding.DistancesBetweenGates(decomposition);
                regionalPathfinding = new RegionalPathfindingAnalysis(decomposition, flowMaps, distances);
                break;
            case MovementMode.FlowGraph:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
                partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition);
                break;
            case MovementMode.RegionalFlowGraph:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
                experimentResults.decomposition = decomposition.regionMap;
                experimentResults.WriteDecompositionToFile($"Results/{simulationSpecifications[simulationID].name}/Decomposition.txt");
                List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps2 = MapRegionPathfinding.CreateFlowMaps(decomposition);
                Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances2 = MapRegionPathfinding.DistancesBetweenGates(decomposition);
                regionalPathfinding = new RegionalPathfindingAnalysis(decomposition, flowMaps2, distances2);
                partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition, distances2);
                //partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition);
                break;
            case MovementMode.RegionalFlowGraphPaths:
                waterDecomposition = new WaterDecomposition();
                decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
                (Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances, Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> paths) pathsAndDistances;
                pathsAndDistances = MapRegionPathfinding.DistancesAndPathsBetweenGates(decomposition);
                regionalPathfindingPaths = new RegionalPathfindingPathsAnalysis(decomposition, pathsAndDistances.paths, pathsAndDistances.distances);
                partialFlowGraph = PartialFlowGraph.PartialFlowGraphFromDecomposition(decomposition, pathsAndDistances.distances);
                break;
            default:
                break;
        }
        stopwatch.Stop();
        Debug.Log($"Preparation time: {stopwatch.Elapsed.TotalMilliseconds} ms");
        experimentResults.preparationTime = stopwatch.Elapsed.TotalMilliseconds;

        if (decomposition == null)
        {
            waterDecomposition = new WaterDecomposition();
            decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
        }
    }

    public void DecomposeMap()
    {
        waterDecomposition = new WaterDecomposition();
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        decomposition = waterDecomposition.Decompose(Map.instance.passabilityMap, -1);
        stopwatch.Stop();
        Debug.Log($"Decomposition time: {stopwatch.Elapsed.TotalMilliseconds} ms");
        stopwatch.Reset();

        List<Color> colors = new List<Color>();
        for (int i = 0; i < decomposition.numberOfClusters + 1; i++)
        {
            colors.Add(new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value));
        }
        for (int x = 0; x < decomposition.regionMap.GetLongLength(0); x++)
        {
            for (int y = 0; y < decomposition.regionMap.GetLongLength(1); y++)
            {
                if (decomposition.regionMap[x, y] != -1)
                {
                    
                    if (decomposition.IsGate(decomposition.regionMap[x, y]))
                    {
                        Map.instance.groundTexture.SetPixel(x, y, colors[colors.Count - 1]);
                    }
                    else if (decomposition.regionMap[x, y] >= 1000)
                    {
                        Map.instance.groundTexture.SetPixel(x, y, Color.green);
                    }
                    else
                    {
                        Map.instance.groundTexture.SetPixel(x, y, colors[decomposition.regionMap[x, y]]);
                    }
                    
                    /*
                    if (decomposition.IsGate(decomposition.regionMap[x, y]))
                    {
                        Map.instance.groundTexture.SetPixel(x, y, colors[decomposition.regionMap[x, y] - RegionalDecomposition.GatewayIndexOffset]);
                    }
                    else
                    {
                        Map.instance.groundTexture.SetPixel(x, y, Color.white);
                    }
                    */
                }
            }
        }
        Map.instance.RedrawTexture();
    }
}

[System.Serializable]
public enum MovementMode
{
    FlowField,
    PathFollowing,
    PathFollowingLowerNumberOfPaths,
    SupremeCommanderFlowField,
    RegionalPath,
    FlowGraph,
    RegionalFlowGraph,
    RegionalFlowGraphPaths
}