using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class UnitMovementManager : MonoBehaviour
{
    [HideInInspector]
    public List<Unit> units = new List<Unit>();
    public Dictionary<Unit, UnitVisualization> unitsToVisualizations;

    private List<Unit> unitsToDelete = new List<Unit>();
    private UnitsECS unitsECS = null;
    public void SetUnits(List<Unit> units)
    {
        this.units = units;
        unitsToVisualizations = null;
        if (unitsECS == null)
        {
            unitsECS = new UnitsECS();
        }
        unitsECS.InitializeUnits(units);
    }

    public void SetUnits(List<Unit> units, List<UnitVisualization> unitVisualizations)
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
        unitsECS.InitializeUnits(units);
    }

    public void RemoveUnit(Unit unit)
    {
        if (unitsToVisualizations != null)
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
                FlowGraph flowGraphRFG = new FlowGraph(Simulator.Instance.partialFlowGraph, start, target, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanning.StartNewFlowGraphPlanWarmUp(flowGraphRFG, units.ToHashSet());
                break;
            case MovementMode.RegionalFlowGraphPaths:
                FlowGraph flowGraphRFGP = new FlowGraph(Simulator.Instance.partialFlowGraph, units[0].position, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanningUsingSubPaths.StartNewFlowGraphPlan(flowGraphRFGP, units.ToHashSet());
                break;

        }
    }

    public void StartMovement(MovementMode movementMode)
    {
        System.Diagnostics.Stopwatch pathfindingStopWatch = new System.Diagnostics.Stopwatch();
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
                    unit.movementMode = MovementMode.PathFollowingLowerNumberOfPaths;
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
                RegionalPath regionalPath = new RegionalPath(Simulator.Instance.regionalPathfinding, units[0].position, Simulator.Instance.target.Center);

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
                FlowGraph flowGraph = new FlowGraph(Simulator.Instance.partialFlowGraph, units[0].position, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                FlowGraphPlanning.StartNewFlowGraphPlan(flowGraph, units);
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.RegionalFlowGraph:
                pathfindingStopWatch.Start();
                FlowGraph flowGraphRFG = new FlowGraph(Simulator.Instance.partialFlowGraph, units[0].position, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanning.StartNewFlowGraphPlan(flowGraphRFG, units.ToHashSet());
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.RegionalFlowGraphPaths:
                pathfindingStopWatch.Start();
                FlowGraph flowGraphRFGP = new FlowGraph(Simulator.Instance.partialFlowGraph, units[0].position, Simulator.Instance.target.Center, Simulator.Instance.decomposition.regionMap);
                RegionalFlowGraphPlanningUsingSubPaths.StartNewFlowGraphPlan(flowGraphRFGP, units.ToHashSet());
                pathfindingStopWatch.Stop();
                break;

        }
        Debug.Log($"Pathfinding Took: {pathfindingStopWatch.Elapsed.TotalMilliseconds} ms");
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

            for (int i = 0; i < units.Count; i++)
            {
                seekForces[i] = units[i].GetSeekForce(internalMovementCycles * deltaTime);
                units[i].desiredVelocity = SimulationSettings.instance.UnitSpeed * seekForces[i];
            }

            for (int i = 0; i < internalMovementCycles; i++)
            {
                unitsECS.UpdateUnits(units);
                unitsECS.GetForces(seekForces, deltaTime);
                unitsECS.GetNewPositionsAndVelocities(units, deltaTime);
            }
            //System.Diagnostics.Stopwatch removingUnitsStopWatch = new System.Diagnostics.Stopwatch();
            //removingUnitsStopWatch.Start();
            for (int i = 0; i < units.Count; i++)
            {
                units[i].TargetReachedTestAndResponse(deltaTime * internalMovementCycles);
            }
            //removingUnitsStopWatch.Stop();
            //Debug.Log($"Removing units stopwatch {removingUnitsStopWatch.Elapsed.TotalMilliseconds}");
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
        /*
        stopwatch.Stop();
        if (stopwatch.Elapsed.TotalMilliseconds > 50)
            Debug.Log($"Unit positions updating {stopwatch.Elapsed.TotalMilliseconds}ms");
        */
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
