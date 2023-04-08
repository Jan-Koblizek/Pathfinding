using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class UnitPathAssignmentFlowGraph
{
    private readonly int _assignedUnits;
    private readonly PathID _pathID;
    private readonly List<Vector2> _centerList;
    public int indexOfGridPath;
    private Target target;

    /// <summary>
    /// For a path holds gate centers list and number of units
    /// </summary>
    /// <param name="assignedUnits"></param>
    /// <param name="pathID"></param>
    /// <param name="centerList"></param>
    public UnitPathAssignmentFlowGraph(int assignedUnits, PathID pathID, List<Vector2> centerList, Target target)
    {
        _assignedUnits = assignedUnits;
        _pathID = pathID;
        _centerList = centerList;
        this.target = target;
    }

    /// <summary>
    /// Adds all given units to the priority queue
    /// </summary>
    /// <param name="units">w/o target</param>
    private static PriorityQueue<float, Unit> AddUnitsToQueue(HashSet<Unit> units, Vector2 gate)
    {
        // one level
        var distanceToGate = new PriorityQueue<float, Unit>();
        foreach (var unit in units)
        {
            distanceToGate.Enqueue(Vector2.Distance(unit.position, gate), unit);
        }

        return distanceToGate;
    }

    private static int GetSmallestDifference(List<UnitPathAssignmentFlowGraph> pathsForUnits)
    {
        for (int i = 0; i < pathsForUnits[0]._centerList.Count; i++)
        {
            var same = pathsForUnits[0]._centerList[i];
            foreach (var pathsForUnit in pathsForUnits)
            {
                if (same != pathsForUnit._centerList[i])
                {
                    return i;
                }
            }
        }
        return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pathsForUnits">paths to create levels from</param>
    /// <param name="units">units to assign</param>
    /// <param name="gateDifferenceDepth">the gate to do the assigment to</param>
    /// <returns></returns>
    private static List<Level> GetLevel(List<UnitPathAssignmentFlowGraph> pathsForUnits, HashSet<Unit> units)
    {
        //returns list of differences for given paths
        if (pathsForUnits.Count == 1)
        {
            var l = new List<Level>();
            var ll = new Level();
            ll.paths = pathsForUnits;
            ll.assignedUnits = units;
            ll.unitCount = units.Count;
        }
        int gateDifferenceDepth = GetSmallestDifference(pathsForUnits);

        var gates = new Dictionary<Vector2, List<UnitPathAssignmentFlowGraph>>();
        foreach (var path in pathsForUnits)
        {
            var gate = path._centerList[gateDifferenceDepth];
            if (gates.TryGetValue(gate, out var list))
            {
                list.Add(path);
            }
            else
            {
                gates[gate] = new List<UnitPathAssignmentFlowGraph>() { path };
            }
        }

        var levels = new List<Level>(gates.Count);
        foreach (var gate in gates)
        {
            var level = new Level();
            level.gateLocation = gate.Key;
            level.paths = gate.Value;
            var count = 0;
            foreach (var path in level.paths)
            {
                count += path._assignedUnits;
            }

            level.unitCount = count;
            levels.Add(level);
        }


        while (units.Count > 0)
        {
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index];
                var gateQueue = AddUnitsToQueue(units, level.gateLocation);

                var h = new HashSet<Unit>();
                if (level.TEMPunits == null)
                {
                    level.TEMPunits = new HashSet<Unit>();
                }
                var remainingUnitCount = level.unitCount - level.assignedUnits.Count;


                for (int i = 0; i < remainingUnitCount; i++)
                {
                    h.Add(gateQueue.Dequeue().item);
                }

                level.TEMPunits = h;
                level.TEMPwishedFor = h.ToArray();
            }


            foreach (var level in levels)
            {
                units.ExceptWith(level.TEMPunits);
            }
            //units contain the units that were not assigned (notTaken units)

            for (int i = 0; i < levels.Count; i++)
            {
                for (var j = 0; j < levels.Count; j++)
                {
                    if (i != j)
                    {
                        levels[i].TEMPunits.ExceptWith(levels[j].TEMPwishedFor);
                    }
                }
            }

            //level.units are taken exactly once

            foreach (var level in levels)
            {
                //save assigned units
                level.assignedUnits.UnionWith(level.TEMPunits);

                //units that we and someone else wants
                level.TEMPunits.SymmetricExceptWith(level.TEMPwishedFor);
            }

            var takenMultipleTimesDict = new Dictionary<Unit, List<int>>();
            for (var i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                foreach (var unitTakenMultipleTimes in level.TEMPunits)
                {
                    if (takenMultipleTimesDict.TryGetValue(unitTakenMultipleTimes, out var list))
                    {
                        list.Add(i);
                    }
                    else
                    {
                        takenMultipleTimesDict[unitTakenMultipleTimes] = new List<int>() { i };
                    }
                }
            }

            //process all once taken units
            foreach (var valuePair in takenMultipleTimesDict)
            {
                var unit = valuePair.Key;
                var ii = valuePair.Value;

                var i = GetClosestGateIndex(unit, ii, levels);
                levels[i].assignedUnits.Add(unit);
            }
        }

        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            //sanity check
            level.TEMPunits = null;
            level.TEMPwishedFor = null;
            Debug.Assert(level.unitCount == level.assignedUnits.Count, "level.unitCount == level.assignedUnits.Count");
        }

        return levels;
    }

    private static int GetClosestGateIndex(Unit unit, List<int> ints, List<Level> levels)
    {
        float distance = Single.MaxValue;
        int min = -1;

        foreach (var i in ints)
        {
            var d = Vector2.Distance(unit.position, levels[i].gateLocation);
            if (d < distance)
            {
                distance = d;
                min = i;
            }
        }
        return min;
    }


    private static void assigner(List<UnitPathAssignmentFlowGraph> pathsForUnits, HashSet<Unit> units, List<List<Vector2>> gridPaths, bool isWarmUp)
    {

        var level = GetLevel(pathsForUnits, units);
        foreach (var l in level)
        {
            if (l.paths.Count == 1)
            {
                AssignPathToUnits(l.paths[0], l.assignedUnits, gridPaths, isWarmUp);
                units.ExceptWith(l.assignedUnits);
                continue;
            }

            assigner(l.paths, l.assignedUnits, gridPaths, isWarmUp);
        }
    }

    private static void AssignPathToUnits(UnitPathAssignmentFlowGraph unitPathAssignment, HashSet<Unit> assignedUnits, List<List<Vector2>> gridPaths, bool isWarmUp)
    {
        int i = unitPathAssignment.indexOfGridPath;
        if (!isWarmUp)
        {
            foreach (var unit in assignedUnits)
            {
                unit.MoveAlongThePath(gridPaths[i]);
                unit.SetTarget(unitPathAssignment.target);
                unit.movementMode = MovementMode.PathFollowing;
                /*
                if (unitPathAssignment.indexOfGridPath == 1)
                {
                    unit.GetComponent<SpriteRenderer>().color = Color.blue;
                }
                */
            }
        }
    }

    /// <summary>
    /// Assigns to every path the required number of units to paths. With use of internal heuristics
    /// </summary>
    /// <param name="pathsForUnits">Paths to be assigned</param>
    internal static void AssignUnitPathsHeuristic(List<UnitPathAssignmentFlowGraph> pathsForUnits, bool isWarmUp)
    {
        //AssignPathsToUnitsHeuristicallyByPathCloseness(pathsForUnits);
        //AssignPathsToUnits(pathsForUnits);
        AssignPathsToUnitsHeuristically(pathsForUnits, isWarmUp);
    }

    /// <summary>
    /// For each unit sets one of the precomputed paths to follow.
    /// </summary>
    /// <param name="pathsForUnits"></param>
    private static void AssignPathsToUnitsHeuristically(List<UnitPathAssignmentFlowGraph> pathsForUnits, bool isWarmUp)
    {
        var units = GetAllUnits();
        pathsForUnits = RemoveZeroPaths(pathsForUnits);
        List<List<Vector2>> gridPaths = GetFilledPathWithAStarBetweenGates(pathsForUnits);
        for (int i = 0; i < pathsForUnits.Count; i++)
        {
            pathsForUnits[i].indexOfGridPath = i;
        }

        assigner(pathsForUnits, units, gridPaths, isWarmUp);
    }

    private static List<UnitPathAssignmentFlowGraph> RemoveZeroPaths(List<UnitPathAssignmentFlowGraph> pathsForUnits)
    {
        //remove extra paths
        List<UnitPathAssignmentFlowGraph> tmp = new List<UnitPathAssignmentFlowGraph>();
        foreach (var unitPath in pathsForUnits)
        {
            if (unitPath._assignedUnits == 0)
            {
                continue;
            }

            tmp.Add(unitPath);
        }
        pathsForUnits = tmp;
        return pathsForUnits;
    }

    /// <summary>
    /// Converts gate to gate path (in grid coordinates) to a path following unit path (also in grid coordinates) by filling every successive gate of the given path using AStar algorithm.
    /// </summary>
    /// <param name="gridPathsBetweenGates">the path in grid coordinates between gates or gate zone centers</param>
    /// <returns>new denser grid path along the original gate path</returns>
    private static List<List<Vector2>> GetFilledPathWithAStarBetweenGates(List<UnitPathAssignmentFlowGraph> pathsForUnits)
    {
        var gridPaths = new List<List<Vector2>>();
        foreach (var possiblePath in pathsForUnits)
        {
            gridPaths.Add(possiblePath._centerList);
            gridPaths[gridPaths.Count - 1].Add(possiblePath.target.Center);
        }

        //lets fill the path between gates for the units, with the aStar algorithm
        for (var index = 0; index < gridPaths.Count; index++)
        {
            var path = gridPaths[index];
            var aStarPath = new List<Vector2>() { path[0] };

            for (int k = 0; k < path.Count - 1; k++)
            {
                //depending on the graph this connection may not only be
                //gate-gate but gate-center-gate (and center-gate-center)
                var gate = path[k];
                var gate2 = path[k + 1];

                var pathBetweenGates = Pathfinding.ConstructPathAStar(gate, gate2, Pathfinding.StepDistance, 0.2f).ToList();


                //if (pathBetweenGates.Count > 3 && MapManager.FlowGraphType == FlowGraphTypeEnum.Zoneless)
                //{
                //    //soft  handover of paths between gates 
                //    var count = 1;
                //    pathBetweenGates.RemoveRange(pathBetweenGates.Count - count, count);
                //    pathBetweenGates.RemoveRange(0, count);
                //}

                //when stitching don't add dupes
                if (aStarPath.Count > 0 && pathBetweenGates.Count > 0 && aStarPath[aStarPath.Count - 1] == pathBetweenGates[0])
                {
                    aStarPath.RemoveAt(aStarPath.Count - 1);
                }
                aStarPath.AddRange(pathBetweenGates);

            }

            aStarPath.Add(path[path.Count - 1]);
            gridPaths[index] = aStarPath;
        }

        return gridPaths;
    }

    private static HashSet<Unit> GetAllUnits()
    {
        HashSet<Unit> units = new HashSet<Unit>(Simulator.Instance.unitMovementManager.units);
        return units;
    }
}

class Level
{
    public HashSet<Unit> TEMPunits = new HashSet<Unit>();
    public HashSet<Unit> assignedUnits = new HashSet<Unit>();
    public List<UnitPathAssignmentFlowGraph> paths;
    public Vector2 gateLocation;
    public int unitCount;
    internal Unit[] TEMPwishedFor;
}