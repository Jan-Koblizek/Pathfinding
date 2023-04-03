using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class FlowFieldSupremeCommander
{
    public MapRegionSupremeCommander goalRegion;
    public (Vector2 flowDirection, float distanceToGate)[,] goalRegionFlowMap;
    public Vector2 goalPosition;
    public GateSupremeCommander[,] gateTargetsForRegions;

    public Vector2 GetFlowFieldDirection(Vector2 pos)
    {
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

    private Vector2 getFlowFieldDirection(Coord coord)
    {
        MapRegionSupremeCommander region = MapRegionsSupremeCommander.Instance.getRegionFromCoord(coord);
        if (region == null)
        {
            return Vector2.zero;
        }
        else if (region == goalRegion)
        {
            int X = coord.X % MapRegionSupremeCommander.RegionSize;
            int Y = coord.Y % MapRegionSupremeCommander.RegionSize;
            return goalRegionFlowMap[X, Y].flowDirection;
        }
        else
        {
            int X = coord.X % MapRegionSupremeCommander.RegionSize;
            int Y = coord.Y % MapRegionSupremeCommander.RegionSize;
            if (gateTargetsForRegions[region.X, region.Y] != null)
            {
                return region.flowMaps[gateTargetsForRegions[region.X, region.Y]][X, Y].flowDirection;
            }
            else
            {
                return Vector2.zero;
            }
        }
    }
}
