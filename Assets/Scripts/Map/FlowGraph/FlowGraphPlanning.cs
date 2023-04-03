using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowGraphPlanning
{
    public static void StartNewFlowGraphPlan(FlowGraph flowGraph, List<Unit> units)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        FlowPaths flowPaths = new FlowPaths();
        List<int> source = new List<int>() { flowGraph.Source.Id };
        List<int> terminal = new List<int>() { flowGraph.Terminal.Id };
        while (true)
        {
            System.Diagnostics.Stopwatch pathStopwatch = new System.Diagnostics.Stopwatch();
            pathStopwatch.Start();
            List<int> shortestPath = Pathfinding.FlowGraphPath(flowGraph, source, terminal);
            pathStopwatch.Stop();
            Debug.Log($"Computing Path: {pathStopwatch.Elapsed.TotalMilliseconds} ms");
            if (shortestPath == null)
            {
                break;
            }
            Debug.Log(shortestPath.Count);
            pathStopwatch.Restart();
            //saturate the path in flow graph
            flowPaths.AddPath(flowGraph, shortestPath);
            pathStopwatch.Stop();
            Debug.Log($"Adding path: {pathStopwatch.Elapsed.TotalMilliseconds} ms");
        }
        stopwatch.Stop();
        Debug.Log($"Calculating paths: {stopwatch.Elapsed.TotalMilliseconds} ms");

        stopwatch.Restart();
        List<ConcurrentPaths> concurrentPaths = flowPaths.Finish();
        stopwatch.Stop();
        Debug.Log($"Flow paths finishing: {stopwatch.Elapsed.TotalMilliseconds} ms");

        stopwatch.Restart();
        List<UnitPathAssignmentFlowGraph> distribution = FlowPaths.AssignUnitCountsToConcurrentPaths(concurrentPaths, units.Count, flowGraph);
        stopwatch.Stop();
        Debug.Log($"Assigning unit counts to paths: {stopwatch.ElapsedMilliseconds} ms");

        stopwatch.Restart();
        UnitPathAssignmentFlowGraph.AssignUnitPathsHeuristic(distribution);
        stopwatch.Stop();
        Debug.Log($"Assigning units to paths: {stopwatch.ElapsedMilliseconds} ms");
    }
}
