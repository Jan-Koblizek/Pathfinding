using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlowNode
{
    [HideInInspector]
    public readonly RegionGateway ChokePoint;
    public readonly int Id;
    public readonly int regionId;
    private static int _nodeId;
    public static void ResetCounter() => _nodeId = 0;

    public FlowNode(Vector2 v, int regionId)
    {
        Center = Coord.CoordFromPosition(v);
        Id = _nodeId++;
        ChokePoint = null;
        this.regionId = regionId;
    }

    //the node in the middle of gate
    public FlowNode(RegionGateway choke, Vector2 v, int regionId)
    {
        Center = Coord.CoordFromPosition(v);
        Id = _nodeId++;
        ChokePoint = choke;
        this.regionId = regionId;
    }

    public Coord Center
    {
        get; set;
    }
}