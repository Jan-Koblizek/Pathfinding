using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class ExperimentResults
{ 
    public string experimentName;
    public string movementModeName;
    public int runNumber;
    public double preparationTime;
    public double pathfindingTime;
    public float finishedTime;
    public float finishedTime90;
    public GatesData gatesData;
    public UnitPathsData unitPathsData;
    public int[,] decomposition;

    public ExperimentResults()
    {
        gatesData = new GatesData();
        unitPathsData = new UnitPathsData();
    }

    public void WriteExperimentResultsToFile(string filePath, bool fail)
    {
        FileInfo fi = new FileInfo(filePath);
        Debug.Log(fi.FullName);
        if (!fi.Directory.Exists)
        {
            Directory.CreateDirectory(fi.DirectoryName);
        }
        StreamWriter writer = new StreamWriter(filePath);
        if (fail)
        {
            Debug.Log("Fail");
            writer.WriteLine("Fail");
            writer.Close();
            return;
        }
        writer.WriteLine($"Experiment Name: {experimentName}");
        writer.WriteLine($"Method Used: {movementModeName}");
        writer.WriteLine($"Run Number: {runNumber}");
        writer.WriteLine($"Preparation Time: {preparationTime}ms");
        writer.WriteLine($"Pathfinding Time: {pathfindingTime}ms");
        writer.WriteLine($"Finish Time: {finishedTime}ms");
        writer.WriteLine($"Finish Time 90%: {finishedTime90}ms");
        writer.WriteLine($"Repaths: {unitPathsData.repaths}");
        writer.WriteLine($"Soft Repaths: {unitPathsData.softRepaths}");

        writer.WriteLine("");
        writer.WriteLine($"Gates Data:");
        WriteGatesData(gatesData.gatesData, writer);

        writer.WriteLine("");
        writer.WriteLine($"Travel Distances:");
        bool first = true;
        foreach ((int unitID, float distance) in unitPathsData.travelDistances)
        {
            if (!first) writer.Write(",");
            else first = false;
            writer.Write($"[{unitID},{distance}]");
        }
        writer.WriteLine("");

        writer.WriteLine("");
        writer.WriteLine($"Finish Times:");
        first = true;
        foreach ((int unitID, float time) in unitPathsData.finishTimes)
        {
            if (!first) writer.Write(",");
            else first = false;
            writer.Write($"[{unitID},{time}]");
        }
        writer.WriteLine("");

        writer.WriteLine("");
        writer.WriteLine($"Stuck Times:");
        first = true;
        foreach ((int unitID, float stuckTime) in unitPathsData.timeStuck)
        {
            if (!first) writer.Write(",");
            else first = false;
            writer.Write($"[{unitID},{stuckTime}]");
        }
        writer.WriteLine("");

        writer.WriteLine("");
        writer.WriteLine($"Stuck Map:");
        for (int y = 0; y < unitPathsData.stuckHeatMap.GetLength(1); y++)
        {
            first = true;
            for (int x = 0; x < unitPathsData.stuckHeatMap.GetLength(0); x++)
            {
                if (!first) writer.Write(",");
                else first = false;
                writer.Write($"{unitPathsData.stuckHeatMap[x,y]}");
            }
            writer.WriteLine("");
        }

        writer.WriteLine("");
        writer.WriteLine($"Movement Positions Map:");
        for (int y = 0; y < unitPathsData.movementPositionsMap.GetLength(1); y++)
        {
            first = true;
            for (int x = 0; x < unitPathsData.movementPositionsMap.GetLength(0); x++)
            {
                if (!first) writer.Write(",");
                else first = false;
                writer.Write($"{unitPathsData.movementPositionsMap[x, y]}");
            }
            writer.WriteLine("");
        }
        writer.Close();
    }

    private void WriteGatesData(Dictionary<int, GateData> gatesData, StreamWriter writer)
    {
        foreach ((int gateID,  GateData gateData) in gatesData)
        {
            writer.Write(gateID + ":");
            WriteGateData(gateData.UnitArrivals, writer);
            writer.WriteLine("");
        }
    }

    private void WriteGateData(Dictionary<int, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition)> data, StreamWriter writer)
    {
        bool first = true;
        foreach ((int unitID, (float ArrivalTime, float distanceFromStart, int StreamID, Vector2 ArrivalPosition) arrivalInfo) in data)
        {
            if (!first) writer.Write(",");
            else first = false;

            writer.Write($"[{unitID},[{arrivalInfo.ArrivalTime},{arrivalInfo.distanceFromStart},{arrivalInfo.StreamID},[{arrivalInfo.ArrivalPosition.x},{arrivalInfo.ArrivalPosition.y}]]]");
        }
    }

    public void WriteGateAnalysisToFile(string filePath) 
    {
        FileInfo fi = new FileInfo(filePath);
        if (!fi.Directory.Exists)
        {
            Directory.CreateDirectory(fi.DirectoryName);
        }
        StreamWriter writer = new StreamWriter(filePath);
        Debug.Log($"Gate Analysis Count {gatesData.gatesAnalysis.Count}");
        for (int i = 0; i < gatesData.gatesAnalysis.Count; i++)
        {
            GateAnalysis gateAnalysis = gatesData.gatesAnalysis[i];
            writer.WriteLine($"Gate ID: {gateAnalysis.GateID}");
            writer.WriteLine($"Flow Capacity: {gateAnalysis.FlowCapacity}");
            writer.WriteLine($"Gate Width: {gateAnalysis.Width}");
            writer.Write("Flow Streams:");
            WriteFlowStreams(gateAnalysis.FlowStreams, writer);
            writer.Write("Joined Flow Streams:");
            WriteJoinedFlowStreams(gateAnalysis.joinedStreams, writer);
            writer.Write("Nearby Gates:");
            WriteNearbyGates(gateAnalysis.nearbyGates, writer);
            writer.Write("Nearby Gate Distances:");
            WriteNearbyGateDistances(gateAnalysis.nearbyGateDistances, writer);
        }
        writer.Close();
    }

    public void WriteDecompositionToFile(string filePath)
    {
        FileInfo fi = new FileInfo(filePath);
        if (!fi.Directory.Exists)
        {
            Directory.CreateDirectory(fi.DirectoryName);
        }
        StreamWriter writer = new StreamWriter(filePath);

        for (int y = 0; y < decomposition.GetLength(1); y++)
        {
            List<string> line = new List<string>();
            for (int x = 0; x < decomposition.GetLength(0); x++)
            {
                line.Add(decomposition[x, y].ToString());
            }
            writer.WriteLine(String.Join(',', line));
        }
        writer.Close();
    }

    private void WriteNearbyGates(List<int> gates, StreamWriter writer)
    {
        bool first = true;
        foreach (int gateID in gates)
        {
            if (!first) writer.Write(",");
            else first = false;
            writer.Write(gateID);
        }
        writer.WriteLine("");
    }

    private void WriteNearbyGateDistances(List<float> gateDistances, StreamWriter writer)
    {
        bool first = true;
        foreach (float distance in gateDistances)
        {
            if (!first) writer.Write(",");
            else first = false;
            writer.Write(distance);
        }
        writer.WriteLine("");
    }

    private void WriteFlowStreams(Dictionary<int, (float DistanceToGoal, float expectedStartTime, float expectedFinishTime, List<(float, float)> expectedFlow)> flowStreams, StreamWriter writer)
    {
        bool first = true;
        foreach ((int streamID, (float DistanceToGoal, float expectedStartTime, float expectedFinishTime, List<(float, float)> expectedFlow) streamInfo) in flowStreams)
        {
            if (!first) writer.Write(",");
            else first = false;

            bool firstExpectedFlow = true;
            string expectedFlowString = "";
            for (int i = 0; i < streamInfo.expectedFlow.Count; i++)
            {
                if (!firstExpectedFlow) expectedFlowString += ",";
                else
                {
                    firstExpectedFlow = false;
                }
                expectedFlowString += $"[{streamInfo.expectedFlow[i].Item1},{streamInfo.expectedFlow[i].Item2}]";
            }

            writer.Write($"[{streamID},[{streamInfo.DistanceToGoal},{streamInfo.expectedStartTime},{streamInfo.expectedFinishTime},[{expectedFlowString}]]]");
        }
        writer.WriteLine("");
    }

    private void WriteJoinedFlowStreams(List<List<int>> joinedStreams, StreamWriter writer)
    {
        for (int i = 0; i < joinedStreams.Count; i++)
        {
            if (i != 0) writer.Write(",");
            writer.Write("[");
            for (int j = 0; j < joinedStreams[i].Count; j++)
            {
                if (j != 0) writer.Write(",");
                writer.Write(joinedStreams[i][j]);
            }
            writer.Write("]");
        }
        writer.WriteLine("");
    }
}
