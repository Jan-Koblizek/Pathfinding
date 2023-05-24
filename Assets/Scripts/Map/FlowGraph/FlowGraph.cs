using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using utils;

public class FlowGraph
{
    public List<FlowEdge> Edges;
    public List<FlowNode> GateNodes;
    public List<FlowNode> ZoneNodes;

    public Dictionary<FlowNode, HashSet<FlowEdge>> NodeToEdges; //graph representation 

    public FlowNode Source;
    public FlowNode Terminal;

    private readonly Dictionary<int, FlowNode> _idToNodes;
    private readonly Dictionary<int, FlowEdge> _idToEdges;
    private readonly Dictionary<(int, int), (int, float)> _nodesIdsToEdgeIdCost;

    public float DistanceToGoal(int from, IList<int> goal)
    {
        if (goal.Count != 1)
            throw new ArgumentException("In this representation only one goal is allowed");
        if (goal[0] != Terminal.Id)
        {
            throw new ArgumentException("goal has different id");
        }

        if (_idToNodes.Count == 0)
        {
            return 0;
        }
        FlowNode node = _idToNodes[from];

        return Vector2.Distance(node.Center.GetWorldPosition(), Terminal.Center.GetWorldPosition());
    }

    private float Intify(float cost)
    {
        cost = (float)Mathf.Ceil(cost);
        if (cost <= 0)
        {
            cost = 1;
        }

        return cost;
    }

    public float DistanceBetweenNeighbors(int a, int b)
    {
        //self loop
        if (a == b)
        {
            return 100000;//don't allow those edges
        }

        var (edgeId, cost) = _nodesIdsToEdgeIdCost[(a, b)];
        var edge = GetFlowEdgeByID(edgeId);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        //no flow
        if (edge.Flow == 0)
        {
            return cost;
        }

        if ((edge.Start.Id == a) && (edge.End.Id == b))
        {
            if (edge.Flow > 0)
            {
                return Intify(cost);
            }

            //going against already used edge - so flow removal
            return Intify(-cost);
        }
        else if ((edge.Start.Id == b) && (edge.End.Id == a))
        {
            if (edge.Flow < 0)

            {
                return Intify(cost);
            }
            return Intify(-cost);

        }
        return Intify(cost);
    }

    public IEnumerable<int> GetNeighbors(int t)
    {
        foreach (var edge in NodeToEdges[_idToNodes[t]])
        {
            //select good end of the edge
            if (edge.Start.Id == t)
            {
                if (Mathf.Abs(edge.Flow - edge.MaxFlow) > 0.01f) yield return edge.End.Id;
            }
            else
            {
                if (Mathf.Abs(edge.Flow + edge.MaxFlow) > 0.01f) yield return edge.Start.Id;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FlowGraph(PartialFlowGraph partialFlowGraph, Vector2 source, Vector2 goal, int[,] regionalMap, RegionalPathfindingAnalysis analysis)
    {
        Coord sourceCoord = Coord.CoordFromPosition(source);
        int sourceZone = regionalMap[sourceCoord.X, sourceCoord.Y];
        MapRegion sourceRegion = analysis.decomposition.mapRegions[sourceZone];
        Coord goalCoord = Coord.CoordFromPosition(goal);
        int goalZone = regionalMap[goalCoord.X, goalCoord.Y];
        MapRegion goalRegion = analysis.decomposition.mapRegions[goalZone];
        Source = new FlowNode(source, sourceZone);
        Terminal = new FlowNode(goal, goalZone);

        Edges = partialFlowGraph.Edges;
        ZoneNodes = new List<FlowNode>();
        GateNodes = new List<FlowNode>();
        ZoneNodes.AddRange(partialFlowGraph.Nodes);
        NodeToEdges = partialFlowGraph.NodeToEdges;
        ZoneNodes.Add(Source);
        ZoneNodes.Add(Terminal);
        _idToNodes = partialFlowGraph._idToNodes;
        _idToNodes.Add(Source.Id, Source);
        _idToNodes.Add(Terminal.Id, Terminal);
        _idToEdges = partialFlowGraph._idToEdges;
        _nodesIdsToEdgeIdCost = partialFlowGraph._nodesIdsToEdgeIdCost;
        NodeToEdges[Source] = new HashSet<FlowEdge>();
        NodeToEdges[Terminal] = new HashSet<FlowEdge>();

        if (goalZone == sourceZone)
        {
            FlowEdge directEdge = new FlowEdge(Source, Terminal, Vector2.Distance(goal, source), 1000000);
            Edges.Add(directEdge);
            _idToEdges.Add(directEdge.Id, directEdge);
            (int, int) key = (Source.Id, Terminal.Id);
            (int, int) opposite = (Terminal.Id, Source.Id);
            (int, float) value = (directEdge.Id, directEdge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Terminal].Add(directEdge);
            NodeToEdges[Source].Add(directEdge);
        }

        if (!partialFlowGraph.zoneToNodes.ContainsKey(sourceZone)) partialFlowGraph.zoneToNodes[sourceZone] = new HashSet<FlowNode> { };
        if (!partialFlowGraph.zoneToNodes.ContainsKey(goalZone)) partialFlowGraph.zoneToNodes[goalZone] = new HashSet<FlowNode> { };
        HashSet<FlowNode> SourceZoneNodes = partialFlowGraph.zoneToNodes[sourceZone];
        HashSet<FlowNode> TerminalZoneNodes = partialFlowGraph.zoneToNodes[goalZone];
        foreach (FlowEdge edge in Edges) edge.Flow = 0;

        foreach (FlowNode node in TerminalZoneNodes)
        {
            //Specify length to speed up initialization
            float edgeLength = analysis.flowMaps[goalRegion.gateways.IndexOf(node.ChokePoint)][goalCoord.X, goalCoord.Y].distanceToGate;
            FlowEdge edge = new FlowEdge(node, Terminal, edgeLength, float.MaxValue);
            Edges.Add(edge);
            _idToEdges.Add(edge.Id, edge);
            (int, int) key = (Terminal.Id, node.Id);
            (int, int) opposite = (node.Id, Terminal.Id);
            (int, float) value = (edge.Id, edge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Terminal].Add(edge);
            NodeToEdges[node].Add(edge);
        }

        foreach (FlowNode node in SourceZoneNodes)
        {
            //Specify length to speed up initialization
            float edgeLength = analysis.flowMaps[sourceRegion.gateways.IndexOf(node.ChokePoint)][sourceCoord.X, sourceCoord.Y].distanceToGate;
            FlowEdge edge = new FlowEdge(Source, node, edgeLength, float.MaxValue);
            Edges.Add(edge);
            _idToEdges.Add(edge.Id, edge);
            (int, int) key = (Source.Id, node.Id);
            (int, int) opposite = (node.Id, Source.Id);
            (int, float) value = (edge.Id, edge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Source].Add(edge);
            NodeToEdges[node].Add(edge);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FlowGraph(PartialFlowGraph partialFlowGraph, Vector2 source, Vector2 goal, int[,] regionalMap)
    {
        Coord sourceCoord = Coord.CoordFromPosition(source);
        int sourceZone = regionalMap[sourceCoord.X, sourceCoord.Y];
        Coord goalCoord = Coord.CoordFromPosition(goal);
        int goalZone = regionalMap[goalCoord.X, goalCoord.Y];
        Source = new FlowNode(source, sourceZone);
        Terminal = new FlowNode(goal, goalZone);

        Edges = partialFlowGraph.Edges;
        ZoneNodes = new List<FlowNode>();
        GateNodes = new List<FlowNode>();
        ZoneNodes.AddRange(partialFlowGraph.Nodes);
        NodeToEdges = partialFlowGraph.NodeToEdges;
        ZoneNodes.Add(Source);
        ZoneNodes.Add(Terminal);
        _idToNodes = partialFlowGraph._idToNodes;
        _idToNodes.Add(Source.Id, Source);
        _idToNodes.Add(Terminal.Id, Terminal);
        _idToEdges = partialFlowGraph._idToEdges;
        _nodesIdsToEdgeIdCost = partialFlowGraph._nodesIdsToEdgeIdCost;

        NodeToEdges[Source] = new HashSet<FlowEdge>();
        NodeToEdges[Terminal] = new HashSet<FlowEdge>();

        if (!partialFlowGraph.zoneToNodes.ContainsKey(sourceZone)) partialFlowGraph.zoneToNodes[sourceZone] = new HashSet<FlowNode> { };
        if (!partialFlowGraph.zoneToNodes.ContainsKey(goalZone)) partialFlowGraph.zoneToNodes[goalZone] = new HashSet<FlowNode> { };
        HashSet<FlowNode> SourceZoneNodes = partialFlowGraph.zoneToNodes[sourceZone];
        HashSet<FlowNode> TerminalZoneNodes = partialFlowGraph.zoneToNodes[goalZone];

        if (goalZone == sourceZone)
        {
            FlowEdge directEdge = new FlowEdge(Source, Terminal, Vector2.Distance(goal, source), 1000000);
            Edges.Add(directEdge);
            _idToEdges.Add(directEdge.Id, directEdge);
            (int, int) key = (Source.Id, Terminal.Id);
            (int, int) opposite = (Terminal.Id, Source.Id);
            (int, float) value = (directEdge.Id, directEdge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Terminal].Add(directEdge);
            NodeToEdges[Source].Add(directEdge);
        }

        foreach (FlowEdge edge in Edges) edge.Flow = 0;

        foreach (FlowNode node in TerminalZoneNodes)
        {
            //Specify length to speed up initialization
            FlowEdge edge = new FlowEdge(node, Terminal, float.MaxValue);
            Edges.Add(edge);
            _idToEdges.Add(edge.Id, edge);
            (int, int) key = (Terminal.Id, node.Id);
            (int, int) opposite = (node.Id, Terminal.Id);
            (int, float) value = (edge.Id, edge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Terminal].Add(edge);
            NodeToEdges[node].Add(edge);
        }

        foreach (FlowNode node in SourceZoneNodes)
        {
            //Specify length to speed up initialization
            FlowEdge edge = new FlowEdge(Source, node, float.MaxValue);
            Edges.Add(edge);
            _idToEdges.Add(edge.Id, edge);
            (int, int) key = (Source.Id, node.Id);
            (int, int) opposite = (node.Id, Source.Id);
            (int, float) value = (edge.Id, edge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[opposite] = value;
            NodeToEdges[Source].Add(edge);
            NodeToEdges[node].Add(edge);
        }
    }

    private FlowGraph(
    Dictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
    List<FlowEdge> edges,
    List<FlowNode> gate,
    List<FlowNode> zone,
    FlowNode terminal,
    FlowNode source)
    {
        Terminal = terminal;
        Source = source;

        Edges = edges;
        GateNodes = gate;
        ZoneNodes = zone;

        NodeToEdges = nodeToEdges;

        _idToNodes = new Dictionary<int, FlowNode>();
        foreach (FlowNode flowNode in nodeToEdges.Keys)
        {
            _idToNodes.Add(flowNode.Id, flowNode);
        }

        _idToEdges = new Dictionary<int, FlowEdge>();
        foreach (FlowEdge edge in Edges)
        {
            _idToEdges.Add(edge.Id, edge);
        }

        _nodesIdsToEdgeIdCost = new Dictionary<(int, int), (int, float)>(Edges.Count * 2);
        foreach (FlowEdge edge in Edges)
        {
            (int, int) key = (edge.Start.Id, edge.End.Id);
            (int, int) oppositeKey = (edge.End.Id, edge.Start.Id);
            (int, float) value = (edge.Id, edge.EdgeLength);
            _nodesIdsToEdgeIdCost[key] = value;
            _nodesIdsToEdgeIdCost[oppositeKey] = value;
        }

        if (edges == null)
        {
            throw new System.ArgumentNullException(nameof(edges));
        }

        if (gate == null)
        {
            throw new System.ArgumentNullException(nameof(gate));
        }

        if (zone == null)
        {
            throw new System.ArgumentNullException(nameof(zone));
        }

        if (nodeToEdges == null)
        {
            throw new System.ArgumentNullException(nameof(nodeToEdges));
        }
    }

    internal FlowNode GetFlowNodeByID(int id)
    {
        return _idToNodes[id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FlowEdge GetFlowEdgeByID(int id)
    {
        return _idToEdges[id];
    }
    internal FlowEdge GetEdgeByNodeIds(int nodeAtStart, int nodeAtEnd)
    {
        var (edgeId, cost) = _nodesIdsToEdgeIdCost[(nodeAtStart, nodeAtEnd)];
        return GetFlowEdgeByID(edgeId);
    }


    /// <summary>
    /// Creates a new flow graph. This FlowGraph has nodes on both sides of each gate, the nodes within one zone are fully connected between each other
    /// </summary>
    /// <param name="chokeIdZone"></param>
    /// <param name="zoneChoke"></param>
    /// <param name="chokes"></param>
    /// <param name="group"></param>
    /// <param name="target"></param>
    /// <returns>The new flow graph</returns>
    internal static FlowGraph MakeZonelessFlowGraph(
        RegionalDecomposition decomposition, Vector2? group = null, Vector2? target = null)
    {
        Dictionary<int, List<int>> zonesToChokes = new Dictionary<int, List<int>>();

        for (var i = 0; i < decomposition.mapRegions.Count; i++)
        {
            var zoneChokes = decomposition.mapRegions[i].gateways;
            zonesToChokes[i] = new List<int>();
            foreach (var choke in zoneChokes)
            {
                zonesToChokes[i].Add(choke.ID);
            }
        }

        Dictionary<int, RegionGateway> chokes = new Dictionary<int, RegionGateway>();
        foreach (RegionGateway choke in decomposition.gateways)
        {
            chokes[choke.ID] = choke;
        }

        FlowEdge.ResetCounter();
        FlowNode.ResetCounter();
        Dictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges = new Dictionary<FlowNode, HashSet<FlowEdge>>();
        Dictionary<int, HashSet<FlowNode>> zoneToNodes = new Dictionary<int, HashSet<FlowNode>>();

        var (source, sink) = ConnectSourceTerminal(group, target, nodeToEdges, zoneToNodes, ref decomposition);

        CreateEdgeWithNodesAroundGates(chokes, zoneToNodes, nodeToEdges, ref decomposition);

        ConnectNodesInZoneWithEdges(zonesToChokes, zoneToNodes, nodeToEdges);


        // now just save the values for the drawableGraph
        var allEdges = from hashEdges in nodeToEdges.Values
                       from edges in hashEdges
                       select edges;

        var filteredEdges = (
                from edge in allEdges.Distinct()
                select edge)
            .ToList();

        var zones = nodeToEdges.Keys.ToList();
        return new FlowGraph(nodeToEdges, filteredEdges, new List<FlowNode>(), zones, sink, source);
    }

    /// <summary>
    /// Last step of connecting the sink and the source in the target zones connect the new flownodes to all other nodes
    /// </summary>
    /// <param name="group"></param>
    /// <param name="target"></param>
    /// <param name="nodeToEdges"></param>
    /// <param name="zoneToNodes"></param>
    /// <returns></returns>
    private static (FlowNode source, FlowNode sink) ConnectSourceTerminal(Vector2? group, Vector2? target, Dictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges, Dictionary<int, HashSet<FlowNode>> zoneToNodes, ref RegionalDecomposition decomposition)
    {
        FlowNode source = new FlowNode(Vector2.zero, -1);
        FlowNode sink = new FlowNode(Vector2.zero, -1);

        if (group != null)
        {
            Vector2 sourcePosition = (Vector2)group;
            Coord sourceCoord = Coord.CoordFromPosition(sourcePosition);
            source = AddSpecialNodeToGraph((Vector2)group, zoneToNodes, nodeToEdges, decomposition.regionMap[sourceCoord.X, sourceCoord.Y]);
        }

        if (target != null)
        {
            Vector2 sinkPosition = (Vector2)target;
            Coord sinkCoord = Coord.CoordFromPosition(sinkPosition);
            sink = AddSpecialNodeToGraph((Vector2)target, zoneToNodes, nodeToEdges, decomposition.regionMap[sinkCoord.X, sinkCoord.Y]);
        }

        return (source, sink);
    }

    private static FlowNode AddSpecialNodeToGraph(Vector2 position,
        IDictionary<int, HashSet<FlowNode>> zoneToNodes,
        IDictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
        int zoneID)
    {
        FlowNode significantNode = new FlowNode(position, zoneID); //sets the size of source, terminal graph circle
        Utilities.AddToHashsetDictionary(zoneToNodes, zoneID, significantNode);

        return significantNode;
    }

    private static void ConnectNodesInZoneWithEdges(
        IDictionary<int, List<int>> zoneChoke,
        IDictionary<int, HashSet<FlowNode>> zoneToNodes,
        IDictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges)
    {
        // for each pair a, b of flownodes in same zone,
        // connect with the width of min of both gates
        // and length of ...

        foreach (var zoneId in zoneChoke.Keys)
        {
            if (zoneToNodes.TryGetValue(zoneId, out var nodes))
            {
                IEnumerable<(FlowNode, FlowNode)> allPairs =
                    from aNode in nodes
                    from bNode in nodes
                    where aNode.Id < bNode.Id
                    select (aNode, bNode);

                foreach (var (a, b) in allPairs)
                {
                    const int maxValue = 8388607;//which is int.MaxValue >> 8;

                    float aSize = (a.ChokePoint?.GetSize()).GetValueOrDefault(maxValue);
                    float bSize = (b.ChokePoint?.GetSize()).GetValueOrDefault(maxValue);
                    float size = Mathf.Min(aSize, bSize);

                    FlowEdge edge = new FlowEdge(a, b, size);
                    Debug.Assert(size < maxValue, $"Chokepoint size for edge: {edge} not limited");


                    Utilities.AddToHashsetDictionary(nodeToEdges, a, edge);
                    Utilities.AddToHashsetDictionary(nodeToEdges, b, edge);
                }
            }
        }
    }

    private static void CreateEdgeWithNodesAroundGates(
        IDictionary<int, RegionGateway> chokes,
        IDictionary<int, HashSet<FlowNode>> zoneToNodes,
        IDictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
        ref RegionalDecomposition decomposition)
    {
        // pseudocode:
        // for each choke point,
        // create 2 FlowNodes a and b,
        // so that they lay in the zones around the choke in small distance
        // connect a, b with Edge

        foreach (RegionGateway choke in chokes.Values)
        {
            // create a, b - point in different zones around the chokepoint

            Vector2 center = choke.GetCentralPosition();
            Vector2 norm = GetPerpendicularVectorToGate(choke);
            FlowNode a = new FlowNode(choke, center + norm, choke.regionA.ID);
            FlowNode b = new FlowNode(choke, center - norm, choke.regionB.ID);

            int aZone = 0, bZone = 0;


            for (float f = 1f; aZone == bZone && f < 10; f += 0.1f) //tdo test if this behaves better with && aZone != -1 && bZone != -1
            {
                aZone = choke.regionA.ID;
                bZone = choke.regionB.ID;
                a.Center = Coord.CoordFromPosition(center + f * norm);
                b.Center = Coord.CoordFromPosition(center - f * norm);
            }

            //both have zone number assigned
            Utilities.AddToHashsetDictionary(zoneToNodes, aZone, a);
            Utilities.AddToHashsetDictionary(zoneToNodes, bZone, b);

            float abDistance = Vector2.Distance(a.Center.GetWorldPosition(), b.Center.GetWorldPosition());
            float gateWidth = choke.GetSize();

            FlowEdge edge = new FlowEdge(a, b, abDistance, gateWidth);
            Utilities.AddToHashsetDictionary(nodeToEdges, a, edge);
            Utilities.AddToHashsetDictionary(nodeToEdges, b, edge);
        }
    }

    private static Vector2 GetPerpendicularVectorToGate(RegionGateway choke)
    {
        Vector2 startOfGate = choke.start.GetWorldPosition();
        Vector2 endOfGate = choke.end.GetWorldPosition();
        Vector2 directionGate = (endOfGate - startOfGate);

        var norm = directionGate.Rotate(90);
        norm.Normalize();

        return norm;
    }
}
