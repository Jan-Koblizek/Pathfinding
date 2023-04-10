using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public static class Pathfinding
{
    private static PathfindingCell[,] pathfindingGrid;
    private static Queue<Coord> queue;
    private static PriorityQueue<float, Coord> queueAStar;

    private static List<NeighborWithDistance> GetNeighborsAStarNoBlocking(Coord coord)
    {
        bool up, down, left, right, upLeft, upRight, downLeft, downRight;
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        up = coord.Y + 1 < height;
        down = coord.Y - 1 >= 0;
        left = coord.X - 1 >= 0;
        right = coord.X + 1 < width;
        upLeft = coord.Y + 1 < height && coord.X - 1 >= 0;
        upRight = coord.Y + 1 < height && coord.X + 1 <= width;
        downLeft = coord.Y - 1 >= 0 && coord.X - 1 >= 0;
        downRight = coord.Y - 1 >= 0 && coord.X + 1 <= width;

        List<NeighborWithDistance> neighbors = new();
        if (left)
        {
            Coord neighbor = new(coord.X - 1, coord.Y);
            neighbors.Add(new(neighbor, 1.0f));
        }
        if (right)
        {
            Coord neighbor = new(coord.X + 1, coord.Y);
            neighbors.Add(new(neighbor, 1.0f));
        }
        if (up)
        {
            Coord neighbor = new(coord.X, coord.Y + 1);
            neighbors.Add(new(neighbor, 1.0f));
        }
        if (down)
        {
            Coord neighbor = new(coord.X, coord.Y - 1);
            neighbors.Add(new(neighbor, 1.0f));
        }

        // Move diagonally
        // Not only the diagonal, but also squares neighboring it must be free
        if (upLeft && left && up)
        {
            Coord neighbor = new(coord.X - 1, coord.Y + 1);
            neighbors.Add(new(neighbor, 1.415f));
        }
        if (upRight && right && up)
        {
            Coord neighbor = new(coord.X + 1, coord.Y + 1);
            neighbors.Add(new(neighbor, 1.415f));
        }
        if (downLeft && left && down)
        {
            Coord neighbor = new(coord.X - 1, coord.Y - 1);
            neighbors.Add(new(neighbor, 1.415f));
        }
        if (downRight && right && down)
        {
            Coord neighbor = new(coord.X + 1, coord.Y - 1);
            neighbors.Add(new(neighbor, 1.415f));
        }

        return neighbors;
    }

    private static List<NeighborWithDistance> GetNeighborsAStar(Coord coord, out bool someNeighborObstructed)
    {
        bool up, down, left, right, upLeft, upRight, downLeft, downRight;
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        up = coord.Y + 1 < height && !Map.instance.tiles[coord.X, coord.Y + 1].obstructed;
        down = coord.Y - 1 >= 0 && !Map.instance.tiles[coord.X, coord.Y - 1].obstructed;
        left = coord.X - 1 >= 0 && !Map.instance.tiles[coord.X - 1, coord.Y].obstructed;
        right = coord.X + 1 < width && !Map.instance.tiles[coord.X + 1, coord.Y].obstructed;
        upLeft = coord.Y + 1 < height && coord.X - 1 >= 0 && !Map.instance.tiles[coord.X - 1, coord.Y + 1].obstructed;
        upRight = coord.Y + 1 < height && coord.X + 1 < width && !Map.instance.tiles[coord.X + 1, coord.Y + 1].obstructed;
        downLeft = coord.Y - 1 >= 0 && coord.X - 1 >= 0 && !Map.instance.tiles[coord.X - 1, coord.Y - 1].obstructed;
        downRight = coord.Y - 1 >= 0 && coord.X + 1 < width && !Map.instance.tiles[coord.X + 1, coord.Y - 1].obstructed;

        someNeighborObstructed = !(up & down & left & right & upLeft & upRight & downLeft & downRight);

        bool upNull, downNull, leftNull, rightNull, upLeftNull, upRightNull, downLeftNull, downRightNull;
        upNull = up && pathfindingGrid[coord.X, coord.Y + 1] == null;
        downNull = down && pathfindingGrid[coord.X, coord.Y - 1] == null;
        leftNull = left && pathfindingGrid[coord.X - 1, coord.Y] == null;
        rightNull = right && pathfindingGrid[coord.X + 1, coord.Y] == null;
        upLeftNull = upLeft && pathfindingGrid[coord.X - 1, coord.Y + 1] == null;
        upRightNull = upRight && pathfindingGrid[coord.X + 1, coord.Y + 1] == null;
        downLeftNull = downLeft && pathfindingGrid[coord.X - 1, coord.Y - 1] == null;
        downRightNull = downRight && pathfindingGrid[coord.X + 1, coord.Y - 1] == null;

        List<NeighborWithDistance> neighbors = new();
        if (left && (leftNull || !pathfindingGrid[coord.X - 1, coord.Y].closed))
        {
            Coord neighbor = new(coord.X - 1, coord.Y);
            if (leftNull)
            {
                pathfindingGrid[coord.X - 1, coord.Y] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (right && (rightNull || !pathfindingGrid[coord.X + 1, coord.Y].closed))
        {
            Coord neighbor = new(coord.X + 1, coord.Y);
            if (rightNull)
            {
                pathfindingGrid[coord.X + 1, coord.Y] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (up && (upNull || !pathfindingGrid[coord.X, coord.Y + 1].closed))
        {
            Coord neighbor = new(coord.X, coord.Y + 1);
            if (upNull)
            {
                pathfindingGrid[coord.X, coord.Y + 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }
        if (down && (downNull || !pathfindingGrid[coord.X, coord.Y - 1].closed))
        {
            Coord neighbor = new(coord.X, coord.Y - 1);
            if (downNull)
            {
                pathfindingGrid[coord.X, coord.Y - 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.0f));
        }

        // Move diagonally
        // Not only the diagonal, but also squares neighboring it must be free
        if (upLeft && left && up && (upLeftNull || !pathfindingGrid[coord.X - 1, coord.Y + 1].closed))
        {
            Coord neighbor = new(coord.X - 1, coord.Y + 1);
            if (upLeftNull)
            {
                pathfindingGrid[coord.X - 1, coord.Y + 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (upRight && right && up && (upRightNull || !pathfindingGrid[coord.X + 1, coord.Y + 1].closed))
        {
            Coord neighbor = new(coord.X + 1, coord.Y + 1);
            if (upRightNull)
            {
                pathfindingGrid[coord.X + 1, coord.Y + 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downLeft && left && down && (downLeftNull || !pathfindingGrid[coord.X - 1, coord.Y - 1].closed))
        {
            Coord neighbor = new(coord.X - 1, coord.Y - 1);
            if (downLeftNull)
            {
                pathfindingGrid[coord.X - 1, coord.Y - 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }
        if (downRight && right && down && (downRightNull || !pathfindingGrid[coord.X + 1, coord.Y - 1].closed))
        {
            Coord neighbor = new(coord.X + 1, coord.Y - 1);
            if (downRightNull)
            {
                pathfindingGrid[coord.X + 1, coord.Y - 1] = new PathfindingCell(float.MaxValue, false, null);
            }
            neighbors.Add(new NeighborWithDistance(neighbor, 1.415f));
        }

        return neighbors;
    }

    public static bool DirectionClear(Vector3 start, Vector3 end)
    {
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        int y0 = Mathf.RoundToInt(start.y);
        int y1 = Mathf.RoundToInt(end.y);
        int x0 = Mathf.RoundToInt(start.x);
        int x1 = Mathf.RoundToInt(end.x);

        bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
        if (steep)
        {
            int t;
            t = x0;
            x0 = y0;
            y0 = t;
            t = x1;
            x1 = y1;
            y1 = t;
        }
        if (x0 > x1)
        {
            int t;
            t = x0;
            x0 = x1;
            x1 = t;
            t = y0;
            y0 = y1;
            y1 = t;
        }
        int dx = x1 - x0;
        float dy = y1 - y0;
        float ystep = dy / dx;
        for (int i = 0; i <= dx; i++)
        {
            float yF = y0 + i * ystep;
            int y = Mathf.RoundToInt(yF);
            if (steep)
            {
                bool om1, o, op1;
                Coord om1Coord, oCoord, op1Coord;
                om1Coord = Coord.CoordFromPosition(new Vector2(y - 1, x0 + i));
                oCoord = Coord.CoordFromPosition(new Vector2(y, x0 + i));
                op1Coord = Coord.CoordFromPosition(new Vector2(y + 1, x0 + i));
                om1 = om1Coord.WithinBounds() && !Map.instance.tiles[om1Coord.X, om1Coord.Y].obstructed;
                o = !Map.instance.tiles[oCoord.X, oCoord.Y].obstructed;
                op1 = op1Coord.WithinBounds() && !Map.instance.tiles[op1Coord.X, op1Coord.Y].obstructed;
                if (!om1 || !o || !op1) return false;
            }
            else
            {
                bool om1, o, op1;
                Coord om1Coord, oCoord, op1Coord;
                om1Coord = Coord.CoordFromPosition(new Vector2(x0 + i, y - 1));
                oCoord = Coord.CoordFromPosition(new Vector2(x0 + i, y));
                op1Coord = Coord.CoordFromPosition(new Vector2(x0 + i, y + 1));
                om1 = om1Coord.WithinBounds() && !Map.instance.tiles[om1Coord.X, om1Coord.Y].obstructed;
                o = !Map.instance.tiles[oCoord.X, oCoord.Y].obstructed;
                op1 = op1Coord.WithinBounds() && !Map.instance.tiles[op1Coord.X, op1Coord.Y].obstructed;
                if (!om1 || !o || !op1) return false;
            }
        }
        return true;
    }

    public static Stack<Vector2> SimplifyPathBasic(Stack<Vector2> originalStack)
    {
        Stack<Vector2> newStack = new();
        Vector2 originalPos = originalStack.Pop();
        Vector2 nextPos = originalStack.Pop();
        Vector2 direction = nextPos - originalPos;
        newStack.Push(originalPos);
        int count = 1;
        while (originalStack.Count > 0)
        {
            count++;
            Vector2 secondPos = originalStack.Pop();
            if (Vector2.Distance(originalPos + direction * count, secondPos) < 1)
            {
                nextPos = secondPos;
            }
            else
            {
                originalPos = nextPos;
                nextPos = secondPos;
                direction = nextPos - originalPos;
                count = 1;
                newStack.Push(originalPos);
            }
        }
        newStack.Push(nextPos);
        while (newStack.Count > 0)
        {
            originalStack.Push(newStack.Pop());
        }
        return originalStack;
    }

    public static Stack<Vector2> SimplifyPathIteration(Stack<Vector2> originalStack)
    {
        Stack<Vector2> newStack = new();
        Vector2 originalPos = originalStack.Pop();
        Vector2 nextPos = originalStack.Pop();
        newStack.Push(originalPos);
        while (originalStack.Count > 0)
        {
            Vector2 secondPos = originalStack.Pop();
            if (DirectionClear(originalPos, secondPos))
            {
                nextPos = secondPos;
            }
            else
            {
                originalPos = nextPos;
                nextPos = secondPos;
                newStack.Push(originalPos);
            }
        }
        newStack.Push(nextPos);
        while (newStack.Count > 0)
        {
            originalStack.Push(newStack.Pop());
        }
        return originalStack;
    }

    public static Stack<Vector2> SimplifyPath(Stack<Vector2> originalStack)
    {
        originalStack = SimplifyPathBasic(originalStack);
        originalStack = SimplifyPathIteration(originalStack);
        return originalStack;
    }

    public static Stack<Vector2> ConstructPathAStar(Vector2 start, Vector2 goal, System.Func<Coord, Coord, float> heuristic, float nearObstaclePenalty)
    {
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        pathfindingGrid = new PathfindingCell[width, height];
        queueAStar = new PriorityQueue<float, Coord>();
        Coord startingCoord = Coord.CoordFromPosition(start);
        Coord goalCoord = Coord.CoordFromPosition(goal);
        pathfindingGrid[startingCoord.X, startingCoord.Y] = new PathfindingCell(0.0f, false, new Coord(-1, -1));
        queueAStar.Enqueue(heuristic(startingCoord, goalCoord), startingCoord);
        bool finished = false;
        while (!finished && queueAStar.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queueAStar.Dequeue();
            if (!pathfindingGrid[processed.item.X, processed.item.Y].closed)
            {
                pathfindingGrid[processed.item.X, processed.item.Y].closed = true;
                if (processed.item == goalCoord)
                {
                    //Debug.Log("goal found");
                    queueAStar.Clear();
                    finished = true;
                    break;
                }
                List<NeighborWithDistance> neighbors = GetNeighborsAStar(processed.item, out bool someNeighborObstructed);
                float currentDistance = pathfindingGrid[processed.item.X, processed.item.Y].distance;
                //float parentHeuristic = heuristic(processed.item, goalCoord);
                foreach (NeighborWithDistance neighbor in neighbors)
                {
                    float storedNeighborDistance = pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance;
                    float neighborHeuristic = heuristic(neighbor.coord, goalCoord);
                    if (storedNeighborDistance > (currentDistance + neighbor.distance))
                    {
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].predecessor = processed.item;
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance = currentDistance + (someNeighborObstructed ? 1.0f + nearObstaclePenalty : 1.0f) * neighbor.distance;
                        queueAStar.Enqueue(neighborHeuristic + currentDistance + neighbor.distance, neighbor.coord);
                    }
                }
            }
        }
        if (finished)
        {
            Stack<Vector2> result = new();
            result.Push(goal);
            if (goalCoord != startingCoord)
            {
                Coord coord = pathfindingGrid[goalCoord.X, goalCoord.Y].predecessor;
                while (coord != startingCoord)
                {
                    result.Push(coord.GetWorldPosition());
                    coord = pathfindingGrid[coord.X, coord.Y].predecessor;
                }
            }
            result.Push(start);
            //result = SimplifyPath(result);
            return result;
        }
        else
        {
            //Debug.Log("No path available");
            return null;
        }
    }

    public static Stack<Vector2> ConstructPathAStar(List<Coord> startRegion, List<Coord> goalRegion, System.Func<Coord, Coord, float> heuristic, float nearObstaclePenalty)
    {
        Coord goal = null;
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        pathfindingGrid = new PathfindingCell[width, height];
        queueAStar = new PriorityQueue<float, Coord>();
        Vector2 startRegionSum = new Vector2();
        for (int i = 0; i < startRegion.Count; i++)
        {
            startRegionSum += startRegion[i].GetWorldPosition();
        }
        Vector2 startRegionCentre = startRegionSum / startRegion.Count;
        Coord heuristicGoal = goalRegion[0];
        float minGoalDistance = Vector2.Distance(heuristicGoal.GetWorldPosition(), startRegionCentre);
        for (int i = 1; i < goalRegion.Count; i++)
        {
            float distance = Vector2.Distance(goalRegion[i].GetWorldPosition(), startRegionCentre);
            if (distance < minGoalDistance)
            {
                minGoalDistance = distance;
                heuristicGoal = goalRegion[i];
            }
        }

        foreach (Coord coord in startRegion)
        {
            pathfindingGrid[coord.X, coord.Y] = new PathfindingCell(0.0f, false, new Coord(-1, -1));
            queueAStar.Enqueue(heuristic(coord, heuristicGoal), coord);
        }
        bool finished = false;
        while (!finished && queueAStar.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queueAStar.Dequeue();
            if (!pathfindingGrid[processed.item.X, processed.item.Y].closed)
            {
                pathfindingGrid[processed.item.X, processed.item.Y].closed = true;
                if (goalRegion.Contains(processed.item))
                {
                    goal = processed.item;
                    //Debug.Log("goal found");
                    queueAStar.Clear();
                    finished = true;
                    break;
                }
                List<NeighborWithDistance> neighbors = GetNeighborsAStar(processed.item, out bool someNeighborObstructed);
                float currentDistance = pathfindingGrid[processed.item.X, processed.item.Y].distance;
                foreach (NeighborWithDistance neighbor in neighbors)
                {
                    float storedNeighborDistance = pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance;
                    float neighborHeuristic = heuristic(neighbor.coord, heuristicGoal);
                    if (storedNeighborDistance > (currentDistance + neighbor.distance))
                    {
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].predecessor = processed.item;
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance = currentDistance + (someNeighborObstructed ? 1.0f + nearObstaclePenalty : 1.0f) * neighbor.distance;
                        queueAStar.Enqueue(neighborHeuristic + currentDistance + neighbor.distance, neighbor.coord);
                    }
                }
            }
        }
        if (finished)
        {
            Stack<Vector2> result = new();
            result.Push(goal.GetWorldPosition());
            if (!startRegion.Contains(goal))
            {
                Coord coord = pathfindingGrid[goal.X, goal.Y].predecessor;
                while (!startRegion.Contains(coord))
                {
                    result.Push(coord.GetWorldPosition());
                    coord = pathfindingGrid[coord.X, coord.Y].predecessor;
                }
                result.Push(coord.GetWorldPosition());
            }
            //result = SimplifyPath(result);
            return result;
        }
        else
        {
            Debug.Log("No path available");
            return null;
        }
    }

    /*
    public static Stack<Vector3> PathWithOffset(Stack<Vector3> original, Vector3 position, int number, int totalNumber, bool inverse)
    {
        Vector3 lastDirection = new Vector3(0.0f, 0.0f, 0.0f);
        Vector3[] originalArray = original.ToArray();
        Stack<Vector3> result = new();
        for (int i = originalArray.Length - 1; i > 0; i--)
        {
            Vector3 direction = originalArray[i] - originalArray[i - 1];
            direction.Normalize();
            if (lastDirection.magnitude > 0.001 && Vector3.Angle(direction, lastDirection) > 90)
            {
                inverse = !inverse;
            }
            Vector2 direction2D = new(direction.x, direction.z);
            Vector2 relativePosition = BasicFormation.getRelativePosition(number, totalNumber, direction2D, inverse);
            result.Push(originalArray[i] + (new Vector3(relativePosition.x, 0.0f, relativePosition.y)));
            if (i > 1) result.Push(originalArray[i - 1] + (new Vector3(relativePosition.x, 0.0f, relativePosition.y)));
        }
        Vector3 direction2 = originalArray[1] - originalArray[0];
        direction2.Normalize();
        Vector2 direction2D2 = new(direction2.x, direction2.z);
        Vector2 relativePosition2 = BasicFormation.getRelativePosition(number, totalNumber, direction2D2, inverse);
        Vector3 startPos = originalArray[0] + (new Vector3(relativePosition2.x, 0.0f, relativePosition2.y));

        if (Vector3.Distance(startPos, position) > 2)
        {
            Stack<Vector3> pathToStart = Pathfinding.ConstructPathAStar(startPos, position, Pathfinding.StepDistance, 0.2f);
            foreach (Vector3 point in pathToStart)
            {
                result.Push(point);
            }
        }
        return result;
    }
    */

    public static PathfindingCell[,] ConstructDistanceField(Coord goal)
    {
        int width = Map.instance.tiles.GetLength(0);
        int height = Map.instance.tiles.GetLength(1);
        pathfindingGrid = new PathfindingCell[width, height];
        queueAStar = new PriorityQueue<float, Coord>();
        pathfindingGrid[goal.X, goal.Y] = new PathfindingCell(0.0f, false, new Coord(-1, -1));
        queueAStar.Enqueue(0, new Coord(goal.X, goal.Y));
        while (queueAStar.Count() != 0)
        {
            ItemWithPriority<float, Coord> processed = queueAStar.Dequeue();
            if (!pathfindingGrid[processed.item.X, processed.item.Y].closed)
            {
                pathfindingGrid[processed.item.X, processed.item.Y].closed = true;
                List<NeighborWithDistance> neighbors = GetNeighborsAStar(processed.item, out bool someNeighborObstructed);
                float currentDistance = pathfindingGrid[processed.item.X, processed.item.Y].distance;
                foreach (NeighborWithDistance neighbor in neighbors)
                {
                    float storedNeighborDistance = pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance;
                    if (storedNeighborDistance > (currentDistance + neighbor.distance))
                    {
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].predecessor = processed.item;
                        pathfindingGrid[neighbor.coord.X, neighbor.coord.Y].distance = currentDistance + (someNeighborObstructed ? 1.2f : 1.0f) * neighbor.distance;
                        queueAStar.Enqueue(currentDistance + neighbor.distance, neighbor.coord);
                    }
                }
            }
        }
        for (int x = 0; x < Map.instance.tiles.GetLength(0); x++)
        {
            for (int y = 0; y < Map.instance.tiles.GetLength(1); y++)
            {
                if (Map.instance.tiles[x, y].obstructed)
                {
                    pathfindingGrid[x, y] = new PathfindingCell(float.MaxValue, false, null);
                }
            }
        }
        return pathfindingGrid;
    }

    public static Vector2[,] ConstructFlowField(Coord goal)
    {
        PathfindingCell[,] distanceField = ConstructDistanceField(goal);
        Vector2[,] flowField = new Vector2[Map.instance.tiles.GetLength(0), Map.instance.tiles.GetLength(1)];
        for (int x = 0; x < Map.instance.tiles.GetLength(0); x++)
        {
            for (int y = 0; y < Map.instance.tiles.GetLength(1); y++)
            {
                if (Map.instance.tiles[x, y].obstructed || distanceField[x,y] == null)
                {
                    flowField[x, y] = new Vector2(0.0f, 0.0f);
                }
                else
                {
                    List<NeighborWithDistance> neighbors = GetNeighborsAStarNoBlocking(new Coord(x, y));
                    float origDistance = distanceField[x, y].distance;
                    Vector2 direction = new(0, 0);
                    bool modified = false;
                    foreach (NeighborWithDistance neighbor in neighbors)
                    {
                        if (distanceField[neighbor.coord.X, neighbor.coord.Y] != null)
                        {
                            float newDistace = origDistance - (origDistance - distanceField[neighbor.coord.X, neighbor.coord.Y].distance) / neighbor.distance;
                            direction += new Vector2(neighbor.coord.X - x, neighbor.coord.Y - y) * Mathf.Clamp((origDistance - newDistace), -4, 4);
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        flowField[x, y] = direction.normalized;
                    }
                    else
                    {
                        flowField[x, y] = new Vector2(0, 0);
                    }
                }
            }
        }
        return flowField;
    }

    /// <summary>
    /// Computes the shortest path from any origin node to any goal node.
    /// </summary>
    /// <param name="map">The map in which the search is executed</param>
    /// <param name="origin">The starting positions</param>
    /// <param name="goal">The targets</param>
    /// <returns></returns>
    public static List<int> FlowGraphPath(FlowGraph flowGraph, List<int> origin, List<int> goal)
    {
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        FlowGraphInitializeStructuresForSearch(origin, goal, ref flowGraph,
            out PriorityQueue<float, int?> openNodes,
            out Dictionary<int?, AStarSearchNodeFlowGraph> visitedNodes,
            out HashSet<int> goals);
        stopwatch.Stop();
        bool firstTimeAssert = true;
        while (openNodes.Count() > 0)
        {
            ItemWithPriority<float, int?> currentNode = openNodes.Dequeue();
            AStarSearchNodeFlowGraph currentData = visitedNodes[currentNode.item];
            currentData.StateClosed = true;

            if (goals.Contains((int)currentNode.item))
            {
                //for a big number of goals it might be good to create a bounding box of all goals and pre-test that
                return FlowGraphGetPathFromBackwardsRun(visitedNodes, currentNode.item, origin);
            }

            foreach (int neighbor in flowGraph.GetNeighbors((int)currentNode.item))
            {
                // The distance from start to a neighbor.
                float distanceToNeighbor = currentData.DistanceFromStart + flowGraph.DistanceBetweenNeighbors((int)currentNode.item, neighbor);

                //known node (open or closed)
                if (visitedNodes.TryGetValue(neighbor, out var neighborData))
                {

                    //this path is shorter
                    if (distanceToNeighbor + 0.01f < neighborData.DistanceFromStart)
                    {
                        neighborData.Predecessor = currentNode.item;
                        neighborData.DistanceFromStart = distanceToNeighbor;
                        neighborData.TotalGoalDistance = distanceToNeighbor + flowGraph.DistanceToGoal(neighbor, goal);

                        //if state is open
                        if (!neighborData.StateClosed)
                        {
                            openNodes.Enqueue(neighborData.TotalGoalDistance, neighbor);
                        }
                        else //this else branch is not needed for well behaved heuristics
                        {
                            if (FlowGraphShouldReevaluate(neighborData, ref firstTimeAssert))
                            {
                                neighborData.StateClosed = false;
                                openNodes.Enqueue(neighborData.TotalGoalDistance, neighbor);
                            }
                        }
                    }
                }
                else //not yet encountered
                {
                    var n = new AStarSearchNodeFlowGraph
                    {
                        Predecessor = currentNode.item,
                        DistanceFromStart = distanceToNeighbor,
                        TotalGoalDistance = distanceToNeighbor + flowGraph.DistanceToGoal(neighbor, goal)
                    };

                    visitedNodes.Add(neighbor, n);
                    openNodes.Enqueue(n.TotalGoalDistance, neighbor);
                }
            }
        }
        return null;
    }


    private static void FlowGraphInitializeStructuresForSearch(List<int> origin, List<int> goal, ref FlowGraph flowGraph,
    out PriorityQueue<float, int?> queue,
    out Dictionary<int?, AStarSearchNodeFlowGraph> visitedNodes,
    out HashSet<int> goals)
    {
        queue = new PriorityQueue<float, int?>();
        visitedNodes = new Dictionary<int?, AStarSearchNodeFlowGraph>();


        foreach (int startPoint in origin)
        {
            queue.Enqueue(0, startPoint);
            AStarSearchNodeFlowGraph node = new AStarSearchNodeFlowGraph
            {
                DistanceFromStart = 0,
                TotalGoalDistance = flowGraph.DistanceToGoal(startPoint, goal),
                Predecessor = null,
                StateClosed = false
            };
            visitedNodes.Add(startPoint, node);
        }

        goals = new HashSet<int>();
        foreach (int point in goal)
        {
            goals.Add(point);
        }
    }

    private static bool FlowGraphShouldReevaluate(AStarSearchNodeFlowGraph neighborData, ref bool firstTimeAssert)
    {
        //this is not needed for well behaved heuristics
        if (firstTimeAssert) firstTimeAssert = false;

        // Standard invariants hold
        if (neighborData.TotalGoalDistance >= 0 && neighborData.DistanceFromStart >= 0) return true;

        // because some paths might be negative, limit them
        // this is due to the float numbers
        if (neighborData.TotalGoalDistance < 0) neighborData.TotalGoalDistance = 0;
        if (neighborData.DistanceFromStart < 0) neighborData.DistanceFromStart = 0;

        return false;
    }

    /// <summary>
    /// Reconstructs the shortest path after a search is finished and reached the goal nodes from start
    /// </summary>
    /// <param name="visitedNodes">Dictionary of reached nodes</param>
    /// <param name="currentNode">The goal node which was reached</param>
    /// <param name="origin">Start locations</param>4
    /// <returns></returns>
    private static List<int> FlowGraphGetPathFromBackwardsRun(Dictionary<int?, AStarSearchNodeFlowGraph> visitedNodes, int? currentNode, List<int> origin)
    {
        List<int> path = new List<int>();
        HashSet<int> seen = new HashSet<int>(); //only used to fix a problem when negative paths make cycle

        while (!visitedNodes[currentNode].Predecessor.Equals(null))
        {
            path.Add((int)currentNode);
            currentNode = visitedNodes[currentNode].Predecessor;

            bool notSeen = seen.Add((int)currentNode);
            if (!notSeen)
            {
                //cycle detected 
                var shortList = path.GetRange(0, seen.Count);
                var startSet = new HashSet<int>(origin);
                for (int i = shortList.Count - 1; i >= 0; i--)
                {
                    if (!startSet.Contains(shortList[i]))
                    {
                        shortList.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }

                if (shortList.Count == 0)
                {
                    return null;
                }

                shortList.Reverse();
                return shortList;
            }
        }

        path.Add((int)currentNode);
        path.Reverse();
        return path;
    }

    internal class AStarSearchNodeFlowGraph
    {
        public float TotalGoalDistance; //from start to goal
        public float DistanceFromStart; //from start to current node
        public int? Predecessor;
        public bool StateClosed;
    }

    public static float EuclideanDistance(Coord coord, Coord goal)
    {
        return Mathf.Sqrt((float)((coord.X - goal.X) * (coord.X - goal.X) + (coord.Y - goal.Y) * (coord.Y - goal.Y)));
    }

    public static float StepDistance(Coord coord, Coord goal)
    {
        int xDiff = Mathf.Abs(coord.X - goal.X);
        int yDiff = Mathf.Abs(coord.Y - goal.Y);
        return Mathf.Max(xDiff, yDiff) - Mathf.Min(xDiff, yDiff) + 1.415f * Mathf.Min(xDiff, yDiff);
    }

    public static float NoHeuristic(Coord coord, Coord goal)
    {
        return 0;
    }
}



public class PathfindingCell
{
    public float distance;
    public bool closed;
    public Coord predecessor;

    public PathfindingCell(float distance, bool closed, Coord predecessor)
    {
        this.distance = distance;
        this.closed = closed;
        this.predecessor = predecessor;
    }
}

public class NeighborWithDistance
{
    public float distance;
    public Coord coord;

    public NeighborWithDistance(Coord neighbor, float distance)
    {
        this.coord = neighbor;
        this.distance = distance;
    }
}