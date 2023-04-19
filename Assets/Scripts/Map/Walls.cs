using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using utils;

public class Walls
{
    private bool[,] map;
    public bool IsInWallsPoint(Vector2 position)
    {
        Coord coord = Coord.CoordFromPosition(position);
        if (!map[coord.X, coord.Y]) return true;
        else return false;
    }

    public bool IsInWallsUnit(Vector2 position)
    {
        float radius = SimulationSettings.UnitRadius;
        Coord coord = Coord.CoordFromPosition(position);
        float fracX = position.x - coord.X;
        float fracY = position.y - coord.Y;
        Vector2 frac = new Vector2(fracX, fracY);

        int width = map.GetLength(0);
        int height = map.GetLength(1);

        bool upperLeftObstructed = coord.X - 1 < 0 || coord.Y + 1 >= height || !map[coord.X - 1, coord.Y + 1];
        bool upperObstructed = coord.Y + 1 >= height || !map[coord.X, coord.Y + 1];
        bool upperRightObstructed = coord.X + 1 >= width || coord.Y + 1 >= height || !map[coord.X + 1, coord.Y + 1];
        bool leftObstructed = coord.X - 1 < 0 || !map[coord.X - 1, coord.Y];
        bool centerObstructed = !map[coord.X, coord.Y];
        bool rightObstructed = coord.X + 1 >= width || !map[coord.X + 1, coord.Y];
        bool bottomLeftObstructed = coord.X - 1 < 0 || coord.Y - 1 < 0 || !map[coord.X - 1, coord.Y - 1];
        bool bottomObstructed = coord.Y - 1 < 0 || !map[coord.X, coord.Y - 1];
        bool bottomRightObstructed = coord.X + 1 >= width || coord.Y - 1 < 0 || !map[coord.X + 1, coord.Y - 1];

        bool inWallUpperLeft = upperLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 1.0f)) <= radius;
        bool inWallUpper = upperObstructed && 1.0f - fracY <= radius;
        bool inWallUpperRigth = upperRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 1.0f)) <= radius;
        bool inWallLeft = leftObstructed && fracX <= radius;
        bool inWallRight = rightObstructed && 1.0f - fracX <= radius;
        bool inWallBottomLeft = bottomLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 0.0f)) <= radius;
        bool inWallBottom = bottomObstructed && fracY <= radius;
        bool inWallBottomRight = bottomRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 0.0f)) <= radius;

        return (inWallUpperLeft || inWallUpperRigth || inWallBottomLeft || inWallBottomRight || inWallUpper || inWallBottom || inWallLeft || inWallRight || centerObstructed);
    }

    public Vector2 GetWallRepulsionForce(Unit unit)
    {
        if (IsInWallsPoint(unit.position))
        {
            return Vector2.zero;
        }
        Vector2 wallRepulsionForce = Vector2.zero;
        Vector2 initDirection = Vector2.right;

        foreach (var (angle, length) in obstacleRepulsionRays)
        {
            Vector2 rayDirection = initDirection.Rotate(angle).normalized;
            float hitDistance;
            bool hitWall = WallRayCasting(unit.position, rayDirection, length, out _, out hitDistance);

            if (!hitWall || angle == 0)
            {
                continue;
            }

            float distanceFactor = (length - hitDistance) / length;       
            Vector2 repulsionForce = -rayDirection * distanceFactor * distanceFactor * distanceFactor;

            wallRepulsionForce += repulsionForce;
        }
        return wallRepulsionForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    public Vector2 GetWallTurningForce(Unit unit)
    {
        if (IsInWallsPoint(unit.position))
        {
            return Vector2.zero;
        }
        Vector2 wallTurningForce = Vector2.zero;
        Vector2 velocity = unit.velocity;

        foreach (var (angle, length) in obstacleTurningRays)
        {
            Vector2 rayDirection = velocity.Rotate(angle).normalized;
            float hitDistance;
            bool hitWall = WallRayCasting(unit.position, rayDirection, length, out _, out hitDistance);

            if (!hitWall || angle == 0)
            {
                continue;
            }

            float distanceFactor = (length - hitDistance) / length;
            Vector2 turningForceDirection;
            if (angle > 0) turningForceDirection = rayDirection.Rotate(-90);
            else turningForceDirection = rayDirection.Rotate(90);
            Vector2 turningForce = distanceFactor * distanceFactor * turningForceDirection;

            wallTurningForce += turningForce;
        }
        return wallTurningForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    public bool pathClearBetweenPositions(Vector2 start, Vector2 end)
    {
        return !WallRayCasting(start, end - start, (end - start).magnitude, out _, out _);
    }

    public bool WallRayCasting(Vector2 origin, Vector2 direction, float length, out Vector2 hitPoint, out float distanceToHit)
    {
        distanceToHit = float.MaxValue;
        hitPoint = new Vector2();
        direction = direction.normalized;
        float distance = 0.0f;
        Vector2 position = origin;
        Coord coord = Coord.CoordFromPosition(position);

        while (true)
        {
            Vector2 newPosition;
            Coord newCoord;
            float distanceChange;
            getNearestCoordBorderHit(coord, position, direction, out newCoord, out newPosition, out distanceChange);
            distance += distanceChange;
            if (distance <= length)
            {
                if (!newCoord.WithinBounds() || !map[newCoord.X, newCoord.Y])
                {
                    distanceToHit = distance;
                    hitPoint = newPosition;
                    return true;
                }
                else
                {
                    coord = newCoord;
                    position = newPosition;
                }
            }
            else break;
        }
        return false;
    }

    private void getNearestCoordBorderHit(in Coord coord, in Vector2 position, in Vector2 direction, out Coord nextTile, out Vector2 borderPoint, out float distanceChange)
    {
        float xShift = float.MaxValue;
        float xDistance = float.MaxValue;
        float yShift = float.MaxValue;
        float yDistance = float.MaxValue;
        Vector2 xPosition = new();
        Vector2 yPosition = new();
        Coord xCoord = new Coord(0, 0);
        Coord yCoord = new Coord(0, 0);
        if (direction.x != 0)
        {
            if (direction.x > 0)
            {
                float xPos = coord.X + 1.0f;
                xShift = xPos - position.x;
                xDistance = (xShift / direction.x) + 0.001f;
                xPosition = position + xDistance * direction;
                xCoord = Coord.CoordFromPosition(xPosition);
            }
            else {
                float xPos = coord.X;
                xShift = xPos - position.x;
                xDistance = (xShift / direction.x) + 0.001f;
                xPosition = position + xDistance * direction;
                xCoord = Coord.CoordFromPosition(xPosition);
            }
        }

        if (direction.y != 0)
        {
            if (direction.y > 0)
            {
                float yPos = coord.Y + 1.0f;
                yShift = yPos - position.y;
                yDistance = (yShift / direction.y) + 0.001f;
                yPosition = position + yDistance * direction;
                yCoord = Coord.CoordFromPosition(yPosition);
            }
            else
            {
                float yPos = coord.Y;
                yShift = yPos - position.y;
                yDistance = (yShift / direction.y) + 0.001f;
                yPosition = position + yDistance * direction;
                yCoord = Coord.CoordFromPosition(yPosition);
            }
        }

        if (yDistance < xDistance)
        {
            nextTile = yCoord;
            borderPoint = yPosition;
            distanceChange = yDistance;
        }
        else
        {
            nextTile = xCoord;
            borderPoint = xPosition;
            distanceChange = xDistance;
        }
    }

    private List<(float angle, float length)> obstacleRepulsionRays = new();

    private void InitializeObstacleRepulsionRays() {
        List<float> angles = new List<float> { 0, 45, 90, 135, -45, -90, -135, 180 };
        List<float> lengths = new List<float> { 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f};
        for (int i = 0; i < Mathf.Min(angles.Count, lengths.Count); i++)
        {
            obstacleRepulsionRays.Add((angles[i], lengths[i]));
        }
    }

    private List<(float angle, float length)> obstacleTurningRays = new();

    private void InitializeObstacleTurningRays()
    {
        List<float> angles = new List<float> { 10, -10 };
        List<float> lengths = new List<float> { 7.0f, 7.0f };
        for (int i = 0; i < Mathf.Min(angles.Count, lengths.Count); i++)
        {
            obstacleTurningRays.Add((angles[i], lengths[i]));
        }
    }

    public void Initialize(Map map)
    {
        this.map = map.passabilityMap;
        InitializeObstacleTurningRays();
        InitializeObstacleRepulsionRays();
    }
}
