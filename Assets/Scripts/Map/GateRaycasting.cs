using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GateRaycasting
{
    public static bool GateRaycast(RegionalDecomposition decomposition, Vector2 origin, Vector2 direction, float length, out Vector2 hitPoint, out float distanceToHit, out int gateID)
    {
        distanceToHit = float.MaxValue;
        hitPoint = new Vector2();
        gateID = -1;
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
                if (!newCoord.WithinBounds() || decomposition.IsGate(decomposition.regionMap[newCoord.X, newCoord.Y]))
                {
                    distanceToHit = distance;
                    hitPoint = newPosition;
                    gateID = decomposition.regionMap[newCoord.X, newCoord.Y] - RegionalDecomposition.GatewayIndexOffset;
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

    private static void getNearestCoordBorderHit(in Coord coord, in Vector2 position, in Vector2 direction, out Coord nextTile, out Vector2 borderPoint, out float distanceChange)
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
            else
            {
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
}