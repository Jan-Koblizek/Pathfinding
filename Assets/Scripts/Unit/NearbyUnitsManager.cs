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

    public void GetNearbyUnitsForces(out Vector2 separationForce, out Vector2 alignmentForce, out Vector2 unitAvoidanceForce)
    {
        refreshNearbyUnits();
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
            Vector2 avoidanceForce = GetSteeringFromAnotherUnit(unit);
            overallAvoidanceForce += avoidanceForce;
        }
        return overallAvoidanceForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private Vector2 GetSteeringFromAnotherUnit(Unit otherUnit)
    {
        Vector2 relativePosition = unit.position - otherUnit.position;
        Vector2 relativeVelocity = unit.velocity - otherUnit.velocity;
        float timeToCollision = relativePosition.magnitude / relativeVelocity.magnitude;
        relativePosition.Normalize();
        relativeVelocity.Normalize();

        if (Geometry.CircleLineIntersection(relativePosition, relativePosition + relativeVelocity, 2*SimulationSettings.UnitRadius))
        {
            Vector2 direction;
            if (Vector2.SignedAngle(relativePosition, relativeVelocity) > 0)
            {
                direction = relativePosition.Rotate(-90);
            }
            else
            {
                direction = relativePosition.Rotate(90);
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
        if (distance != 0) repulsiveForce = maxForce * SeparationForceCalculator(distance / 1.2f);
        else throw new UnitsOverEachOtherException("Units occupy the same position - this should not happen");
        return repulsiveForce.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private float SeparationForceCalculator(float boundsDistance)
    {
        float factor = Mathf.Clamp01(1 - boundsDistance);
        return factor * factor;
    }

    private Vector2 getAlignmentForce()
    {
        Vector2 sum = Vector2.zero;
        foreach (Unit unit in nearbyUnits) sum += unit.velocity;
        if (sum.magnitude == 0) return sum;
        Vector2 desiredVelocity = sum / nearbyUnits.Count;
        return (desiredVelocity - unit.velocity).LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    private void refreshNearbyUnits()
    {
        nearbyUnitsUpdateTime += Time.deltaTime;
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
        for (int i = Mathf.Max(currentCoord.X - 2, 0); i < Mathf.Min(currentCoord.X + 3, Map.instance.tiles.GetLength(0)); i++)
        {
            for (int j = Mathf.Max(currentCoord.Y - 2, 0); j < Mathf.Min(currentCoord.Y + 3, Map.instance.tiles.GetLength(1)); j++)
            {
                nearbyUnits.AddRange(Map.instance.tiles[i, j].units);
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