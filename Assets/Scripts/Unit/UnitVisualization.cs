using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitVisualization : MonoBehaviour
{
    public Vector2 position { 
        get {  return transform.position; } 
        set { transform.position = value; }
    }

    public void Remove()
    {
        position = new Vector2(-1000, -1000);
        Destroy(gameObject);
    }
}
