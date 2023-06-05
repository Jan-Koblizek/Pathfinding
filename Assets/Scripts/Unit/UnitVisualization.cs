using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitVisualization : MonoBehaviour
{
    private SpriteRenderer rndr;
    public Vector2 position { 
        get {  return transform.position; } 
        set { transform.position = value; }
    }

    private void Start()
    {
        rndr = GetComponent<SpriteRenderer>();
        SetColor(Color.red);
    }
    public void Remove()
    {
        position = new Vector2(-1000, -1000);
        Destroy(gameObject);
    }

    public void SetColor(Color color)
    {
        rndr.material.color = color;
    }
}
