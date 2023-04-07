using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionalFlowGraphPlanning
{
    public static void StartNewFlowGraphPlan(FlowGraph flowGraph, List<Unit> units)
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

        UnitPathAssignmentRegionalFlowGraph.AssignUnitPathsHeuristic(distribution, false);
    }

    public static void StartNewFlowGraphPlanWarmUp(FlowGraph flowGraph, List<Unit> units)
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

        UnitPathAssignmentRegionalFlowGraph.AssignUnitPathsHeuristic(distribution, true);
    }
}
