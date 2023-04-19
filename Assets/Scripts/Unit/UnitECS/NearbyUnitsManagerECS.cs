using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;
using utils;

public class NearbyUnitsManagerECS
{
    public NativeArray<int> unitsMap;
    public int mapWidth;
    public int mapHeight;
    public void Initialize(List<Unit> units, bool newMap)
    {
        if (newMap)
        {
            mapWidth = 2 * Map.instance.passabilityMap.GetLength(0);
            mapHeight = 2 * Map.instance.passabilityMap.GetLength(1);
            unitsMap = new NativeArray<int>(mapWidth * mapHeight, Allocator.Persistent);

            for (int i = 0; i < unitsMap.Length; i++)
            {
                unitsMap[i] = -1;
            }
        }

        for (int i = 0; i < units.Count; i++)
        {
            Vector2 unitPosition = units[i].position;
            int X = Mathf.FloorToInt(unitPosition.x * 2);
            int Y = Mathf.FloorToInt(unitPosition.y * 2);
            unitsMap[X + Y * mapWidth] = i;
        }
    }

    public void SetUnitPositions(NativeList<Vector2> unitPositions)
    {
        JobHandle positionsJobHandle;
        SetPositionsJob positionsJob;

        positionsJob = new SetPositionsJob()
        {
            unitsMap = unitsMap,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            unitPositions = unitPositions,
        };

        positionsJobHandle = positionsJob.Schedule(unitPositions.Length, 64);
        positionsJobHandle.Complete();
    }

    [BurstCompile]
    private struct SetPositionsJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> unitsMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;

        [ReadOnly]
        public NativeList<Vector2> unitPositions;

        public void Execute(int index)
        {
            Vector2 unitPosition = unitPositions[index];
            int X = Mathf.FloorToInt(unitPosition.x * 2);
            int Y = Mathf.FloorToInt(unitPosition.y * 2);
            unitsMap[X + Y * mapWidth] = index;
        }
    }

    public void CleanUnitTiles(NativeList<Vector2> unitPositions)
    {
        JobHandle positionsJobHandle;
        CleanPositionsJob positionsJob;

        positionsJob = new CleanPositionsJob()
        {
            unitsMap = unitsMap,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            unitPositions = unitPositions,
        };

        positionsJobHandle = positionsJob.Schedule(unitPositions.Length, 64);
        positionsJobHandle.Complete();
    }

    [BurstCompile]
    private struct CleanPositionsJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> unitsMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;

        [ReadOnly]
        public NativeList<Vector2> unitPositions;

        public void Execute(int index)
        {
            Vector2 unitPosition = unitPositions[index];
            int X = Mathf.FloorToInt(unitPosition.x * 2);
            int Y = Mathf.FloorToInt(unitPosition.y * 2);

            for (int i = -1; i < 2; i++)
            {
                for (int j = -1; j < 2; j++)
                {
                    int clearingIndex = X + i + (Y + j) * mapWidth;
                    if (clearingIndex >= 0 && clearingIndex <= unitsMap.Length)
                    {
                        unitsMap[clearingIndex] = -1;
                    }
                }
            }
        }
    }

    public void UpdateUnitPositions(NativeList<Vector2> unitPositions)
    {
        JobHandle positionsJobHandle;
        UpdatePositionsJob positionsJob;

        positionsJob = new UpdatePositionsJob()
        {
            unitsMap = unitsMap,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            unitPositions = unitPositions,
        };

        positionsJobHandle = positionsJob.Schedule(unitPositions.Length, 64);
        positionsJobHandle.Complete();
    }

    [BurstCompile]
    private struct UpdatePositionsJob : IJobParallelFor
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeArray<int> unitsMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;

        [ReadOnly]
        public NativeList<Vector2> unitPositions;

        public void Execute(int index)
        {
            Vector2 unitPosition = unitPositions[index];
            int X = Mathf.FloorToInt(unitPosition.x * 2);
            int Y = Mathf.FloorToInt(unitPosition.y * 2);

            for (int i = -1; i < 2; i++)
            {
                for (int j = -1; j < 2; j++)
                {
                    int clearingIndex = X + i + (Y + j) * mapWidth;
                    if (clearingIndex >= 0 && clearingIndex <= unitsMap.Length && unitsMap[clearingIndex] == index)
                    {
                        unitsMap[clearingIndex] = -1;
                    }
                }
            }

            unitsMap[X + Y * mapWidth] = index;
        }
    }

    public (NativeArray<Vector2> separationForces, NativeArray<Vector2> alignmentForces, NativeArray<Vector2> avoidanceForces) GetNearbyUnitsForces(NativeList<Vector2> unitPositions, NativeList<Vector2> unitVelocities, NativeList<Vector2> desiredVelocities, float deltaTime)
    {
        NativeArray<Vector2> separationForces = new NativeArray<Vector2>(unitPositions.Length, Allocator.TempJob);
        NativeArray<Vector2> alignmentForces = new NativeArray<Vector2>(unitPositions.Length, Allocator.TempJob);
        NativeArray<Vector2> avoidanceForces = new NativeArray<Vector2>(unitPositions.Length, Allocator.TempJob);
        JobHandle unitForcesJobHandle;
        NearbyUnitForcesJob unitForcesJob;

        unitForcesJob = new NearbyUnitForcesJob()
        {
            deltaTime = deltaTime,
            unitsMap = unitsMap,
            mapWidth = mapWidth,
            mapHeight = mapHeight,
            maxForce = SimulationSettings.instance.MaxForce,
            unitRadius = SimulationSettings.UnitRadius,
            unitPositions = unitPositions,
            unitVelocities = unitVelocities,
            desiredVelocities = desiredVelocities,

            separationForces = separationForces,
            alignmentForces = alignmentForces,
            avoidanceForces = avoidanceForces
        };

        unitForcesJobHandle = unitForcesJob.Schedule(unitPositions.Length, 64);
        unitForcesJobHandle.Complete();

        return (separationForces, alignmentForces, avoidanceForces);
    }

    [BurstCompile]
    private struct NearbyUnitForcesJob : IJobParallelFor
    {
        public float deltaTime;
        [ReadOnly]
        public NativeArray<int> unitsMap;
        [ReadOnly]
        public int mapWidth;
        [ReadOnly]
        public int mapHeight;
        [ReadOnly]
        public float maxForce;
        [ReadOnly]
        public float unitRadius;

        [ReadOnly]
        public NativeList<Vector2> unitPositions;
        [ReadOnly]
        public NativeList<Vector2> unitVelocities;
        [ReadOnly]
        public NativeList<Vector2> desiredVelocities;

        public NativeArray<Vector2> separationForces;
        public NativeArray<Vector2> alignmentForces;
        public NativeArray<Vector2> avoidanceForces;

        public void Execute(int index)
        {
            Vector2 unitPosition = unitPositions[index];
            (int X, int Y) currentCoord = (Mathf.FloorToInt(unitPosition.x * 2), Mathf.FloorToInt(unitPosition.y * 2));
            NativeList<(Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID)> nearbyUnits = new NativeList<(Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID)>(50, Allocator.Temp);
            for (int i = Mathf.Max(currentCoord.X - 5, 0); i < Mathf.Min(currentCoord.X + 6, mapWidth); i++)
            {
                for (int j = Mathf.Max(currentCoord.Y - 5, 0); j < Mathf.Min(currentCoord.Y + 6, mapHeight); j++)
                {
                    if ((i != currentCoord.X || j != currentCoord.Y) && unitsMap[i + j * mapWidth] != -1)
                    {
                        int unitIndex = unitsMap[i + j * mapWidth];
                        nearbyUnits.Add((unitPositions[unitIndex], unitVelocities[unitIndex], desiredVelocities[unitIndex], unitIndex));
                    }
                }
            }

            separationForces[index] = getSeparationForce(index, nearbyUnits);
            alignmentForces[index] = getAlignmentForce(index, nearbyUnits);
            avoidanceForces[index] = getUnitAvoidanceForce(index, nearbyUnits);
            nearbyUnits.Dispose();
        }

        private Vector2 getUnitAvoidanceForce(int index, NativeList<(Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID)> nearbyUnits)
        {
            var overallAvoidanceForce = Vector2.zero;
            foreach ((Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID) unit in nearbyUnits)
            {
                if (Vector2.Distance(unitPositions[index], unit.position) < 1.6f * 2 * unitRadius)
                {
                    Vector2 avoidanceForce = GetVelocitySteeringFromAnotherUnit(index, unit);
                    overallAvoidanceForce += avoidanceForce;
                }
            }
            return overallAvoidanceForce.LimitMagnitude(maxForce);
        }

        private Vector2 GetVelocitySteeringFromAnotherUnit(int index, (Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID) otherUnit)
        {
            var collidingRadius = 2 * unitRadius;
            collidingRadius *= 1;
            var relativePosition = (unitPositions[index]) - (otherUnit.otherUnitID < index ? otherUnit.position + otherUnit.velocity * deltaTime : otherUnit.position);
            var relativeVelocity = desiredVelocities[index] - otherUnit.desiredVelocity;

            var possibleCollisionAtStep = relativePosition.magnitude / relativeVelocity.magnitude;

            //if (possibleCollisionAtStep > LOOKAHEAD)
            //    return Vector2.Zero;

            if (Geometry.CircleIntersectsRay(relativePosition, relativePosition + relativeVelocity, collidingRadius))
            {
                float angle = Utilities.Modulo2Pi(relativeVelocity.ToAngle() - Mathf.PI / 2);
                if (angle == 0)
                {
                    uint seed = (uint)index + (uint)(otherUnit.position.x * 1352484635 + otherUnit.position.y * 24622789637);
                    if (seed == 0) seed = 42;
                    angle = new Unity.Mathematics.Random(seed).NextFloat(-1.0f, 1.0f) * 0.001f;
                }

                var normal = GetVelocityAvoidanceDirectionVector(relativePosition, angle, collidingRadius, relativeVelocity, out float size);
                return normal;
            }
            else
            {
                return Vector2.zero;
            }
        }

        /// <summary>
        /// Computes the turn vector to avoid collision in ORCA (does not handle same direction approach of different speed moving object)
        /// </summary>
        /// <param name="relativePosition"></param>
        /// <param name="angle"></param>
        /// <param name="collidingRadius"></param>
        /// <param name="relativeVelocity"></param>
        /// <param name="size">how much steering is necessary</param>   
        /// <returns></returns>
        private Vector2 GetVelocityAvoidanceDirectionVector(Vector2 relativePosition, float angle, float collidingRadius, Vector2 relativeVelocity, out float size)
        {
            var horizontalIntersection = Geometry.GetHorizontalIntersection(relativePosition, angle, 0);
            var verticalIntersection = Geometry.GetVerticalIntersection(relativePosition, angle, 0);

            var intersection = Mathf.Abs(horizontalIntersection.x) < Mathf.Abs(verticalIntersection.y)
                        ? horizontalIntersection
                        : verticalIntersection;

            //DebugOutput.WriteLine($"[{ID} (vs{unit.ID})] rv:{relativeVelocity}, rp:{relativePosition}, inters: h:{horizontalIntersection.X}, v:{verticalIntersection.Y}");

            var intersectionDistance = intersection.x == 0 ? intersection.y : intersection.x;
            size = intersectionDistance > 0 ? collidingRadius - intersectionDistance : collidingRadius + intersectionDistance;
            //Debug.Assert(Mathf.Abs(intersectionDistance) <= collidingRadius * 1.5f);
            //if (Mathf.Abs(intersectionDistance) > collidingRadius * 1.5f) Debug.Log(intersectionDistance);
            if (intersection == verticalIntersection)
            {
                if (relativeVelocity.x * verticalIntersection.y < 0)
                    return relativeVelocity.PerpendicularClockwise();
                return relativeVelocity.PerpendicularCounterClockwise();
            }
            else
            {
                if (relativeVelocity.y * horizontalIntersection.x > 0)
                    return relativeVelocity.PerpendicularClockwise();
                return relativeVelocity.PerpendicularCounterClockwise();
            }
        }

        private Vector2 getSeparationForce(int index, NativeList<(Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID)> nearbyUnits)
        {
            Vector2 aggregateForce = Vector2.zero;
            foreach ((Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID) unit in nearbyUnits)
            {
                Vector2 oneUnitForce = getOneUnitSeparationForce(index, unit);
                aggregateForce += oneUnitForce;
            }
            return aggregateForce;
        }

        private Vector2 getOneUnitSeparationForce(int index, (Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID) otherUnit)
        {
            Vector2 offset = (unitPositions[index] + deltaTime * unitVelocities[index]) - (otherUnit.position + otherUnit.velocity * deltaTime);
            //Vector2 offset = unitPositions[index] - (otherUnit.position + (otherUnit.otherUnitID < index ? otherUnit.velocity * deltaTime : Vector2.zero));
            float distance = offset.magnitude;
            float distanceBounds = distance - 2 * unitRadius;
            if (distance != 0) offset.Normalize();
            Vector2 maxForce2 = maxForce * offset;

            //Units collide with each other
            if (distanceBounds <= 0) return maxForce2;

            Vector2 repulsiveForce;
            if (distance != 0)
            {
                repulsiveForce = maxForce2 * SeparationForceCalculator(distanceBounds / 1.0f);
                return repulsiveForce.LimitMagnitude(maxForce);
            }
            return Vector2.zero;
        }

        private float SeparationForceCalculator(float boundsDistance)
        {
            float factor = Mathf.Clamp01(1.0f - boundsDistance);
            return Mathf.Max(factor * factor * factor * factor * factor * factor, factor * factor * 0.25f);
        }

        private Vector2 getAlignmentForce(int index, NativeList<(Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID)> nearbyUnits)
        {
            Vector2 sum = Vector2.zero;
            foreach ((Vector2 position, Vector2 velocity, Vector2 desiredVelocity, int otherUnitID) unit in nearbyUnits) sum += unit.velocity;
            if (sum.magnitude == 0) return sum;
            Vector2 desiredVelocity = sum / nearbyUnits.Length;
            return (desiredVelocity - unitVelocities[index]).LimitMagnitude(maxForce);
        }
    }

    public void Clean()
    {
        unitsMap.Dispose();
    }
}
