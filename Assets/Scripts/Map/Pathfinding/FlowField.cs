using UnityEngine;

public class FlowField
{
    private Vector2[,] flowField;
    public Vector2 getMovementDirection(Vector2 pos)
    {
        Coord ff = new Coord(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.y));
        Coord fc = new Coord(Mathf.FloorToInt(pos.x), Mathf.CeilToInt(pos.y));
        Coord cf = new Coord(Mathf.CeilToInt(pos.x), Mathf.FloorToInt(pos.y));
        Coord cc = new Coord(Mathf.CeilToInt(pos.x), Mathf.CeilToInt(pos.y));
        float strengthFF = 1.5f - Vector2.Distance(pos, new Vector2(ff.X, ff.Y));
        float strengthFC = 1.5f - Vector2.Distance(pos, new Vector2(fc.X, fc.Y));
        float strengthCF = 1.5f - Vector2.Distance(pos, new Vector2(cf.X, cf.Y));
        float strengthCC = 1.5f - Vector2.Distance(pos, new Vector2(cc.X, cc.Y));

        Vector2 flowFF = flowField[ff.X, ff.Y] * strengthFF;
        Vector2 flowFC = flowField[fc.X, fc.Y] * strengthFC;
        Vector2 flowCF = flowField[cf.X, cf.Y] * strengthCF;
        Vector2 flowCC = flowField[cc.X, cc.Y] * strengthCC;

        return (flowFF + flowFC + flowCF + flowCC).normalized;
    }

    public FlowField(Coord targetCoord)
    {
        flowField = Pathfinding.ConstructFlowField(targetCoord);
    }
}
