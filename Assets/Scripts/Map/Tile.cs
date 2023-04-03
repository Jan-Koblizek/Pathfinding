using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public bool obstructed;
    [HideInInspector]
    public List<Unit> units = new List<Unit>();
    public Coord coord;
}