using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitPathsData
{
    public int repaths;
    public int softRepaths;
    public Dictionary<int, List<Vector2>> unitPaths { get; set; }
    public Dictionary<int, float> finishTimes { get; set; }
    public Dictionary<int, float> timeStuck { get; private set; }
    public float[,] stuckHeatMap { get; private set; }
    public float[,] movementPositionsMap { get; private set; }
    public Dictionary<int, float> travelDistances { get; private set; }

    public UnitPathsData() {
        unitPaths = new Dictionary<int, List<Vector2>>();
        finishTimes = new Dictionary<int, float>();
        timeStuck = new Dictionary<int, float>();
        travelDistances = new Dictionary<int, float>();
        repaths = 0;
        softRepaths = 0;
    }

    public void ComputeResults(int specificationID)
    {
        int mapWidth = Simulator.Instance.simulationSpecifications[specificationID].mapTexture.width;
        int mapHeight = Simulator.Instance.simulationSpecifications[specificationID].mapTexture.height;
        movementPositionsMap = new float[mapWidth, mapHeight];
        stuckHeatMap = new float[mapWidth, mapHeight];

        foreach((int unitID, List<Vector2> path) in unitPaths)
        {
            timeStuck[unitID] = 0;
            travelDistances[unitID] = 0;
            if (path.Count > 0)
            {
                IncrementWeighted(movementPositionsMap, path[0]);
            }
            for (int i = 1; i < path.Count; i++)
            {
                IncrementWeighted(movementPositionsMap, path[i]);
                float distance = Vector2.Distance(path[i-1], path[i]);
                travelDistances[unitID] += distance;
                if (distance < Unit.pathPositionsUpdatePeriod * SimulationSettings.instance.UnitSpeed * 0.5f)
                {
                    IncrementWeighted(stuckHeatMap, path[i-1], 0.5f);
                    IncrementWeighted(stuckHeatMap, path[i], 0.5f);
                    timeStuck[unitID] += Unit.pathPositionsUpdatePeriod;
                }
            }
        }
    }

    private void IncrementWeighted(float[,] map, Vector2 pos, float weight = 1.0f)
    {
        Coord ff = new Coord(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        Coord fc = new Coord(Mathf.FloorToInt(pos.x), Mathf.CeilToInt(pos.y));
        Coord cf = new Coord(Mathf.CeilToInt(pos.x), Mathf.FloorToInt(pos.y));
        Coord cc = new Coord(Mathf.CeilToInt(pos.x), Mathf.CeilToInt(pos.y));
        float strengthFF = 1.5f - Vector2.Distance(pos, new Vector2(ff.X, ff.Y));
        float strengthFC = 1.5f - Vector2.Distance(pos, new Vector2(fc.X, fc.Y));
        float strengthCF = 1.5f - Vector2.Distance(pos, new Vector2(cf.X, cf.Y));
        float strengthCC = 1.5f - Vector2.Distance(pos, new Vector2(cc.X, cc.Y));

        float strengthSum = strengthFF + strengthFC + strengthCF + strengthCC;

        if (ff.WithinBounds()) map[ff.X, ff.Y] += weight * (strengthFF / strengthSum);
        if (fc.WithinBounds()) map[fc.X, fc.Y] += weight * (strengthFC / strengthSum);
        if (cf.WithinBounds()) map[cf.X, cf.Y] += weight * (strengthCF / strengthSum);
        if (cc.WithinBounds()) map[cc.X, cc.Y] += weight * (strengthCC / strengthSum);
    }
}
