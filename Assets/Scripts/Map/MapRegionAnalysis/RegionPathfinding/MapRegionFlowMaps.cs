using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Unity.VisualScripting;
using UnityEngine;

public static class MapRegionPathfinding
{
    public static (
        Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances, 
        Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> paths) DistancesAndPathsBetweenGates(RegionalDecomposition decomposition)
    {
        Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances = new Dictionary<RegionGateway, Dictionary<RegionGateway, float>>();
        Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> paths = new Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>>();
        for (int i = 0; i < decomposition.gateways.Count; i++)
        {
            RegionGateway gate = decomposition.gateways[i];
            (Dictionary<RegionGateway, float> distances, Dictionary<RegionGateway, List<Vector2>> paths) gateResult = computeDistancesAndPathsToNearbyGates(gate, ref decomposition, ref distances, ref paths);
            distances[gate] = gateResult.distances;
            paths[gate] = gateResult.paths;
        }

        foreach (RegionGateway gate1 in paths.Keys)
        {
            foreach (RegionGateway gate2 in paths[gate1].Keys)
            {
                Vector2 goalRegionCentre = new Vector2();
                if (gate1.regionA.ID == gate2.regionA.ID) goalRegionCentre = gate2.GetCentralPosition() + gate2.regionBDirection * gate2.GetSize() * 0.5f;
                else if (gate1.regionA.ID == gate2.regionB.ID) goalRegionCentre = gate2.GetCentralPosition() + gate2.regionADirection * gate2.GetSize() * 0.5f;
                else if (gate1.regionB.ID == gate2.regionA.ID) goalRegionCentre = gate2.GetCentralPosition() + gate2.regionBDirection * gate2.GetSize() * 0.5f;
                else if (gate1.regionB.ID == gate2.regionB.ID) goalRegionCentre = gate2.GetCentralPosition() + gate2.regionADirection * gate2.GetSize() * 0.5f;
                paths[gate1][gate2].Add(goalRegionCentre);
            }
        }
        return (distances, paths);
    }

    private static (Dictionary<RegionGateway, float> distances, Dictionary<RegionGateway, List<Vector2>> paths) computeDistancesAndPathsToNearbyGates(
        RegionGateway gate, ref RegionalDecomposition decomposition, 
        ref Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesToGates,
        ref Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> pathsBetweenGates)
    {
        Dictionary<RegionGateway, float> distancesToNearbyGates = new Dictionary<RegionGateway, float>();
        Dictionary<RegionGateway, List<Vector2>> pathsToNearbyGates = new Dictionary<RegionGateway, List<Vector2>>();

        List<RegionGateway> nearbyGates = new List<RegionGateway>();
        nearbyGates.AddRange(gate.regionA.gateways);
        nearbyGates.AddRange(gate.regionB.gateways);
        nearbyGates.RemoveAll((RegionGateway x) => x == gate);

        for (int i = 0; i < nearbyGates.Count; i++)
        {
            RegionGateway nearbyGate = nearbyGates[i];
            if (distancesToGates.ContainsKey(nearbyGate) && distancesToGates[nearbyGate].ContainsKey(gate))
            {
                distancesToNearbyGates[nearbyGate] = distancesToGates[nearbyGate][gate];
                List<Vector2> pathOpposite = pathsBetweenGates[nearbyGate][gate];
                List<Vector2> path = new List<Vector2>();
                for (int j = pathOpposite.Count - 1; j >= 0; j--)
                {
                    path.Add(pathOpposite[j]);
                }
                pathsToNearbyGates[nearbyGate] = path;
            }
            else
            {
                float distance = 0.0f;
                Vector2 currentPosition = gate.gateTilesCoords[gate.gateTilesCoords.Count / 2].GetWorldPosition();
                List<Vector2> pathBetweenTheGates = Pathfinding.ConstructPathAStar(
                    gate.gateTilesCoords[gate.gateTilesCoords.Count / 2].GetWorldPosition(),
                    nearbyGate.gateTilesCoords[nearbyGate.gateTilesCoords.Count / 2].GetWorldPosition(),
                    Pathfinding.StepDistance,
                    0.0f).ToList();
                if (pathBetweenTheGates != null && pathBetweenTheGates.Count > 0)
                {
                    foreach (Vector2 point in pathBetweenTheGates)
                    {
                        distance += Vector2.Distance(point, currentPosition);
                        currentPosition = point;
                    }
                    distancesToNearbyGates[nearbyGate] = distance;
                    pathsToNearbyGates[nearbyGate] = pathBetweenTheGates;
                }
                else
                {
                    distancesToNearbyGates[nearbyGate] = float.MaxValue;
                    pathsToNearbyGates[nearbyGate] = null;
                }
            }
        }
        return (distancesToNearbyGates, pathsToNearbyGates);
    }

    public static Dictionary<RegionGateway, Dictionary<RegionGateway, float>> DistancesBetweenGates(RegionalDecomposition decomposition)
    {
        Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distances = new Dictionary<RegionGateway, Dictionary<RegionGateway, float>>();
        for (int i = 0; i < decomposition.gateways.Count; i++)
        {
            RegionGateway gate = decomposition.gateways[i];
            distances[gate] = computeDistancesToNearbyGates(gate, ref decomposition, ref distances);
        }
        return distances;
    }

    private static Dictionary<RegionGateway, float> computeDistancesToNearbyGates(RegionGateway gate, ref RegionalDecomposition decomposition, ref Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesToGates)
    {
        Dictionary<RegionGateway, float> distancesToNearbyGates = new Dictionary<RegionGateway, float>();
        List<RegionGateway> nearbyGates = new List<RegionGateway>();
        nearbyGates.AddRange(gate.regionA.gateways);
        nearbyGates.AddRange(gate.regionB.gateways);
        nearbyGates.RemoveAll((RegionGateway x) => x == gate);

        for (int i = 0; i < nearbyGates.Count; i++)
        {
            RegionGateway nearbyGate = nearbyGates[i];
            if (distancesToGates.ContainsKey(nearbyGate) && distancesToGates[nearbyGate].ContainsKey(gate))
            {
                distancesToNearbyGates[nearbyGate] = distancesToGates[nearbyGate][gate];
            }
            else
            {
                float distance = 0.0f;
                Vector2 currentPosition = gate.gateTilesCoords[gate.gateTilesCoords.Count / 2].GetWorldPosition();
                Stack<Vector2> pathBetweenTheGates = Pathfinding.ConstructPathAStar(
                    gate.gateTilesCoords[gate.gateTilesCoords.Count / 2].GetWorldPosition(), 
                    nearbyGate.gateTilesCoords[nearbyGate.gateTilesCoords.Count / 2].GetWorldPosition(),
                    Pathfinding.StepDistance,
                    0.0f);
                if (pathBetweenTheGates != null && pathBetweenTheGates.Count > 0)
                {
                    foreach (Vector2 point in pathBetweenTheGates)
                    {
                        distance += Vector2.Distance(point, currentPosition);
                        currentPosition = point;
                    }
                    distancesToNearbyGates[nearbyGate] = distance;
                }
                else
                {
                    distancesToNearbyGates[nearbyGate] = float.MaxValue;
                }
            }
        }
        return distancesToNearbyGates;
    }

    public static List<(Vector2 flowDirection, float distanceToGate)[,]> CreateFlowMaps(RegionalDecomposition decomposition)
    {
        List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps = initializeFlowMaps(decomposition);
        for (int i = 0; i < decomposition.mapRegions.Count; i++)
        {
            MapRegion region = decomposition.mapRegions[i];
            for (int j = 0; j < region.gateways.Count; j++)
            {
                costructDistanceFieldForOneGateAndRegion(region.gateways[j], region, ref flowMaps, ref decomposition);
            }
        }
        constructFlowFields(ref flowMaps, ref decomposition);
        return flowMaps;
    }

    private static List<(Vector2 flowDirection, float distanceToGate)[,]> initializeFlowMaps(RegionalDecomposition decomposition)
    {
        List <(Vector2 flowDirection, float distanceToGate)[,]> flowMaps = new List<(Vector2 flowDirection, float distanceToGate)[,]>();
        int maxGates = 0;
        foreach (MapRegion region in decomposition.mapRegions)
        {
            if (region.gateways.Count > maxGates) maxGates = region.gateways.Count; 
        }

        int mapWidth = decomposition.depthMap.GetLength(0);
        int mapHeight = decomposition.depthMap.GetLength(1);
        for (int i = 0; i < maxGates; i++)
        {
            flowMaps.Add(new (Vector2 flowDirection, float distanceToGate)[mapWidth, mapHeight]);
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    flowMaps[i][x, y].distanceToGate = float.MaxValue;
                }
            }
        }
        return flowMaps;
    }


    private static void costructDistanceFieldForOneGateAndRegion(RegionGateway gate, MapRegion region, ref List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps, ref RegionalDecomposition decomposition)
    {
        int gateIndex = 0;
        for (int i = 0; i < region.gateways.Count; i++)
        {
            if (region.gateways[i] == gate) gateIndex = i;
        }

        PriorityQueue<float, Coord> queue = new PriorityQueue<float, Coord>();
        List<Coord> coordsGate = gate.gateTilesCoords;

        int pivot1Pos = (coordsGate.Count - 1) / 2;
        float cost = 1.0f;
        while (pivot1Pos >= 0)
        {
            cost += 0.5f;
            Coord coord1 = coordsGate[pivot1Pos];
            flowMaps[gateIndex][coord1.X, coord1.Y].distanceToGate = cost;
            queue.Enqueue(cost, coord1);

            Coord coord2 = coordsGate[coordsGate.Count - (pivot1Pos + 1)];
            flowMaps[gateIndex][coord2.X, coord2.Y].distanceToGate = cost;
            queue.Enqueue(cost, coord2);
            pivot1Pos--;
        }

        while (queue.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queue.Dequeue();
            List<NeighborWithDistance> neighbors = getNeighbors(processed.item, region.ID, ref decomposition.regionMap);
            float currentDistance = flowMaps[gateIndex][processed.item.X, processed.item.Y].distanceToGate;
            foreach (NeighborWithDistance neighbor in neighbors)
            {
                float storedNeighborDistance = flowMaps[gateIndex][neighbor.coord.X, neighbor.coord.Y].distanceToGate;
                if (storedNeighborDistance > (currentDistance + neighbor.distance))
                {
                    flowMaps[gateIndex][neighbor.coord.X, neighbor.coord.Y].distanceToGate = currentDistance + neighbor.distance;
                    queue.Enqueue(currentDistance + neighbor.distance, neighbor.coord);
                }
            }
        }
    }

    private static void constructFlowFields(ref List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps, ref RegionalDecomposition decomposition)
    {
        int width = decomposition.depthMap.GetLength(0);
        int height = decomposition.depthMap.GetLength(1);

        for (int i = 0; i < flowMaps.Count; i++) {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!decomposition.obstructionMap[x, y])
                    {
                        flowMaps[i][x, y].flowDirection = new Vector2(0.0f, 0.0f);
                    }
                    else if (!decomposition.IsGate(decomposition.regionMap[x, y]) && i < decomposition.mapRegions[decomposition.regionMap[x, y]].gateways.Count)
                    {
                        Coord coord = new Coord(x, y);
                        RegionGateway gate = decomposition.mapRegions[decomposition.regionMap[x, y]].gateways[i];
                        List<NeighborWithDistance> neighbors = getNeighborsWithGates(coord, decomposition.regionMap[x, y], ref gate, ref decomposition.regionMap);
                        float origDistance = flowMaps[i][x, y].distanceToGate;
                        Vector2 direction = new(0, 0);
                        bool modified = false;
                        foreach (NeighborWithDistance neighbor in neighbors)
                        {
                            float newDistace = origDistance - (origDistance - flowMaps[i][neighbor.coord.X, neighbor.coord.Y].distanceToGate) / neighbor.distance;
                            direction += new Vector2(neighbor.coord.X - x, neighbor.coord.Y - y) * Mathf.Clamp((origDistance - newDistace), -4, 4);
                            modified = true;
                        }
                        if (modified)
                        {
                            flowMaps[i][x, y].flowDirection = direction.normalized;
                        }
                        else
                        {
                            flowMaps[i][x, y].flowDirection = new Vector2(0, 0);
                        }
                    }
                }
            }
        }
    }

    private static List<NeighborWithDistance> getNeighbors(Coord coord, int region, ref int[,] regionMap)
    {
        bool up, down, left, right, upLeft, upRight, downLeft, downRight;
        int width = regionMap.GetLength(0);
        int height = regionMap.GetLength(1);
        up = coord.Y + 1 < height && regionMap[coord.X, coord.Y + 1] == region;
        down = coord.Y - 1 >= 0 && regionMap[coord.X, coord.Y - 1] == region;
        left = coord.X - 1 >= 0 && regionMap[coord.X - 1, coord.Y] == region;
        right = coord.X + 1 < width && regionMap[coord.X + 1, coord.Y] == region;
        upLeft = coord.Y + 1 < height && coord.X - 1 >= 0 && regionMap[coord.X - 1, coord.Y + 1] == region;
        upRight = coord.Y + 1 < height && coord.X + 1 < width && regionMap[coord.X + 1, coord.Y + 1] == region;
        downLeft = coord.Y - 1 >= 0 && coord.X - 1 >= 0 && regionMap[coord.X - 1, coord.Y - 1] == region;
        downRight = coord.Y - 1 >= 0 && coord.X + 1 < width && regionMap[coord.X + 1, coord.Y - 1] == region;

        List<NeighborWithDistance> neighbors = new();
        if (left)
        {
            Coord neighbor = new(coord.X - 1, coord.Y);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (right)
        {
            Coord neighbor = new(coord.X + 1, coord.Y);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (up)
        {
            Coord neighbor = new(coord.X, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (down)
        {
            Coord neighbor = new(coord.X, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }

        // Move diagonally
        // Not only the diagonal, but also squares neighboring it must be free
        if (upLeft && left && up)
        {
            Coord neighbor = new(coord.X - 1, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (upRight && right && up)
        {
            Coord neighbor = new(coord.X + 1, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downLeft && left && down)
        {
            Coord neighbor = new(coord.X - 1, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downRight && right && down)
        {
            Coord neighbor = new(coord.X + 1, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }

        return neighbors;
    }

    private static List<NeighborWithDistance> getNeighborsWithGates(Coord coord, int region, ref RegionGateway gate, ref int[,] regionMap)
    {
        bool up, down, left, right, upLeft, upRight, downLeft, downRight;
        int width = regionMap.GetLength(0);
        int height = regionMap.GetLength(1);
        up = coord.Y + 1 < height && (regionMap[coord.X, coord.Y + 1] == region || regionMap[coord.X, coord.Y + 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X, coord.Y + 1)));
        down = coord.Y - 1 >= 0 && (regionMap[coord.X, coord.Y - 1] == region || regionMap[coord.X, coord.Y - 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X, coord.Y - 1)));
        left = coord.X - 1 >= 0 && (regionMap[coord.X - 1, coord.Y] == region || regionMap[coord.X - 1, coord.Y] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X - 1, coord.Y)));
        right = coord.X + 1 < width && (regionMap[coord.X + 1, coord.Y] == region || regionMap[coord.X + 1, coord.Y] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X + 1, coord.Y)));
        upLeft = coord.Y + 1 < height && coord.X - 1 >= 0 && (regionMap[coord.X - 1, coord.Y + 1] == region || regionMap[coord.X - 1, coord.Y + 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X - 1, coord.Y + 1)));
        upRight = coord.Y + 1 < height && coord.X + 1 < width && (regionMap[coord.X + 1, coord.Y + 1] == region || regionMap[coord.X + 1, coord.Y + 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X + 1, coord.Y + 1)));
        downLeft = coord.Y - 1 >= 0 && coord.X - 1 >= 0 && (regionMap[coord.X - 1, coord.Y - 1] == region || regionMap[coord.X - 1, coord.Y - 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X - 1, coord.Y - 1)));
        downRight = coord.Y - 1 >= 0 && coord.X + 1 < width && (regionMap[coord.X + 1, coord.Y - 1] == region || regionMap[coord.X + 1, coord.Y - 1] >= 1000000000 && gate.gateTilesCoords.Contains(new Coord(coord.X + 1, coord.Y - 1)));

        List<NeighborWithDistance> neighbors = new();
        if (left)
        {
            Coord neighbor = new(coord.X - 1, coord.Y);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (right)
        {
            Coord neighbor = new(coord.X + 1, coord.Y);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (up)
        {
            Coord neighbor = new(coord.X, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (down)
        {
            Coord neighbor = new(coord.X, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }

        // Move diagonally
        // Not only the diagonal, but also squares neighboring it must be free
        if (upLeft && left && up)
        {
            Coord neighbor = new(coord.X - 1, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (upRight && right && up)
        {
            Coord neighbor = new(coord.X + 1, coord.Y + 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downLeft && left && down)
        {
            Coord neighbor = new(coord.X - 1, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downRight && right && down)
        {
            Coord neighbor = new(coord.X + 1, coord.Y - 1);
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }

        return neighbors;
    }
}