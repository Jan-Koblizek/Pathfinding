using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;

public class UnitMovementManager : MonoBehaviour
{
    [HideInInspector]
    public List<Unit> units = new List<Unit>();
    public Dictionary<Unit, UnitVisualization> unitsToVisualizations;

    public List<Unit> unitsToDelete = new List<Unit>();
    public UnitsECS unitsECS = null;
    public void SetUnits(List<Unit> units, bool forceNew = false)
    {
        this.units = units;
        unitsToVisualizations = null;
        if (unitsECS == null)
        {
            unitsECS = new UnitsECS();
        }
        unitsECS.InitializeUnits(units, forceNew);
    }

    public void SetUnits(List<Unit> units, List<UnitVisualization> unitVisualizations, bool forceNew = false)
    {
        this.units = units;
        unitsToVisualizations = new Dictionary<Unit, UnitVisualization>();
        for (int i = 0; i < units.Count; i++)
        {
            unitsToVisualizations[units[i]] = unitVisualizations[i];
        }
        if (unitsECS == null)
        {
            unitsECS = new UnitsECS();
        }
        unitsECS.InitializeUnits(units, forceNew);
    }

    public void RemoveUnit(Unit unit)
    {
        if (unitsToVisualizations != null && unitsToVisualizations.ContainsKey(unit))
        {
            Destroy(unitsToVisualizations[unit].gameObject);
            unitsToVisualizations.Remove(unit);
        }
        unitsToDelete.Add(unit);
    }

    public void StartWarmUp(MovementMode movementMode, Vector2 start, Vector2 target)
    {
        switch (movementMode)
        {
            case MovementMode.FlowField:
                FlowField flowField = new FlowField(Coord.CoordFromPosition(target));
                break;
            case MovementMode.PathFollowing:
                Stack<Vector2> path = Pathfinding.ConstructPathAStar(start, target, Pathfinding.StepDistance, 0.2f);
                break;
            case MovementMode.PathFollowingLowerNumberOfPaths:
                Stack<Vector2> _ = Pathfinding.ConstructPathAStar(start, target, Pathfinding.StepDistance, 0.2f);
                break;
            case MovementMode.SupremeCommanderFlowField:
                FlowFieldSupremeCommander scFlowField = PathfindingSupremeCommander.CreateSupremeCommanderFlowField(target);
                break;
            case MovementMode.RegionalPath:
                RegionalPath regionalPath = new RegionalPath(Simulator.Instance.regionalPathfinding, start, target);
                break;
            case MovementMode.FlowGraph:
                FlowGraph flowGraph = new FlowGraph(Simulator.Instance.partialFlowGraph, start, target, Simulator.Instance.decomposition.regionMap);
                FlowGraphPlanning.StartNewFlowGraphPlanWarmUp(flowGraph, units);
                break;
            case MovementMode.RegionalFlowGraph:
                //Debug.Log($"{start}, {target}, {Simulator.Instance.decomposition.regionMap[(int)start.x, (int)start.y]}, {Simulator.Instance.decomposition.regionMap[(int)target.x, (int)target.y]}");
                FlowGraph flowGraphRFG = new FlowGraph(Simulator.Instance.partialFlowGraph, start, target, Simulator.Instance.decomposition.regionMap, Simulator.Instance.regionalPathfinding);
                RegionalFlowGraphPlanning.StartNewFlowGraphPlanWarmUp(flowGraphRFG, units.ToHashSet());
                break;
            case MovementMode.RegionalFlowGraphPaths:
                FlowGraph flowGraphRFGP = new FlowGraph(Simulator.Instance.partialFlowGraph, start, target, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanningUsingSubPaths.StartNewFlowGraphPlan(flowGraphRFGP, units.ToHashSet());
                break;

        }
    }

    public void StartMovement(MovementMode movementMode)
    {
        Vector2 positionsSum = new Vector2(0.0f, 0.0f);
        foreach (Unit unit in units) positionsSum += unit.position;
        Vector2 meanPosition = positionsSum / units.Count;
        float closestUnitToCentreDistance = float.MaxValue;
        Vector2 closestUnitToCentrePosition = new Vector2();
        foreach (Unit unit in units)
        {
            float distance = Vector2.Distance(unit.position, meanPosition);
            if (distance < closestUnitToCentreDistance)
            {
                closestUnitToCentreDistance = distance;
                closestUnitToCentrePosition = unit.position;
            }
        }

        List<UnitPathAssignmentRegionalFlowGraph> RFGDistribution = new List<UnitPathAssignmentRegionalFlowGraph>();
        System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        System.Diagnostics.Stopwatch pathfindingStopWatch = new System.Diagnostics.Stopwatch();
        //System.Diagnostics.Stopwatch pathfindingStopWatch = new System.Diagnostics.Stopwatch();
        switch (movementMode)
        {
            case MovementMode.FlowField:
                pathfindingStopWatch.Start();
                FlowField flowField = new FlowField(Coord.CoordFromPosition(Simulator.Instance.target.Center));

                foreach (Unit unit in units)
                {
                    unit.UseFlowField(flowField);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.FlowField;
                }

                pathfindingStopWatch.Stop();
                break;
            case MovementMode.PathFollowing:
                pathfindingStopWatch.Start();
                foreach (Unit unit in units)
                {
                    unit.MoveTo(Simulator.Instance.target.Center);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.PathFollowing;
                }
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.PathFollowingLowerNumberOfPaths:
                Debug.Log("Lower Number Of Paths");
                pathfindingStopWatch.Start();
                List<Stack<Vector2>> computedPaths = new List<Stack<Vector2>>();
                foreach (Unit unit in units)
                {
                    bool pathFound = false;
                    int pathIndex = -1;
                    for (int i = 0; i < computedPaths.Count; i++)
                    {
                        if (computedPaths[i] != null && computedPaths[i].Count > 0 && 
                            Map.instance.walls.pathClearBetweenPositions(unit.position, computedPaths[i].Peek()))
                        {
                            pathFound = true;
                            pathIndex = i;
                            break;
                        }
                    }
                    if (!pathFound)
                    {
                        Stack<Vector2> path = Pathfinding.ConstructPathAStar(unit.position, Simulator.Instance.target.Center, Pathfinding.StepDistance, 0.2f);
                        computedPaths.Add(path);
                        pathIndex = computedPaths.Count - 1;
                    }
                    unit.MoveAlongThePath(computedPaths[pathIndex]);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.PathFollowing;
                }
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.SupremeCommanderFlowField:
                pathfindingStopWatch.Start();
                FlowFieldSupremeCommander scFlowField = PathfindingSupremeCommander.CreateSupremeCommanderFlowField(Simulator.Instance.target.Center);
                foreach (Unit unit in units)
                {
                    unit.UseSupremeCommanderFlowField(scFlowField);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.SupremeCommanderFlowField;
                }
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.RegionalPath:
                pathfindingStopWatch.Start();
                RegionalPath regionalPath = new RegionalPath(Simulator.Instance.regionalPathfinding, closestUnitToCentrePosition, Simulator.Instance.target.Center);

                foreach (Unit unit in units)
                {
                    unit.UseRegionalPath(regionalPath);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.RegionalPath;
                }
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.FlowGraph:
                pathfindingStopWatch.Start();
                FlowGraph flowGraph = new FlowGraph(Simulator.Instance.partialFlowGraph, closestUnitToCentrePosition, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                FlowGraphPlanning.StartNewFlowGraphPlan(flowGraph, units);
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.RegionalFlowGraph:
                pathfindingStopWatch.Start();
                FlowGraph flowGraphRFG = new FlowGraph(Simulator.Instance.partialFlowGraph, closestUnitToCentrePosition, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap, Simulator.Instance.regionalPathfinding);
                RegionalFlowGraphPlanning.StartNewFlowGraphPlan(flowGraphRFG, units.ToHashSet(), out RFGDistribution);
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.RegionalFlowGraphPaths:
                pathfindingStopWatch.Start();
                FlowGraph flowGraphRFGP = new FlowGraph(Simulator.Instance.partialFlowGraph, closestUnitToCentrePosition, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanningUsingSubPaths.StartNewFlowGraphPlan(flowGraphRFGP, units.ToHashSet());
                pathfindingStopWatch.Stop();
                break;
        }
        Simulator.Instance.experimentResults.pathfindingTime = pathfindingStopWatch.Elapsed.TotalMilliseconds;
        Debug.Log($"Pathfinding Time {Simulator.Instance.experimentResults.pathfindingTime} ms");


        if (//movementMode == MovementMode.RegionalFlowGraphPaths || 
            movementMode == MovementMode.RegionalFlowGraph //|| 
            /*movementMode == MovementMode.FlowGraph*/)
        {
            if (Simulator.Instance.experimentResults.gatesData != null && Simulator.Instance.experimentResults.gatesData.gatesAnalysis == null)
            {
                Coord startCoord = Coord.CoordFromPosition(closestUnitToCentrePosition);
                Coord targetCoord = Coord.CoordFromPosition(Simulator.Instance.target.Center);
                int startRegion = Simulator.Instance.decomposition.regionMap[startCoord.X, startCoord.Y];
                int targetRegion = Simulator.Instance.decomposition.regionMap[targetCoord.X, targetCoord.Y];

                Simulator.Instance.experimentResults.gatesData.gatesAnalysis = new List<GateAnalysis>();
                Dictionary<int, GateAnalysis> analysisDictionary = new Dictionary<int, GateAnalysis>();
                foreach (RegionGateway gate in Simulator.Instance.decomposition.gateways)
                {
                    GateAnalysis gateAnalysis = new GateAnalysis();
                    for (int i = 0; i < gate.regionA.gateways.Count; i++)
                    {
                        if (gate.regionA.gateways[i].ID != gate.ID)
                        {
                            gateAnalysis.nearbyGates.Add(gate.regionA.gateways[i].ID);
                            if (!Simulator.Instance.regionalPathfinding.distancesBetweenGates.ContainsKey(gate) ||
                                !Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate].ContainsKey(gate.regionA.gateways[i]))
                            {
                                //Debug.Log(Simulator.Instance.regionalPathfinding.distancesBetweenGates.ContainsKey(gate));
                                gateAnalysis.nearbyGateDistances.Add(float.MaxValue);
                            }
                            else
                            {
                                //Debug.Log(Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate][gate.regionA.gateways[i]]);
                                gateAnalysis.nearbyGateDistances.Add(Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate][gate.regionA.gateways[i]]);
                            }
                        }
                    }
                    for (int i = 0; i < gate.regionB.gateways.Count; i++)
                    {
                        int id = gate.regionB.gateways[i].ID;
                        if (id != gate.ID && !gateAnalysis.nearbyGates.Contains(id))
                        {
                            gateAnalysis.nearbyGates.Add(id);
                            if (!Simulator.Instance.regionalPathfinding.distancesBetweenGates.ContainsKey(gate) ||
                                !Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate].ContainsKey(gate.regionB.gateways[i]))
                            {
                                //Debug.Log(Simulator.Instance.regionalPathfinding.distancesBetweenGates.ContainsKey(gate));
                                gateAnalysis.nearbyGateDistances.Add(float.MaxValue);
                            }
                            else
                            {
                                //Debug.Log(Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate][gate.regionB.gateways[i]]);
                                gateAnalysis.nearbyGateDistances.Add(Simulator.Instance.regionalPathfinding.distancesBetweenGates[gate][gate.regionB.gateways[i]]);
                            }
                        }
                    }
                    if (gate.regionB.ID == startRegion || gate.regionA.ID == startRegion)
                    {
                        gateAnalysis.nearbyGates.Add(-1);
                        gateAnalysis.nearbyGateDistances.Add(Simulator.Instance.regionalPathfinding.flowMaps[Simulator.Instance.decomposition.mapRegions[startRegion].gateways.IndexOf(gate)][startCoord.X, startCoord.Y].distanceToGate);
                    }
                    if (gate.regionB.ID == targetRegion || gate.regionA.ID == targetRegion)
                    {
                        gateAnalysis.nearbyGates.Add(-2);
                        gateAnalysis.nearbyGateDistances.Add(Simulator.Instance.regionalPathfinding.flowMaps[Simulator.Instance.decomposition.mapRegions[targetRegion].gateways.IndexOf(gate)][targetCoord.X, targetCoord.Y].distanceToGate);
                    }

                    gateAnalysis.GateID = gate.ID;
                    gateAnalysis.Width = gate.GetSize();
                    gateAnalysis.FlowCapacity = FlowEdge.GetMaxFlow(gate.GetSize());
                    gateAnalysis.joinedStreams = new List<List<int>>();
                    gateAnalysis.FlowStreams = new Dictionary<int, (float DistanceToGoal, float expectedStartTime, float expectedFinishTime, List<(float, float)> expectedFlow)>();
                    analysisDictionary[gate.ID] = gateAnalysis;
                }

                Dictionary<(int, int), List<int>> gateCombinationStreams = new Dictionary<(int, int), List<int>>();
                for (int streamIndex = 0; streamIndex < RFGDistribution.Count; streamIndex++)
                {
                    UnitPathAssignmentRegionalFlowGraph flowStream = RFGDistribution[streamIndex];
                    float distanceBehind = 0.0f;
                    float pathLength = 0.0f;
                    for (int i = 1; i < flowStream.centerList.Count; i++)
                    {
                        pathLength += Vector2.Distance(flowStream.centerList[i-1], flowStream.centerList[i]);
                    }

                    for (int i = 0; i < flowStream.centerList.Count; i++)
                    {
                        if (i>0) distanceBehind += Vector2.Distance(flowStream.centerList[i - 1], flowStream.centerList[i]);
                        if (i < flowStream.centerList.Count - 1 &&
                            flowStream.nodes[i].ChokePoint != null &&
                            flowStream.nodes[i].ChokePoint != flowStream.nodes[i+1].ChokePoint)
                        {
                            analysisDictionary[flowStream.nodes[i].ChokePoint.ID].FlowStreams[streamIndex] = (
                                pathLength - distanceBehind,
                                distanceBehind / SimulationSettings.instance.UnitSpeed,
                                distanceBehind / SimulationSettings.instance.UnitSpeed + (float)flowStream.assignedUnits / flowStream.flow,
                                new List<(float, float)>() {(0.0f, flowStream.flow)});

                            if (flowStream.nodes[i + 1].ChokePoint != null)
                            {
                                if (!gateCombinationStreams.ContainsKey((flowStream.nodes[i].ChokePoint.ID, flowStream.nodes[i + 1].ChokePoint.ID)))
                                {
                                    gateCombinationStreams[(flowStream.nodes[i].ChokePoint.ID, flowStream.nodes[i + 1].ChokePoint.ID)] = new List<int>();
                                }
                                gateCombinationStreams[(flowStream.nodes[i].ChokePoint.ID, flowStream.nodes[i + 1].ChokePoint.ID)].Add(streamIndex);
                                //Debug.Log($"ID1: {flowStream.nodes[i].ChokePoint.ID}, ID2: {flowStream.nodes[i + 1].ChokePoint.ID}, N Streams: {gateCombinationStreams[(flowStream.nodes[i].ChokePoint.ID, flowStream.nodes[i + 1].ChokePoint.ID)].Count}");
                            }
                        }
                    }
                }

                foreach (KeyValuePair<(int, int), List<int>> keyValuePair in gateCombinationStreams)
                {
                    //Debug.Log(keyValuePair.Key);
                    if (analysisDictionary[keyValuePair.Key.Item1].joinedStreams == null)
                        analysisDictionary[keyValuePair.Key.Item1].joinedStreams = new List<List<int>>();
                    analysisDictionary[keyValuePair.Key.Item1].joinedStreams.Add(keyValuePair.Value);
                }

                foreach (GateAnalysis analysis in analysisDictionary.Values)
                {
                    Simulator.Instance.experimentResults.gatesData.gatesAnalysis.Add(analysis);
                }
            }
        }

        foreach (Unit unit in units)
        {
            unit.SetRegionalDecomposition(Simulator.Instance.decomposition);
        }
    }
    /*
    private struct GetSeekForcesJob : IJobParallelFor
    {
        public float deltaTime;
        public NativeArray<Vector2> seekForces;
        [ReadOnly]
        public List<Unit> units;
        public void Execute(int index)
        {
            seekForces[index] = units[index].GetSeekForce(deltaTime);
            units[index].desiredVelocity = SimulationSettings.instance.UnitSpeed * seekForces[index];
        }
    }
    */

    public void MoveUnits(float deltaTime, int internalMovementCycles)
    {
        //System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        //stopwatch.Start();
        if (unitsECS != null)
        {
            NativeArray<Vector2> seekForces = new NativeArray<Vector2>(units.Count, Allocator.TempJob);

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < units.Count; i++)
            {
                seekForces[i] = units[i].GetSeekForce(internalMovementCycles * deltaTime);
                units[i].desiredVelocity = SimulationSettings.instance.UnitSpeed * seekForces[i];
            }
            stopwatch.Stop();
            //Debug.Log($"Computing seek forces {stopwatch.Elapsed.TotalMilliseconds}");

            for (int i = 0; i < internalMovementCycles; i++)
            {
                unitsECS.UpdateUnits(units);
                unitsECS.GetForces(seekForces, deltaTime);
                unitsECS.GetNewPositionsAndVelocities(units, deltaTime);
            }

            for (int i = 0; i < units.Count; i++)
            {
                units[i].TryUpdatePathPositions(deltaTime * internalMovementCycles);
                units[i].TargetReachedTestAndResponse(deltaTime * internalMovementCycles);
            }

            seekForces.Dispose();
        }
        else
        {        
            foreach (Unit unit in units)
            {
                if (unit != null)
                {
                    Vector2 force = unit.ComputeForces(deltaTime);
                    unit.CalculateNewPosition(deltaTime, force);
                }
            }
            foreach (Unit unit in units)
            {
                unit.UpdatePositionAndVelocity();
            }
        }

        foreach (Unit deletedUnit in unitsToDelete)
        {
            units.Remove(deletedUnit);
        }
        if (unitsToDelete.Count > 0 && unitsECS != null)
        {
            unitsECS.UnitsRemoved(units);
        }
        
        unitsToDelete = new List<Unit>();

        if (unitsToVisualizations != null)
        {
            foreach (Unit unit in units)
            {
                unitsToVisualizations[unit].position = unit.position;
            }
        }
    }

    public void CleanUnitList()
    {
        for (int i = units.Count - 1; i>=0; i--)
        {
            if (units[i] == null) units.RemoveAt(i);
        }
    }

    private void CleanECS()
    {
        if (unitsECS != null)
        {
            unitsECS.CleanArrays();
        }
    }

    private void OnDestroy()
    {
        CleanECS();
    }
}
