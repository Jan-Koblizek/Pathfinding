using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        public static Vector2 PerpendicularClockwise(this Vector2 value)
        {
            return new Vector2(value.y, 0f - value.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PerpendicularCounterClockwise(this Vector2 value)
        {
            return new Vector2(0f - value.y, value.x);
        }

        public static float ToAngle(this Vector2 value)
        {
            return (float)Mathf.Atan2(value.x, 0f - value.y);
        }

        public static float OctileDistance(this Vector2 v1, Vector2 v2)
        {
            int dx = Mathf.CeilToInt(Mathf.Abs(v1.x - v2.x));
            int dy = Mathf.CeilToInt(Mathf.Abs(v1.y - v2.y));
            return Mathf.Max(dx, dy) + Mathf.Min(dx, dy) * 0.415f;
        }
    }
}
