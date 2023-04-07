using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitPathAssignmentRegionalFlowGraph : MonoBehaviour
{
    private readonly int _assignedUnits;
    private readonly PathID _pathID;
    private readonly List<Vector2> _centerList;
    private readonly List<FlowNode> nodes;
    public int indexOfGridPath;
    private Target target;

    /// <summary>
    /// For a path holds gate centers list and number of units
    /// </summary>
    /// <param name="assignedUnits"></param>
    /// <param name="pathID"></param>
    /// <param name="centerList"></param>
    public UnitPathAssignmentRegionalFlowGraph(int assignedUnits, PathID pathID, List<Vector2> centerList, Target target, FlowGraph flowGraph)
    {
        _assignedUnits = assignedUnits;
        _pathID = pathID;
        List<NodeID> nodeIDs = PlannerPath.GetPathById(pathID).Path;
        nodes = new List<FlowNode>();
        for (int i = 0; i < nodeIDs.Count; i++)
        {
            nodes.Add(flowGraph.GetFlowNodeByID(nodeIDs[i].nodeID));
        }
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

    private static int GetSmallestDifference(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits)
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
    private static List<LevelRegional> GetLevel(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits, HashSet<Unit> units)
    {
        //returns list of differences for given paths
        if (pathsForUnits.Count == 1)
        {
            var l = new List<LevelRegional>();
            var ll = new LevelRegional();
            ll.paths = pathsForUnits;
            ll.assignedUnits = units;
            ll.unitCount = units.Count;
        }
        int gateDifferenceDepth = GetSmallestDifference(pathsForUnits);

        var gates = new Dictionary<Vector2, List<UnitPathAssignmentRegionalFlowGraph>>();
        foreach (var path in pathsForUnits)
        {
            var gate = path._centerList[gateDifferenceDepth];
            if (gates.TryGetValue(gate, out var list))
            {
                list.Add(path);
            }
            else
            {
                gates[gate] = new List<UnitPathAssignmentRegionalFlowGraph>() { path };
            }
        }

        var levels = new List<LevelRegional>(gates.Count);
        foreach (var gate in gates)
        {
            var level = new LevelRegional();
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

    private static int GetClosestGateIndex(Unit unit, List<int> ints, List<LevelRegional> levels)
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


    private static void assigner(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits, HashSet<Unit> units, List<RegionalPath> gridPaths, bool isWarmUp)
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

    private static void AssignPathToUnits(UnitPathAssignmentRegionalFlowGraph unitPathAssignment, HashSet<Unit> assignedUnits, List<RegionalPath> gridPaths, bool isWarmUp)
    {
        int i = unitPathAssignment.indexOfGridPath;
        if (!isWarmUp)
        {
            foreach (var unit in assignedUnits)
            {
                unit.UseRegionalPath(gridPaths[i]);
                unit.SetTarget(unitPathAssignment.target);
                unit.movementMode = MovementMode.RegionalPath;
            }
        }
    }

    /// <summary>
    /// Assigns to every path the required number of units to paths. With use of internal heuristics
    /// </summary>
    /// <param name="pathsForUnits">Paths to be assigned</param>
    internal static void AssignUnitPathsHeuristic(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits, bool isWarmUp)
    {
        //AssignPathsToUnitsHeuristicallyByPathCloseness(pathsForUnits);
        //AssignPathsToUnits(pathsForUnits);
        AssignPathsToUnitsHeuristically(pathsForUnits, isWarmUp);
    }

    /// <summary>
    /// For each unit sets one of the precomputed paths to follow.
    /// </summary>
    /// <param name="pathsForUnits"></param>
    private static void AssignPathsToUnitsHeuristically(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits, bool isWarmUp)
    {
        var units = GetAllUnits();
        pathsForUnits = RemoveZeroPaths(pathsForUnits);
        List<RegionalPath> gridPaths = GetRegionalPaths(pathsForUnits);
        for (int i = 0; i < pathsForUnits.Count; i++)
        {
            pathsForUnits[i].indexOfGridPath = i;
        }

        assigner(pathsForUnits, units, gridPaths, isWarmUp);
    }

    private static List<UnitPathAssignmentRegionalFlowGraph> RemoveZeroPaths(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits)
    {
        //remove extra paths
        List<UnitPathAssignmentRegionalFlowGraph> tmp = new List<UnitPathAssignmentRegionalFlowGraph>();
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
    private static List<RegionalPath> GetRegionalPaths(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits)
    {
        var gridPaths = new List<RegionalPath>();
        foreach (var possiblePath in pathsForUnits)
        {
            RegionalPath regionalPath = new RegionalPath();
            regionalPath.regionalPathfindingAnalysis = Simulator.Instance.regionalPathfinding;
            RegionGateway currentGateway = null;
            List<RegionGateway> regionGateways = new List<RegionGateway>();
            Dictionary<int, int> regionDirections = new Dictionary<int, int>();
            Dictionary<int, int> gatewayDirections = new Dictionary<int, int>();
            for (int i = 0; i < possiblePath.nodes.Count; i++) { 
                FlowNode node = possiblePath.nodes[i];
                if (node.ChokePoint != null && (currentGateway == null || currentGateway.ID != node.ChokePoint.ID))
                {
                    currentGateway = node.ChokePoint;
                    regionGateways.Add(currentGateway);
                    MapRegion currentRegion = regionalPath.regionalPathfindingAnalysis.decomposition.mapRegions[node.regionId];
                    regionDirections[node.regionId] = currentRegion.gateways.IndexOf(currentGateway);
                    gatewayDirections[currentGateway.ID] = currentGateway.regionA != currentRegion ? 0 : 1;
                }
            }
            List<Vector2> finalPath = Pathfinding.ConstructPathAStar(
                            regionGateways[regionGateways.Count - 1].GetCentralPosition(), 
                            possiblePath._centerList[possiblePath._centerList.Count - 1], 
                            Pathfinding.StepDistance, 0.2f).ToList();
            regionalPath.finalPath = finalPath;
            regionalPath.regionDirections = regionDirections;
            regionalPath.gatewayDirections = gatewayDirections;
            regionalPath.goalCoord = Coord.CoordFromPosition(possiblePath._centerList[possiblePath._centerList.Count - 1]);
            gridPaths.Add(regionalPath);
        }
        return gridPaths;
    }

    private static HashSet<Unit> GetAllUnits()
    {
        HashSet<Unit> units = new HashSet<Unit>(Simulator.Instance.unitMovementManager.units);
        return units;
    }
}

class LevelRegional
{
    public HashSet<Unit> TEMPunits = new HashSet<Unit>();
    public HashSet<Unit> assignedUnits = new HashSet<Unit>();
    public List<UnitPathAssignmentRegionalFlowGraph> paths;
    public Vector2 gateLocation;
    public int unitCount;
    internal Unit[] TEMPwishedFor;
}