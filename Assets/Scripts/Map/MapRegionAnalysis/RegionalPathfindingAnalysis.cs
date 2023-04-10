using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionalPathfindingAnalysis
{
    public RegionalDecomposition decomposition;
    public List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps;
    public Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesBetweenGates;

    public RegionalPathfindingAnalysis(RegionalDecomposition decomposition, List<(Vector2 flowDirection, float distanceToGate)[,]> flowMaps, Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesBetweenGates)
    {
        this.decomposition = decomposition;
        this.flowMaps = flowMaps;
        this.distancesBetweenGates = distancesBetweenGates;
    }
}

public class RegionalPath
{
    public RegionalPathfindingAnalysis regionalPathfindingAnalysis;
    public Dictionary<int, int> regionDirections;
    public Dictionary<int, int> gatewayDirections;
    public List<RegionGateway> gatewayPath;
    public List<Vector2> finalPath;
    public Coord goalCoord;

    public RegionalPath(RegionalPathfindingAnalysis regionalPathfindingAnalysis, Vector2 start, Vector2 goal)
    {
        this.regionalPathfindingAnalysis = regionalPathfindingAnalysis;
        (regionDirections, gatewayDirections, gatewayPath, finalPath) = RegionalPathfinding.ConstructRegionalPath(regionalPathfindingAnalysis, start, goal, RegionalPathfinding.SimpleRegionalHeuristic);
        goalCoord = Coord.CoordFromPosition(goal);
    }

    public RegionalPath() { }
}

public class RegionalPathfindingPathsAnalysis
{
    public RegionalDecomposition decomposition;
    public Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> gatewayPaths;
    public Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesBetweenGates;

    public RegionalPathfindingPathsAnalysis(RegionalDecomposition decomposition, Dictionary<RegionGateway, Dictionary<RegionGateway, List<Vector2>>> gatewayPaths, Dictionary<RegionGateway, Dictionary<RegionGateway, float>> distancesBetweenGates)
    {
        this.decomposition = decomposition;
        this.gatewayPaths = gatewayPaths;
        this.distancesBetweenGates = distancesBetweenGates;
    }
}

public class RegionalPathUsingSubPaths
{
    public RegionalPathfindingPathsAnalysis regionalPathfindingAnalysis;
    public Dictionary<int, int> regionDirections;
    public Dictionary<int, int> gatewayDirections;
    public List<RegionGateway> gatewayPath;
    public List<Vector2> finalPath;
    public List<Vector2> startingPath;
    public Coord goalCoord;

    public RegionalPathUsingSubPaths() { }
}