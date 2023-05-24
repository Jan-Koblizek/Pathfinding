using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GatesData
{
    public Dictionary<int, GateData> gatesData = new Dictionary<int, GateData>();
    public List<GateAnalysis> gatesAnalysis;
    public Dictionary<int, List<(int gateID, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition))>> unitGatePaths;

    public void GatesDataFromUnitPaths()
    {
        gatesData = new Dictionary<int, GateData>();
        foreach ((int unitID, List<(int gateID, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition))> gatePath) in unitGatePaths)
        {
            foreach ((int gateID, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition)) in gatePath)
            {
                if (!gatesData.ContainsKey(gateID))
                    gatesData[gateID] = new GateData();
                gatesData[gateID].UnitArrivals[unitID] = (ArrivalTime, distanceFromStart, StreamID, ArrivalPosition);
            }
        }
    }
}

//Method and run specific
public class GateData 
{
    public Dictionary<int, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition)> UnitArrivals;

    public GateData()
    {
        UnitArrivals = new Dictionary<int, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition)>();
    }
}

//Common for all the methods and runs
public class GateAnalysis
{
    public int GateID;
    public float FlowCapacity;
    public float Width;
    public Dictionary<int, (float DistanceToGoal, float expectedStartTime, float expectedFinishTime, List<(float, float)> expectedFlow)> FlowStreams;
    public List<List<int>> joinedStreams;

    public GateAnalysis()
    {
        FlowStreams = new Dictionary<int, (float DistanceToGoal, float expectedStartTime, float expectedFinishTime, List<(float, float)> expectedFlow)>();
        joinedStreams = new List<List<int>>();
    }
}