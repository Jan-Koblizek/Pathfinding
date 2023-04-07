using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionalFlowGraphPlanning
{
    public static void StartNewFlowGraphPlan(FlowGraph flowGraph, HashSet<Unit> units)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        FlowPaths flowPaths = new FlowPaths();
        List<int> source = new List<int>() { flowGraph.Source.Id };
        List<int> terminal = new List<int>() { flowGraph.Terminal.Id };
        while (true)
        {
            List<int> shortestPath = Pathfinding.FlowGraphPath(flowGraph, source, terminal);
            if (shortestPath == null)
            {
                break;
            }
            //saturate the path in flow graph
            flowPaths.AddPath(flowGraph, shortestPath);
        }

        List<ConcurrentPaths> concurrentPaths = flowPaths.Finish();

        List<UnitPathAssignmentRegionalFlowGraph> distribution = FlowPaths.AssignUnitCountsToConcurrentPathsRegional(concurrentPaths, units.Count, flowGraph);
        RegionalFlowGraphPath path = UnitPathAssignmentRegionalFlowGraph.CreateRegionalFlowGraphPath(distribution, units, true);

        Vector2 goal = flowGraph.Terminal.Center.GetWorldPosition();
        bool sameStartingGate = true;
        int startingGateID = path.regionalPaths[0].gatewayPath[0].ID;
        for (int i = 1; i < path.regionalPaths.Count; i++)
        {
            if (path.regionalPaths[i].gatewayPath[0].ID != startingGateID)
            {
                sameStartingGate = false;
            }
        }

        if (sameStartingGate)
        {
            foreach (Unit unit in units)
            {
                unit.movementMode = MovementMode.RegionalFlowGraph;
                unit.UseRegionalFlowGraphPath(path, 0);
                unit.SetTarget(Simulator.Instance.target);
            }
        }
        else
        {
            UnitPathAssignmentRegionalFlowGraph.AssignStartingPaths(path, units);
        }
    }

    public static void StartNewFlowGraphPlanWarmUp(FlowGraph flowGraph, HashSet<Unit> units)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        FlowPaths flowPaths = new FlowPaths();
        List<int> source = new List<int>() { flowGraph.Source.Id };
        List<int> terminal = new List<int>() { flowGraph.Terminal.Id };
        while (true)
        {
            List<int> shortestPath = Pathfinding.FlowGraphPath(flowGraph, source, terminal);
            if (shortestPath == null)
            {
                break;
            }
            //saturate the path in flow graph
            flowPaths.AddPath(flowGraph, shortestPath);
        }

        List<ConcurrentPaths> concurrentPaths = flowPaths.Finish();

        List<UnitPathAssignmentRegionalFlowGraph> distribution = FlowPaths.AssignUnitCountsToConcurrentPathsRegional(concurrentPaths, units.Count, flowGraph);
        RegionalFlowGraphPath path = UnitPathAssignmentRegionalFlowGraph.CreateRegionalFlowGraphPath(distribution, units, true);
    }
}
