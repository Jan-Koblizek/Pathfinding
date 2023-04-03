using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace utils
{
    public static class Geometry
    {
        public static Vector2 GetCircleLineIntersection(Vector2 start, Vector2 end, float radius)
        {
            Vector2 difference = end - start;
            float drSquare = difference.x * difference.x + difference.y * difference.y;
            float determinant = start.x * end.y - end.x * start.y;

            float sqrtInsides = radius * radius * drSquare - determinant * determinant;

            if (sqrtInsides < 0)
                return Vector2.zero;

            var sqrt = Mathf.Sqrt(sqrtInsides);

            var sgnStar = difference.y < 0 ? -1 : 1;
            var a = determinant * difference.y;
            var b = sgnStar * difference.x * sqrt;

            var c = -determinant * difference.x;
            var d = Mathf.Abs(difference.y) * sqrt;

            var x1 = (float)(a + b) / drSquare;
            var x2 = (float)(a - b) / drSquare;

            var y1 = (float)(c + d) / drSquare;
            var y2 = (float)(c - d) / drSquare;

            var point1 = new Vector2(x1, y1);
            var point2 = new Vector2(x2, y2);

            var distance1 = Vector2.Distance(start, point1);
            var distance2 = Vector2.Distance(start, point2);

            return distance1 < distance2 ? point1 : point2;
        }

        public static bool CircleLineIntersection(Vector2 start, Vector2 end, float radius)
        {
            //http://mathworld.wolfram.com/Circle-LineIntersection.html
            Vector2 d = end - start;
            double drSquare = Mathf.Sqrt(d.x * d.x + d.y * d.y);
            double determinant = (double)start.x * end.y - (double)end.x * start.y;
            double discriminant = radius * radius * drSquare - determinant * determinant;

            if (discriminant <= 0)
            {
                return false;
            }
            else
            {
                if (Vector2.Dot(start, end - start) > 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetVerticalIntersection(double k, double q, int x)
        {
            return new Vector2(x, (float)(k * x + q));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetVerticalIntersection(Vector2 origin, Vector2 direction, int x)
        {
            var (k, q) = GetLineParameters(origin, direction);
            var v = new Vector2(x, (float)(k * x + q));
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetHorizontalIntersection(double k, double q, int y)
        {
            return new Vector2((float)((y - q) / k), y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetHorizontalIntersection(Vector2 origin, Vector2 direction, int y)
        {
            var (k, q) = GetLineParameters(origin, direction);
            var v = new Vector2((float)((y - q) / k), y);
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (double, double) GetLineParameters(Vector2 origin, Vector2 direction)
        {
            var k = Mathf.Atan2(direction.x, direction.y);
            var q = origin.y - k * origin.y;
            return (k, q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AngleToVector(float angle)
        {
            return new Vector2((float)Mathf.Cos(angle), -(float)Mathf.Sin(angle));
        }
    }
}
