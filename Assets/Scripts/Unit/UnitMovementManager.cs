using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitMovementManager : MonoBehaviour
{
    public List<Unit> units = new List<Unit>();
    public void SetUnits(List<Unit> units)
    {
        this.units = units;
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
                Debug.Log("Supreme Commander Flow Field");
                pathfindingStopWatch.Start();
                FlowFieldSupremeCommander scFlowField = PathfindingSupremeCommander.CreateSupremeCommanderFlowField(Simulator.Instance.target.Center);
                Debug.Log("Flow Field Constructed");
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
                //RegionalPath regionalPath = new RegionalPath(Simulator.Instance.regionalPathfinding, units[0].position, Simulator.Instance.target.Center);

                foreach (Unit unit in units)
                {
                    RegionalPath regionalPath = new RegionalPath(Simulator.Instance.regionalPathfinding, unit.position, Simulator.Instance.target.Center);
                    unit.UseRegionalPath(regionalPath);
                    unit.SetTarget(Simulator.Instance.target);
                    unit.movementMode = MovementMode.RegionalPath;
                }
                pathfindingStopWatch.Stop();
                break;
            case MovementMode.FlowGraph:
                FlowGraph flowGraph = FlowGraph.MakeZonelessFlowGraph(Simulator.Instance.decomposition, units[0].position, Simulator.Instance.target.Center);
                pathfindingStopWatch.Start();
                FlowGraphPlanning.StartNewFlowGraphPlan(flowGraph, units);
                pathfindingStopWatch.Stop();
                break;
                
        }
        Debug.Log($"Pathfinding Took: {pathfindingStopWatch.Elapsed.TotalMilliseconds} ms");
    }

    public void MoveUnits()
    {
        foreach (Unit unit in units)
        {
            if (unit != null)
            {
                unit.Move();
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
}
