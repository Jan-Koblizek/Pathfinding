using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegionGateway
{
    public int ID;
    public Coord start;
    public Coord end;

    public MapRegion regionA;
    public MapRegion regionB;

    public List<Coord> gateTilesCoords;
    public Vector2 regionADirection;
    public Vector2 regionBDirection;

    private bool smallerSize;

    public RegionGateway(Coord start, Coord end, MapRegion regionA, MapRegion regionB, int ID, bool smallerSize = false)
    {
        this.start = start;
        this.end = end;
        this.regionA = regionA;
        this.regionB = regionB;
        this.ID = ID;
    }

    public Vector2 GetCentralPosition()
    {
        return (start.GetWorldPosition() + end.GetWorldPosition()) / 2;
    }

    public Coord GetCentralCoord()
    {
        return gateTilesCoords[gateTilesCoords.Count / 2];
    }

    public float GetSize()
    {
        Coord centre = GetCentralCoord();
        int depth = WaterDecomposition.GetPixelDepth(Simulator.Instance.decomposition.obstructionMap, centre);
        return Mathf.Min(depth * 2, smallerSize ? gateTilesCoords.Count - 1 : gateTilesCoords.Count);
    }
}