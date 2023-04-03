using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct PathID : IComparable<PathID>
{
    public readonly int pathID;

    public PathID(int pathID)
    {
        this.pathID = pathID;
    }

    public int CompareTo(PathID other)
    {
        return pathID.CompareTo(other.pathID);
    }
    public override string ToString()
    {
        return pathID.ToString();
    }
}


public struct NodeID
{
    public readonly int nodeID;

    public NodeID(int nodeID)
    {
        this.nodeID = nodeID;
    }

    public override string ToString()
    {
        return nodeID.ToString();
    }
}

public struct EdgeID
{
    public readonly int ID;

    public EdgeID(int id)
    {
        ID = id;
    }
}