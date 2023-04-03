using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Search;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class MapRegionSupremeCommander
{
    public const int RegionSize = 8;
    public int X;
    public int Y;

    private bool[,] obstructionMap;
    public Dictionary<GateSupremeCommander, (Vector2 flowDirection, float distanceToGate)[,]> flowMaps;
    public List<GateSupremeCommander> gates;

    public MapRegionSupremeCommander(int X, int Y)
    {
        this.X = X;
        this.Y = Y;

        obstructionMap = new bool[RegionSize, RegionSize];
        for (int i = 0; i < RegionSize; i++) 
        {
            for (int j = 0; j < RegionSize; j++)
            {
                obstructionMap[i, j] = Map.instance.tiles[RegionSize * X + i, RegionSize * Y + j].obstructed;
            }
        }

        gates = new List<GateSupremeCommander>();
        flowMaps = new Dictionary<GateSupremeCommander, (Vector2 flowDirection, float distanceToGate)[,]>();
        initializeGates();
        for (int i = 0; i < gates.Count; i++)
        {
            computeDistancesToOtherGates(gates[i]);
            (Vector2 flowDirection, float distanceToGate)[,] flowMap = createFlowMap(gates[i]);
            flowMaps[gates[i]] = flowMap;
        }
    }

    public (Vector2 flowDirection, float distanceToGate)[,] createFlowMap(Coord coord)
    {
        coord.X = X % RegionSize;
        coord.Y = Y % RegionSize;
        PriorityQueue<float, Coord> queue = new PriorityQueue<float, Coord>();
        (Vector2 flowDirection, float distanceToGate)[,] flowMap = new (Vector2 flowDirection, float distanceToGate)[RegionSize, RegionSize];
        for (int i = 0; i < RegionSize; i++)
        {
            for (int j = 0; j < RegionSize; j++)
            {
                flowMap[i, j] = (new Vector2(), float.MaxValue);
            }
        }

        flowMap[coord.X, coord.Y].distanceToGate = 0.0f;
        queue.Enqueue(0.0f, coord);

        while (queue.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queue.Dequeue();
            List<NeighborWithDistance> neighbors = getNeighbors(processed.item);
            float currentDistance = flowMap[processed.item.X, processed.item.Y].distanceToGate;
            foreach (NeighborWithDistance neighbor in neighbors)
            {
                float storedNeighborDistance = flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate;
                if (storedNeighborDistance > (currentDistance + neighbor.distance))
                {
                    flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate = currentDistance + neighbor.distance;
                    queue.Enqueue(currentDistance + neighbor.distance, neighbor.coord);
                }
            }
        }

        for (int x = 0; x < RegionSize; x++)
        {
            for (int y = 0; y < RegionSize; y++)
            {
                if (obstructionMap[x, y])
                {
                    flowMap[x, y].flowDirection = new Vector2(0.0f, 0.0f);
                }
                else
                {
                    List<NeighborWithDistance> neighbors = getNeighbors(new Coord(x, y));
                    float origDistance = flowMap[x, y].distanceToGate;
                    Vector2 direction = new(0, 0);
                    bool modified = false;
                    foreach (NeighborWithDistance neighbor in neighbors)
                    {
                        float newDistace = origDistance - (origDistance - flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate) / neighbor.distance;
                        direction += new Vector2(neighbor.coord.X - x, neighbor.coord.Y - y) * Mathf.Clamp((origDistance - newDistace), -4, 4);
                        modified = true;
                    }
                    if (modified)
                    {
                        flowMap[x, y].flowDirection = direction.normalized;
                    }
                    else
                    {
                        flowMap[x, y].flowDirection = new Vector2(0, 0);
                    }
                }
            }
        }
        return flowMap;
    }

    private (Vector2 flowDirection, float distanceToGate)[,] createFlowMap(GateSupremeCommander gate)
    {
        PriorityQueue<float, Coord> queue = new PriorityQueue<float, Coord>();
        List<Coord> coordsGateCentre = gate.GetCentreCoords(X, Y);
        List<Coord> coordsGate = gate.GetGateCoords(X, Y);
        HashSet<Coord> gateCoords = new HashSet<Coord>();
        foreach (Coord c in coordsGate)
        {
            gateCoords.Add(new Coord(c.X % RegionSize, c.Y % RegionSize));
        }
        (Vector2 flowDirection, float distanceToGate)[,] flowMap = new (Vector2 flowDirection, float distanceToGate)[RegionSize, RegionSize];
        for (int i = 0; i < RegionSize; i++)
        {
            for (int j = 0; j < RegionSize; j++)
            {
                flowMap[i, j] = (new Vector2(), float.MaxValue);
            }
        }

        foreach (Coord c in coordsGateCentre)
        {
            Coord coord = new Coord(c.X % RegionSize, c.Y % RegionSize);
            flowMap[coord.X, coord.Y].distanceToGate = 1.0f;
            queue.Enqueue(1.0f, coord);
        }
        int pivot1Pos = (coordsGate.Count - 1) / 2;
        float cost = 1.0f;
        while (pivot1Pos >= 0)
        {
            cost += 0.5f;
            Coord c1 = coordsGate[pivot1Pos];
            Coord coord1 = new Coord(c1.X % RegionSize, c1.Y % RegionSize);
            flowMap[coord1.X, coord1.Y].distanceToGate = cost;
            queue.Enqueue(cost, coord1);

            Coord c2 = coordsGate[coordsGate.Count - (pivot1Pos + 1)];
            Coord coord2 = new Coord(c2.X % RegionSize, c2.Y % RegionSize);
            flowMap[coord2.X, coord2.Y].distanceToGate = cost;
            queue.Enqueue(cost, coord2);
            pivot1Pos--;
        }

        while (queue.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queue.Dequeue();
            List<NeighborWithDistance> neighbors = getNeighbors(processed.item);
            float currentDistance = flowMap[processed.item.X, processed.item.Y].distanceToGate;
            foreach (NeighborWithDistance neighbor in neighbors)
            {
                float storedNeighborDistance = flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate;
                if (storedNeighborDistance > (currentDistance + neighbor.distance))
                {
                    flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate = currentDistance + neighbor.distance;
                    queue.Enqueue(currentDistance + neighbor.distance, neighbor.coord);
                }
            }
        }
        
        for (int x = 0; x < RegionSize; x++)
        {
            for (int y = 0; y < RegionSize; y++)
            {
                if (obstructionMap[x, y])
                {
                    flowMap[x, y].flowDirection = new Vector2(0.0f, 0.0f);
                }
                else
                {
                    Coord coord = new Coord(x, y);
                    List<NeighborWithDistance> neighbors = getNeighbors(coord);
                    float origDistance = flowMap[x, y].distanceToGate;
                    Vector2 direction = new(0, 0);
                    bool modified = false;
                    foreach (NeighborWithDistance neighbor in neighbors)
                    {
                        float newDistace = origDistance - (origDistance - flowMap[neighbor.coord.X, neighbor.coord.Y].distanceToGate) / neighbor.distance;
                        direction += new Vector2(neighbor.coord.X - x, neighbor.coord.Y - y) * Mathf.Clamp((origDistance - newDistace), -4, 4);
                        modified = true;
                    }
                    if (gateCoords.Contains(coord))
                    {
                        Vector2 gateDirection = new Vector2();
                        if (gate.regionA.X != X || gate.regionA.Y != Y)
                        {
                            gateDirection = new Vector2(X - gate.regionA.X, Y - gate.regionA.Y);
                        }
                        else if (gate.regionB.X != X || gate.regionB.Y != Y)
                        {
                            gateDirection = new Vector2(X - gate.regionB.X, Y - gate.regionB.Y);
                        }
                        direction += gateDirection;
                    }
                    if (modified)
                    {
                        flowMap[x, y].flowDirection = direction.normalized;
                    }
                    else
                    {
                        flowMap[x, y].flowDirection = new Vector2(0, 0);
                    }
                }
            }
        }
        return flowMap;
    }

    private void computeDistancesToOtherGates(GateSupremeCommander gate)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            GateSupremeCommander gate2 = gates[i];
            if (gate != gate2)
            {
                if (gate2.distancesToNeighboringGates.ContainsKey(gate))
                {
                    bool added = gate.distancesToNeighboringGates.TryAdd(gate2, gate2.distancesToNeighboringGates[gate]);
                    if (added) gate.neighboringGates.Add(gate2);
                }
                else
                {
                    float distance = computeDistanceBetweenGates(gate, gate2);
                    gate.distancesToNeighboringGates.Add(gate2, distance);
                    gate.neighboringGates.Add(gate2);
                }
            }
        }
    }

    private float computeDistanceBetweenGates(GateSupremeCommander gate1, GateSupremeCommander gate2)
    {
        PriorityQueue<float, Coord> queue = new PriorityQueue<float, Coord>();
        List<Coord> centreCoordsGate1 = gate1.GetCentreCoords(X, Y);
        List<Coord> centreCoordsGate2 = gate2.GetCentreCoords(X, Y);

        float[,] distances = new float[RegionSize, RegionSize];
        for (int i = 0; i < RegionSize; i++)
        {
            for (int j = 0; j < RegionSize; j++)
            {
                distances[i, j] = float.MaxValue;
            }
        }

        foreach (Coord c in centreCoordsGate1)
        {
            Coord coord = new Coord(c.X % RegionSize, c.Y % RegionSize);
            distances[coord.X, coord.Y] = 1.0f;
            queue.Enqueue(1.0f, coord);
        }

        while (queue.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queue.Dequeue();
            List<NeighborWithDistance> neighbors = getNeighbors(processed.item);
            float currentDistance = distances[processed.item.X, processed.item.Y];
            foreach (NeighborWithDistance neighbor in neighbors)
            {
                float storedNeighborDistance = distances[neighbor.coord.X, neighbor.coord.Y];
                if (storedNeighborDistance > (currentDistance + neighbor.distance))
                {
                    distances[neighbor.coord.X, neighbor.coord.Y] = currentDistance + neighbor.distance;
                    queue.Enqueue(currentDistance + neighbor.distance, neighbor.coord);
                }
            }
        }

        float minDistance = float.MaxValue;
        foreach (Coord c in centreCoordsGate2)
        {
            float distance = distances[c.X % RegionSize, c.Y % RegionSize];
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }
        return minDistance;
    }

    private void initializeGates()
    {
        int mapWidth = Map.instance.tiles.GetLength(0);
        int mapHeight = Map.instance.tiles.GetLength(1);
        List<Coord> gateCoordsA = new List<Coord>();
        List<Coord> gateCoordsB = new List<Coord>();
        if (RegionSize * Y + RegionSize < mapHeight)
        {
            for (int i = 0; i < RegionSize; i++)
            {
                if (obstructionMap[i, RegionSize - 1] || 
                    Map.instance.tiles[RegionSize * X + i, RegionSize * Y + RegionSize].obstructed)
                {
                    addGateFromGateTiles(gateCoordsA, gateCoordsB, true);
                    gateCoordsA = new List<Coord>();
                    gateCoordsB = new List<Coord>();
                }
                else
                {
                    gateCoordsA.Add(new Coord(RegionSize * X + i, RegionSize * Y + RegionSize - 1));
                    gateCoordsB.Add(new Coord(RegionSize * X + i, RegionSize * Y + RegionSize));
                }
            }
            addGateFromGateTiles(gateCoordsA, gateCoordsB, true);
            gateCoordsA = new List<Coord>();
            gateCoordsB = new List<Coord>();
        }

        if (RegionSize * X + RegionSize < mapWidth)
        {
            for (int i = 0; i < RegionSize; i++)
            {
                if (obstructionMap[RegionSize - 1, i] || 
                    Map.instance.tiles[RegionSize * X + RegionSize, RegionSize * Y + i].obstructed)
                {
                    addGateFromGateTiles(gateCoordsA, gateCoordsB, false);
                    gateCoordsA = new List<Coord>();
                    gateCoordsB = new List<Coord>();
                }
                else
                {
                    gateCoordsA.Add(new Coord(RegionSize * X + RegionSize - 1, RegionSize * Y + i));
                    gateCoordsB.Add(new Coord(RegionSize * X + RegionSize, RegionSize * Y + i));
                }
            }
            addGateFromGateTiles(gateCoordsA, gateCoordsB, false);
            gateCoordsA = new List<Coord>();
            gateCoordsB = new List<Coord>();
        }
        //foreach (GateSupremeCommander gate in gates) Debug.Log(gate);

        if (X > 0)
        {
            List<GateSupremeCommander> leftRegionGates = MapRegionsSupremeCommander.Instance.regions[X - 1, Y].gates;
            for (int i = 0; i < leftRegionGates.Count; i++)
            {
                if (leftRegionGates[i].regionB.X == X)
                {
                    gates.Add(leftRegionGates[i]);
                }
            }
        }

        if (Y > 0)
        {
            List<GateSupremeCommander> lowerRegionGates = MapRegionsSupremeCommander.Instance.regions[X, Y - 1].gates;
            for (int i = 0; i < lowerRegionGates.Count; i++)
            {
                if (lowerRegionGates[i].regionB.Y == Y)
                {
                    gates.Add(lowerRegionGates[i]);
                }
            }
        }
    }

    private void addGateFromGateTiles(List<Coord> gateTilesA, List<Coord> gateTilesB, bool upper)
    {
        if (gateTilesA.Count != 0)
        {
            GateSupremeCommander gate;
            if (upper)
                gate = new GateSupremeCommander((X, Y), (X, Y + 1), gateTilesA, gateTilesB);
            else
                gate = new GateSupremeCommander((X, Y), (X + 1, Y), gateTilesA, gateTilesB);
            gates.Add(gate);
        }
    }

    private List<NeighborWithDistance> getNeighbors(Coord coord)
    {
        bool up, down, left, right, upLeft, upRight, downLeft, downRight;
        int width = RegionSize;
        int height = RegionSize;
        up = coord.Y + 1 < height && !obstructionMap[coord.X, coord.Y + 1];
        down = coord.Y - 1 >= 0 && !obstructionMap[coord.X, coord.Y - 1];
        left = coord.X - 1 >= 0 && !obstructionMap[coord.X - 1, coord.Y];
        right = coord.X + 1 < width && !obstructionMap[coord.X + 1, coord.Y];
        upLeft = coord.Y + 1 < height && coord.X - 1 >= 0 && !obstructionMap[coord.X - 1, coord.Y + 1];
        upRight = coord.Y + 1 < height && coord.X + 1 < width && !obstructionMap[coord.X + 1, coord.Y + 1];
        downLeft = coord.Y - 1 >= 0 && coord.X - 1 >= 0 && !obstructionMap[coord.X - 1, coord.Y - 1];
        downRight = coord.Y - 1 >= 0 && coord.X + 1 < width && !obstructionMap[coord.X + 1, coord.Y - 1];

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
