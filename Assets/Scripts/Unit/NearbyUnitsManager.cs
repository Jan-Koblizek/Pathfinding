using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using utils;

public class NearbyUnitsManager
{
    private Unit unit;
    private List<Unit> nearbyUnits;
    private float nearbyUnitsUpdateTime = 0.0f;
    private const float nearbyUnitsRefreshPeriod = 0.2f;
    public NearbyUnitsManager(Unit unit)
    {
        this.unit = unit;
        this.nearbyUnits = new List<Unit>();
    }

    public void GetNearbyUnitsForces(float deltaTime, out Vector2 separationForce, out Vector2 alignmentForce, out Vector2 unitAvoidanceForce)
    {
        refreshNearbyUnits(deltaTime);
        separationForce = getSeparationForce();
        alignmentForce = getAlignmentForce();
        unitAvoidanceForce = getUnitAvoidanceForce();
    }

    public bool NearbyPositionOccupied(Vector2 position)
    {
        foreach (Unit unit in nearbyUnits)
        {
            float distanceNow = Vector2.Distance(this.unit.position, unit.position);
            float distanceFuture = Vector2.Distance(position, unit.position);
            if (distanceFuture < 2*SimulationSettings.UnitRadius && distanceFuture < distanceNow) 
                return true;
        }
        return false;
    }

    protected Vector2 getUnitAvoidanceForce()
    {
        var overallAvoidanceForce = Vector2.zero;
        foreach (Unit unit in nearbyUnits)
        {
            if (Vector2.Distance(this.unit.position, unit.position) < 1.6f * 2 * SimulationSettings.UnitRadius)
            {
                Vector2 avoidanceForce = GetVelocitySteeringFromAnotherUnit(unit);
                //Vector2 avoidanceForce = GetSteeringFromAnotherUnit(unit);
                overallAvoidanceForce += avoidanceForce;
            }
        }
        return overallAvoidanceForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private Vector2 GetVelocitySteeringFromAnotherUnit(Unit otherUnit)
    {
        var collidingRadius = 2 * SimulationSettings.UnitRadius;
        collidingRadius *= 1;
        var relativePosition = (this.unit.position) - (otherUnit.position);
        var relativeVelocity = this.unit.desiredVelocity - otherUnit.desiredVelocity;

        var possibleCollisionAtStep = relativePosition.magnitude / relativeVelocity.magnitude;

        //if (possibleCollisionAtStep > LOOKAHEAD)
        //    return Vector2.Zero;

        if (Geometry.CircleIntersectsRay(relativePosition, relativePosition + relativeVelocity, collidingRadius))
        {
            float angle = Utilities.Modulo2Pi(relativeVelocity.ToAngle() - Mathf.PI / 2);
            if (angle == 0)
            {
                angle = Random.Range(1.0f, -1.0f) * 0.001f;
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

    private Vector2 GetSteeringFromAnotherUnit(Unit otherUnit)
    {
        Vector2 relativePosition = unit.position - otherUnit.position;
        Vector2 relativeVelocity = unit.desiredVelocity - otherUnit.desiredVelocity;
        float timeToCollision = (relativePosition.magnitude - 2*SimulationSettings.UnitRadius) / relativeVelocity.magnitude;
        //Debug.Log($"angle1 {Vector2.SignedAngle(relativePosition, relativeVelocity)}, angle2 {Vector2.SignedAngle(relativePosition, relativeVelocity)}");

        if (Geometry.CircleIntersectsRay(relativePosition, relativePosition + relativeVelocity, 2*SimulationSettings.UnitRadius))
        {
            relativePosition.Normalize();
            relativeVelocity.Normalize();
            Vector2 direction;
            if (Vector2.SignedAngle(relativePosition, relativeVelocity) > 0)
            {
                direction = relativePosition.Rotate(90);
            }
            else
            {
                direction = relativePosition.Rotate(-90);
            }
            return direction / timeToCollision;
        }
        else
        {
            return Vector2.zero;
        }
    }

    private Vector2 getSeparationForce()
    {
            Vector2 aggregateForce = Vector2.zero;
            foreach (Unit unit in nearbyUnits)
            {
                Vector2 oneUnitForce = getOneUnitSeparationForce(unit);
                aggregateForce += oneUnitForce;
            }
            return aggregateForce;
    }

    private Vector2 getOneUnitSeparationForce(Unit otherUnit)
    {
        Vector2 offset = unit.position - otherUnit.position;
        float distance = offset.magnitude;
        float distanceBounds = distance - 2 * SimulationSettings.UnitRadius;
        if (distance != 0) offset.Normalize();
        var maxForce = SimulationSettings.instance.MaxForce * offset;

        //Units collide with each other
        if (distanceBounds <= 0) return maxForce;

        Vector2 repulsiveForce;
        if (distance != 0) repulsiveForce = maxForce * SeparationForceCalculator(distanceBounds / 1.0f);
        else throw new UnitsOverEachOtherException("Units occupy the same position - this should not happen");
        return repulsiveForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private float SeparationForceCalculator(float boundsDistance)
    {
        float factor = Mathf.Clamp01(1.0f - boundsDistance);
        return factor * factor * factor * factor * factor * factor;
    }

    private Vector2 getAlignmentForce()
    {
        Vector2 sum = Vector2.zero;
        foreach (Unit unit in nearbyUnits) sum += unit.velocity;
        if (sum.magnitude == 0) return sum;
        Vector2 desiredVelocity = sum / nearbyUnits.Count;
        return (desiredVelocity - unit.velocity).LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private void refreshNearbyUnits(float deltaTime)
    {
        nearbyUnitsUpdateTime += deltaTime;
        if (nearbyUnitsUpdateTime >= 0.0f)
        {
            nearbyUnitsUpdateTime = -nearbyUnitsRefreshPeriod;
            updateNearbyUnits();
        }
    }

    private void updateNearbyUnits()
    {
        Coord currentCoord = unit.currentCoord;
        nearbyUnits = new List<Unit>();
        for (int i = Mathf.Max(currentCoord.X - 2, 0); i < Mathf.Min(currentCoord.X + 3, Map.instance.passabilityMap.GetLength(0)); i++)
        {
            for (int j = Mathf.Max(currentCoord.Y - 2, 0); j < Mathf.Min(currentCoord.Y + 3, Map.instance.passabilityMap.GetLength(1)); j++)
            {
                nearbyUnits.AddRange(Map.instance.unitsMap[i, j]);
            }
        }
        nearbyUnits.Remove(unit);
    }
}

public class UnitsOverEachOtherException : System.Exception
{
    public UnitsOverEachOtherException() { }
    public UnitsOverEachOtherException(string message)
        : base(message) { }
    public UnitsOverEachOtherException(string message, System.Exception inner)
        : base(message, inner) { }
}