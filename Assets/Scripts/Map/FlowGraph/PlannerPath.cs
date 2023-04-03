using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

internal struct PlannerPath : IEquatable<PlannerPath>, IPath
{
    internal struct RawPath
    {
        private static int _id;
        public static readonly Dictionary<int, RawPath> RawDictionary = new Dictionary<int, RawPath>();
        public static readonly Dictionary<PathID, int> PathIDToRaw = new Dictionary<PathID, int>();

        public int ID;
        public readonly List<NodeID> Raw;

        public RawPath(List<NodeID> rawPath)
        {
            Raw = rawPath;
            ID = _id++;
            RawDictionary[ID] = this;
        }

        internal static void Clear()
        {
            _id = 0;
            RawDictionary.Clear();
            PathIDToRaw.Clear();
        }
    }

    public static readonly PlannerPath Empty = new PlannerPath(empty: true);

    private static int _idToAssign;
    private static readonly Dictionary<int, List<PlannerPath>> CountToPaths = new Dictionary<int, List<PlannerPath>>();
    private static readonly Dictionary<PathID, PlannerPath> IdToPath = new Dictionary<PathID, PlannerPath>();

    public readonly PathID PathID;
    private readonly int _rawPathId;
    public List<NodeID> Path => RawPath.RawDictionary[_rawPathId].Raw;

    public readonly float Flow;
    public readonly float Cost;

    float IPath.Cost => Cost;
    float IPath.Flow => Flow;

    public NodeID this[int i] => ((IReadOnlyList<NodeID>)Path)[i];


    /// <summary>
    /// Constructor for cloning the path with new flow (and id)
    /// </summary>
    /// <param name="flow">new flow</param>
    /// <param name="original">path to clone from</param>
    private PlannerPath(float flow, PlannerPath original) :
        this(flow, original.Cost, RawPath.RawDictionary[original._rawPathId].Raw, original._rawPathId)
    {
    }

    /// <summary>
    /// Empty constructor
    /// </summary>
    /// <param name="empty"></param>
    private PlannerPath(bool empty) //bool is ignored but has to be there
    {
        //initialize the empty values only
        PathID = new PathID(-1);
        _rawPathId = -1;
        Flow = -1;
        Cost = -1;

    }

    /// <summary>
    /// Standard path constructor combined with clone
    /// </summary>      
    private PlannerPath(float flow, float cost, List<NodeID> path, int rawId = -1)
    {
        Flow = flow;
        Cost = cost;

        PathID = new PathID(_idToAssign);
        ++_idToAssign;

        //when cloning load the id directly, else compare one by one
        _rawPathId =
            rawId != -1 ?
                rawId :
                FindRawID(path);

        RawPath.PathIDToRaw[PathID] = _rawPathId;
        CountToPaths[path.Count].Add(this);
        IdToPath[PathID] = this;
    }

    /// <summary>
    /// Copies the path changes the flow value
    /// </summary>
    /// <param name="path">the path</param>
    /// <param name="flow">new flow value</param>
    /// <param name="orderedPath">found or created path</param>
    /// <returns>true if a new flow was created; false if flow already existed</returns>
    public static bool ClonePath(PathID path, float flow, out PlannerPath orderedPath)
    {
        var plannerPath = IdToPath[path];
        var id = FindPathId(plannerPath.Path, flow);
        if (id.pathID == -1)
        {
            orderedPath = new PlannerPath(flow, plannerPath);
            return true;
        }
        orderedPath = IdToPath[id];
        return false;
    }



    /// <summary>
    /// Creates ordered partial flow path from list of node IDs
    /// </summary>
    /// <param name="flowGraph">FlowGraph</param>
    /// <param name="shortestPath">a* list of ints</param>
    /// <param name="flow"></param>
    /// <param name="orderedPath">if success returns the new flow</param>
    /// <returns>true if flow was created false if flow already existed, null if the path.</returns>
    public static bool CreatePath(FlowGraph flowGraph, IList<int> shortestPath, float flow, out PlannerPath orderedPath)
    {
        var path = ConvertToNodes(shortestPath);
        return CreatePath(flowGraph, path, flow, out orderedPath);
    }

    public static bool CreatePath(FlowGraph flowGraph, List<NodeID> path, float flow, out PlannerPath orderedPath)
    {
        orderedPath = Empty;
        if (path == null || path.Count == 0)
        {
            throw new ArgumentException("Invalid size of input");
        }

        var id = FindPathId(path, flow);
        if (id.pathID == -1)
        {
            var cost = ComputeCost(flowGraph, path);
            orderedPath = new PlannerPath(flow, cost, path);
            return true;
        }
        orderedPath = IdToPath[id];
        return false;
    }


    /// <summary>
    /// Finds the path with given id and returns it or in case the id is invalid returns empty object.
    /// </summary>
    /// <param name="id">ID of path to return</param>
    /// <returns>Path or empty path</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PlannerPath GetPathById(PathID id)
    {
        if (IdToPath.TryGetValue(id, out var path))
        {
            return path;
        }

        throw new KeyNotFoundException($"PathID:{id} was not found");
    }

    /// <summary>
    /// Finds the path with given id and returns it or in case the id is invalid returns empty object.
    /// </summary>
    /// <param name="id">ID of path to return</param>
    /// <returns>Path or empty path</returns>
    internal static PlannerPath GetPathById(int id)
    {
        var pathID = new PathID(id);
        return GetPathById(pathID);
    }

    /// <summary>
    /// Compares the existing paths with a list of nodes of same count, to see if this list has an Id already assigned
    /// </summary>
    /// <param name="path">Path consisting of nodes ids</param>
    /// <param name="reservedFlow">Reserved flow of the path</param>
    /// <returns>If id exists returns the id, other-ways returns -1</returns>
    private static PathID FindPathId(List<NodeID> path, float reservedFlow)
    {
        if (CountToPaths.TryGetValue(path.Count, out var paths))
        {
            foreach (var p in paths)
            {
                // p.Flow != flow
                if (Math.Abs(p.Flow - reservedFlow) > 0.01f)
                {
                    continue;
                }
                bool difference = false;
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i].nodeID != p[i].nodeID)
                    {
                        difference = true;
                        break;
                    }
                }
                if (!difference)
                {
                    return p.PathID;
                }
            }
        }
        else
        {
            CountToPaths.Add(path.Count, new List<PlannerPath>(1));
        }

        return new PathID(-1);
    }

    private static int FindRawID(List<NodeID> path)
    {
        if (CountToPaths.TryGetValue(path.Count, out var paths))
        {
            foreach (var p in paths)
            {
                bool difference = false;
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i].nodeID != p[i].nodeID)
                    {
                        difference = true;
                        break;
                    }
                }
                if (!difference)
                {
                    return p._rawPathId;
                }
            }
        }

        var newRaw = new RawPath(path);
        return newRaw.ID;
    }

    private static float ComputeCost(FlowGraph flowGraph, IList<NodeID> path)
    {
        float sum = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            var edge = flowGraph.GetEdgeByNodeIds(path[i].nodeID, path[i + 1].nodeID);
            sum += edge.EdgeLength;
        }
        return sum;
    }


    /// <summary>
    /// Converts PathID to a list of gates centers.
    /// </summary>
    /// <param name="flowGraph">FlowGraph containing the gates</param>
    /// <param name="pathID">path in graph</param>
    /// <returns></returns>
    internal static List<Vector2> GetPathsFlowNodeCenters(FlowGraph flowGraph, PathID pathID)
    {
        List<Vector2> pathCenters = new List<Vector2>();

        var path = GetPathById(pathID).Path;
        foreach (var nodeID in path)
        {
            FlowNode flowNode = flowGraph.GetFlowNodeByID(nodeID.nodeID);
            Vector2 center = flowNode.Center.GetWorldPosition();
            pathCenters.Add(center);
        }

        return pathCenters;
    }


    /// <summary>
    /// Converts the a* ints to nodeIds
    /// </summary>
    /// <param name="path">list of ints</param>
    /// <returns>new array</returns>
    private static List<NodeID> ConvertToNodes(IList<int> path)
    {
        var list = new List<NodeID>(path.Count);
        foreach (int v in path)
        {
            list.Add(new NodeID(v));
        }

        return list;
    }

    /// <summary>
    /// Clear to be called instead of initialization
    /// </summary>
    internal static void Clear()
    {
        CountToPaths.Clear();
        IdToPath.Clear();
        RawPath.Clear();
        _idToAssign = 0;
    }


    public bool Equals(PlannerPath other)
    {
        return PathID.Equals(other.PathID);
    }
}