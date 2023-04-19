using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionalFlowGraphPathUsingSubPathsExecutor
{
    int pathIndex;
    public Unit unit;
    public RegionalFlowGraphPathUsingSubPaths regionalPath;

    private RegionalPathfindingPathsAnalysis analysis;
    private RegionalDecomposition decomposition;
    private PathExecutor finalPathExecutor;
    private PathExecutor regionalPathExecutor;
    private int goalRegion;
    private int lastRegion;
    private int lastDividerGate;
    private bool reachedFinalRegion = false;

    public RegionalFlowGraphPathUsingSubPathsExecutor(Unit unit, RegionalFlowGraphPathUsingSubPaths regionalPath, int pathIndex)
    {
        this.pathIndex = pathIndex;
        this.unit = unit;
        this.regionalPath = regionalPath;
        decomposition = regionalPath.regionalPaths[pathIndex].regionalPathfindingAnalysis.decomposition;
        goalRegion = decomposition.regionMap[regionalPath.regionalPaths[pathIndex].goalCoord.X, regionalPath.regionalPaths[pathIndex].goalCoord.Y];
        analysis = regionalPath.regionalPaths[0].regionalPathfindingAnalysis;
        regionalPathExecutor = new PathExecutor(unit, ref regionalPath.regionalPaths[pathIndex].startingPath);
        lastRegion = decomposition.regionMap[unit.currentCoord.X, unit.currentCoord.Y];
        lastDividerGate = -1;
    }

    public Vector2 GetSeekForce(float deltaTime)
    {
        int currentRegion = decomposition.regionMap[unit.currentCoord.X, unit.currentCoord.Y];
        if (!reachedFinalRegion &&
            regionalPath.regionalPaths[pathIndex].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
            regionalPath.regionalPaths[pathIndex].regionDirections.ContainsKey(currentRegion))
        {
            if (currentRegion != lastRegion)
            {
                Vector2 pos = unit.position;
                Coord ff = new Coord(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
                Coord fc = new Coord(Mathf.FloorToInt(pos.x), Mathf.CeilToInt(pos.y));
                Coord cf = new Coord(Mathf.CeilToInt(pos.x), Mathf.FloorToInt(pos.y));
                Coord cc = new Coord(Mathf.CeilToInt(pos.x), Mathf.CeilToInt(pos.y));

                int regionFF = decomposition.regionMap[ff.X, ff.Y];
                int regionFC = decomposition.regionMap[fc.X, fc.Y];
                int regionCF = decomposition.regionMap[cf.X, cf.Y];
                int regionCC = decomposition.regionMap[cc.X, cc.Y];


                if (decomposition.IsGate(regionFF) && lastDividerGate != regionFF &&
                regionalPath.regionToFlowDivider.ContainsKey(regionFF) &&
                regionalPath.regionToFlowDivider[regionFF].paths.FindIndex(x => x.id == pathIndex) > -1)
                {
                    lastDividerGate = regionFF;
                    pathIndex = regionalPath.regionToFlowDivider[regionFF].GetNextPathId(pos);
                }
                else if (decomposition.IsGate(regionFC) &&
                    regionalPath.regionToFlowDivider.ContainsKey(regionFC) && lastDividerGate != regionFC &&
                    regionalPath.regionToFlowDivider[regionFC].paths.FindIndex(x => x.id == pathIndex) > -1)
                {
                    lastDividerGate = regionFC;
                    pathIndex = regionalPath.regionToFlowDivider[regionFC].GetNextPathId(pos);
                }
                else if (decomposition.IsGate(regionCF) && lastDividerGate != regionCF &&
                    regionalPath.regionToFlowDivider.ContainsKey(regionCF) &&
                    regionalPath.regionToFlowDivider[regionCF].paths.FindIndex(x => x.id == pathIndex) > -1)
                {
                    lastDividerGate = regionCF;
                    pathIndex = regionalPath.regionToFlowDivider[regionCF].GetNextPathId(pos);
                }
                else if (decomposition.IsGate(regionCC) && lastDividerGate != regionCC &&
                    regionalPath.regionToFlowDivider.ContainsKey(regionCC) &&
                    regionalPath.regionToFlowDivider[regionCC].paths.FindIndex(x => x.id == pathIndex) > -1)
                {
                    lastDividerGate = regionCC;
                    pathIndex = regionalPath.regionToFlowDivider[regionCC].GetNextPathId(pos);
                }

                if (regionalPath.regionalPaths[pathIndex].regionDirections.ContainsKey(currentRegion))
                {
                    RegionGateway nextGateway = decomposition.mapRegions[currentRegion].gateways[regionalPath.regionalPaths[pathIndex].regionDirections[currentRegion]];
                    int index = regionalPath.regionalPaths[pathIndex].gatewayPath.IndexOf(nextGateway);
                    if (index > 0) {
                        RegionGateway previousGateway = regionalPath.regionalPaths[pathIndex].gatewayPath[index - 1];
                        regionalPathExecutor = new PathExecutor(unit, analysis.gatewayPaths[previousGateway][nextGateway]);
                    }
                }
                lastRegion = currentRegion;
            }
            return regionalPathExecutor.GetPathFollowingForce(deltaTime);
        }
        else if (reachedFinalRegion || currentRegion == goalRegion)
        {
            reachedFinalRegion = true;
            return followFinalPath(deltaTime);
        }
        else
        {
            for (int i = 0; i < regionalPath.regionalPaths.Count; i++)
            {
                if (regionalPath.regionalPaths[i].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
                    regionalPath.regionalPaths[i].regionDirections.ContainsKey(currentRegion))
                {
                    pathIndex = i;
                    break;
                }
            }
            Debug.Log("Zero seek");
            return new Vector2(0, 0);
        }
    }

    private Vector2 followFinalPath(float deltaTime)
    {
        if (finalPathExecutor == null)
            finalPathExecutor = new PathExecutor(unit, ref regionalPath.regionalPaths[pathIndex].finalPath);

        return finalPathExecutor.GetPathFollowingForce(deltaTime);
    }
}
