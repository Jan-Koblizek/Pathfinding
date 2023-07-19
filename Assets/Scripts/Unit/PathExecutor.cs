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

    private float targetUpdatePeriod;
    private float targetUpdatePeriodBase = 5.0f;
    private float targetUpdateTime;

    private int targetLastSeenCounter = 0;
    private const int recalculatePathThreshold = 5;

    public PathExecutor(Unit unit, ref List<Vector2> path)
    {
        targetUpdatePeriod = targetUpdatePeriodBase;
        this.unit = unit;
        this.path = path;
        targetUpdateTime = targetUpdatePeriod * 2;
        previousSteeringTarget = unit.position;
        steeringTarget = unit.position;
    }

    public PathExecutor(Unit unit, List<Vector2> path)
    {
        targetUpdatePeriod = targetUpdatePeriodBase;
        this.unit = unit;
        this.path = path;
        targetUpdateTime = targetUpdatePeriod * 2;
        previousSteeringTarget = unit.position;
        steeringTarget = unit.position;
    }

    public PathExecutor(Unit unit, ref Stack<Vector2> path)
    {
        targetUpdatePeriod = targetUpdatePeriodBase;
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
        int closestPathIndex = GetIndexOfTheClosestPathPoint();
        previousSteeringTarget = steeringTarget;
        int pathIndex;
        if (Map.instance.walls.pathClearBetweenPositions(unit.position, path[steeringTargetIndex]))
        {
            pathIndex = steeringTargetIndex;
            targetLastSeenCounter = 0;
        }
        else
        {
            pathIndex = closestPathIndex;

            while (pathIndex != 0 && !Map.instance.walls.pathClearBetweenPositions(unit.position, path[pathIndex]))
            {
                if (pathIndex > 10) pathIndex -= 10;
                else pathIndex = 0;
            }
            if (pathIndex != 0) targetLastSeenCounter = 0;
        }

        int startPathIndex = pathIndex;
        int i = (int)Mathf.Sqrt(Mathf.Max(pathIndex - (closestPathIndex+5), 1));
        while (i < (path.Count - pathIndex) && i <= 20)
        {
            Vector2 target = path[i + startPathIndex];
            if (Map.instance.walls.pathClearBetweenPositions(unit.position, target))
            {
                targetLastSeenCounter = 0;
                pathIndex = i + startPathIndex;
                i+=(int)Mathf.Sqrt(Mathf.Max(Mathf.Max(pathIndex - (closestPathIndex+5), 1), i));
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
        if (Vector2.Distance(unit.position, steeringTarget) > 2*targetUpdateTime &&
            targetUpdateTime < targetUpdatePeriod)
            return true;
        else
        {
            targetUpdateTime = 0;
            targetUpdatePeriod = targetUpdatePeriodBase + UnityEngine.Random.Range(-1.0f, 1.0f);
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
        unit.repaths++;
        path = Pathfinding.ConstructPathAStar(unit.position, path[path.Count - 1], Pathfinding.StepDistance, 0.2f).ToList();
    }
}