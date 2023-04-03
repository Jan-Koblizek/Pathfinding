using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Target : MonoBehaviour
{
    public GameObject prefab;
    public float radius;
    public float Radius { get { return radius; } }
    private Vector2 centre;
    public Vector2 Center { get { return centre; } }
    
    public Target(Vector2 centre, float radius)
    {
        this.centre = centre;
        this.radius = radius;
        GameObject go = Instantiate(prefab, new Vector3(centre.x, centre.y, 0.0f), Quaternion.identity, null);
        go.transform.localScale = new Vector3(2 * radius, 2 * radius, 2 * radius);
    }

    public bool UnitReachedTarget(Unit unit, out float distanceToBounds)
    {
        float distanceToCentre = Vector2.Distance(unit.position, Center);
        distanceToBounds = distanceToCentre - radius;
        if (distanceToBounds <= 0) return true;
        else return false;
    }

    private void Start()
    {
        this.centre = new Vector2(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y));
        transform.localScale = new Vector3(2 * radius, 2 * radius, 2 * radius);
    }
}