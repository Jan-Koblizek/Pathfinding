using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using utils;

public class PathFlowDivider
{
    public RegionGateway gateway;
    public List<DividerPathInfo> paths;
    private List<int> initialUnitCounts;
    private float totalFlow;
    private int unitsEntered = 0;
    //private List<int> initialUnitCounts;

    public PathFlowDivider(RegionGateway gateway, List<RegionGateway> goalGates, List<int> pathIds, List<float> pathFlows, List<int> pathUnitCounts)
    {
        this.gateway = gateway;
        paths = new List<DividerPathInfo>();
        initialUnitCounts = new List<int>(pathUnitCounts);
        for (int i = 0; i < goalGates.Count; i++)
        {
            RegionGateway gateway2 = goalGates[i];
            Vector2 gatewayDirection = (gateway.end.GetWorldPosition() - gateway.start.GetWorldPosition()).normalized;
            Vector2 directionBetweenGates = (gateway2.GetCentralPosition() - gateway.GetCentralPosition()).normalized;
            float directionFactor = Vector2.Dot(gatewayDirection, directionBetweenGates);
            DividerPathInfo info = new DividerPathInfo(pathIds[i], pathUnitCounts[i], pathFlows[i], 0.0f, directionFactor);
            paths.Add(info);
        }
        paths.Sort((x, y) => x.gateDirectionFactor.CompareTo(y.gateDirectionFactor));

        float pathFlowSum = pathFlows.Sum();
        float currentFlowSum = 0.0f;
        for (int i = 0; i < paths.Count; i++) {
            currentFlowSum += paths[i].flow;
            paths[i].divisionThreshold = currentFlowSum / pathFlowSum;
        }
        totalFlow = pathFlowSum;
    }

    public int GetNextPathId(Vector2 unitPosition)
    {
        Vector2 gateDirection = gateway.end.GetWorldPosition() - gateway.start.GetWorldPosition();
        Vector2 unitDirection = unitPosition - gateway.start.GetWorldPosition();
        Vector2 projected = unitDirection.ProjectOnto(gateDirection);
        float factor = projected.magnitude / gateDirection.magnitude;
        int id = 0;
        int i = 0;
        while ((i < paths.Count - 1) && factor > paths[i].divisionThreshold)
        {
            id++;
            i++;
        }
        UpdateCountsAndThresholds(id);
        return paths[i].id;
    }

    private void UpdateCountsAndThresholds(int id)
    {
        paths[id].unitCount--;
        if (paths[id].unitCount == 0)
        {
            float currentFlow = totalFlow - paths[id].flow;
            paths[id].flow = 0;
            totalFlow = currentFlow;
            if (currentFlow > 0.00001) {
                float currentFlowSum = 0.0f;
                for (int i = 0; i < paths.Count; i++)
                {
                    currentFlowSum += paths[i].flow;
                    paths[i].divisionThreshold = currentFlowSum / totalFlow;
                }
            }
        }
        else
        {
            float shift = Mathf.Max(1.0f / (unitsEntered + 10), 0.01f);
            float totalWeightedFlow = 0.0f;
            for (int i = 0; i < paths.Count; i++)
            {
                totalWeightedFlow += paths[i].flow * ((float)paths[i].unitCount / (float)initialUnitCounts[i]);
            }
            for (int i = 0; i < paths.Count; i++)
            {
                float flowUntilThreshold = 0.0f;
                if (i < id)
                {
                    for (int j = 0; j <= i; j++)
                    {
                        flowUntilThreshold += paths[j].flow * ((float)paths[j].unitCount / (float)initialUnitCounts[j]);
                    }
                    paths[i].divisionThreshold += (flowUntilThreshold / totalWeightedFlow) * shift;
                }
                if (i > id)
                {
                    for (int j = paths.Count-1; j >= i; j--)
                    {
                        flowUntilThreshold += paths[j].flow * ((float)paths[j].unitCount / (float)initialUnitCounts[j]);
                    }
                    paths[i-1].divisionThreshold -= (flowUntilThreshold / totalWeightedFlow) * shift;
                }
            }
        }
        unitsEntered++;
    }

    public class DividerPathInfo
    {
        public int id;
        public int unitCount;
        public float flow;
        public float divisionThreshold;
        public float gateDirectionFactor;

        public DividerPathInfo(int id, int unitCount, float flow, float divisionThreshold, float gateDistanceFactor)
        {
            this.id = id;
            this.unitCount = unitCount;
            this.flow = flow;
            this.divisionThreshold = divisionThreshold;
            this.gateDirectionFactor = gateDistanceFactor;
        }
    }
}