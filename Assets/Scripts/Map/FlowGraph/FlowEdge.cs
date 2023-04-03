using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;

public class FlowEdge
{
    private static int _globalEdgeIdCounter;

    public readonly int Id;

    public float DrawWidth;
    public readonly float Capacity;//solver capacity //Width of the gate (not divided by unit size)
    public readonly FlowNode Start, End;

    public readonly float EdgeLength; //solver cost -- the length of the edge in grid units
    public float Flow;

    public FlowEdge(FlowNode zoneMiddle, FlowNode gateMiddle, float edgeLength, float gateWidth)
    {
        if (zoneMiddle.Id < gateMiddle.Id)
        {
            Start = zoneMiddle;
            End = gateMiddle;
        }
        else
        {
            End = zoneMiddle;
            Start = gateMiddle;
        }

        EdgeLength = edgeLength;
        DrawWidth = gateWidth;
        Capacity = gateWidth / 2 * SimulationSettings.UnitRadius + 0.4f; // 2* (Unit radius + small space between units) -> the size expresses how m times unit can fit into it - in future use EdgeCapacity method instead this
        Id = _globalEdgeIdCounter++;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowEdge"/> class. With the length that is computed from the arguments.
    /// The real path length may be either the diff distance or more complex "A* coupled with visibility" size
    /// </summary>
    public FlowEdge(FlowNode start, FlowNode end, float gateWidth) : this(
        start,
        end,
        GetDistanceBetweenFlowNodes(start, end),
        gateWidth
    )
    {
    }

    private static float GetDistanceBetweenFlowNodes(FlowNode start, FlowNode end)
    {
        float distance = MinimumDistanceBetweenChokes(start, end);
        return distance;
    }

    public static void ResetCounter()
    {
        _globalEdgeIdCounter = 0;
    }

    public float TimeSpentInTheEdge(float unitSpeed, float unitRadius)
    {
        return EdgeLength / unitSpeed;
    }

    public int EdgeCapacity(float unitRadius)
    {
        if (2 * unitRadius > this.Capacity)
            return 0;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (this.EdgeLength == 0)
        {
            return Int32.MaxValue;
        }

        double howManyUnitsFitIntoThePath = (this.EdgeLength * this.Capacity / Math.Pow(2 * unitRadius, 2));
        return (int)Math.Ceiling(howManyUnitsFitIntoThePath);
    }


    public static float MinimumDistanceBetweenChokes(FlowNode a, FlowNode b)
    {
        List<Coord> start = a.ChokePoint?.gateTilesCoords ?? new List<Coord> { a.Center };
        List<Coord> end = b.ChokePoint?.gateTilesCoords ?? new List<Coord> { b.Center };
        Stack<Vector2> pathStack = Pathfinding.ConstructPathAStar(start, end,  Pathfinding.StepDistance, 0.2f);
        List<Vector2> path = pathStack.ToList();
        if (path == null)
        {
            //no path found
            return int.MaxValue;
        }

        if (path.Count == 1)
        {
            // start == end
            return 0;
        }

        float sum = 0;
        for (int i = 1; i < path.Count; i++)
        {
            sum += Vector2.Distance(path[i - 1], path[i]);
        }

        return sum;
    }
}
