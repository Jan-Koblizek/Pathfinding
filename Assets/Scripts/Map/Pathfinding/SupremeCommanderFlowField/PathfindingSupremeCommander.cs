using System.Collections;
using System.Collections.Generic;
using UnityEditor.Search;
using UnityEngine;

public static class PathfindingSupremeCommander
{
    public static FlowFieldSupremeCommander CreateSupremeCommanderFlowField(Vector2 goal)
    {
        Coord goalCoord = Coord.CoordFromPosition(goal);
        return CreateSupremeCommanderFlowField(goalCoord);
    }
    public static FlowFieldSupremeCommander CreateSupremeCommanderFlowField(Coord goal)
    {
        Dictionary<GateSupremeCommander, float> gateDictionary = new Dictionary<GateSupremeCommander, float>();
        FlowFieldSupremeCommander result = new FlowFieldSupremeCommander();
        PriorityQueue<float, GateSupremeCommander> priorityQueue = new PriorityQueue<float, GateSupremeCommander>();
        result.goalPosition = goal.GetWorldPosition();
        result.goalRegion = MapRegionsSupremeCommander.Instance.getRegionFromCoord(goal);
        result.goalRegionFlowMap = result.goalRegion.createFlowMap(goal);
        foreach (GateSupremeCommander gate in result.goalRegion.gates)
        {
            float minDistance = float.MaxValue;
            List<Coord> centralGateCoords = gate.GetCentreCoords(result.goalRegion.X, result.goalRegion.Y);
            foreach (Coord c in centralGateCoords)
            {
                float distance = result.goalRegionFlowMap[c.X % MapRegionSupremeCommander.RegionSize, c.Y % MapRegionSupremeCommander.RegionSize].distanceToGate;
                if (distance < minDistance) minDistance = distance;
            }
            priorityQueue.Enqueue(new ItemWithPriority<float, GateSupremeCommander>(minDistance, gate));
            gateDictionary.Add(gate, minDistance);
        }

        while (priorityQueue.Count() != 0)
        {
            ItemWithPriority<float, GateSupremeCommander> processed = priorityQueue.Dequeue();
            List<(float distance, GateSupremeCommander gate)> neighbors = new List<(float distance, GateSupremeCommander gate)>();
            foreach (GateSupremeCommander gate in processed.item.neighboringGates)
            {
                if (processed.item.distancesToNeighboringGates.ContainsKey(gate)) {
                    neighbors.Add((processed.item.distancesToNeighboringGates[gate], gate));
                }
            }
            float currentDistance = gateDictionary[processed.item];
            foreach ((float distance, GateSupremeCommander gate) neighbor in neighbors)
            {
                if (gateDictionary.ContainsKey(neighbor.gate))
                {
                    float storedNeighborDistance = gateDictionary[neighbor.gate];
                    if (storedNeighborDistance > (currentDistance + neighbor.distance))
                    {
                        gateDictionary[neighbor.gate] = currentDistance + neighbor.distance;
                        priorityQueue.Enqueue(new ItemWithPriority<float, GateSupremeCommander>(currentDistance + neighbor.distance, neighbor.gate));
                    }
                }
                else
                {
                    gateDictionary[neighbor.gate] = currentDistance + neighbor.distance;
                    priorityQueue.Enqueue(new ItemWithPriority<float, GateSupremeCommander>(currentDistance + neighbor.distance, neighbor.gate));
                }
            }
        }

        result.gateTargetsForRegions = new GateSupremeCommander[MapRegionsSupremeCommander.Instance.regions.GetLength(0),
                                                                MapRegionsSupremeCommander.Instance.regions.GetLength(1)];
        for (int x = 0; x < result.gateTargetsForRegions.GetLength(0); x++)
        {
            for (int y = 0; y < result.gateTargetsForRegions.GetLength(1); y++)
            {
                MapRegionSupremeCommander region = MapRegionsSupremeCommander.Instance.regions[x, y];
                GateSupremeCommander gateTarget = null;
                float minDistance = float.MaxValue;
                foreach (GateSupremeCommander gate in region.gates)
                {
                    if (gateDictionary.ContainsKey(gate))
                    {
                        float distance = gateDictionary[gate];
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            gateTarget = gate;
                        }
                    }
                }
                result.gateTargetsForRegions[x,y] = gateTarget;
            }
        }
        return result;
    }
}
