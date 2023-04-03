using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapRegion
{
    public int ID;
    public List<RegionGateway> gateways;
    public Coord centre;

    public MapRegion(int iD, Coord coord)
    {
        ID = iD;
        gateways = new List<RegionGateway>();
        centre = coord;
    }
}
