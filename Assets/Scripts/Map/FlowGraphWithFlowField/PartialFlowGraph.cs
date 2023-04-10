using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.GraphicsBuffer;

public class PartialFlowGraph
{
    public List<FlowEdge> Edges;
    public List<FlowNode> Nodes;
    public Dictionary<int, HashSet<FlowNode>> zoneToNodes;

    public Dictionary<FlowNode, HashSet<FlowEdge>> NodeToEdges; //graph representation 

    public FlowNode Source;
    public FlowNode Terminal;

    public readonly Dictionary<int, FlowNode> _idToNodes;
    public readonly Dictionary<int, FlowEdge> _idToEdges;
    public readonly Dictionary<(int, int), (int, float)> _nodesIdsToEdgeIdCost;

    private PartialFlowGraph(
        Dictionary<int, HashSet<FlowNode>> zonesToNodes,
        Dictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
        List<FlowEdge> edges,
        List<FlowNode> gate,
        List<FlowNode> zone)
    {
        Edges = edges;
        Nodes = zone;
        this.zoneToNodes = zonesToNodes;

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

    internal static PartialFlowGraph PartialFlowGraphFromDecomposition(
        RegionalDecomposition decomposition,
        Dictionary<RegionGateway, Dictionary<RegionGateway, float>> gateDistances = null)
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

        FlowEdge.ResetCounter();
        FlowNode.ResetCounter();
        Dictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges = new Dictionary<FlowNode, HashSet<FlowEdge>>();
        Dictionary<int, HashSet<FlowNode>> zoneToNodes = new Dictionary<int, HashSet<FlowNode>>();

        CreateEdgeWithNodesAroundGates(zoneToNodes, nodeToEdges, ref decomposition);
        ConnectNodesInZoneWithEdges(zonesToChokes, zoneToNodes, nodeToEdges, ref gateDistances);


        // now just save the values for the drawableGraph
        var allEdges = from hashEdges in nodeToEdges.Values
                       from edges in hashEdges
                       select edges;

        var filteredEdges = (
                from edge in allEdges.Distinct()
                select edge)
            .ToList();

        var zones = nodeToEdges.Keys.ToList();
        return new PartialFlowGraph(zoneToNodes, nodeToEdges, filteredEdges, new List<FlowNode>(), zones);
    }

    private static void ConnectNodesInZoneWithEdges(
    IDictionary<int, List<int>> zoneChoke,
    IDictionary<int, HashSet<FlowNode>> zoneToNodes,
    IDictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
    ref Dictionary<RegionGateway, Dictionary<RegionGateway, float>> gateDistances)
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
                    float aSize = a.ChokePoint.GetSize();
                    float bSize = b.ChokePoint.GetSize();
                    float size = Mathf.Min(aSize, bSize);

                    FlowEdge edge;
                    if (gateDistances != null)
                    {
                        edge = new FlowEdge(a, b, gateDistances[a.ChokePoint][b.ChokePoint], size);
                    }
                    else
                    {
                        edge = new FlowEdge(a, b, size);
                    }
                    Utilities.AddToHashsetDictionary(nodeToEdges, a, edge);
                    Utilities.AddToHashsetDictionary(nodeToEdges, b, edge);
                }
            }
        }
    }

    private static void CreateEdgeWithNodesAroundGates(
        IDictionary<int, HashSet<FlowNode>> zoneToNodes,
        IDictionary<FlowNode, HashSet<FlowEdge>> nodeToEdges,
        ref RegionalDecomposition decomposition)
    {
        foreach (RegionGateway choke in decomposition.gateways)
        {
            // create a, b - point in different zones around the chokepoint

            Vector2 center = choke.GetCentralPosition();
            int aZone = choke.regionA.ID;
            int bZone = choke.regionB.ID;

            FlowNode a = new FlowNode(choke, center, aZone);
            FlowNode b = new FlowNode(choke, center, bZone);

            a.Center = Coord.CoordFromPosition(center);
            b.Center = Coord.CoordFromPosition(center);

            //both have zone number assigned
            Utilities.AddToHashsetDictionary(zoneToNodes, aZone, a);
            Utilities.AddToHashsetDictionary(zoneToNodes, bZone, b);

            float gateWidth = choke.GetSize();

            FlowEdge edge = new FlowEdge(a, b, 0, gateWidth);
            Utilities.AddToHashsetDictionary(nodeToEdges, a, edge);
            Utilities.AddToHashsetDictionary(nodeToEdges, b, edge);
        }
    }
}
