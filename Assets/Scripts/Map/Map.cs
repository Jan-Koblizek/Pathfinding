using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Diagnostics;

public class Map : MonoBehaviour
{
    public GameObject tilesParent;
    private Texture2D groundMap;

    public GameObject obstructedTilePrefab;
    public GameObject freeTilePrefab;

    [HideInInspector]
    public Tile[,] tiles;
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
        groundMap = map;
        int width = (groundMap.width / 16) * 16;
        int height = (groundMap.height / 16) * 16;

        if (tiles != null)
        {
            for (int i = 0; i < tiles.GetLength(0); i++)
            {
                for (int j = 0; j < tiles.GetLength(1); j++)
                {
                    Destroy(tiles[i, j].gameObject);
                }
            }
        }

        tiles = new Tile[width, height];
        //smallChunks = new MapSmallChunk[width / 4, height / 4];
        //largeChunks = new MapLargeChunk[width / 16, height / 16];

        for (int i = 0; i < height; i += 1)
        {
            for (int j = 0; j < width; j += 1)
            {
                Color pixel1 = groundMap.GetPixel(j, i);
                if (pixel1.r > 0.5)
                {
                    GameObject go = Instantiate(freeTilePrefab, new Vector3(j, i, 0.0f), Quaternion.identity, tilesParent.transform);
                    Tile tile = go.GetComponent<Tile>();
                    tiles[j, i] = tile;
                    tile.coord = new Coord(j, i);
                }
                else
                {
                    GameObject go = Instantiate(obstructedTilePrefab, new Vector3(j, i, 0.0f), Quaternion.identity, tilesParent.transform);
                    Tile tile = go.GetComponent<Tile>();
                    tiles[j, i] = tile;
                    tile.coord = new Coord(j, i);
                }
            }
        }
        walls = new Walls();
        walls.Initialize(this);
        Camera.main.GetComponent<CameraController>().UpdateBounds();
    }

    public Tile GetTile(Coord coord)
    {
        if (coord.X < tiles.GetLength(0) && coord.X >= 0 && coord.Y < tiles.GetLength(1) && coord.Y >= 0)
        {
            return tiles[coord.X, coord.Y];
        }
        return null;
    }

    public Tile GetTile(int x, int y)
    {
        if (x < tiles.GetLength(0) && x >= 0 && y < tiles.GetLength(1) && y >= 0)
        {
            return tiles[x, y];
        }
        return null;
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
        return new Coord(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
    }

    public Vector2 GetWorldPosition()
    {
        return new Vector2(X, Y);
    }

    public bool WithinBounds()
    {
        return X >= 0 && X < Map.instance.tiles.GetLength(0) && Y >= 0 && Y < Map.instance.tiles.GetLength(1);
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

    public Tile GetTile()
    {
        return Map.instance.tiles[X, Y];
    }

    public override string ToString()
    {
        return "Coord: {" + X + ", " + Y + "}";
    }
}