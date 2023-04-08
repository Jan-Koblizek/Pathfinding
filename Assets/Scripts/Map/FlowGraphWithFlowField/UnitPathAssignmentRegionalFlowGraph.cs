using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitPathAssignmentRegionalFlowGraph
{
    public readonly int _assignedUnits;
    public readonly float _flow;
    private readonly PathID _pathID;
    private readonly List<Vector2> _centerList;
    private readonly List<FlowNode> nodes;
    public int indexOfGridPath;

    /// <summary>
    /// For a path holds gate centers list and number of units
    /// </summary>
    /// <param name="assignedUnits"></param>
    /// <param name="pathID"></param>
    /// <param name="centerList"></param>
    public UnitPathAssignmentRegionalFlowGraph(int assignedUnits, PathID pathID, List<Vector2> centerList, FlowGraph flowGraph)
    {
        _flow = PlannerPath.GetPathById(pathID).Flow;
        _assignedUnits = assignedUnits;
        _pathID = pathID;
        List<NodeID> nodeIDs = PlannerPath.GetPathById(pathID).Path;
        nodes = new List<FlowNode>();
        for (int i = 0; i < nodeIDs.Count; i++)
        {
            nodes.Add(flowGraph.GetFlowNodeByID(nodeIDs[i].nodeID));
        }
        _centerList = centerList;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pathsForUnits">paths to create levels from</param>
    /// <param name="units">units to assign</param>
    /// <param name="gateDifferenceDepth">the gate to do the assigment to</param>
    /// <returns></returns>
    private static List<LevelRegional> GetLevel(RegionalFlowGraphPath regionalPath, HashSet<Unit> units, List<int> pathIndices, Dictionary<int, int> unitCounts)
    {
        //returns list of differences for given paths
        if (pathIndices.Count == 1)
        {
            var l = new List<LevelRegional>();
            var ll = new LevelRegional();
            ll.pathIndices = pathIndices;
            ll.assignedUnits = units;
            ll.unitCount = units.Count;
        }

        var gates = new Dictionary<Vector2, List<int>>();
        foreach (var pathIndex in pathIndices)
        {
            var gate = regionalPath.regionalPaths[pathIndex].gatewayPath[0].GetCentralPosition();
            if (gates.TryGetValue(gate, out var list))
            {
                list.Add(pathIndex);
            }
            else
            {
                gates[gate] = new List<int>() { pathIndex };
            }
        }
        var levels = new List<LevelRegional>(gates.Count);
        foreach (var gate in gates)
        {
            var level = new LevelRegional();
            level.gateLocation = gate.Key;
            level.pathIndices = gate.Value;
            var count = 0;
            foreach (int pathIndex in level.pathIndices)
            {
                count += unitCounts[pathIndex];
            }

            level.unitCount = count;
            levels.Add(level);
        }

        while (units.Count > 0)
        {
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index];
                PriorityQueue<float, Unit> gateQueue = AddUnitsToQueue(units, level.gateLocation);

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


    private static void assigner(RegionalFlowGraphPath regionalPath, HashSet<Unit> units, List<int> pathIndices, Dictionary<int, int> unitCounts)
    {
        var level = GetLevel(regionalPath, units, pathIndices, unitCounts);
        foreach (var l in level)
        {
            if (l.pathIndices.Count == 1)
            {
                AssignPathToUnits(regionalPath, l.assignedUnits, l.pathIndices[0]);
                units.ExceptWith(l.assignedUnits);
                continue;
            }

            assigner(regionalPath, l.assignedUnits, l.pathIndices, unitCounts);
        }
    }

    private static void AssignPathToUnits(RegionalFlowGraphPath regionalPath, HashSet<Unit> assignedUnits, int pathIndex)
    {
        foreach (var unit in assignedUnits)
        {
            unit.UseRegionalFlowGraphPath(regionalPath, pathIndex);
            unit.SetTarget(Simulator.Instance.target);
            unit.movementMode = MovementMode.RegionalFlowGraph;
        }
    }

    internal static RegionalFlowGraphPath CreateRegionalFlowGraphPath(List<UnitPathAssignmentRegionalFlowGraph> pathsForUnits, HashSet<Unit> units, bool isWarmUp)
    {
        pathsForUnits = RemoveZeroPaths(pathsForUnits);
        List<RegionalPath> gridPaths = GetRegionalPaths(pathsForUnits);
        for (int i = 0; i < pathsForUnits.Count; i++)
        {
            pathsForUnits[i].indexOfGridPath = i;
        }
        List <(RegionalPath path, float flow, int numberOfUnits)> regionalPaths = new List<(RegionalPath, float, int)>();

        for (int i = 0; i < pathsForUnits.Count; i++)
        {
            regionalPaths.Add((gridPaths[i], pathsForUnits[i]._flow, pathsForUnits[i]._assignedUnits));
        }

        RegionalFlowGraphPath regionalFlowGraphPath = new RegionalFlowGraphPath(regionalPaths);
        return regionalFlowGraphPath;
    }

    /// <summary>
    /// For each unit sets one of the precomputed paths to follow.
    /// </summary>
    /// <param name="pathsForUnits"></param>
    public static void AssignStartingPaths(RegionalFlowGraphPath regionalPath, HashSet<Unit> units)
    {
        List<int> paths = new List<int>();
        Dictionary<int, int> unitCounts = new Dictionary<int, int>();
        for (int i = 0; i < regionalPath.regionalPaths.Count; i++)
        {
            paths.Add(i);
        }

        int iter = 0;
        while (true)
        {
            int gateId = regionalPath.regionalPaths[paths[iter]].gatewayPath[0].ID;
            unitCounts[paths[iter]] = regionalPath.unitCounts[paths[iter]];
            for (int j = paths.Count-1; j > iter; j--)
            {
                int otherGateId = regionalPath.regionalPaths[paths[j]].gatewayPath[0].ID;
                if (otherGateId == gateId)
                {
                    unitCounts[paths[iter]] += regionalPath.unitCounts[paths[j]];
                    paths.RemoveAt(j);
                }
            }
            iter++;
            if (iter == paths.Count) break;
        }
        assigner(regionalPath, units, paths, unitCounts);
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
        for (int i = 0; i < pathsForUnits.Count; i++)
        {
            UnitPathAssignmentRegionalFlowGraph possiblePath = pathsForUnits[i];
            RegionalPath regionalPath = new RegionalPath();
            regionalPath.regionalPathfindingAnalysis = Simulator.Instance.regionalPathfinding;
            RegionGateway currentGateway = null;
            List<RegionGateway> regionGateways = new List<RegionGateway>();
            Dictionary<int, int> regionDirections = new Dictionary<int, int>();
            Dictionary<int, int> gatewayDirections = new Dictionary<int, int>();
            for (int j = 0; j < possiblePath.nodes.Count; j++) { 
                FlowNode node = possiblePath.nodes[j];
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
            regionalPath.gatewayPath = regionGateways;
            regionalPath.goalCoord = Coord.CoordFromPosition(possiblePath._centerList[possiblePath._centerList.Count - 1]);
            gridPaths.Add(regionalPath);
        }
        return gridPaths;
    }
}

class LevelRegional
{
    public HashSet<Unit> TEMPunits = new HashSet<Unit>();
    public HashSet<Unit> assignedUnits = new HashSet<Unit>();
    public List<int> pathIndices = new List<int>();
    public Vector2 gateLocation;
    public int unitCount;
    internal Unit[] TEMPwishedFor;
}