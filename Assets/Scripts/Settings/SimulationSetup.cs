using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/SimulationSetup",
                 fileName = "SimulationSetup")]
public class SimulationSetup : ScriptableObject
{
    public Texture2D mapTexture;
    public Texture2D unitStartMap;
    public float targetSize;

    public Vector2 GetTargetPosition()
    {
        for (int x = 0; x < unitStartMap.width; x++)
        {
            for (int y = 0; y < unitStartMap.height; y++)
            {
                Color pixel = unitStartMap.GetPixel(x, y);
                if (pixel.g > 0.5f && pixel.r < 0.5f && pixel.b < 0.5f)
                {
                    return new Vector2(x, y);
                }
            }
        }
        return new Vector2(0.0f, 0.0f);
    }

    public List<Vector2> GetWarmUpStartPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        for (int x = 0; x < mapTexture.width; x++)
        {
            for (int y = 0; y < mapTexture.height; y++)
            {
                Color pixel = mapTexture.GetPixel(x, y);
                if (pixel.g > 0.5f && pixel.r < 0.5f && pixel.b < 0.5f)
                {
                    positions.Add(new Vector2(x,y));
                }
            }
        }
        return positions;
    }

    public List<Vector2> GetWarmUpGoalPositions()
    {
        List<Vector2> positions = new List<Vector2>();
        for (int x = 0; x < mapTexture.width; x++)
        {
            for (int y = 0; y < mapTexture.height; y++)
            {
                Color pixel = mapTexture.GetPixel(x, y);
                if (pixel.b > 0.5f && pixel.r < 0.5f && pixel.g < 0.5f)
                {
                    positions.Add(new Vector2(x, y));
                }
            }
        }
        return positions;
    }
}
