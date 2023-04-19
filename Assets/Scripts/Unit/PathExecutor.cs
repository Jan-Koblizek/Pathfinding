using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathExecutor
{
    private Unit unit;
    private int steeringTargetIndex;
    public Vector2 previousSteeringTarget;
    public Vector2 steeringTarget;
    private List<Vector2> path;

    private float targetUpdatePeriod = 2.0f;
    private float targetUpdateTime;

    private int targetLastSeenCounter = 0;
    private const int recalculatePathThreshold = 5;

    public PathExecutor(Unit unit, ref List<Vector2> path)
    {
        this.unit = unit;
        this.path = path;
        targetUpdateTime = targetUpdatePeriod * 2;
        previousSteeringTarget = unit.position;
        steeringTarget = unit.position;
    }

    public PathExecutor(Unit unit, List<Vector2> path)
    {
        this.unit = unit;
        this.path = path;
        targetUpdateTime = targetUpdatePeriod * 2;
        previousSteeringTarget = unit.position;
        steeringTarget = unit.position;
    }

    public PathExecutor(Unit unit, ref Stack<Vector2> path)
    {
        this.unit = unit;
        this.path = path.ToList();
        targetUpdateTime = targetUpdatePeriod * 2;
        previousSteeringTarget = unit.position;
        steeringTarget = unit.position;
    }

    public Vector2 GetPathFollowingForce(float deltaTime)
    {
        int furthestVisiblePathIndex = getFurthestVisiblePathIndex(deltaTime);
        steeringTargetIndex = furthestVisiblePathIndex;
        steeringTarget = path[steeringTargetIndex];
        float factor = Mathf.Clamp01(targetUpdateTime * 2);
        Vector2 result = (factor * steeringTarget + (1.0f - factor) * previousSteeringTarget) - unit.position;
        return result.normalized;
    }

    public int getFurthestVisiblePathIndex(float deltaTime)
    {
        //Don't check too often to save CPU power
        if (SkipTargetIndexChecking(deltaTime))
        {
            return steeringTargetIndex;
        }
        previousSteeringTarget = steeringTarget;
        int i;
        int pathIndex;
        if (Map.instance.walls.pathClearBetweenPositions(unit.position, path[steeringTargetIndex]))
        {
            pathIndex = steeringTargetIndex;
            i = pathIndex + 1;
            targetLastSeenCounter = 0;
        }
        else
        {
            pathIndex = GetIndexOfTheClosestPathPoint();
            i = pathIndex;

            while (pathIndex != 0 && !Map.instance.walls.pathClearBetweenPositions(unit.position, path[pathIndex]))
            {
                if (pathIndex > 10) i = pathIndex -= 10;
                else i = pathIndex = 0;
            }
        }

        while (i < path.Count)
        {
            Vector2 target = path[i];
            if (Map.instance.walls.pathClearBetweenPositions(unit.position, target))
            {
                pathIndex = i;
                i++;
                targetLastSeenCounter = 0;
            }
            else
            {
                break;
            }
        }

        TargetNotSeen();
        steeringTargetIndex = pathIndex;
        return pathIndex;
    }

    private bool SkipTargetIndexChecking(float deltaTime)
    {
        targetUpdateTime += deltaTime;
        if (Vector2.Distance(unit.position, steeringTarget) > 2 &&
            targetUpdateTime < targetUpdatePeriod)
            return true;
        else
        {
            targetUpdateTime = UnityEngine.Random.Range(-0.5f, 0.5f);
            return false;
        }
    }

    private int GetIndexOfTheClosestPathPoint()
    {
        float minDistance = float.MaxValue;
        int minIndex = 0;

        for (int i = 0; i < path.Count; i++)
        {
            float distance = Vector2.Distance(unit.position, path[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                minIndex = i;
            }
        }
        return minIndex;
    }

    private void TargetNotSeen()
    {
        targetLastSeenCounter++;
        if (targetLastSeenCounter <= recalculatePathThreshold) return;
        path = Pathfinding.ConstructPathAStar(unit.position, path[path.Count - 1], Pathfinding.StepDistance, 0.2f).ToList();
    }
}