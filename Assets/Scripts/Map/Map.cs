using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Diagnostics;

public class Map : MonoBehaviour
{
    public SpriteRenderer mapPlane;
    private Texture2D obstructionMap;
    [HideInInspector]
    public Texture2D groundTexture;

    [HideInInspector]
    public bool[,] passabilityMap;
    [HideInInspector]
    public List<Unit>[,] unitsMap;
    //public MapSmallChunk[,] smallChunks;
    //public MapLargeChunk[,] largeChunks;

    public static Map instance;
    public Walls walls;


    private void Awake()
    {
        instance = this;
    }
    public void Initialize(Texture2D map)
    {
        obstructionMap = map;
        int width = (obstructionMap.width / 16) * 16;
        int height = (obstructionMap.height / 16) * 16;

        passabilityMap = new bool[width, height];
        groundTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        groundTexture.filterMode = FilterMode.Point;
        mapPlane.gameObject.transform.localScale = new Vector3(1, 1, 1);
        mapPlane.gameObject.transform.position = new Vector3(0, 0, 0);
        unitsMap = new List<Unit>[width, height];
        //smallChunks = new MapSmallChunk[width / 4, height / 4];
        //largeChunks = new MapLargeChunk[width / 16, height / 16];

        for (int i = 0; i < height; i += 1)
        {
            for (int j = 0; j < width; j += 1)
            {
                Color pixel1 = obstructionMap.GetPixel(j, i);
                if (pixel1.r > 0.5 || pixel1.g > 0.5 || pixel1.b > 0.5)
                {
                    passabilityMap[j, i] = true;
                    groundTexture.SetPixel(j, i, Color.white);

                }
                else
                {
                    passabilityMap[j, i] = false;
                    groundTexture.SetPixel(j,i, Color.black);
                }
                unitsMap[j, i] = new List<Unit>();
            }
        }

        walls = new Walls();
        walls.Initialize(this);
        Camera.main.GetComponent<CameraController>().UpdateBounds();
        RedrawTexture();
    }

    public void RedrawTexture()
    {
        groundTexture.Apply();
        Sprite groundSprite = Sprite.Create(groundTexture, new Rect(0, 0, groundTexture.width, groundTexture.height), new Vector2(0, 0), 1);
        mapPlane.sprite = groundSprite;
    }

    public bool GetTilePassable(Coord coord)
    {
        if (coord.X < passabilityMap.GetLength(0) && coord.X >= 0 && coord.Y < passabilityMap.GetLength(1) && coord.Y >= 0)
        {
            return passabilityMap[coord.X, coord.Y];
        }
        return false;
    }

    public bool GetTilePassable(int x, int y)
    {
        if (x < passabilityMap.GetLength(0) && x >= 0 && y < passabilityMap.GetLength(1) && y >= 0)
        {
            return passabilityMap[x, y];
        }
        return false;
    }
}
[Serializable]
public class ColorGameObjectPair
{
    public Color color;
    public GameObject gameObject;
}
public class Coord : IEquatable<Coord>
{
    public int X;
    public int Y;

    public Coord(int x, int y)
    {
        this.X = x;
        this.Y = y;
    }

    public static Coord CoordFromPosition(Vector3 position)
    {
        return new Coord(Mathf.RoundToInt(position.x - 0.5f), Mathf.RoundToInt(position.y - 0.5f));
    }

    public Vector2 GetWorldPosition()
    {
        return new Vector2(X + 0.5f, Y + 0.5f);
    }

    public bool WithinBounds()
    {
        return X >= 0 && X < Map.instance.passabilityMap.GetLength(0) && Y >= 0 && Y < Map.instance.passabilityMap.GetLength(1);
    }

    public bool Equals(Coord b)
    {
        if (this.X == b.X && this.Y == b.Y)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(Coord a, object b)
    {
        if (a.Equals(b)) { return true; }
        return false;
    }

    public static bool operator !=(Coord a, object b)
    {
        if (a.Equals(b)) { return false; }
        return false;
    }

    public static bool operator ==(Coord a, Coord b)
    {
        if (a.Equals(b)) { return true; }
        return false;
    }

    public static bool operator !=(Coord a, Coord b)
    {
        if (a.Equals(b)) { return false; }
        return true;
    }

    public bool Passable()
    {
        return Map.instance.passabilityMap[X, Y];
    }

    public List<Unit> UnitsAtTile()
    {
        return Map.instance.unitsMap[X, Y];
    }

    public override string ToString()
    {
        return "Coord: {" + X + ", " + Y + "}";
    }
}