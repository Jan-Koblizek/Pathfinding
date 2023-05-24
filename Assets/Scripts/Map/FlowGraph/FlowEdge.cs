using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.Collections;
using UnityEngine;

public class FlowEdge
{
    private static int _globalEdgeIdCounter;

    public readonly int Id;

    public float DrawWidth;
    public readonly float MaxFlow;//solver max flow - units per second
    public readonly FlowNode Start, End;

    public readonly float EdgeLength; //solver cost -- the length of the edge in grid units
    public float Flow;

    public FlowEdge(FlowNode start, FlowNode end, float edgeLength, float gateWidth)
    {
        if (start.Id < end.Id)
        {
            Start = start;
            End = end;
        }
        else
        {
            End = start;
            Start = end;
        }

        EdgeLength = edgeLength;
        DrawWidth = gateWidth;
        MaxFlow = GetMaxFlow(gateWidth); 
        Id = _globalEdgeIdCounter++;
        Flow = 0;
    }

    public static float GetMaxFlow(float gateWidth)
    {
        float horizontalCapacity = gateWidth / (2 * SimulationSettings.UnitRadius + 0.3f);
        float verticalCapacity = 1.0f / (2 * SimulationSettings.UnitRadius + 0.7f);
        return SimulationSettings.instance.UnitSpeed * horizontalCapacity * verticalCapacity;
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
        float distance = DistanceBetweenChokeCenters(start, end);
        return distance;
    }

    public static void ResetCounter()
    {
        _globalEdgeIdCounter = 0;
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

    public static float DistanceBetweenChokeCenters(FlowNode a, FlowNode b)
    {
        List<Coord> start = new List<Coord> { a.Center };
        List<Coord> end = new List<Coord> { b.Center };
        Stack<Vector2> pathStack = Pathfinding.ConstructPathAStar(start, end, Pathfinding.StepDistance, 0.2f);
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
