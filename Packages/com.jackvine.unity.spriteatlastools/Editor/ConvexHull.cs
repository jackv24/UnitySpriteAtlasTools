// Edited from: https://gist.github.com/dLopreiato/7fd142d0b9728518552188794b8a750c

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpriteAtlasTools.Editor
{
    internal static class ConvexHull
    {
        public static IEnumerable<Vector2> ComputeConvexHull(IEnumerable<Vector2> sourcePoints)
        {
            var points = new List<Vector2>(sourcePoints);
            points.Sort(
                (a, b) =>
                    Math.Abs(a.x - b.x) <= Mathf.Epsilon ? a.y.CompareTo(b.y) : (a.x > b.x ? 1 : -1));

            // Importantly, DList provides O(1) insertion at beginning and end
            var hull = new CircularList<Vector2>();
            int lowerSize = 0, upperSize = 0; // size of lower and upper hulls

            // Builds a hull such that the output polygon starts at the leftmost Vector2.
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 p = points[i], p1;

                // build lower hull (at end of output list)
                while (lowerSize >= 2 && (p1 = hull.Last).Sub(hull[^2]).Cross(p.Sub(p1)) >= 0)
                {
                    hull.PopLast();
                    lowerSize--;
                }
                hull.PushLast(p);
                lowerSize++;

                // build upper hull (at beginning of output list)
                while (upperSize >= 2 && (p1 = hull.First).Sub(hull[1]).Cross(p.Sub(p1)) <= 0)
                {
                    hull.PopFirst();
                    upperSize--;
                }
                if (upperSize != 0) // when U=0, share the Vector2 added above
                    hull.PushFirst(p);
                upperSize++;
                Debug.Assert(upperSize + lowerSize == hull.Count + 1);
            }
            hull.PopLast();
            return hull;
        }

        private static Vector2 Sub(this Vector2 a, Vector2 b)
        {
            return a - b;
        }

        private static float Cross(this Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private class CircularList<T> : List<T>
        {
            public T Last
            {
                get => this[Count - 1];
                set => this[Count - 1] = value;
            }

            public T First
            {
                get => this[0];
                set => this[0] = value;
            }

            public void PushLast(T obj)
            {
                Add(obj);
            }

            public T PopLast()
            {
                var retVal = this[Count - 1];
                RemoveAt(Count - 1);
                return retVal;
            }

            public void PushFirst(T obj)
            {
                Insert(0, obj);
            }

            public T PopFirst()
            {
                var retVal = this[0];
                RemoveAt(0);
                return retVal;
            }
        }
    }
}
