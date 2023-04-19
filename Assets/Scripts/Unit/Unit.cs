using System.Collections.Generic;
using UnityEngine;
using utils;

public class Unit
{
    private Target target;
    float checkIfReachedTargetInterval = 0;

    public Vector2 position;
    private Vector2 position2;
    public Vector2 velocity;
    public Vector2 desiredVelocity;

    private float maxSpeed;
    [HideInInspector]
    public Coord currentCoord;
    private FlowField flowField;
    private PathExecutor pathExecutor;
    private RegionalPathExecutor regionalPathExecutor;
    private RegionalFlowGraphPathExecutor regionalFlowGraphPathExecutor;
    private RegionalFlowGraphPathUsingSubPathsExecutor regionalFlowGraphPathUsingSubPathsExecutor;
    private FlowFieldSupremeCommander flowFieldSupremeCommander;
    private NearbyUnitsManager nearbyUnitsManager;

    public MovementMode movementMode;

    public void Initialize(Vector2 pos)
    {
        position = pos;
        currentCoord = Coord.CoordFromPosition(pos);
        ChangeCoord(position);
        nearbyUnitsManager = new NearbyUnitsManager(this);
        maxSpeed = SimulationSettings.instance.UnitSpeed;
    }

    private void ApplyForce(Vector2 force, float deltaTime)
    {
        force = force.LimitMagnitude(SimulationSettings.instance.MaxForce);
        float speed = velocity.magnitude;
        float factor = Mathf.Clamp01(1.0f - (20 - 15 * (speed / maxSpeed)) * deltaTime);
        Vector2 goalVelocity = factor * velocity + (1.0f - factor) * force;
        velocity = goalVelocity.LimitMagnitude(maxSpeed);
    }

    /*
    private Vector2 LimitRotation(Vector2 desiredVelocity)
    {
        float angle = Vector2.Angle(desiredVelocity, velocity);
        Vector2 newVelocity;

        var allowedMaxAngle = SimulationSettings.instance.MaxRotationPerSecond;

        if (angle > allowedMaxAngle)
        {
            var angleVector = GeometryHelper.AngleToVector(allowedMaxAngle);
            newVelocity = angleVector * newVelocity.Length();
        }
        else if (-angle > allowedMaxAngle)
        {
            var angleVector = GeometryHelper.AngleToVector(-allowedMaxAngle);
            newVelocity = angleVector * newVelocity.Length();
        }
        //else; //no change

        return newVelocity;
    }
    */

    public Vector2 GetSeekForce(float deltaTime)
    {
        Vector2 seek;

        switch (movementMode)
        {
            case MovementMode.FlowField:
                seek = flowField.getMovementDirection(position);
                break;
            case MovementMode.PathFollowing:
                if (pathExecutor != null) seek = pathExecutor.GetPathFollowingForce(deltaTime);
                else seek = Vector2.zero;
                break;
            case MovementMode.PathFollowingLowerNumberOfPaths:
                if (pathExecutor != null) seek = pathExecutor.GetPathFollowingForce(deltaTime);
                else seek = Vector2.zero;
                break;
            case MovementMode.SupremeCommanderFlowField:
                seek = flowFieldSupremeCommander.GetFlowFieldDirection(position, deltaTime);
                break;
            case MovementMode.RegionalPath:
                seek = regionalPathExecutor.GetSeekForce(deltaTime);
                break;
            case MovementMode.RegionalFlowGraph:
                seek = regionalFlowGraphPathExecutor.GetSeekForce(deltaTime);
                break;
            case MovementMode.RegionalFlowGraphPaths:
                seek = regionalFlowGraphPathUsingSubPathsExecutor.GetSeekForce(deltaTime);
                break;
            default:
                seek = Vector2.zero;
                break;
        }

        return seek;
    }

    public Vector2 ComputeForces(float deltaTime)
    {
        Vector2 seek = GetSeekForce(deltaTime);
        desiredVelocity = SimulationSettings.instance.UnitSpeed * seek;

        Vector2 separationForce, alignmentForce, unitTurningForce;
        nearbyUnitsManager.GetNearbyUnitsForces(deltaTime, out separationForce, out alignmentForce, out unitTurningForce);
 
        Vector2 wallRepulsionForce = Map.instance.walls.GetWallRepulsionForce(this);
        Vector2 wallTurningForce = Map.instance.walls.GetWallTurningForce(this);

        Vector2 combination =
            10.0f * seek +
            100.0f * separationForce +
            50.0f * wallRepulsionForce +
            10.0f * wallTurningForce +
            5.0f * unitTurningForce +
            0.5f * alignmentForce;

        return combination.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    public void CalculateNewPosition(float deltaTime, Vector2 force)
    {
        ApplyForce(force, deltaTime);
        Vector2 movement = velocity * deltaTime;
        Vector2 newPosition = new Vector2(position.x + movement.x, position.y + movement.y);
        Vector2 newPositionX = new Vector2(position.x + movement.x, position.y);
        Vector2 newPositionY = new Vector2(position.x, position.y + movement.y);
        if (Map.instance.walls.IsInWallsUnit(position))
        {
            Debug.Log("Unit in the walls");
        }
        if (Map.instance.walls.IsInWallsUnit(newPosition) && !Map.instance.walls.IsInWallsUnit(newPositionX))
        {
            newPosition = newPositionX;
        }
        else if (Map.instance.walls.IsInWallsUnit(newPosition) && !Map.instance.walls.IsInWallsUnit(newPositionY))
        {
            newPosition = newPositionY;
        }

        if (!Map.instance.walls.IsInWallsUnit(newPosition) && !nearbyUnitsManager.NearbyPositionOccupied(newPosition)) {
            position2 = newPosition;
            ChangeCoord(position2);
            TargetReachedTestAndResponse(deltaTime);
        }
    }

    public void TargetReachedTestAndResponse(float deltaTime)
    {
        bool reachedTarget = CheckIfReachedTarget(deltaTime);
        if (reachedTarget)
        {
            Simulator.Instance.UnitReachedTarget(this);
            currentCoord.UnitsAtTile().Remove(this);
        }
    }

    public void UpdatePositionAndVelocity()
    {
        position = position2;
    }

    public void ChangeCoord(Vector2 position)
    {
        Coord newCoord = Coord.CoordFromPosition(position);
        if (currentCoord != newCoord)
        {
            currentCoord.UnitsAtTile().Remove(this);
            newCoord.UnitsAtTile().Add(this);
            currentCoord = newCoord;
        }
    }

    public bool MoveTo(Vector2 target)
    {
        Stack<Vector2> path = Pathfinding.ConstructPathAStar(position, target, Pathfinding.StepDistance, 0.2f);
        if (path != null && path.Count > 0)
        {
            pathExecutor = new PathExecutor(this, ref path);
            return true;
        }
        return false;
    }

    public void MoveAlongThePath(Stack<Vector2> path)
    {
        pathExecutor = new PathExecutor(this, ref path);
    }

    public void MoveAlongThePath(List<Vector2> path)
    {
        pathExecutor = new PathExecutor(this, ref path);
    }

    public void UseFlowField(FlowField flowField)
    {
        this.flowField = flowField;
    }

    public void UseSupremeCommanderFlowField(FlowFieldSupremeCommander scFlowField)
    {
        flowFieldSupremeCommander = scFlowField;
    }

    public void UseRegionalPath(RegionalPath path)
    {
        regionalPathExecutor = new RegionalPathExecutor(this, path);
    }

    public void UseRegionalFlowGraphPath(RegionalFlowGraphPath graphPath, int startingPath)
    {
        regionalFlowGraphPathExecutor = new RegionalFlowGraphPathExecutor(this, graphPath, startingPath);
    }

    public void UseRegionalFlowGraphPathUsingSubPaths(RegionalFlowGraphPathUsingSubPaths graphPath, int startingPath)
    {
        regionalFlowGraphPathUsingSubPathsExecutor = new RegionalFlowGraphPathUsingSubPathsExecutor(this, graphPath, startingPath);
    }

    public void SetTarget(Target target)
    {
        this.target = target;
    }

    private bool CheckIfReachedTarget(float deltaTime)
    {
        bool result = false;
        float distanceToBounds;
        checkIfReachedTargetInterval -= deltaTime;
        if (checkIfReachedTargetInterval <= 0)
        {
            result = target.UnitReachedTarget(this, out distanceToBounds);
            checkIfReachedTargetInterval = distanceToBounds / SimulationSettings.instance.UnitSpeed;
        }
        return result;
    }
}