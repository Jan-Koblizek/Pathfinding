using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.UIElements;
using utils;

public class UnitsECS
{
    private WallsECS walls;
    private NearbyUnitsManagerECS nearbyUnitsManager;
    public NativeList<Vector2> unitPositions;
    public NativeList<Vector2> unitVelocities;
    private NativeList<Vector2> unitMovements;
    public NativeList<Vector2> unitDesiredVelocities;

    private NativeList<Vector2> newUnitPositions;
    private NativeList<Vector2> forces;
    public void InitializeUnits(List<Unit> units)
    {
        if (unitPositions.IsCreated)
        {
            unitPositions.Dispose();
            newUnitPositions.Dispose();
            unitVelocities.Dispose();
            unitMovements.Dispose();
            unitDesiredVelocities.Dispose();
            forces.Dispose();
        }
        unitPositions = new NativeList<Vector2>(units.Count, Allocator.Persistent);
        newUnitPositions = new NativeList<Vector2>(units.Count, Allocator.Persistent);
        unitVelocities = new NativeList<Vector2>(units.Count, Allocator.Persistent);
        unitMovements = new NativeList<Vector2>(units.Count, Allocator.Persistent);
        unitDesiredVelocities = new NativeList<Vector2>(units.Count, Allocator.Persistent);
        forces = new NativeList<Vector2>(units.Count, Allocator.Persistent);

        for (int i = 0; i < units.Count; i++)
        {
            newUnitPositions.Add(units[i].position);
            unitPositions.Add(units[i].position);
            unitVelocities.Add(units[i].velocity);
            unitMovements.Add(units[i].velocity);
            unitDesiredVelocities.Add(units[i].desiredVelocity);
            forces.Add(Vector2.zero);
        }

        if (walls != null)
        {
            nearbyUnitsManager.Initialize(units, false);
        }
        else
        {
            walls = new WallsECS();
            walls.Initialize(Map.instance);

            nearbyUnitsManager = new NearbyUnitsManagerECS();
            nearbyUnitsManager.Initialize(units, true);
        }
    }

    /*
    public void UpdateUnits(List<Unit> units)
    {
        UpdateUnitsJob updateUnitsJob;
        JobHandle updateUnitsHandle;
        updateUnitsJob = new UpdateUnitsJob()
        {
            units = units,
            unitPositions = unitPositions,
            unitVelocities = unitVelocities,
            unitDesiredVelocities = unitDesiredVelocities
        };

        updateUnitsHandle = updateUnitsJob.Schedule(units.Count, 64);
        updateUnitsHandle.Complete();
    }
    */

    public void UpdateUnits(List<Unit> units)
    {
        for (int i = 0; i < units.Count; i++)
        {
            unitPositions[i] = units[i].position;
            unitVelocities[i] = units[i].velocity;
            unitDesiredVelocities[i] = units[i].desiredVelocity;
        }
    }

    /*
    private struct UpdateUnitsJob : IJobParallelFor
    {
        [ReadOnly]
        public List<Unit> units;
        public NativeArray<Vector2> unitPositions;
        public NativeArray<Vector2> unitVelocities;
        public NativeArray<Vector2> unitDesiredVelocities;
        public void Execute(int index)
        {
            unitPositions[index] = units[index].position;
            unitVelocities[index] = units[index].velocity;
            unitDesiredVelocities[index] = units[index].desiredVelocity;
        }
    }
    */

    public void UnitsRemoved(List<Unit> units)
    {
        nearbyUnitsManager.CleanUnitTiles(unitPositions);
        unitPositions.RemoveRange(units.Count, unitPositions.Length - units.Count);
        unitVelocities.RemoveRange(units.Count, unitVelocities.Length - units.Count);
        unitDesiredVelocities.RemoveRange(units.Count, unitDesiredVelocities.Length - units.Count);

        newUnitPositions.RemoveRange(units.Count, newUnitPositions.Length - units.Count);
        unitMovements.RemoveRange(units.Count, unitMovements.Length - units.Count);
        forces.RemoveRange(units.Count, forces.Length - units.Count);

        for (int i = 0; i < units.Count; i++)
        {
            unitPositions[i] = units[i].position;
            unitVelocities[i] = units[i].velocity;
            unitDesiredVelocities[i] = units[i].desiredVelocity;
        }

        nearbyUnitsManager.SetUnitPositions(unitPositions);
    }

    public void GetForces(NativeArray<Vector2> seekForces, float deltaTime)
    {
        nearbyUnitsManager.UpdateUnitPositions(unitPositions);
        NativeArray<Vector2> wallRepulsionForces;
        NativeArray<Vector2> wallTurningForces;
        (wallRepulsionForces, wallTurningForces) = walls.getWallForces(unitPositions, unitVelocities);
              
        NativeArray<Vector2> unitSeparationForces;
        NativeArray<Vector2> alignmentForces;
        NativeArray<Vector2> unitAvoidanceForces;
        (unitSeparationForces, alignmentForces, unitAvoidanceForces) = nearbyUnitsManager.GetNearbyUnitsForces(unitPositions, unitVelocities, unitDesiredVelocities, deltaTime);
  
        JobHandle forcesJobHandle;
        CombineForcesJob forcesJob;
        //Debug.Log(unitVelocities[0].magnitude);
        forcesJob = new CombineForcesJob()
        {
            forces = forces,
            seekForces = seekForces,
            wallRepulsionForces = wallRepulsionForces,
            wallTurningForces = wallTurningForces,
            unitSeparationForces = unitSeparationForces,
            alignmentForces = alignmentForces,
            unitAvoidanceForces = unitAvoidanceForces,
            velocities = this.unitVelocities,
            maxForce = SimulationSettings.instance.MaxForce
        };
        forcesJobHandle = forcesJob.Schedule(unitPositions.Length, 64);
        forcesJobHandle.Complete();

        wallRepulsionForces.Dispose();
        wallTurningForces.Dispose();
        unitSeparationForces.Dispose();
        alignmentForces.Dispose();
        unitAvoidanceForces.Dispose();
    }

    public void GetNewPositionsAndVelocities(List<Unit> units, float deltaTime)
    {
        JobHandle velocitiesJobHandle;
        ComputeNewVelocitiesAndMovementsJob velocitiesJob;

        velocitiesJob = new ComputeNewVelocitiesAndMovementsJob()
        {
            deltaTime = deltaTime,
            maxSpeed = SimulationSettings.instance.UnitSpeed,
            maxForce = SimulationSettings.instance.MaxForce,
            forces = forces,
            obstructionMap = walls.map,
            obstructionMapWidth = walls.mapWidth,
            obstructionMapHeight = walls.mapHeight,
            velocities = unitVelocities,
            movements = unitMovements,
            positions = unitPositions
        };

        velocitiesJobHandle = velocitiesJob.Schedule(unitPositions.Length, 64);
        velocitiesJobHandle.Complete();

        JobHandle positionsJobHandle;
        ComputeNewPositionsJob positionsJob;

        positionsJob = new ComputeNewPositionsJob()
        {
            deltaTime = deltaTime,
            unitRadius = SimulationSettings.UnitRadius,
            unitMap = nearbyUnitsManager.unitsMap,
            unitMapWidth = nearbyUnitsManager.mapWidth,
            unitMapHeight = nearbyUnitsManager.mapHeight,
            obstructionMap = walls.map,
            obstructionMapWidth = walls.mapWidth,
            obstructionMapHeight = walls.mapHeight,

            positions = unitPositions,
            newPositions = newUnitPositions,
            movements = unitMovements
        };

        positionsJobHandle = positionsJob.Schedule(unitPositions.Length, 64);
        positionsJobHandle.Complete();

        for (int i = 0; i < units.Count; i++)
        {
            units[i].position = newUnitPositions[i];
            units[i].velocity = unitVelocities[i];
            int X = Mathf.RoundToInt(newUnitPositions[i].x - 0.5f);
            int Y = Mathf.RoundToInt(newUnitPositions[i].y - 0.5f);
            if (units[i].currentCoord.X != X || units[i].currentCoord.Y != Y)
                units[i].currentCoord = new Coord(X, Y);
        }
    }

    public void CleanArrays()
    {
        unitPositions.Dispose();
        newUnitPositions.Dispose();
        unitVelocities.Dispose();
        unitDesiredVelocities.Dispose();
        unitMovements.Dispose();
        forces.Dispose();
        walls.CleanArrays();
        nearbyUnitsManager.Clean();
    }

    [BurstCompile]
    private struct CombineForcesJob : IJobParallelFor
    {
        public NativeArray<Vector2> forces;
        [ReadOnly]
        public NativeArray<Vector2> seekForces;
        [ReadOnly]
        public NativeArray<Vector2> wallRepulsionForces;
        [ReadOnly]
        public NativeArray<Vector2> wallTurningForces;
        [ReadOnly]
        public NativeArray<Vector2> unitSeparationForces;
        [ReadOnly]
        public NativeArray<Vector2> alignmentForces;
        [ReadOnly]
        public NativeArray<Vector2> unitAvoidanceForces;
        [ReadOnly]
        public NativeArray<Vector2> velocities;

        [ReadOnly]
        public float maxForce;

        public void Execute(int index)
        {
            //float unitAvoidanceForceMagnitude = Mathf.Clamp(unitAvoidanceForces[index].magnitude, 0.0f, 1.0f);
            //float speedFactor = 1.0f - Mathf.Clamp(2 * velocities[index].magnitude - 1.0f, 0.0f, 1.0f);
            //float factor = unitAvoidanceForceMagnitude * (speedFactor * speedFactor);
            float factor = Mathf.Clamp((2.0f * unitSeparationForces[index].magnitude - 1.0f), 0.0f, 1.0f);
            Vector2 force =
                (15.0f - 20.0f * factor) * seekForces[index] +
                100.0f * unitSeparationForces[index] +
                50.0f * wallRepulsionForces[index] +
                10.0f * wallTurningForces[index] +
                5.0f * unitAvoidanceForces[index] +
                0.5f * alignmentForces[index];

            forces[index] = force.LimitMagnitude(maxForce);
        }
    }

    [BurstCompile]
    private struct ComputeNewVelocitiesAndMovementsJob : IJobParallelFor
    {
        [ReadOnly]
        public float deltaTime;
        [ReadOnly]
        public float maxSpeed;
        [ReadOnly]
        public float maxForce;
        [ReadOnly]
        public NativeArray<bool> obstructionMap;
        [ReadOnly]
        public int obstructionMapWidth, obstructionMapHeight;

        [ReadOnly]
        public NativeArray<Vector2> forces;
        [ReadOnly]
        public NativeList<Vector2> positions;
        public NativeArray<Vector2> velocities;
        public NativeArray<Vector2> movements;

        public void Execute(int index)
        {
            Vector2 force = forces[index];
            Vector2 velocity = velocities[index];
            Vector2 position = positions[index];

            velocities[index] = ApplyForce(force, velocity);

            Vector2 movement = velocity * deltaTime;
            Vector2 newPosition = new Vector2(position.x + movement.x, position.y + movement.y);
            Vector2 newPositionX = new Vector2(position.x + movement.x, position.y);
            Vector2 newPositionY = new Vector2(position.x, position.y + movement.y);

            if (IsInWallsUnit(newPosition) && !IsInWallsUnit(newPositionX))
            {
                newPosition = newPositionX;
            }
            else if (IsInWallsUnit(newPosition) && !IsInWallsUnit(newPositionY))
            {
                newPosition = newPositionY;
            }
            movements[index] = newPosition - position;
        }

        private Vector2 ApplyForce(Vector2 force, Vector2 velocity)
        {
            force = force.LimitMagnitude(maxForce);
            float speed = velocity.magnitude;
            float factor = Mathf.Clamp01(1.0f - (10.0f - 5 * (speed / maxSpeed)) * deltaTime);
            Vector2 goalVelocity = factor * velocity + (1.0f - factor) * force;
            return goalVelocity.LimitMagnitude(maxSpeed);
        }

        public bool IsInWallsUnit(Vector2 position)
        {
            float radius = SimulationSettings.UnitRadius;
            (int X, int Y) coord = coordFromPosition(position);
            float fracX = position.x - coord.X;
            float fracY = position.y - coord.Y;
            Vector2 frac = new Vector2(fracX, fracY);


            bool upperLeftObstructed = coord.X - 1 < 0 || coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X - 1 + (coord.Y + 1) * obstructionMapWidth];
            bool upperObstructed = coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X + (coord.Y + 1) * obstructionMapWidth];
            bool upperRightObstructed = coord.X + 1 >= obstructionMapWidth || coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X + 1 + (coord.Y + 1) * obstructionMapWidth];
            bool leftObstructed = coord.X - 1 < 0 || !obstructionMap[coord.X - 1 + (coord.Y) * obstructionMapWidth];
            bool centerObstructed = !obstructionMap[coord.X + (coord.Y) * obstructionMapWidth];
            bool rightObstructed = coord.X + 1 >= obstructionMapWidth || !obstructionMap[coord.X + 1 + (coord.Y) * obstructionMapWidth];
            bool bottomLeftObstructed = coord.X - 1 < 0 || coord.Y - 1 < 0 || !obstructionMap[coord.X - 1 + (coord.Y - 1) * obstructionMapWidth];
            bool bottomObstructed = coord.Y - 1 < 0 || !obstructionMap[coord.X + (coord.Y - 1) * obstructionMapWidth];
            bool bottomRightObstructed = coord.X + 1 >= obstructionMapWidth || coord.Y - 1 < 0 || !obstructionMap[coord.X + 1 + (coord.Y - 1) * obstructionMapWidth];

            bool inWallUpperLeft = upperLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 1.0f)) <= radius;
            bool inWallUpper = upperObstructed && 1.0f - fracY <= radius;
            bool inWallUpperRigth = upperRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 1.0f)) <= radius;
            bool inWallLeft = leftObstructed && fracX <= radius;
            bool inWallRight = rightObstructed && 1.0f - fracX <= radius;
            bool inWallBottomLeft = bottomLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 0.0f)) <= radius;
            bool inWallBottom = bottomObstructed && fracY <= radius;
            bool inWallBottomRight = bottomRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 0.0f)) <= radius;

            return (inWallUpperLeft || inWallUpperRigth || inWallBottomLeft || inWallBottomRight || inWallUpper || inWallBottom || inWallLeft || inWallRight || centerObstructed);
        }

        private (int X, int Y) coordFromPosition(Vector2 position)
        {
            return (Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }
    }

    [BurstCompile]
    private struct ComputeNewPositionsJob : IJobParallelFor
    {
        [ReadOnly]
        public float deltaTime;
        [ReadOnly]
        public float unitRadius;
        [ReadOnly]
        public NativeArray<int> unitMap;
        [ReadOnly]
        public int unitMapWidth, unitMapHeight;
        [ReadOnly]
        public NativeArray<bool> obstructionMap;
        [ReadOnly]
        public int obstructionMapWidth, obstructionMapHeight;

        [ReadOnly]
        public NativeList<Vector2> positions;
        public NativeArray<Vector2> newPositions;
        [ReadOnly]
        public NativeList<Vector2> movements;

        public void Execute(int index)
        {
            Vector2 movement = movements[index];
            Vector2 position = positions[index];
            Vector2 newPosition = position + movement;

            (int X, int Y) currentCoord = (Mathf.FloorToInt(newPosition.x * 2), Mathf.FloorToInt(newPosition.y * 2));
            NativeList<(Vector2, Vector2, int)> nearbyUnits = new NativeList<(Vector2, Vector2, int)>(50, Allocator.Temp);
            for (int i = Mathf.Max(currentCoord.X - 3, 0); i < Mathf.Min(currentCoord.X + 4, unitMapWidth); i++)
            {
                for (int j = Mathf.Max(currentCoord.Y - 3, 0); j < Mathf.Min(currentCoord.Y + 4, unitMapHeight); j++)
                {
                    if ((i != currentCoord.X || j != currentCoord.Y) && unitMap[i + j * unitMapWidth] != -1)
                    {
                        int unitIndex = unitMap[i + j * unitMapWidth];
                        nearbyUnits.Add((positions[unitIndex], movements[unitIndex], unitIndex));
                    }
                }
            }

            if (!IsInWallsUnit(newPosition) && !NearbyPositionOccupied(position, newPosition, nearbyUnits, index))
            {
                newPositions[index] = newPosition;
            }
            else
            {
                newPositions[index] = position;
            }
        }

        private (int X, int Y) coordFromPosition(Vector2 position)
        {
            return (Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));
        }

        public bool IsInWallsUnit(Vector2 position)
        {
            float radius = SimulationSettings.UnitRadius;
            (int X, int Y) coord = coordFromPosition(position);
            float fracX = position.x - coord.X;
            float fracY = position.y - coord.Y;
            Vector2 frac = new Vector2(fracX, fracY);


            bool upperLeftObstructed = coord.X - 1 < 0 || coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X - 1 + (coord.Y + 1) * obstructionMapWidth];
            bool upperObstructed = coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X + (coord.Y + 1) * obstructionMapWidth];
            bool upperRightObstructed = coord.X + 1 >= obstructionMapWidth || coord.Y + 1 >= obstructionMapHeight || !obstructionMap[coord.X + 1 + (coord.Y + 1) * obstructionMapWidth];
            bool leftObstructed = coord.X - 1 < 0 || !obstructionMap[coord.X - 1 + (coord.Y) * obstructionMapWidth];
            bool centerObstructed = !obstructionMap[coord.X + (coord.Y) * obstructionMapWidth];
            bool rightObstructed = coord.X + 1 >= obstructionMapWidth || !obstructionMap[coord.X + 1 + (coord.Y) * obstructionMapWidth];
            bool bottomLeftObstructed = coord.X - 1 < 0 || coord.Y - 1 < 0 || !obstructionMap[coord.X - 1 + (coord.Y - 1) * obstructionMapWidth];
            bool bottomObstructed = coord.Y - 1 < 0 || !obstructionMap[coord.X + (coord.Y - 1) * obstructionMapWidth];
            bool bottomRightObstructed = coord.X + 1 >= obstructionMapWidth || coord.Y - 1 < 0 || !obstructionMap[coord.X + 1 + (coord.Y - 1) * obstructionMapWidth];

            bool inWallUpperLeft = upperLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 1.0f)) <= radius;
            bool inWallUpper = upperObstructed && 1.0f - fracY <= radius;
            bool inWallUpperRight = upperRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 1.0f)) <= radius;
            bool inWallLeft = leftObstructed && fracX <= radius;
            bool inWallRight = rightObstructed && 1.0f - fracX <= radius;
            bool inWallBottomLeft = bottomLeftObstructed && Vector2.Distance(frac, new Vector2(0.0f, 0.0f)) <= radius;
            bool inWallBottom = bottomObstructed && fracY <= radius;
            bool inWallBottomRight = bottomRightObstructed && Vector2.Distance(frac, new Vector2(1.0f, 0.0f)) <= radius;

            return (inWallUpperLeft || inWallUpperRight || inWallBottomLeft || inWallBottomRight || inWallUpper || inWallBottom || inWallLeft || inWallRight || centerObstructed);
        }

        private bool NearbyPositionOccupied(Vector2 oldPosition, Vector2 newPosition, NativeList<(Vector2, Vector2, int)> nearbyUnits, int index)
        {
            foreach ((Vector2 otherUnitPosition, Vector2 otherUnitMovement, int otherUnitID) in nearbyUnits)
            {
                float distanceNow = Vector2.Distance(oldPosition, otherUnitPosition);
                float distanceFuture = Vector2.Distance(newPosition, otherUnitPosition + (otherUnitID < index ? otherUnitMovement : Vector2.zero));
                if (distanceFuture < 2 * unitRadius && distanceFuture < distanceNow)
                    return true;
            }
            return false;
        }
    }
}
