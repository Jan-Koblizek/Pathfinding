using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionalDecomposition
{
    public const int GatewayIndexOffset = 1000000000;
    public List<RegionGateway> gateways;
    public List<MapRegion> mapRegions;
    public int[,] depthMap;
    public int numberOfClusters = 0;
    public int[,] regionMap;
    public bool[,] obstructionMap;
    
    public bool IsGate(int region)
    {
        return region >= 1000000000;
    }

    public RegionalDecomposition(List<RegionGateway> gateways, List<MapRegion> mapRegions, int[,] depthMap, int numberOfClusters, int[,] regionMap, bool[,] obstructionMap)
    {
        this.gateways = gateways;
        this.mapRegions = mapRegions;
        this.depthMap = depthMap;
        this.numberOfClusters = numberOfClusters;
        this.regionMap = regionMap;
        this.obstructionMap = obstructionMap;
    }
}
