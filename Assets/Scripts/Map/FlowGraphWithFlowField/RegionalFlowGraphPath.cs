using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RegionalFlowGraphPath
{
    public List<RegionalPath> regionalPaths;
    public List<int> unitCounts;
    public List<float> pathFlows;

    public Dictionary<int, PathFlowDivider> regionToFlowDivider;

    public RegionalFlowGraphPath(List<(RegionalPath regionalPath, float flow, int numberOfUnits)> paths) {
        List<List<int>> gatewayOrders = new List<List<int>>();
        regionalPaths = new List<RegionalPath>();
        unitCounts = new List<int>();
        pathFlows = new List<float>();

        for (int i = 0; i < paths.Count; i++)
        {
            regionalPaths.Add(paths[i].regionalPath);
            unitCounts.Add(paths[i].numberOfUnits);
            pathFlows.Add(paths[i].flow);
            gatewayOrders.Add(new List<int>());
            for (int j = 0; j < paths[i].regionalPath.gatewayPath.Count; j++)
            {
                gatewayOrders[i].Add(paths[i].regionalPath.gatewayPath[j].ID);
            }
        }

        Dictionary<int, HashSet<int>> lastCommonGates = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < paths.Count; i++)
        {
            for (int j = 0; j < paths.Count; j++)
            {
                if (j!=i)
                {
                    int k = 0;
                    bool onCommonPath = false;
                    int otherPosition = 0;
                    while (k < gatewayOrders[i].Count)
                    {
                        if (!onCommonPath)
                        {
                            int index = gatewayOrders[j].IndexOf(gatewayOrders[i][k]);
                            if (index != -1)
                            {
                                otherPosition = index;
                                onCommonPath = true;
                            }
                        }
                        else if (onCommonPath)
                        {
                            otherPosition++;
                            try
                            {
                                if (gatewayOrders[i][k] != gatewayOrders[j][otherPosition])
                                {
                                    Utilities.AddToHashsetDictionary(lastCommonGates, gatewayOrders[i][k - 1], i);
                                    Utilities.AddToHashsetDictionary(lastCommonGates, gatewayOrders[i][k - 1], j);
                                    onCommonPath = false;
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.Log(gatewayOrders.Count);
                                Debug.Log(gatewayOrders[i].Count);
                                Debug.Log(gatewayOrders[j].Count);
                                Debug.Log(k);
                                Debug.Log(otherPosition);
                                throw e;
                            }
                        }
                        k++;
                    }
                }
            }
        }

        regionToFlowDivider = new Dictionary<int, PathFlowDivider>();
        foreach (KeyValuePair<int, HashSet<int>> lastCommonGate in lastCommonGates)
        {
            int gateID = lastCommonGate.Key;
            List<RegionGateway> goalGates = new List<RegionGateway>();
            List<int> pathIds = new List<int>();
            List< float > pathFlows = new List<float>();
            List<int> pathUnitCounts = new List<int>();
            List<int> pathsToDivide = lastCommonGate.Value.ToList();
            RegionGateway gateway = regionalPaths[pathsToDivide[0]].gatewayPath.Find(x => x.ID == gateID);

            for (int i = 0; i < pathsToDivide.Count; i++)
            {
                int pathIndex = pathsToDivide[i];
                int gateIndex = regionalPaths[pathIndex].gatewayPath.FindIndex(x => x.ID == gateID);
                goalGates.Add(regionalPaths[pathIndex].gatewayPath[gateIndex + 1]);
                pathIds.Add(pathIndex);
                pathFlows.Add(this.pathFlows[pathIndex]);
                pathUnitCounts.Add(this.unitCounts[pathIndex]);
            }

            PathFlowDivider pathFlowDivider = new PathFlowDivider(gateway, goalGates, pathIds, pathFlows, pathUnitCounts);
            regionToFlowDivider[gateID + RegionalDecomposition.GatewayIndexOffset] = pathFlowDivider;
        }
    }
}
