using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Search;
using UnityEngine;
using utils;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;

public class WaterDecomposition
{
    private static List<List<Coord>> circleMap;
    public RegionalDecomposition Decompose(bool[,] mapSource, int wallThreshold)
    {
        List<RegionGateway> gateways;
        List<MapRegion> mapRegions;
        int[,] depthMap;
        int numberOfClusters = 0;
        int[,] regionMap;
        int mapWidth;
        int mapHeight;
        int gateRegionIndex = RegionalDecomposition.GatewayIndexOffset;

        mapRegions = new List<MapRegion>();
        mapWidth = mapSource.GetLength(0);
        mapHeight = mapSource.GetLength(1);
        bool[,] obstructionMap = new bool[mapWidth, mapHeight];
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                obstructionMap[x, y] = mapSource[x, y];
            }
        }

        int maxDepthUsed;
        buildDepthMap(obstructionMap, wallThreshold, mapWidth, mapHeight, out depthMap, out maxDepthUsed);
        buildRegionsMap(obstructionMap, maxDepthUsed, mapWidth, mapHeight, ref depthMap, out regionMap, out mapRegions, out numberOfClusters);
        refineRegions(obstructionMap, mapWidth, mapHeight, ref regionMap, ref depthMap, ref mapRegions);
        buildGates(obstructionMap, mapWidth, mapHeight, ref regionMap, out gateways, ref mapRegions);
        refineGates(obstructionMap, ref gateways, gateRegionIndex, ref regionMap);
        return new RegionalDecomposition(gateways, mapRegions, depthMap, numberOfClusters, regionMap, obstructionMap);
    }

    private void buildDepthMap(bool[,] map, int wallThreshold, int mapWidth, int mapHeight, out int[,] depthMap, out int maxDepthUsed)
    {
        bool dynamicWallThreshold = false;
        if (wallThreshold <= 0)
        {
            wallThreshold = 1;
            dynamicWallThreshold = true;
        }

        /************BUILD CIRCLE MAP************
         *
         */

        circleMap = new List<List<Coord>>();
        int maxDist = ((Mathf.Min(mapWidth, mapHeight) / 2) + 1);

        for (int i = 0; i <= maxDist; i++)
        {
            circleMap.Add(new List<Coord>());
        }

        for (int i = 0; i <= maxDist; i++)
        {
            for (int j = 1; j <= maxDist; j++)
            {
                int distance = Mathf.RoundToInt(octileDistance(i, j));
                if (distance <= maxDist)
                {
                    circleMap[distance].Add(new Coord(i, j));
                }
            }
        }

        /************BUILD DEPTH MAP************
         *
         */

        depthMap = new int[mapWidth, mapHeight];
        maxDepthUsed = 0;
        for (int i = 0; i < mapWidth; i++)
        {
            int distance = 0;
            for (int j = 0; j < mapHeight; j++)
            {
                int counter = 0;
                int hitFirstWallDist = 0;

                if (map[i, j])
                {
                    bool hitWall = false;
                    depthMap[i, j] = maxDist;
                    while (!hitWall)
                    {
                        // Use circleMap to fill in height for tile
                        for (int it = 0; it < circleMap[distance].Count; it++)
                        {
                            //counter = 0;
                            int cX = circleMap[distance][it].X; //circleX
                            int cY = circleMap[distance][it].Y; //circleY
                                                                //			Check all 4 quadrants!!!
                            for (int quadrant = 0; quadrant < 4; ++quadrant)
                            {
                                int tmpX = cX; cX = -cY; cY = tmpX;
                                int mX = i + cX; //mapX
                                int mY = j + cY; //mapY
                                                 //			Either wall or end of map!
                                if (mX < 0 || mX >= mapWidth || mY < 0 || mY >= mapHeight || !map[mX, mY])
                                {
                                    if (counter == 0)
                                    {
                                        hitFirstWallDist = distance;
                                        if (dynamicWallThreshold)
                                        {
                                            wallThreshold = (maxDist - distance) / 16 + 1;
                                        }
                                    }
                                    ++counter;
                                    if (counter == wallThreshold)
                                    {
                                        depthMap[i, j] = distance;
                                        if (maxDepthUsed < depthMap[i, j])
                                        {
                                            maxDepthUsed = depthMap[i, j];
                                        }
                                        hitWall = true;
                                    }
                                }
                            }
                            if (hitWall) { break; }
                        }
                        ++distance;
                    }
                    //End by reducing current circle by 2 (because of the octile distance)
                    distance = Mathf.Max(1, hitFirstWallDist - 2);
                    //distance = 0;
                }
                else
                {
                    depthMap[i, j] = 0;
                }
            }
        }
    }

    public static int GetPixelDepth(bool[,] obstructionMap, Coord coord)
    {
        int distance = 0;
        int counter = 0;

        int mapWidth = obstructionMap.GetLength(0);
        int mapHeight = obstructionMap.GetLength(1);

        if (obstructionMap[coord.X, coord.Y])
        {
            bool hitWall = false;
            while (!hitWall)
            {
                // Use circleMap to fill in height for tile
                for (int it = 0; it < circleMap[distance].Count; it++)
                {
                    int cX = circleMap[distance][it].X; //circleX
                    int cY = circleMap[distance][it].Y; //circleY
                                                        //			Check all 4 quadrants!!!
                    for (int quadrant = 0; quadrant < 4; ++quadrant)
                    {
                        int tmpX = cX; cX = -cY; cY = tmpX;
                        int mX = coord.X + cX; //mapX
                        int mY = coord.Y + cY; //mapY
                                         //			Either wall or end of map!
                        if (mX < 0 || mX >= mapWidth || mY < 0 || mY >= mapHeight || !obstructionMap[mX, mY])
                        {
                            ++counter;
                            if (counter == 1)
                            {
                                return distance;
                            }
                        }
                    }
                }
                ++distance;
            }
            return distance;
        }
        return 0;
    }

    private void buildRegionsMap(bool[,] map, int maxDepthUsed, int mapWidth, int mapHeight, ref int[,] depthMap, out int[,] regionMap, out List<MapRegion> mapRegions, out int numberOfClusters)
    {
        mapRegions = new List<MapRegion>();
        List<Coord>[] coordDepthMap = new List<Coord>[maxDepthUsed + 1];
        for (int i = 0; i < maxDepthUsed + 1; i++)
        {
            coordDepthMap[i] = new List<Coord>();
        }
        for (int i = 0; i < mapWidth; ++i)
        {
            for (int j = 0; j < mapHeight; ++j)
            {
                if (depthMap[i, j] > 0)
                {
                    coordDepthMap[depthMap[i, j]].Add(new Coord(i, j));
                }
            }
        }

        regionMap = new int[mapWidth, mapHeight];
        for (int i = 0; i < mapWidth; ++i)
        {
            //zoneClusterMap[i] = new int[mapHeight];
            for (int j = 0; j < mapHeight; ++j)
            {
                regionMap[i, j] = -1;
            }
        }
        int currentClusterID = 0;

        for (int waterLevel = maxDepthUsed; waterLevel > 0; --waterLevel)
        {
            List<Coord> currentDepthCoords = coordDepthMap[waterLevel];
            while (currentDepthCoords.Count != 0)
            {
                List<(int, Coord)> tilesWithNeighbors = new List<(int, Coord)>();
                for (int i = 0; i < currentDepthCoords.Count; i++)
                {
                    int neighborClusterID = -1;
                    int dX = 1; //directionX
                    int dY = 0; //directionY
                    for (int direction = 0; direction < 4; ++direction)
                    {
                        int tmpX = dX; dX = -dY; dY = tmpX;
                        int nX = currentDepthCoords[i].X + dX; //neighborX
                        int nY = currentDepthCoords[i].Y + dY; //neighborY
                        if (coordsInsideMapAndSomeZone(nX, nY, mapWidth, mapHeight, ref regionMap))
                        {
                            if (neighborClusterID == -1)
                            {
                                neighborClusterID = regionMap[nX, nY];
                            }
                            else if (neighborClusterID != regionMap[nX, nY])
                            {
                                break;
                            }
                        }
                    }
                    if (neighborClusterID != -1)
                    {
                        tilesWithNeighbors.Add(new(neighborClusterID, currentDepthCoords[i]));
                    }
                }
                if (tilesWithNeighbors.Count != 0)
                {
                    for (int i = 0; i < tilesWithNeighbors.Count; i++)
                    {
                        Coord coord = tilesWithNeighbors[i].Item2;
                        regionMap[coord.X, coord.Y] = tilesWithNeighbors[i].Item1;
                        mapRegions[tilesWithNeighbors[i].Item1].numberOfTiles++;
                        currentDepthCoords.Remove(coord);
                    }
                    tilesWithNeighbors.Clear();
                }
                else
                {
                    //pick any tile and assign new clusterID
                    Coord coord = currentDepthCoords[currentDepthCoords.Count - 1];
                    currentDepthCoords.RemoveAt(currentDepthCoords.Count - 1);
                    regionMap[coord.X, coord.Y] = currentClusterID;
                    mapRegions.Add(new MapRegion(currentClusterID, coord));
                    currentClusterID++;
                }
            }
        }
        numberOfClusters = currentClusterID;
    }

    private void refineRegions(bool[,] map, int mapWidth, int mapHeight, ref int[,] regionMap, ref int[,] depthMap, ref List<MapRegion> regions)
    {
        Dictionary<int, Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)>> mapRegionNeighbors = new Dictionary<int, Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)>>();
        for (int i = 0; i < mapWidth; ++i)
        {
            for (int j = 0; j < mapHeight; ++j)
            {
                if (map[i, j])
                {
                    int dX = 1; //directionX
                    int dY = 0; //directionY
                    Dictionary<int, (int, Coord)> neighbors = new Dictionary<int, (int, Coord)>();
                    int region = regionMap[i, j];
                    for (int direction = 0; direction < 4; ++direction)
                    {
                        int tmpX = dX; dX = -dY; dY = tmpX;
                        int nX = i + dX; //neighborX
                        int nY = j + dY; //neighborY
                        if (coordsInsideMapAndSomeZone(nX, nY, mapWidth, mapHeight, ref regionMap))
                        {
                            int otherRegion = regionMap[nX, nY];
                            if (otherRegion != region)
                            {
                                if (!neighbors.ContainsKey(region))
                                {
                                    neighbors[otherRegion] = (0, new Coord(0, 0));
                                }
                                if (neighbors[otherRegion].Item1 < depthMap[nX, nY])
                                {
                                    neighbors[otherRegion] = (depthMap[nX, nY], new Coord(nX, nY));
                                }
                            }
                        }
                    }
                    foreach (KeyValuePair<int, (int, Coord)> pair in neighbors)
                    {
                        if (!mapRegionNeighbors.ContainsKey(region))
                        {
                            mapRegionNeighbors[region] = new Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentrePosition)>();
                        }
                        if (!mapRegionNeighbors[region].ContainsKey(pair.Key))
                        {
                            mapRegionNeighbors[region][pair.Key] = (0, 0, new Coord(0, 0));
                        }

                        (int totalGateTiles, int maxGateDepth, Coord gateCentre) dictValue = mapRegionNeighbors[region][pair.Key];
                        dictValue.totalGateTiles++;
                        if (pair.Value.Item1 > dictValue.maxGateDepth)
                        {
                            dictValue.maxGateDepth = pair.Value.Item1;
                            dictValue.gateCentre = pair.Value.Item2;
                        }
                        mapRegionNeighbors[region][pair.Key] = dictValue;
                    }
                }
            }
        }
        //int counter = 0;
        bool mergingDone = true;
        while (mergingDone) {
            TryToMergeRegions(ref mapRegionNeighbors, ref regionMap, ref depthMap, ref regions, out mergingDone);
            //counter++;
        }
    }

    private void TryToMergeRegions(ref Dictionary<int, Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)>> mapRegionNeighbors, ref int[,] regionMap, ref int[,] depthMap, ref List<MapRegion> regions, out bool mergingPerformed)
    {
        mergingPerformed = false;
        Dictionary<int, int> mergingDictionary = new Dictionary<int, int>();
        HashSet<int> regionsNotMerged = new HashSet<int>();
        HashSet<int> regionsNotMergedInto = new HashSet<int>();

        foreach (KeyValuePair<int, Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)>> regionInfo in mapRegionNeighbors)
        {
            MapRegion region = regions[regionInfo.Key];
            int regionDepth = depthMap[region.centre.X, region.centre.Y];

            List<(int regionId, int gateTilesCount)> mergingCandidates = new List<(int regionId, int gateTilesCount)>();
            foreach (KeyValuePair<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)> otherRegionInfo in mapRegionNeighbors[regionInfo.Key])
            {
                MapRegion otherRegion = regions[otherRegionInfo.Key];
                int otherRegionDepth = depthMap[otherRegion.centre.X, otherRegion.centre.Y];
                if (!mergingDictionary.ContainsKey(regionInfo.Key) && !regionsNotMerged.Contains(regionInfo.Key) && !regionsNotMergedInto.Contains(otherRegionInfo.Key) && regionDepth <= otherRegionDepth)
                {
                    if (otherRegionInfo.Value.maxGateDepth >= Mathf.Floor(regionDepth * 0.9f))
                    {
                        mergingCandidates.Add((otherRegionInfo.Key, otherRegionInfo.Value.totalGateTiles));
                    }
                    else if (mapRegionNeighbors[otherRegion.ID][region.ID].maxGateDepth >= Mathf.Floor(regionDepth * 0.9f))
                    {
                        mergingCandidates.Add((otherRegionInfo.Key, otherRegionInfo.Value.totalGateTiles));
                    }
                    /*
                    else if (region.numberOfTiles <= otherRegionInfo.Value.totalGateTiles * 4)
                    {
                        Debug.Log($"Region depth {regionDepth}, gate depth {otherRegionInfo.Value.maxGateDepth}, other gate depth {mapRegionNeighbors[otherRegion.ID][region.ID].maxGateDepth}");
                        mergingCandidates.Add((otherRegionInfo.Key, otherRegionInfo.Value.totalGateTiles));
                    }
                    */
                }
            }
            if (mergingCandidates.Count > 0)
            {
                mergingCandidates.Sort((x, y) => x.gateTilesCount.CompareTo(y.gateTilesCount));
                mergingDictionary[regionInfo.Key] = mergingCandidates[mergingCandidates.Count-1].regionId;
                regionsNotMerged.Add(regionInfo.Key);
                regionsNotMerged.Add(mergingCandidates[mergingCandidates.Count - 1].regionId);
                regionsNotMergedInto.Add(regionInfo.Key);
            }

        }

        foreach (KeyValuePair<int, int> mergedRegions in mergingDictionary)
        {
            MapRegion smallerRegion = regions[mergedRegions.Key];
            MapRegion biggerRegion = regions[mergedRegions.Value];
            Dictionary<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)> smallerRegionInfo = mapRegionNeighbors[smallerRegion.ID];
            mapRegionNeighbors.Remove(smallerRegion.ID);
            foreach (KeyValuePair<int, (int totalGateTiles, int maxGateDepth, Coord gateCentre)> smallerRegionNeighbor in smallerRegionInfo)
            {
                if (smallerRegionNeighbor.Key != biggerRegion.ID)
                {
                    if (!mapRegionNeighbors[biggerRegion.ID].ContainsKey(smallerRegionNeighbor.Key))
                    {
                        mapRegionNeighbors[biggerRegion.ID][smallerRegionNeighbor.Key] = (smallerRegionNeighbor.Value.totalGateTiles, smallerRegionNeighbor.Value.maxGateDepth, smallerRegionNeighbor.Value.gateCentre);
                        (int totalGateTiles, int maxGateDepth, Coord gateCentre) opposite = mapRegionNeighbors[smallerRegionNeighbor.Key][smallerRegion.ID];
                        mapRegionNeighbors[smallerRegionNeighbor.Key].Remove(smallerRegion.ID);
                        mapRegionNeighbors[smallerRegionNeighbor.Key][biggerRegion.ID] = opposite;
                    }
                    else
                    {
                        (int totalGateTiles, int maxGateDepth, Coord gateCentre) biggerRegionNeighborInfo = mapRegionNeighbors[biggerRegion.ID][smallerRegionNeighbor.Key];
                        (int totalGateTiles, int maxGateDepth, Coord gateCentre) biggerRegionNeighborInfoOpposite = mapRegionNeighbors[smallerRegionNeighbor.Key][biggerRegion.ID];

                        mapRegionNeighbors[biggerRegion.ID][smallerRegionNeighbor.Key] = (
                                            smallerRegionNeighbor.Value.totalGateTiles + biggerRegionNeighborInfo.totalGateTiles,
                                            Mathf.Max(smallerRegionNeighbor.Value.maxGateDepth, biggerRegionNeighborInfo.maxGateDepth),
                                            smallerRegionNeighbor.Value.maxGateDepth > biggerRegionNeighborInfo.maxGateDepth ? smallerRegionNeighbor.Value.gateCentre : biggerRegionNeighborInfo.gateCentre
                                            );
                        (int totalGateTiles, int maxGateDepth, Coord gateCentre) opposite = mapRegionNeighbors[smallerRegionNeighbor.Key][smallerRegion.ID];
                        mapRegionNeighbors[smallerRegionNeighbor.Key].Remove(smallerRegion.ID);
                        mapRegionNeighbors[smallerRegionNeighbor.Key][biggerRegion.ID] = (
                                            opposite.totalGateTiles + biggerRegionNeighborInfoOpposite.totalGateTiles,
                                            Mathf.Max(opposite.maxGateDepth, biggerRegionNeighborInfoOpposite.maxGateDepth),
                                            opposite.maxGateDepth > biggerRegionNeighborInfoOpposite.maxGateDepth ? opposite.gateCentre : biggerRegionNeighborInfoOpposite.gateCentre
                                            );
                    }
                }
            }
            mapRegionNeighbors[biggerRegion.ID].Remove(smallerRegion.ID);
            MergeRegions(smallerRegion, biggerRegion, ref regionMap);
            mergingPerformed = true;
        }
    }

    private void MergeRegions(MapRegion smallerRegion, MapRegion biggerRegion, ref int[,] regionMap)
    {
        biggerRegion.numberOfTiles += smallerRegion.numberOfTiles;
        smallerRegion.numberOfTiles = 0;
        int x = smallerRegion.centre.X;
        int y = smallerRegion.centre.Y;
        int mapWidth = regionMap.GetLength(0);
        int mapHeight = regionMap.GetLength(1);

        regionMap[x, y] = biggerRegion.ID;
        bool inTheRegion = true;
        int distance = 1;
        while (inTheRegion)
        {
            inTheRegion = false;
            for (int it = 0; it < circleMap[distance].Count; it++)
            {
                //counter = 0;
                int cX = circleMap[distance][it].X; //circleX
                int cY = circleMap[distance][it].Y; //circleY
                                                    //			Check all 4 quadrants!!!
                for (int quadrant = 0; quadrant < 4; ++quadrant)
                {
                    int tmpX = cX; cX = -cY; cY = tmpX;
                    int mX = x + cX; //mapX
                    int mY = y + cY; //mapY

                    if (mX >= 0 && mX < mapWidth && mY >= 0 && mY < mapHeight && regionMap[mX, mY] == smallerRegion.ID)
                    {
                        inTheRegion = true;
                        regionMap[mX, mY] = biggerRegion.ID;
                    }
                }
            }
            ++distance;
        }
    }

    private void buildGates(bool[,] map, int mapWidth, int mapHeight, ref int[,] regionMap, out List<RegionGateway> gateways, ref List<MapRegion> mapRegions)
    {
        gateways = new List<RegionGateway>();

        int[,] gateClusterMap;
        gateClusterMap = new int[mapWidth, mapHeight];
        for (int i = 0; i < mapWidth; ++i)
        {
            for (int j = 0; j < mapHeight; ++j)
            {
                gateClusterMap[i, j] = -1;
                if (map[i, j])
                {
                    int dX = 1; //directionX
                    int dY = 0; //directionY
                    for (int direction = 0; direction < 4; ++direction)
                    {
                        int tmpX = dX; dX = -dY; dY = tmpX;
                        int nX = i + dX; //neighborX
                        int nY = j + dY; //neighborY
                        if (coordsInsideMapAndSomeZone(nX, nY, mapWidth, mapHeight, ref regionMap))
                        {
                            if (regionMap[nX, nY] != regionMap[i, j])
                            {
                                gateClusterMap[i, j] = 1;
                            }
                        }
                    }
                }
            }
        }

        for (int i = 0; i < mapWidth; ++i)
        {
            for (int j = 0; j < mapHeight; ++j)
            {
                if (gateClusterMap[i, j] != -1)
                {
                    List<Coord> gateClusterCoords = new List<Coord>();
                    //FloodFill gate cluster with clear and add tiles to gateClusterTiles
                    Queue<Coord> Q = new Queue<Coord>();
                    Q.Enqueue(new Coord(i, j));
                    while (Q.Count > 0)
                    {
                        Coord node = Q.Dequeue();
                        int x = node.X;
                        int y = node.Y;
                        if (gateClusterMap[x, y] != -1)
                        {
                            int w, e; //w west, e east
                            for (w = x; w > 0 && gateClusterMap[w - 1, y] != -1; --w) { }
                            for (e = x; e + 1 < mapWidth && gateClusterMap[e + 1, y] != -1; ++e) { }
                            for (int h = w; h <= e; ++h)
                            {
                                gateClusterCoords.Add(new Coord(h, y));
                                gateClusterMap[h, y] = -1;

                                if (y > 0 && gateClusterMap[h, y - 1] != -1)
                                {
                                    Q.Enqueue(new Coord(h, y - 1));
                                }
                                if (y + 1 < mapHeight && gateClusterMap[h, y + 1] != -1)
                                {
                                    Q.Enqueue(new Coord(h, y + 1));
                                }
                            }
                        }
                    }

                    //Find gate regions
                    int iter;
                    MapRegion region1 = mapRegions[regionMap[gateClusterCoords[0].X, gateClusterCoords[0].Y]];
                    MapRegion region2 = null;
                    for (iter = 1; iter < gateClusterCoords.Count; iter++)
                    {
                        if (mapRegions[regionMap[gateClusterCoords[iter].X, gateClusterCoords[iter].Y]] != region1)
                        {
                            region2 = mapRegions[regionMap[gateClusterCoords[iter].X, gateClusterCoords[iter].Y]];
                            break;
                        }
                    }

                    //remove from gateClusterTiles tiles that don't touch wall
                    iter = 0;
                    while (iter < gateClusterCoords.Count)
                    {
                        Coord coord = gateClusterCoords[iter];
                        int x = coord.X;
                        int y = coord.Y;
                        if (x > 0 && map[x - 1, y]
                        && x + 1 < mapWidth && map[x + 1, y]
                        && y > 0 && map[x, y - 1]
                        && y + 1 < mapHeight && map[x, y + 1])
                        {
                            gateClusterCoords.RemoveAt(iter);
                        }
                        else
                        {
                            iter++;
                        }
                    }

                    //for (iter = 0; iter < gateClusterCoords.Count; iter++) gateClusterMap[gateClusterCoords[iter].X, gateClusterCoords[iter].Y] = 1;
                    //continue;
                    //if any tiles left in gateClusterTiles
                    if (gateClusterCoords.Count > 0)
                    {
                        //if 1 tile left in gateClusterTiles
                        //Make new Gateway with same start and end
                        if (gateClusterCoords.Count == 1)
                        {
                            int x = gateClusterCoords[0].X;
                            int y = gateClusterCoords[0].Y;
                            gateways.Add(new RegionGateway(new Coord(x, y), new Coord(x, y), region1, region2, gateways.Count));
                        }
                        //if 2 tiles left in gateClusterTiles
                        //Make new Gateway with them
                        else if (gateClusterCoords.Count == 2)
                        {
                            Coord coord1 = gateClusterCoords[0];
                            int x1 = coord1.X;
                            int y1 = coord1.Y;
                            Coord coord2 = gateClusterCoords[1];
                            int x2 = coord2.X;
                            int y2 = coord2.Y;

                            bool surrounded = partiallySurrounded(coord1, map) || partiallySurrounded(coord2, map);
                            gateways.Add(new RegionGateway(new Coord(x1, y1), new Coord(x2, y2), region1, region2, gateways.Count, surrounded));
                        }
                        //if more than 2 tiles left in gateClusterTiles
                        //choose the two that have maximum distance between them
                        //Make new Gateway with them
                        else
                        {
                            int maxDistSqr = 0;
                            Coord start = new Coord(0, 0);
                            Coord end = new Coord(0, 0);

                            int i1 = 0;
                            while (i1 < gateClusterCoords.Count)
                            {
                                Coord coord1 = gateClusterCoords[0];
                                int x1 = coord1.X;
                                int y1 = coord1.Y;
                                int i2 = i1 + 1;
                                while (i2 < gateClusterCoords.Count)
                                {
                                    Coord coord2 = gateClusterCoords[i2];
                                    int x2 = coord2.X;
                                    int y2 = coord2.Y;
                                    int distSqr = (x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2);
                                    
                                    if (distSqr > maxDistSqr)
                                    {
                                        maxDistSqr = distSqr;
                                        start.X = x1;
                                        start.Y = y1;
                                        end.X = x2;
                                        end.Y = y2;
                                    }
                                    i2++;
                                }
                                i1++;
                            }
                            
                            bool surrounded = partiallySurrounded(start, map) || partiallySurrounded(end, map);
                            gateways.Add(new RegionGateway(start, end, region1, region2, gateways.Count, surrounded));
                        }
                    }
                }
            }
        }

        foreach (RegionGateway gate in gateways)
        {
            gateClusterMap[gate.start.X, gate.start.Y] = 1;
            gateClusterMap[gate.end.X, gate.end.Y] = 1;
            gate.regionA.gateways.Add(gate);
            gate.regionB.gateways.Add(gate);
        }
    }

    private bool partiallySurrounded(Coord coord, bool[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        bool upper = coord.Y + 1 < height && map[coord.X, coord.Y + 1];
        bool lower = coord.Y - 1 >= 0 && map[coord.X, coord.Y - 1];
        bool left = coord.X - 1 >= 0 && map[coord.X - 1, coord.Y];
        bool right = coord.X + 1 < width && map[coord.X + 1, coord.Y];

        int counter = 0;
        if (upper) counter++;
        if (lower) counter++;
        if (right) counter++;
        if (left) counter++;

        if (counter > 2) return false;
        else return true;
    }
    
    private void refineGates(bool[,] map, ref List<RegionGateway> gateways, int gateRegionIndex, ref int[,] regionMap)
    {
        for (int i = 0; i < gateways.Count; i++)
        {
            RegionGateway gate = gateways[i];
            gate.gateTilesCoords = new List<Coord>();
            Vector2 position = gate.start.GetWorldPosition();
            float distance = (gate.end.GetWorldPosition() - gate.start.GetWorldPosition()).magnitude;
            Vector2 direction = (gate.end.GetWorldPosition() - gate.start.GetWorldPosition()).normalized;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                distance *= Mathf.Abs(direction.x);
                direction = direction / Mathf.Abs(direction.x);
            }
            else
            {
                distance *= Mathf.Abs(direction.y);
                direction = direction / Mathf.Abs(direction.y);
            }
            int distanceCovered = 0;
            while (distanceCovered <= distance + 0.5)
            {
                Coord coord = Coord.CoordFromPosition(position);
                gate.gateTilesCoords.Add(coord);
                position += direction;
                regionMap[coord.X, coord.Y] = gateRegionIndex;
                distanceCovered++;
            }
            gateRegionIndex++;

            Vector2 regionDirection1 = direction.Rotate(90);
            Vector2 regionDirection2 = direction.Rotate(-90);
            //Vector2 directionToRegionA = (gate.regionA.centre.GetWorldPosition() - position).normalized;
            //Vector2 directionToRegionB = (gate.regionB.centre.GetWorldPosition() - position).normalized;
            Coord sampleCoord = Coord.CoordFromPosition(regionDirection1 * gate.gateTilesCoords.Count * 0.5f + gate.GetCentralPosition());
            int sample = regionMap[sampleCoord.X, sampleCoord.Y];

            //if (Vector2.Angle(regionDirection1, directionToRegionA) < Vector2.Angle(regionDirection1, directionToRegionB))
            if (sample == gate.regionA.ID)
            {
                gate.regionADirection = regionDirection1;
                gate.regionBDirection = regionDirection2;
            }
            else
            {
                gate.regionADirection = regionDirection2;
                gate.regionBDirection = regionDirection1;
            }

            clearRegionAroundTheGate(gate.regionADirection, gate.regionA.ID, gate.regionB.ID, gate.gateTilesCoords, ref regionMap);
            clearRegionAroundTheGate(gate.regionBDirection, gate.regionB.ID, gate.regionA.ID, gate.gateTilesCoords, ref regionMap);
        }
    }

    private void clearRegionAroundTheGate(Vector2 regionDirection, int regionID, int otherRegionID, List<Coord> gateTiles, ref int[,] regionMap)
    {
        Vector2Int up = new Vector2Int(0, 1);
        Vector2Int down = new Vector2Int(0, -1);
        Vector2Int left = new Vector2Int(-1, 0);
        Vector2Int right = new Vector2Int(1, 0);
        List<Vector2Int> directions = new List<Vector2Int>();
        if (Vector2.Angle(regionDirection, up) < 90) directions.Add(up);
        if (Vector2.Angle(regionDirection, down) < 90) directions.Add(down);
        if (Vector2.Angle(regionDirection, left) < 90) directions.Add(left);
        if (Vector2.Angle(regionDirection, right) < 90) directions.Add(right);

        Queue<Coord> processedCoords = new Queue<Coord>(gateTiles);
        //int counter = 0;
        while (processedCoords.Count > 0)
        {
            /*
            if (counter > 1000)
            {
                Debug.Log($"Coord: X {processedCoords.Peek().X}, Y {processedCoords.Peek().Y}"); 
                Debug.Log(regionID);
                Debug.Log(otherRegionID);
                break;
            }
            counter++;
            */
            Coord coord = processedCoords.Dequeue();
            if (regionMap[coord.X, coord.Y] == otherRegionID) 
                regionMap[coord.X, coord.Y] = regionID;
            foreach (Vector2Int direction in directions)
            {
                Coord neighbor = new Coord(coord.X + direction.x, coord.Y + direction.y);
                if (neighbor.WithinBounds())
                {
                    int neighborRegion = regionMap[neighbor.X, neighbor.Y];
                    if (neighborRegion == otherRegionID)
                    {
                        processedCoords.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    private bool coordsInsideMapAndSomeZone(int x, int y, int mapWidth, int mapHeight, ref int[,] regionMap)
    {
        return x >= 0 && x < mapWidth && y >= 0 && y < mapHeight && regionMap[x,y] != -1;
    }
    private float octileDistance(int deltaX, int deltaY)
    {
        return (float)Mathf.Max(deltaX, deltaY) + 0.4f * Mathf.Min(deltaX, deltaY);
    }
}