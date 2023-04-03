using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapRegionsSupremeCommander
{
    public MapRegionSupremeCommander[,] regions;
    private static MapRegionsSupremeCommander instance;
    public static MapRegionsSupremeCommander Instance
    {
        get {
            if (instance == null)
            {
                instance = new MapRegionsSupremeCommander();
                instance.initialize();
            }
            return instance; 
        }
    }

    private MapRegionsSupremeCommander()
    {
        regions = new MapRegionSupremeCommander[Map.instance.tiles.GetLength(0) / 8, Map.instance.tiles.GetLength(1) / 8];
    }

    private void initialize()
    {
        for (int i = 0; i < regions.GetLength(0); i++)
        {
            for (int j = 0; j < regions.GetLength(1); j++)
            {
                regions[i, j] = new MapRegionSupremeCommander(i, j);
            }
        }
    }

    public MapRegionSupremeCommander getRegionFromCoord(Coord coord)
    {
        int X = coord.X / MapRegionSupremeCommander.RegionSize;
        int Y = coord.Y / MapRegionSupremeCommander.RegionSize;
        if (X >= 0 && X < regions.GetLength(0) &&
            Y >= 0 && Y < regions.GetLength(1))
        {
            return regions[X, Y];
        }
        else
        {
            return null;
        }
    }
}
