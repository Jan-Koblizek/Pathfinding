using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public static class RegionalPathfinding
{
    public static (Dictionary<int, int> regionDirections, Dictionary<int, int> gatewayDirections, List<RegionGateway> gatewayPath, List<Vector2> finalPath) ConstructRegionalPath(RegionalPathfindingAnalysis regionalPathfindingAnalysis, Vector2 start, Vector2 goal, System.Func<RegionGateway, Coord, RegionalPathfindingAnalysis, float> heuristic)
    {
        Coord startCoord = Coord.CoordFromPosition(start);
        Coord goalCoord = Coord.CoordFromPosition(goal);
        MapRegion startRegion = regionalPathfindingAnalysis.decomposition.mapRegions[regionalPathfindingAnalysis.decomposition.regionMap[startCoord.X, startCoord.Y]];
        MapRegion goalRegion = regionalPathfindingAnalysis.decomposition.mapRegions[regionalPathfindingAnalysis.decomposition.regionMap[goalCoord.X, goalCoord.Y]];
        Dictionary<RegionGateway, float> gatewayDictionary = new Dictionary<RegionGateway, float>();
        Dictionary<RegionGateway, RegionGateway> predecessors = new Dictionary<RegionGateway, RegionGateway>();
        Stack<RegionGateway> gatewayPath = new Stack<RegionGateway>();
        PriorityQueue<float, RegionGateway> priorityQueue = new PriorityQueue<float, RegionGateway>();
        RegionGateway finalGateway = null;

        if (startRegion == goalRegion)
        {
            List<Vector2> path = Pathfinding.ConstructPathAStar(
            new List<Coord>() { Coord.CoordFromPosition(start) }, 
            new List<Coord>() { Coord.CoordFromPosition(goal)}, Pathfinding.StepDistance, 0.2f, 
            ref Simulator.Instance.decomposition.regionMap, 
            new HashSet<int> { finalGateway.ID + RegionalDecomposition.GatewayIndexOffset, goalRegion.ID}
            ).ToList();
            return (new Dictionary<int, int>(), new Dictionary<int, int>(), new List<RegionGateway>(), path);
        }

        for (int i = 0; i < startRegion.gateways.Count; i++)
        {
            RegionGateway gateway = startRegion.gateways[i];
            float distance = regionalPathfindingAnalysis.flowMaps[i][startCoord.X, startCoord.Y].distanceToGate;
            priorityQueue.Enqueue(new ItemWithPriority<float, RegionGateway>(distance + heuristic(gateway, goalCoord, regionalPathfindingAnalysis), gateway));
            gatewayDictionary.Add(gateway, distance);
            predecessors[gateway] = null;
        }

        while (priorityQueue.Count() != 0)
        {
            ItemWithPriority<float, RegionGateway> processed = priorityQueue.Dequeue();
            List<(float distance, GateSupremeCommander gate)> neighbors = new List<(float distance, GateSupremeCommander gate)>();
            float currentDistance = gatewayDictionary[processed.item];

            if (processed.item.regionA == goalRegion || processed.item.regionB == goalRegion)
            {
                finalGateway = processed.item;
                break;
            }

            foreach ((RegionGateway gateway, float distance) in regionalPathfindingAnalysis.distancesBetweenGates[processed.item])
            {
                float neighborHeuristic = heuristic(gateway, goalCoord, regionalPathfindingAnalysis);
                if (gatewayDictionary.ContainsKey(gateway))
                {
                    float storedNeighborDistance = gatewayDictionary[gateway];
                    if (storedNeighborDistance > (currentDistance + distance))
                    {
                        gatewayDictionary[gateway] = currentDistance + distance;
                        predecessors[gateway] = processed.item;
                        priorityQueue.Enqueue(new ItemWithPriority<float, RegionGateway>(currentDistance + distance + neighborHeuristic, gateway));
                    }
                }
                else
                {
                    gatewayDictionary[gateway] = currentDistance + distance;
                    predecessors[gateway] = processed.item;
                    priorityQueue.Enqueue(new ItemWithPriority<float, RegionGateway>(currentDistance + distance + neighborHeuristic, gateway));
                }
            }
        }

        if (finalGateway != null)
        {
            RegionGateway gateway = finalGateway;
            gatewayPath.Push(gateway);
            while (predecessors[gateway] != null)
            {
                gateway = predecessors[gateway];
                gatewayPath.Push(gateway);
            }
        }
        else
        {
            return (null, null, null, null);
        }

        Coord finalGatewayCentralCoord = finalGateway.gateTilesCoords[finalGateway.gateTilesCoords.Count / 2];
        List<RegionGateway> gatewayList = gatewayPath.ToList();
        List<Vector2> finalPath = Pathfinding.ConstructPathAStar(
            new List<Coord>() { Coord.CoordFromPosition(finalGatewayCentralCoord.GetWorldPosition()) }, 
            new List<Coord>() { Coord.CoordFromPosition(goal)}, Pathfinding.StepDistance, 0.2f, 
            ref Simulator.Instance.decomposition.regionMap, 
            new HashSet<int> { finalGateway.ID + RegionalDecomposition.GatewayIndexOffset, goalRegion.ID}
            ).ToList();

        List<MapRegion> regionPath = new List<MapRegion>();
        regionPath.Add(startRegion);
        for (int i = 0; i < gatewayPath.Count; i++)
        {
            MapRegion region;
            if (gatewayList[i].regionA == regionPath[i]) region = gatewayList[i].regionB;
            else region = gatewayList[i].regionA;
            regionPath.Add(region);
        }

        Dictionary<int, int> regionDirectionDictionary = new Dictionary<int, int>();
        Dictionary<int, int> gatewayDirectionDictionary = new Dictionary<int, int>();

        for (int i = 0; i < gatewayList.Count; i++)
        {
            int targetGatewayIndex = regionPath[i].gateways.IndexOf(gatewayList[i]);
            int gatewayIndex = gatewayList[i].regionA != regionPath[i] ? 0 : 1;
            regionDirectionDictionary[regionPath[i].ID] = targetGatewayIndex;
            gatewayDirectionDictionary[gatewayList[i].ID] = gatewayIndex;
        }
        return (regionDirectionDictionary, gatewayDirectionDictionary, gatewayList, finalPath);
    }

    public static float ComplexDistanceBetween(RegionGateway gateway, Coord goal, RegionalPathfindingAnalysis pathfindingAnalysis)
    {
        Vector2 goalPosition = goal.GetWorldPosition();
        Vector2 gatePosition = gateway.gateTilesCoords[gateway.gateTilesCoords.Count / 2].GetWorldPosition();
        return Vector2.Distance(goalPosition, gatePosition);
    }

    public static float SimpleRegionalHeuristic(RegionGateway gateway, Coord goal, RegionalPathfindingAnalysis pathfindingAnalysis)
    {
        Vector2 goalPosition = goal.GetWorldPosition();
        Vector2 gatePosition = gateway.gateTilesCoords[gateway.gateTilesCoords.Count / 2].GetWorldPosition();
        return Vector2.Distance(goalPosition, gatePosition);
    }
}