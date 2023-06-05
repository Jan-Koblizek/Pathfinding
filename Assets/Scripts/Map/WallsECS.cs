using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using utils;

public class WallsECS
{
    public NativeArray<bool> map;
    public int mapWidth;
    public int mapHeight;

    public void CleanArrays()
    {
        map.Dispose();
        obstacleRepulsionRays.Dispose();
        obstacleTurningRays.Dispose();
    }

    public (NativeArray<Vector2> repulsionForces, NativeArray<Vector2> turningForces) getWallForces(NativeList<Vector2> unitPositions, NativeList<Vector2> unitVelocities)
    {
        NativeArray<Vector2> repulsionForces = new NativeArray<Vector2>(unitPositions.Length, Allocator.TempJob);
        JobHandle wallRepulsionJobHandle;
        GetWallRepulsionForcesJob wallRepulsionJob;

        wallRepulsionJob = new GetWallRepulsionForcesJob()
        {
            unitPositions = unitPositions,
            forces = repulsionForces,
            maxForce = SimulationSettings.instance.MaxForce,
            passabilityMap = map,
            mapWidth = mapWidth,
            mapHeight  = mapHeight,
            obstacleRepulsionRays = obstacleRepulsionRays,
            time = Time.time
        };

        wallRepulsionJobHandle = wallRepulsionJob.Schedule(unitPositions.Length, 64);


        NativeArray<Vector2> turningForces = new NativeArray<Vector2>(unitPositions.Length, Allocator.TempJob);
        JobHandle turningJobHandle;
        GetWallTurningForcesJob turningJob;

        turningJob = new GetWallTurningForcesJob()
        {
            unitPositions = unitPositions,
            unitVelocities = unitVelocities,
            forces = turningForces,
            maxForce = SimulationSettings.instance.MaxForce,
            passabilityMap = map,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            obstacleTurningRays = obstacleTurningRays
        };

        turningJobHandle = turningJob.Schedule(unitPositions.Length, 64);

        wallRepulsionJobHandle.Complete();
        turningJobHandle.Complete();

        return (repulsionForces, turningForces);
    }

    private NativeArray<(float angle, float length)> obstacleRepulsionRays;
    private void InitializeObstacleRepulsionRays()
    {
        List<float> angles = new List<float> { 0, 45, 90, 135, -45, -90, -135, 180 };
        List<float> lengths = new List<float> { 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f, 3.0f };
        if (obstacleRepulsionRays.IsCreated)
        {
            obstacleRepulsionRays.Dispose();
        }
        obstacleRepulsionRays = new NativeArray<(float angle, float length)>(Mathf.Min(angles.Count, lengths.Count), Allocator.Persistent);
        for (int i = 0; i < Mathf.Min(angles.Count, lengths.Count); i++)
        {
            obstacleRepulsionRays[i] = (angles[i], lengths[i]);
        }
    }

    private NativeArray<(float angle, float length)> obstacleTurningRays;

    private void InitializeObstacleTurningRays()
    {
        List<float> angles = new List<float> { 10, -10 };
        List<float> lengths = new List<float> { 7.0f, 7.0f };
        if (obstacleTurningRays.IsCreated)
        {
            obstacleTurningRays.Dispose();
        }
        obstacleTurningRays = new NativeArray<(float angle, float length)>(Mathf.Min(angles.Count, lengths.Count), Allocator.Persistent);
        for (int i = 0; i < Mathf.Min(angles.Count, lengths.Count); i++)
        {
            obstacleTurningRays[i] = (angles[i], lengths[i]);
        }
    }

    public void Initialize(Map mapInit)
    {
        mapWidth = mapInit.passabilityMap.GetLength(0);
        mapHeight = mapInit.passabilityMap.GetLength(1);
        map = new NativeArray<bool>(mapWidth * mapHeight, Allocator.Persistent);
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                map[y * mapWidth + x] = mapInit.passabilityMap[x, y];
            }
        }
        InitializeObstacleTurningRays();
        InitializeObstacleRepulsionRays();
    }

    [BurstCompile]
    private struct GetWallTurningForcesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeList<Vector2> unitPositions;
        [ReadOnly]
        public NativeList<Vector2> unitVelocities;

        public NativeArray<Vector2> forces;
        [ReadOnly]
        public float maxForce;

        [ReadOnly]
        public NativeArray<bool> passabilityMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;

        [ReadOnly]
        public NativeArray<(float, float)> obstacleTurningRays;
        public void Execute(int index)
        {
            forces[index] = GetWallTurningForce(unitPositions[index], unitVelocities[index]);
        }

        private Vector2 GetWallTurningForce(Vector2 position, Vector2 velocity)
        {
            if (IsInWallsPoint(position))
            {
                return Vector2.zero;
            }
            Vector2 wallTurningForce = Vector2.zero;

            foreach (var (angle, length) in obstacleTurningRays)
            {
                Vector2 rayDirection = velocity.Rotate(angle).normalized;
                float hitDistance;
                bool hitWall = WallRayCasting(position, rayDirection, length, out _, out hitDistance);

                if (!hitWall || angle == 0)
                {
                    continue;
                }

                float distanceFactor = (length - hitDistance) / length;
                Vector2 turningForceDirection;
                if (angle > 0) turningForceDirection = rayDirection.Rotate(-90);
                else turningForceDirection = rayDirection.Rotate(90);
                Vector2 turningForce = distanceFactor * distanceFactor * turningForceDirection;

                wallTurningForce += turningForce;
            }
            return wallTurningForce.LimitMagnitude(maxForce);
        }

        private bool IsInWallsPoint(Vector2 position)
        {
            (int X, int Y) coord = coordFromPosition(position);
            if (!passabilityMap[coord.X + coord.Y * mapWidth]) return true;
            else return false;
        }


        private bool WallRayCasting(Vector2 origin, Vector2 direction, float length, out Vector2 hitPoint, out float distanceToHit)
        {
            distanceToHit = float.MaxValue;
            hitPoint = new Vector2();
            direction = direction.normalized;
            float distance = 0.0f;
            Vector2 position = origin;
            (int X, int Y) coord = coordFromPosition(position);

            while (true)
            {
                Vector2 newPosition;
                (int X, int Y) newCoord;
                float distanceChange;
                getNearestCoordBorderHit(coord, position, direction, out newCoord, out newPosition, out distanceChange);
                distance += distanceChange;
                if (distance <= length)
                {
                    if (!withinBounds(newCoord) || !passabilityMap[newCoord.X + newCoord.Y * mapWidth])
                    {
                        distanceToHit = distance;
                        hitPoint = newPosition;
                        return true;
                    }
                    else
                    {
                        coord = newCoord;
                        position = newPosition;
                    }
                }
                else break;
            }
            return false;
        }

        private void getNearestCoordBorderHit(in (int X, int Y) coord, in Vector2 position, in Vector2 direction, out (int X, int Y) nextTile, out Vector2 borderPoint, out float distanceChange)
        {
            float xShift = float.MaxValue;
            float xDistance = float.MaxValue;
            float yShift = float.MaxValue;
            float yDistance = float.MaxValue;
            Vector2 xPosition = new();
            Vector2 yPosition = new();
            (int X, int Y) xCoord = new (0, 0);
            (int X, int Y) yCoord = new (0, 0);
            if (direction.x != 0)
            {
                if (direction.x > 0)
                {
                    float xPos = coord.X + 1.0f;
                    xShift = xPos - position.x;
                    xDistance = (xShift / direction.x) + 0.001f;
                    xPosition = position + xDistance * direction;
                    xCoord = coordFromPosition(xPosition);
                }
                else
                {
                    float xPos = coord.X;
                    xShift = xPos - position.x;
                    xDistance = (xShift / direction.x) + 0.001f;
                    xPosition = position + xDistance * direction;
                    xCoord = coordFromPosition(xPosition);
                }
            }

            if (direction.y != 0)
            {
                if (direction.y > 0)
                {
                    float yPos = coord.Y + 1.0f;
                    yShift = yPos - position.y;
                    yDistance = (yShift / direction.y) + 0.001f;
                    yPosition = position + yDistance * direction;
                    yCoord = coordFromPosition(yPosition);
                }
                else
                {
                    float yPos = coord.Y;
                    yShift = yPos - position.y;
                    yDistance = (yShift / direction.y) + 0.001f;
                    yPosition = position + yDistance * direction;
                    yCoord = coordFromPosition(yPosition);
                }
            }

            if (yDistance < xDistance)
            {
                nextTile = yCoord;
                borderPoint = yPosition;
                distanceChange = yDistance;
            }
            else
            {
                nextTile = xCoord;
                borderPoint = xPosition;
                distanceChange = xDistance;
            }
        }

        private (int X, int Y) coordFromPosition(Vector2 position)
        {
            return (Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        private bool withinBounds((int X, int Y) coord)
        {
            return coord.X >= 0 && coord.X < mapWidth && coord.Y >= 0 && coord.Y < mapHeight;
        }
    }

    [BurstCompile]
    private struct GetWallRepulsionForcesJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeList<Vector2> unitPositions;

        public NativeArray<Vector2> forces;
        [ReadOnly]
        public float maxForce;

        [ReadOnly]
        public NativeArray<bool> passabilityMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;
        [ReadOnly]
        public float time;
        [ReadOnly]
        public NativeArray<(float, float)> obstacleRepulsionRays;
        public void Execute(int index)
        {
            forces[index] = GetWallRepulsionForce(unitPositions[index], index);
        }

        private Vector2 GetWallRepulsionForce(Vector2 position, int index)
        {
            if (IsInWallsPoint(position))
            {
                return Vector2.zero;
            }
            
            Vector2 wallRepulsionForce = Vector2.zero;
            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)((index + 13) * (time + 7)));
            Vector2 initDirection = (Vector2.right).Rotate(random.NextFloat()*90.0f);

            NativeList<Vector2> repulsionForces = new NativeList<Vector2>(obstacleRepulsionRays.Length, Allocator.Temp);
            foreach (var (angle, length) in obstacleRepulsionRays)
            {
                Vector2 rayDirection = initDirection.Rotate(angle).normalized;
                float hitDistance;
                bool hitWall = WallRayCasting(position, rayDirection, length, out _, out hitDistance);

                if (!hitWall || angle == 0)
                {
                    continue;
                }

                float distanceFactor = (length - hitDistance) / length;
                Vector2 repulsionForce = -rayDirection * distanceFactor * distanceFactor * distanceFactor;

                repulsionForces.Add(repulsionForce);
            }
            /*
            for (int i = 0; i < repulsionForces.Length; i++)
            {
                if (repulsionForces[i].magnitude < 0.02 && 
                    repulsionForces[(i-1 + repulsionForces.Length) % repulsionForces.Length].magnitude > 0.02 && 
                    repulsionForces[(i+1)%repulsionForces.Length].magnitude > 0.02)
                {
                    repulsionForces[i] = new Vector2(0.0f, 0.0f);
                    repulsionForces[(i - 1 + repulsionForces.Length) % repulsionForces.Length] = new Vector2(0.0f, 0.0f);
                    repulsionForces[(i + 1) % repulsionForces.Length] = new Vector2(0.0f, 0.0f);
                }
            }
            */
            for (int i = 0; i < repulsionForces.Length; i++)
            {
                wallRepulsionForce += repulsionForces[i];
            }

            return wallRepulsionForce.LimitMagnitude(maxForce);
        }

        private bool IsInWallsPoint(Vector2 position)
        {
            (int X, int Y) coord = coordFromPosition(position);
            if (!passabilityMap[coord.X + coord.Y * mapWidth]) return true;
            else return false;
        }


        private bool WallRayCasting(Vector2 origin, Vector2 direction, float length, out Vector2 hitPoint, out float distanceToHit)
        {
            distanceToHit = float.MaxValue;
            hitPoint = new Vector2();
            direction = direction.normalized;
            float distance = 0.0f;
            Vector2 position = origin;
            (int X, int Y) coord = coordFromPosition(position);

            while (true)
            {
                Vector2 newPosition;
                (int X, int Y) newCoord;
                float distanceChange;
                getNearestCoordBorderHit(coord, position, direction, out newCoord, out newPosition, out distanceChange);
                distance += distanceChange;
                if (distance <= length)
                {
                    if (!withinBounds(newCoord) || !passabilityMap[newCoord.X + newCoord.Y * mapWidth])
                    {
                        distanceToHit = distance;
                        hitPoint = newPosition;
                        return true;
                    }
                    else
                    {
                        coord = newCoord;
                        position = newPosition;
                    }
                }
                else break;
            }
            return false;
        }

        private void getNearestCoordBorderHit(in (int X, int Y) coord, in Vector2 position, in Vector2 direction, out (int X, int Y) nextTile, out Vector2 borderPoint, out float distanceChange)
        {
            float xShift = float.MaxValue;
            float xDistance = float.MaxValue;
            float yShift = float.MaxValue;
            float yDistance = float.MaxValue;
            Vector2 xPosition = new();
            Vector2 yPosition = new();
            (int X, int Y) xCoord = new(0, 0);
            (int X, int Y) yCoord = new(0, 0);
            if (direction.x != 0)
            {
                if (direction.x > 0)
                {
                    float xPos = coord.X + 1.0f;
                    xShift = xPos - position.x;
                    xDistance = (xShift / direction.x) + 0.001f;
                    xPosition = position + xDistance * direction;
                    xCoord = coordFromPosition(xPosition);
                }
                else
                {
                    float xPos = coord.X;
                    xShift = xPos - position.x;
                    xDistance = (xShift / direction.x) + 0.001f;
                    xPosition = position + xDistance * direction;
                    xCoord = coordFromPosition(xPosition);
                }
            }

            if (direction.y != 0)
            {
                if (direction.y > 0)
                {
                    float yPos = coord.Y + 1.0f;
                    yShift = yPos - position.y;
                    yDistance = (yShift / direction.y) + 0.001f;
                    yPosition = position + yDistance * direction;
                    yCoord = coordFromPosition(yPosition);
                }
                else
                {
                    float yPos = coord.Y;
                    yShift = yPos - position.y;
                    yDistance = (yShift / direction.y) + 0.001f;
                    yPosition = position + yDistance * direction;
                    yCoord = coordFromPosition(yPosition);
                }
            }

            if (yDistance < xDistance)
            {
                nextTile = yCoord;
                borderPoint = yPosition;
                distanceChange = yDistance;
            }
            else
            {
                nextTile = xCoord;
                borderPoint = xPosition;
                distanceChange = xDistance;
            }
        }

        private (int X, int Y) coordFromPosition(Vector2 position)
        {
            return (Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        private bool withinBounds((int X, int Y) coord)
        {
            return coord.X >= 0 && coord.X < mapWidth && coord.Y >= 0 && coord.Y < mapHeight;
        }
    }
}
