using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class RegionalFlowGraphPathExecutor
{
    public int pathIndex { get; private set; }
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

    public Vector2 GetSeekForce(float deltaTime)
    {
        /*
        if (pathIndex == 0)
            Simulator.Instance.unitMovementManager.unitsToVisualizations[unit].SetColor(Color.red);
        if (pathIndex == 1)
            Simulator.Instance.unitMovementManager.unitsToVisualizations[unit].SetColor(Color.blue);
        if (pathIndex == 2)
            Simulator.Instance.unitMovementManager.unitsToVisualizations[unit].SetColor(Color.green);
        */
        int currentRegion = decomposition.regionMap[unit.currentCoord.X, unit.currentCoord.Y];
        if (!reachedFinalRegion &&
            regionalPath.regionalPaths[pathIndex].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
            regionalPath.regionalPaths[pathIndex].regionDirections.ContainsKey(currentRegion))
        {
            Vector2 pos = unit.position;
            Coord ff = new Coord(Mathf.Clamp(Mathf.FloorToInt(pos.x), 0, decomposition.regionMap.GetLength(0) - 1), Mathf.Clamp(Mathf.FloorToInt(pos.y), 0, decomposition.regionMap.GetLength(1) - 1));
            Coord fc = new Coord(Mathf.Clamp(Mathf.FloorToInt(pos.x), 0, decomposition.regionMap.GetLength(0) - 1), Mathf.Clamp(Mathf.CeilToInt(pos.y), 0, decomposition.regionMap.GetLength(1) - 1));
            Coord cf = new Coord(Mathf.Clamp(Mathf.CeilToInt(pos.x), 0, decomposition.regionMap.GetLength(0) - 1), Mathf.Clamp(Mathf.FloorToInt(pos.y), 0, decomposition.regionMap.GetLength(1) - 1));
            Coord cc = new Coord(Mathf.Clamp(Mathf.CeilToInt(pos.x), 0, decomposition.regionMap.GetLength(0) - 1), Mathf.Clamp(Mathf.CeilToInt(pos.y), 0, decomposition.regionMap.GetLength(1) - 1));
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
            return followFinalPath(deltaTime);
        }
        else
        {
            bool corrected = false;
            for (int i = 0; i < regionalPath.regionalPaths.Count; i++)
            {
                if (regionalPath.regionalPaths[i].gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
                    regionalPath.regionalPaths[i].regionDirections.ContainsKey(currentRegion))
                {
                    unit.softRepaths++;
                    pathIndex = i;
                    corrected = true;
                    break;
                }
            }
            if (!corrected)
            {
                //Debug.Log(currentRegion);
                unit.repaths++;
                List<Vector2> finalPath = regionalPath.regionalPaths[pathIndex].finalPath;
                unit.MoveAlongThePath(Pathfinding.ConstructPathAStar(unit.position, finalPath[finalPath.Count - 1], Pathfinding.StepDistance, 0.2f)?.ToList());
                unit.movementMode = MovementMode.PathFollowing;
            }
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
