using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace utils
{
    public static class Vector2Extensions
    {
        public static Vector2 LimitMagnitude(this Vector2 v, float maxMagnitude)
        {
            if (v.magnitude > maxMagnitude)
            {
                return v.normalized * maxMagnitude;
            }
            return v;
        }
        public static Vector2 ProjectOnto(this Vector2 vector1, Vector2 vector2)
        {
            float dot = Vector2.Dot(vector1, vector2);
            return dot / vector2.sqrMagnitude * vector2;
        }

        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);

            float tx = v.x;
            float ty = v.y;
            v.x = (cos * tx) - (sin * ty);
            v.y = (sin * tx) + (cos * ty);
            return v;
        }
    }
}
