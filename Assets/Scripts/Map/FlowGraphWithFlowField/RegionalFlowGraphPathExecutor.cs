using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class RegionalFlowGraphPathExecutor
{
    int pathIndex;
    public Unit unit;
    public RegionalFlowGraphPath regionalPath;

    private RegionalDecomposition decomposition;
    private PathExecutor finalPathExecutor;
    private int goalRegion;
    private int lastRegion;
    private int lastDividerGate;
    private bool reachedFinalRegion = false;

    public RegionalFlowGraphPathExecutor(Unit unit, RegionalFlowGraphPath regionalPath, int pathIndex)
    {
        lastRegion = -1;
        lastDividerGate = -1;
        this.pathIndex = pathIndex;
        this.unit = unit;
        this.regionalPath = regionalPath;
        decomposition = regionalPath.regionalPaths[pathIndex].regionalPathfindingAnalysis.decomposition;
        goalRegion = decomposition.regionMap[regionalPath.regionalPaths[pathIndex].goalCoord.X, regionalPath.regionalPaths[pathIndex].goalCoord.Y];
    }

    private Vector2 getFlowFieldDirection(int currentRegion)
    {
        if (regionalPath.regionalPaths[pathIndex].regionDirections.ContainsKey(currentRegion))
        {
            int gateIndex = regionalPath.regionalPaths[pathIndex].regionDirections[currentRegion];
            return regionalPath.regionalPaths[pathIndex].regionalPathfindingAnalysis.flowMaps[gateIndex][unit.currentCoord.X, unit.currentCoord.Y].flowDirection;
        }
        else if (regionalPath.regionalPaths[pathIndex].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset))
        {
            int gatewayIndex = currentRegion - RegionalDecomposition.GatewayIndexOffset;
            if (regionalPath.regionalPaths[pathIndex].gatewayDirections[gatewayIndex] == 0)
            {
                return decomposition.gateways[gatewayIndex].regionADirection;
            }
            else
            {
                return decomposition.gateways[gatewayIndex].regionBDirection;
            }
        }
        else
        {
            return new Vector2(0, 0);
        }
    }

    public Vector2 GetSeekForce()
    {
        int currentRegion = decomposition.regionMap[unit.currentCoord.X, unit.currentCoord.Y];
        if (!reachedFinalRegion &&
            regionalPath.regionalPaths[pathIndex].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
            regionalPath.regionalPaths[pathIndex].regionDirections.ContainsKey(currentRegion))
        {
            Vector2 pos = unit.position;
            Coord ff = new Coord(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
            Coord fc = new Coord(Mathf.FloorToInt(pos.x), Mathf.CeilToInt(pos.y));
            Coord cf = new Coord(Mathf.CeilToInt(pos.x), Mathf.FloorToInt(pos.y));
            Coord cc = new Coord(Mathf.CeilToInt(pos.x), Mathf.CeilToInt(pos.y));
            float strengthFF = 1.5f - Vector2.Distance(pos, new Vector2(ff.X, ff.Y));
            float strengthFC = 1.5f - Vector2.Distance(pos, new Vector2(fc.X, fc.Y));
            float strengthCF = 1.5f - Vector2.Distance(pos, new Vector2(cf.X, cf.Y));
            float strengthCC = 1.5f - Vector2.Distance(pos, new Vector2(cc.X, cc.Y));

            int regionFF = decomposition.regionMap[ff.X, ff.Y];
            int regionFC = decomposition.regionMap[fc.X, fc.Y];
            int regionCF = decomposition.regionMap[cf.X, cf.Y];
            int regionCC = decomposition.regionMap[cc.X, cc.Y];

            if (currentRegion != lastRegion)
            {
                lastRegion = currentRegion;
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
            }

            Vector2 flowFF = getFlowFieldDirection(regionFF) * strengthFF;
            Vector2 flowFC = getFlowFieldDirection(regionFC) * strengthFC;
            Vector2 flowCF = getFlowFieldDirection(regionCF) * strengthCF;
            Vector2 flowCC = getFlowFieldDirection(regionCC) * strengthCC;

            return (flowFF + flowFC + flowCF + flowCC).normalized;
        }
        else if (reachedFinalRegion || currentRegion == goalRegion)
        {
            reachedFinalRegion = true;
            return followFinalPath();
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
            return new Vector2(0, 0);
        }
    }

    private Vector2 followFinalPath()
    {
        if (finalPathExecutor == null)
            finalPathExecutor = new PathExecutor(unit, regionalPath.regionalPaths[pathIndex].finalPath);
        
        return finalPathExecutor.GetPathFollowingForce();
    }
}
