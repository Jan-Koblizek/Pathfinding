using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateSupremeCommander
{
    public (int X, int Y) regionA;
    public (int X, int Y) regionB;

    public List<GateSupremeCommander> neighboringGates;
    public Dictionary<GateSupremeCommander, float> distancesToNeighboringGates;

    public List<Coord> coordsA;
    public List<Coord> coordsB;

    public GateSupremeCommander((int X, int Y) regionA, (int X, int Y) regionB, List<Coord> tilesA, List<Coord> tilesB)
    {
        this.regionA = regionA;
        this.regionB = regionB;
        this.coordsA = tilesA;
        this.coordsB = tilesB;
        neighboringGates = new List<GateSupremeCommander>();
        distancesToNeighboringGates = new Dictionary<GateSupremeCommander, float>();
    }

    public List<Coord> GetCentreCoords(int X, int Y)
    {
        List<Coord> tiles = new List<Coord>();
        List<Coord> gateCoords = GetGateCoords(X, Y);
        if (gateCoords.Count % 2 == 0)
        {
            tiles.Add(gateCoords[(gateCoords.Count / 2) - 1]);
            tiles.Add(gateCoords[gateCoords.Count / 2]);
        }
        else
        {
            tiles.Add(gateCoords[(gateCoords.Count - 1) / 2]);
        }
        return tiles;
    }

    public List<Coord> GetGateCoords(int X, int Y)
    {
        List<Coord> gateCoords = new List<Coord>();
        if (regionA.X == X && regionA.Y == Y) gateCoords = coordsA;
        else if (regionB.X == X && regionB.Y == Y) gateCoords = coordsB;
        else throw new WrongGateRegionCoordinates("The coordinates supplied do not correspond to the regions around the gate");
        return gateCoords;
    }
}

public class WrongGateRegionCoordinates : System.Exception
{
    public WrongGateRegionCoordinates() { }
    public WrongGateRegionCoordinates(string message)
        : base(message) { }
    public WrongGateRegionCoordinates(string message, System.Exception inner)
        : base(message, inner) { }
}
