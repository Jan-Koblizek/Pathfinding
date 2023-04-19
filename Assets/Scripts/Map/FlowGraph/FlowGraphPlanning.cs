using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class FlowGraphPlanning
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartNewFlowGraphPlan(FlowGraph flowGraph, List<Unit> units)
    {
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
        List<UnitPathAssignmentFlowGraph> distribution = FlowPaths.AssignUnitCountsToConcurrentPaths(concurrentPaths, units.Count, flowGraph);       
        UnitPathAssignmentFlowGraph.AssignUnitPathsHeuristic(distribution, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void StartNewFlowGraphPlanWarmUp(FlowGraph flowGraph, List<Unit> units)
    {
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
        List<UnitPathAssignmentFlowGraph> distribution = FlowPaths.AssignUnitCountsToConcurrentPaths(concurrentPaths, units.Count, flowGraph);
        UnitPathAssignmentFlowGraph.AssignUnitPathsHeuristic(distribution, true);
    }
}
