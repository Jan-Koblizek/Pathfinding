using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Diagnostics;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
using static UnityEngine.GraphicsBuffer;
using static UnityEngine.RuleTile.TilingRuleOutput;

public class PathExecutor
{
    private Unit unit;
    private int steeringTargetIndex;
    private Vector2 steeringTarget;
    private List<Vector2> path;

    private float targetUpdatePeriod = 1.0f;
    private float targetUpdateTime = float.MaxValue;

    private int targetLastSeenCounter = 0;
    private const int recalculatePathThreshold = 5;

    public PathExecutor(Unit unit, List<Vector2> path)
    {
        this.unit = unit;
        this.path = path;
    }

    public PathExecutor(Unit unit, Stack<Vector2> path)
    {
        this.unit = unit;
        this.path = path.ToList();
    }

    public Vector2 GetPathFollowingForce()
    {
        int furthestVisiblePathIndex = getFurthestVisiblePathIndex();
        steeringTargetIndex = furthestVisiblePathIndex;
        steeringTarget = path[steeringTargetIndex];
        //Debug.Log(steeringTargetIndex);
        Vector2 result = steeringTarget - unit.position;
        //Debug.Log(result.normalized);
        return result.normalized;
    }

    public int getFurthestVisiblePathIndex()
    {
        //Don't check too often to save CPU power
        if (SkipTargetIndexChecking())
        {
            return steeringTargetIndex;
        }

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

    private bool SkipTargetIndexChecking()
    {
        targetUpdateTime += Time.deltaTime;
        if (Vector2.Distance(unit.position, steeringTarget) > 2 &&
            targetUpdateTime < targetUpdatePeriod)
            return true;
        else
        {
            targetUpdateTime = 0;
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