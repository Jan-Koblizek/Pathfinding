using UnityEngine;

public class RegionalPathExecutor
{
    public Unit unit;
    public RegionalPath regionalPath;

    private RegionalDecomposition decomposition;
    private PathExecutor finalPathExecutor;
    private int goalRegion;

    public RegionalPathExecutor(Unit unit, RegionalPath regionalPath)
    {
        this.unit = unit;
        this.regionalPath = regionalPath;
        decomposition = regionalPath.regionalPathfindingAnalysis.decomposition;
        goalRegion = decomposition.regionMap[regionalPath.goalCoord.X, regionalPath.goalCoord.Y];
    }

    private Vector2 getFlowFieldDirection(Coord coord)
    {
        int currentRegion = decomposition.regionMap[coord.X, coord.Y];
        if (regionalPath.regionDirections.ContainsKey(currentRegion))
        {
            int gateIndex = regionalPath.regionDirections[currentRegion];
            return regionalPath.regionalPathfindingAnalysis.flowMaps[gateIndex][unit.currentCoord.X, unit.currentCoord.Y].flowDirection;
        }
        else if (regionalPath.gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset))
        {
            int gatewayIndex = currentRegion - RegionalDecomposition.GatewayIndexOffset;
            if (regionalPath.gatewayDirections[gatewayIndex] == 0)
            {
                return decomposition.gateways[currentRegion - RegionalDecomposition.GatewayIndexOffset].regionADirection;
            }
            else
            {
                return decomposition.gateways[currentRegion - RegionalDecomposition.GatewayIndexOffset].regionBDirection;
            }
        }
        return new Vector2(0, 0);
    }

    public Vector2 GetSeekForce(float deltaTime)
    {
        int currentRegion = decomposition.regionMap[unit.currentCoord.X, unit.currentCoord.Y];
        if (regionalPath.gatewayDirections.ContainsKey(currentRegion - RegionalDecomposition.GatewayIndexOffset) ||
            regionalPath.regionDirections.ContainsKey(currentRegion))
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

            Vector2 flowFF = getFlowFieldDirection(ff) * strengthFF;
            Vector2 flowFC = getFlowFieldDirection(fc) * strengthFC;
            Vector2 flowCF = getFlowFieldDirection(cf) * strengthCF;
            Vector2 flowCC = getFlowFieldDirection(cc) * strengthCC;

            return (flowFF + flowFC + flowCF + flowCC).normalized;
        }
        else if (currentRegion == goalRegion)
        {
            return followFinalPath(deltaTime);
        }
        return new Vector2(0, 0);
    }

    private Vector2 followFinalPath(float deltaTime)
    {
        if (finalPathExecutor == null) finalPathExecutor = new PathExecutor(unit, ref regionalPath.finalPath);
        return finalPathExecutor.GetPathFollowingForce(deltaTime);
    }
}
