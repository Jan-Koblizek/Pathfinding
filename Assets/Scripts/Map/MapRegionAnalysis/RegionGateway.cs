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
        int count = gateTilesCoords.Count;
        if (gateTilesCoords.Count > 2)
        {
            bool previousNearWall = false;
            count = 0;
            for (int i = 0; i < gateTilesCoords.Count; i++)
            {
                var coord = gateTilesCoords[i];
                bool up = coordsInsideWall(coord.X, coord.Y + 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool down = coordsInsideWall(coord.X, coord.Y - 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool right = coordsInsideWall(coord.X + 1, coord.Y, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool left = coordsInsideWall(coord.X - 1, coord.Y, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);

                bool upRight = coordsInsideWall(coord.X + 1, coord.Y + 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool upLeft = coordsInsideWall(coord.X - 1, coord.Y + 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool downRight = coordsInsideWall(coord.X + 1, coord.Y - 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);
                bool downLeft = coordsInsideWall(coord.X - 1, coord.Y - 1, Simulator.Instance.decomposition.obstructionMap.GetLength(0),
                    Simulator.Instance.decomposition.obstructionMap.GetLength(1), ref Simulator.Instance.decomposition.obstructionMap);

                bool nearWall = up || down || right || left || upRight || upLeft || downRight || downLeft;

                if ((nearWall && !previousNearWall) || (!nearWall))
                {
                    count++;
                }
                previousNearWall = nearWall;
            }
        }
        Coord centre = GetCentralCoord();
        int depth = WaterDecomposition.GetPixelDepth(Simulator.Instance.decomposition.obstructionMap, centre);
        return Mathf.Min(count, Mathf.Min(depth * 2, smallerSize ? gateTilesCoords.Count - 1 : gateTilesCoords.Count));
    }

    private bool coordsInsideWall(int x, int y, int mapWidth, int mapHeight, ref bool[,] obstructionMap)
    {
        return !(x >= 0 && x < mapWidth && y >= 0 && y < mapHeight && obstructionMap[x, y]);
    }
}