using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowNode
{
    [HideInInspector]
    public readonly RegionGateway ChokePoint;
    private float _x;
    private float _y;
    public readonly int Id;
    private static int _nodeId;
    public static void ResetCounter() => _nodeId = 0;

    public FlowNode(Vector2 v)
    {
        Center = Coord.CoordFromPosition(v);
        Id = _nodeId++;
    }

    //the node in the middle of gate
    public FlowNode(RegionGateway choke, Vector2 v) : this(v)
    {
        ChokePoint = choke;
    }

    public Coord Center
    {
        get; set;
    }
}