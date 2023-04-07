using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking.Types;

public class FlowPaths
{
    private readonly AlternativeFlowToPathsDistribution _working = new AlternativeFlowToPathsDistribution();
    private readonly AlternativeFlowToPathsDistribution _completed = new AlternativeFlowToPathsDistribution();

    /// <summary>
    /// Finds the maximum saturation for given path, updates the flow graph edges by the saturation for the path and saves this path.
    /// </summary>
    /// <param name="flowGraph">Graph with flow</param>
    /// <param name="shortestPath">Path representing flow as ordered list of Node IDs</param>
    internal void AddPath(FlowGraph flowGraph, List<int> shortestPath)
    {
        float saturatingFlow = GetSaturatingFlow(flowGraph, shortestPath);

        //saturate computes the sub paths
        var alternatingSubPaths = SaturatePath(flowGraph, shortestPath, saturatingFlow);
        if (saturatingFlow > 0)
        {
            if (alternatingSubPaths.Count > 1)
            {
                _working.MutatePath(_completed, alternatingSubPaths, saturatingFlow, flowGraph);
            }
            else if (shortestPath.Count > 1)
            {
                PlannerPath.CreatePath(flowGraph, shortestPath, saturatingFlow, out var orderedPartialFlow);
                _working.AddPath(orderedPartialFlow);
            }
        }
    }


    /// <summary>
    /// Computes the maximum addition to flow so that the path won't exceed its capacity.
    /// </summary>
    /// <param name="flowGraph">Underlying graph</param>
    /// <param name="shortestPath">Path in question</param>
    /// <returns>The flow on path that can be send trough all the edges.</returns>
    private static float GetSaturatingFlow(FlowGraph flowGraph, List<int> shortestPath)
    {
        float minAvailableCapacity = float.MaxValue;
        for (int i = 0; i < shortestPath.Count - 1;)
        {
            int start = shortestPath[i];
            int end = shortestPath[++i];
            FlowEdge flowEdge = flowGraph.GetEdgeByNodeIds(start, end);


            float availableCapacity;
            if (start < end)
            {
                availableCapacity = flowEdge.Capacity - flowEdge.Flow;
            }
            else
            {
                availableCapacity = flowEdge.Capacity + flowEdge.Flow;
            }

            if (availableCapacity < minAvailableCapacity)
            {
                minAvailableCapacity = availableCapacity;
            }
        }

        return minAvailableCapacity;
    }

    /// <summary>
    /// Updates flow graph values on edges by filling the extra flow and returns the alternating flow direction paths.
    /// </summary>
    /// <param name="flowGraph">The graph to edit</param>
    /// <param name="shortestPath">Ordered Node IDs</param>
    /// <param name="flow">flow to add</param>
    /// <returns>List of indexes to the path that uses the edges in opposite direction to the flow</returns>
    private static List<List<NodeID>> SaturatePath(FlowGraph flowGraph, List<int> shortestPath, float flow)
    {
        var flowSplitter = new PathSplitter();
        for (int i = 0; i < shortestPath.Count - 1;)
        {
            var start = shortestPath[i];
            var end = shortestPath[++i];
            var flowEdge = flowGraph.GetEdgeByNodeIds(start, end);

            if (start < end)
            {
                //the flow already present has opposite direction
                bool oppositeDirection = flowEdge.Flow < 0;
                flowSplitter.AddEdge(start, end, oppositeDirection);

                flowEdge.Flow += flow;
                Debug.Assert(flowEdge.Capacity >= flowEdge.Flow - 0.01f, "flowEdge.Capacity > flowEdge.Flow");
            }
            else
            {
                //the flow already present has opposite direction
                bool oppositeDirection = flowEdge.Flow > 0;
                flowSplitter.AddEdge(start, end, oppositeDirection);

                flowEdge.Flow -= flow;
                Debug.Assert(flowEdge.Capacity >= -flowEdge.Flow - 0.01f, "Capacity has to be > Flow");
            }
        }

        //List of indexes to the path that uses the edges in opposite direction to the flow
        List<List<NodeID>> alternatingFlowDirectionPath = flowSplitter.Finish();
        return alternatingFlowDirectionPath;
    }

    internal List<ConcurrentPaths> Finish()
    {
        //move everything from working to finished, return finished
        return _working.Finish(_completed);
    }



    /// <summary>
    /// Takes alternative concurrent paths, number of units, selects fastest assignment returns number of units that belong to each of the selected concurrent path's path.
    /// </summary>
    /// <param name="alternatives">Possible alternatives caused by mutations</param>
    /// <param name="numberOfUnits">Total number of units</param>
    /// <param name="flowGraph">Graph</param>
    /// <returns></returns>
    internal static List<UnitPathAssignmentFlowGraph> AssignUnitCountsToConcurrentPaths(List<ConcurrentPaths> alternatives, int numberOfUnits, FlowGraph flowGraph)
    {
        //select best solution from alternatives
        var bestPaths = SelectConcurrentPaths(alternatives, numberOfUnits, out int lowerBound);

        //for the best solution find how many units belong to which path
        var assignment = AssignUnitCountsToPaths(numberOfUnits, bestPaths, lowerBound);

        //return paths with unit counts in map grid coordinates
        return GetPathInMap(flowGraph, bestPaths, assignment);
    }

    internal static List<UnitPathAssignmentRegionalFlowGraph> AssignUnitCountsToConcurrentPathsRegional(List<ConcurrentPaths> alternatives, int numberOfUnits, FlowGraph flowGraph)
    {
        //select best solution from alternatives
        var bestPaths = SelectConcurrentPaths(alternatives, numberOfUnits, out int lowerBound);

        //for the best solution find how many units belong to which path
        var assignment = AssignUnitCountsToPaths(numberOfUnits, bestPaths, lowerBound);

        //return paths with unit counts in map grid coordinates
        return GetPathInMapRegional(flowGraph, bestPaths, assignment);
    }

    private static List<UnitPathAssignmentFlowGraph> GetPathInMap(FlowGraph flowGraph, ConcurrentPaths bestPaths, List<int> assignment)
    {
        var unitAssignments = new List<UnitPathAssignmentFlowGraph>();
        if (assignment == null)
        {
            return unitAssignments;
        }

        List<PathID> pathIds = new List<PathID>(bestPaths.GetAtOnce());

        for (int j = 0; j < pathIds.Count; j++)
        {
            int assignedUnitCount = assignment[j];
            if (assignedUnitCount != 0)
            {
                var pathID = pathIds[j];
                var centers = PlannerPath.GetPathsFlowNodeCenters(flowGraph, pathID);

                var possiblePath = new UnitPathAssignmentFlowGraph(assignedUnitCount, pathID, centers, Simulator.Instance.target);
                unitAssignments.Add(possiblePath);
            }
        }

        return unitAssignments;
    }

    private static List<UnitPathAssignmentRegionalFlowGraph> GetPathInMapRegional(FlowGraph flowGraph, ConcurrentPaths bestPaths, List<int> assignment)
    {
        var unitAssignments = new List<UnitPathAssignmentRegionalFlowGraph>();
        if (assignment == null)
        {
            return unitAssignments;
        }

        List<PathID> pathIds = new List<PathID>(bestPaths.GetAtOnce());

        for (int j = 0; j < pathIds.Count; j++)
        {
            int assignedUnitCount = assignment[j];
            if (assignedUnitCount != 0)
            {
                var pathID = pathIds[j];
                var centers = PlannerPath.GetPathsFlowNodeCenters(flowGraph, pathID);

                var possiblePath = new UnitPathAssignmentRegionalFlowGraph(assignedUnitCount, pathID, centers, Simulator.Instance.target, flowGraph);
                unitAssignments.Add(possiblePath);
            }
        }
        return unitAssignments;
    }

    private static List<int> AssignUnitCountsToPaths(int numberOfUnits, ConcurrentPaths bestPaths, int lowerBound)
    {
        List<int> assignment;
        var middleBound = lowerBound + 1;
        var upperBound = lowerBound + 2;
        var finishedUnitsLower = bestPaths.TransitOfPaths(lowerBound, out var assignmentLower);
        var finishedUnitsMiddle = bestPaths.TransitOfPaths(middleBound, out var assignmentMiddle);
        var finishedUnitsUpper = bestPaths.TransitOfPaths(upperBound, out var assignmentUpper);

        //if the real number of units is not in range lower..upper
        if (!(finishedUnitsLower <= numberOfUnits && numberOfUnits <= finishedUnitsUpper))
        {

            var str = $" O(1) method failed to compute lower and upper bounds \n({finishedUnitsLower} <= {numberOfUnits} <= {finishedUnitsUpper}), recovered by binary search: ";
            BinarySearch(numberOfUnits, bestPaths, ref lowerBound, ref middleBound, ref upperBound, ref finishedUnitsLower, ref finishedUnitsMiddle, ref finishedUnitsUpper,
                ref assignmentLower, ref assignmentMiddle, ref assignmentUpper);

            Debug.Assert(false, str + $"({finishedUnitsLower} <= {numberOfUnits} <= {finishedUnitsUpper})");
        }

        //move the middle to be one of the boarders
        if (numberOfUnits >= finishedUnitsMiddle)
        {
            assignmentLower = assignmentMiddle;
            finishedUnitsLower = finishedUnitsMiddle;
        }
        else
        {
            assignmentUpper = assignmentMiddle;
            finishedUnitsUpper = finishedUnitsMiddle;
        }


        //test if the assignment with exact value exists else 
        if (numberOfUnits == finishedUnitsLower)
        {
            assignment = assignmentLower;
        }
        else if (numberOfUnits == finishedUnitsUpper)
        {
            assignment = assignmentUpper;
        }
        else
        {
            var max = assignmentUpper.Max();
            var maxIndex = assignmentUpper.IndexOf(max);
            #if addMax
                //add to zero problem
                    var toAdd = numberOfUnits - finishedUnitsLower;
                    assignmentLower[maxIndex] += toAdd;

                    assignment = assignmentLower;
            #else
            //Remove the excessive flow from max sub flow
            var toRemove = finishedUnitsUpper - numberOfUnits;
            if (assignmentUpper[maxIndex] > toRemove)
            {
                assignmentUpper[maxIndex] -= toRemove;
            }

            assignment = assignmentUpper;
            #endif
            Debug.Assert(assignment.Sum() == numberOfUnits,
                $"assignment.Sum() == numberOfUnits {assignment.Sum()} != {numberOfUnits} " +
                $"if a>n, means that random path will have less flow then it was computed");
        }
        return assignment;
    }


    private static ConcurrentPaths SelectConcurrentPaths(List<ConcurrentPaths> alternatives, int numberOfUnits, out int lowerBound)
    {
        //select best solution from alternatives
        var altSteps = new List<float>();
        foreach (var alternative in alternatives)
        {
            var steps = alternative.GetTotalNumberOfSteps(numberOfUnits);
            altSteps.Add(steps);
        }

        var min = altSteps.Min();
        var i = altSteps.IndexOf(min);

        lowerBound = (int)Mathf.Floor(min);
        return alternatives[i];
    }

    private static void BinarySearch(int numberOfUnits, ConcurrentPaths bestPaths,
    ref int lowerBound, ref int middleBound, ref int upperBound,
    ref int finishedUnitsLower, ref int finishedUnitsMiddle, ref int finishedUnitsUpper,
    ref List<int> assignmentLower, ref List<int> assignmentMiddle, ref List<int> assignmentUpper)
    {
        //fallback to binary search
        while (numberOfUnits > finishedUnitsUpper)
        {
            upperBound *= 2;
            finishedUnitsUpper = bestPaths.TransitOfPaths(upperBound, out assignmentUpper);
        }

        if (numberOfUnits < finishedUnitsLower)
        {
            lowerBound = 0;
            finishedUnitsLower = bestPaths.TransitOfPaths(lowerBound, out assignmentLower);
        }

        while (lowerBound + 1 != upperBound)
        {
            middleBound = (int)((lowerBound + upperBound) / 2);
            finishedUnitsMiddle = bestPaths.TransitOfPaths(middleBound, out assignmentMiddle);
            if (numberOfUnits >= finishedUnitsMiddle)
            {
                assignmentLower = assignmentMiddle;
                finishedUnitsLower = finishedUnitsMiddle;
                lowerBound = middleBound;
            }
            else
            {
                assignmentUpper = assignmentMiddle;
                finishedUnitsUpper = finishedUnitsMiddle;
                upperBound = middleBound;
            }
        }
    }
}
