using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Diagnostics;
using UnityEngine.UI;
using UnityEngine.UIElements;
using utils;
using static UnityEditor.PlayerSettings;

public class Unit : MonoBehaviour
{
    private bool finishedMoving;

    private Target target;
    float checkIfReachedTargetInterval = 0;

    public Vector2 position;
    public Vector2 velocity;
    protected float time = 0;
    [HideInInspector]
    public Vector2 moveDirection;

    public float speed;
    [HideInInspector]
    public Coord currentCoord;
    private FlowField flowField;
    [HideInInspector]
    public float deltaTime;
    private PathExecutor pathExecutor;
    private RegionalPathExecutor regionalPathExecutor;
    private FlowFieldSupremeCommander flowFieldSupremeCommander;
    private NearbyUnitsManager nearbyUnitsManager;

    public MovementMode movementMode;

    private void Start()
    {
        position = new Vector2(this.transform.position.x, this.transform.position.y);
        currentCoord = Coord.CoordFromPosition(this.transform.position);
        ChangeCoord(currentCoord);
        nearbyUnitsManager = new NearbyUnitsManager(this);
    }

    private void ApplyForce(Vector2 force)
    {
        force = force.LimitMagnitude(SimulationSettings.instance.MaxForce);
        Vector2 desiredVelocity = (1.0f - 20 * Time.deltaTime) * velocity + force;
        //velocity = limitRotation(desiredVelocity);
        velocity = desiredVelocity.LimitMagnitude(SimulationSettings.instance.UnitSpeed);
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

    private Vector2 ComputeForces()
    {
        Vector2 separationForce, alignmentForce, unitTurningForce;
        nearbyUnitsManager.GetNearbyUnitsForces(out separationForce, out alignmentForce, out unitTurningForce);
        Vector2 wallRepulsionForce = Map.instance.walls.GetWallRepulsionForce(this);
        Vector2 wallTurningForce = Map.instance.walls.GetWallTurningForce(this);
        Vector2 separation = separationForce;
        Vector2 alignment = alignmentForce;
        Vector2 seek;

        switch (movementMode)
        {
            case MovementMode.FlowField:
                seek = flowField.getMovementDirection(position);
                break;
            case MovementMode.PathFollowing:
                if (pathExecutor != null) seek = pathExecutor.GetPathFollowingForce();
                else seek = Vector2.zero;
                break;
            case MovementMode.PathFollowingLowerNumberOfPaths:
                if (pathExecutor != null) seek = pathExecutor.GetPathFollowingForce();
                else seek = Vector2.zero;
                break;
            case MovementMode.SupremeCommanderFlowField:
                seek = flowFieldSupremeCommander.GetFlowFieldDirection(position);
                break;
            case MovementMode.RegionalPath:
                seek = regionalPathExecutor.GetSeekForce();
                break;
            default:
                seek = Vector2.zero;
                break;
        }
        var combination =
            1.0f * seek +
            50.0f * separation +
            10.0f * wallRepulsionForce +
            5.0f * wallTurningForce +
            1.0f * unitTurningForce +
            0.5f * alignment;

        return combination.LimitMagnitude(SimulationSettings.instance.MaxForce);
    }

    public void Move()
    {
        Vector2 force = ComputeForces();
        ApplyForce(force);
        Vector2 movement = velocity * Time.deltaTime;
        Vector2 newPosition = new Vector2(position.x + movement.x, position.y + movement.y);
        if (Map.instance.walls.IsInWallsUnit(position))
        {
            Debug.Log("Unit in the walls");
        }
        if (!Map.instance.walls.IsInWallsUnit(newPosition) && !nearbyUnitsManager.NearbyPositionOccupied(newPosition)) {
            position = newPosition;
            transform.position = position;

            Coord newCoord = Coord.CoordFromPosition(position);
            if (currentCoord != newCoord)
            {
                ChangeCoord(newCoord);
            }
            bool reachedTarget = CheckIfReachedTarget();
            if (reachedTarget)
            {
                Simulator.Instance.UnitReachedTarget();
                currentCoord.GetTile().units.Remove(this);
                Destroy(gameObject);
            }
        }
    }

    /*
    private void Update()
    {
        deltaTime = Time.deltaTime;
        if (!finishedMoving)
        {
            finishedMoving = !pathExecutor.ExecutePath(ref path, deltaTime);

            if (!finishedMoving && moveDirection.magnitude > 0)
            {
                pathExecutor.Move(deltaTime);
                Coord newCoord = Coord.CoordFromPosition(position);
                if (currentCoord != newCoord)
                {
                    ChangeCoord(newCoord);
                }
            }
        }
    }
    */

    private void ChangeCoord(Coord newCoord)
    {
        currentCoord.GetTile().units.Remove(this);
        newCoord.GetTile().units.Add(this);
        currentCoord = newCoord;
    }

    public bool MoveTo(Vector2 target)
    {
        Stack<Vector2> path = Pathfinding.ConstructPathAStar(position, target, Pathfinding.StepDistance, 0.2f);
        if (path != null && path.Count > 0)
        {
            finishedMoving = false;
            pathExecutor = new PathExecutor(this, path);
            return true;
        }
        return false;
    }

    public void MoveAlongThePath(Stack<Vector2> path)
    {
        finishedMoving = false;
        pathExecutor = new PathExecutor(this, path);
    }

    public void MoveAlongThePath(List<Vector2> path)
    {
        finishedMoving = false;
        pathExecutor = new PathExecutor(this, path);
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

    public void SetTarget(Target target)
    {
        this.target = target;
    }

    private bool CheckIfReachedTarget()
    {
        bool result = false;
        float distanceToBounds;
        checkIfReachedTargetInterval -= Time.deltaTime;
        if (checkIfReachedTargetInterval <= 0)
        {
            result = target.UnitReachedTarget(this, out distanceToBounds);
            checkIfReachedTargetInterval = distanceToBounds / SimulationSettings.instance.UnitSpeed;
        }
        return result;
    }
}