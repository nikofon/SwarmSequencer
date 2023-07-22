using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
namespace SwarmSequencer
{
    namespace MathTools
    {
        public static class MathHelper
        {
            public static readonly Vector3 NaNVector3 = new Vector3(float.NaN, float.NaN, float.NaN);

            public static bool IsNaNVector3(Vector3 v)
            {
                return float.IsNaN(v.x) && float.IsNaN(v.y) && float.IsNaN(v.z);
            }

            public static Vector3 NanToZero(Vector3 v)
            {
                return new Vector3(NanToZero(v.x), NanToZero(v.y), NanToZero(v.z));
            }

            public static Vector3 ConfineVector3ToPrecision(Vector3 v, float precision)
            {
                return new Vector3(v.x.ConfineToPrecision(precision), v.y.ConfineToPrecision(precision), v.z.ConfineToPrecision(precision));
            }

            public static float NanToZero(float f)
            {
                return float.IsNaN(f) ? 0 : f;
            }

            /// <summary>
            /// Basic bezier interpolation between two points with one control point
            /// </summary>
            /// <param name="startPoint"></param>
            /// <param name="endPoint"></param>
            /// <param name="control"></param>
            /// <param name="t"></param>
            /// <returns></returns>
            public static Vector3 BezierInterpolation(Vector3 startPoint, Vector3 endPoint, Vector3 control, float t)
            {
                Vector3 pQZero = Vector3.Lerp(startPoint, control, t);
                Vector3 pQOne = Vector3.Lerp(control, endPoint, t);
                return Vector3.Lerp(pQZero, pQOne, t);
            }

            /// <summary>
            /// Approximates a bezier curve
            /// </summary>
            /// <param name="startPoint">Starting point</param>
            /// <param name="endPoint">End point</param>
            /// <param name="control">Bezier control</param>
            /// <param name="step">Approximation precision</param>
            /// <returns>Array of approximation points</returns>

            public static Vector3[] BezierAproximation(Vector3 startPoint, Vector3 endPoint, Vector3 control, float step = 0.02f)
            {
                int length = Mathf.CeilToInt(1 / step);
                Vector3[] result = new Vector3[length];
                for (int i = 0; i < length; i++)
                {
                    result[i] = BezierInterpolation(startPoint, endPoint, control, Mathf.Min(1, i * step));
                }
                return result;
            }

            public static int FindCellContainingPointIgnoreZ(Vector2 point, Dictionary<int, Vector3[]> cells)
            {
                int res = -1;
                foreach (var v in cells)
                {
                    bool inside = point.x <= FindMax<float>(v.Value[0].x, v.Value[1].x, v.Value[2].x, v.Value[3].x) && point.x > FindMin<float>(v.Value[0].x, v.Value[1].x, v.Value[2].x, v.Value[3].x) &&
                        point.y <= FindMax<float>(v.Value[0].y, v.Value[1].y, v.Value[2].y, v.Value[3].y) && point.y > FindMin<float>(v.Value[0].y, v.Value[1].y, v.Value[2].y, v.Value[3].y);
                    if (inside) { res = v.Key; break; }
                }
                return res;
            }

            public static T FindMax<T>(params T[] comparers) where T : IComparable
            {
                T max = comparers[0];
                foreach (T f in comparers)
                {
                    if (f.CompareTo(max) > 0) max = f;
                }
                return max;
            }

            public static T FindMin<T>(params T[] comparers) where T : IComparable
            {
                T min = comparers[0];
                foreach (T f in comparers)
                {
                    if (f.CompareTo(min) < 0) min = f;
                }
                return min;
            }

            public static bool InBounds(Vector3 point, Vector3 minBound, Vector3 maxBound)
            {
                return point.x <= maxBound.x && point.x >= minBound.x && point.y <= maxBound.y && point.y >= minBound.y && point.z <= maxBound.z && point.z >= minBound.z;
            }
        }

        public class Grid
        {
            public Vector2 Center { get; private set; }
            Vector3[] points;

            public Dictionary<int, Vector3[]> Cells;
            public Vector3[] WorldPositions { get => m_worldPositions; }
            Vector3[] m_worldPositions;

            public Quaternion GridRotation
            {
                get { return m_gridRotation; }
                set { m_gridRotation = value; m_worldPositions = RecalculatePositions(points, Matrix4x4.TRS(GridOrigin, GridRotation, CellSize * Vector3.one)); }
            }
            Quaternion m_gridRotation;
            public Vector3 GridOrigin
            {
                get { return m_gridOrigin; }
                set { m_gridOrigin = value; m_worldPositions = RecalculatePositions(points, Matrix4x4.TRS(GridOrigin, GridRotation, CellSize * Vector3.one)); }
            }
            Vector3 m_gridOrigin;
            public Vector2Int GridDimensions
            {
                get { return m_gridSize; }
                set { m_gridSize = value; points = ResizeGrid(value, TRS); }
            }
            Vector2Int m_gridSize;
            public float CellSize
            {
                get { return m_cellSize; }
                set { m_cellSize = value; m_worldPositions = RecalculatePositions(points, Matrix4x4.TRS(GridOrigin, GridRotation, CellSize * Vector3.one)); }
            }
            float m_cellSize;
            public Matrix4x4 TRS { get { return m_transformMatrix; } }
            public Matrix4x4 TRSInverse { get { return TRS.inverse; } }
            Matrix4x4 m_transformMatrix;

            public Grid(Vector2Int gridSize, float cellSize, Vector3 gridOrigin, Quaternion gridRotation)
            {
                m_transformMatrix = Matrix4x4.TRS(gridOrigin, gridRotation, cellSize * Vector3.one);
                GridDimensions = gridSize;
                m_gridOrigin = gridOrigin;
                m_gridRotation = gridRotation;
                m_cellSize = cellSize;
                m_worldPositions = RecalculatePositions(points, Matrix4x4.TRS(GridOrigin, GridRotation, CellSize * Vector3.one));
                Center = new Vector2((gridSize.x - 1) / 2f, (gridSize.y - 1) / 2f);
            }

            public Vector3 RelativeToWorldPos(Vector3 position)
            {
                return TRS.MultiplyPoint3x4(position);
            }

            public Vector2 CellIndexToRelativePosition(int cellIndex)
            {
                return new Vector2(cellIndex / GridDimensions.y, cellIndex % GridDimensions.y) - Center;
            }

            public int RelativePositionToCellIndex(float x, float y)
            {
                Vector2 pos = new Vector2(x, y) + Center;
                return (int)(pos.x * GridDimensions.y + pos.y);
            }

            public Vector3 WorldToRelativePos(Vector3 position)
            {
                return TRSInverse.MultiplyPoint3x4(position);
            }

            public int RelativePositionToCellIndex(Vector2 position)
            {
                Vector2 pos = position + Center;
                return (int)(pos.x * GridDimensions.y + pos.y);
            }

            public int FindCellContainingWorldPointIgnoreZ(Vector2 point)
            {
                var pos = WorldToRelativePos(point);
                return RelativePositionToCellIndex(pos);
            }

            public Grid(Vector2Int gridSize, Matrix4x4 TRS)
            {
                m_transformMatrix = TRS;
                GridDimensions = gridSize;
                m_gridOrigin = TRS.MultiplyPoint3x4(Vector3.zero);
                m_gridRotation = TRS.rotation;
                m_cellSize = TRS.lossyScale.x;
                m_worldPositions = RecalculatePositions(points, TRS);
                Center = new Vector2((gridSize.x - 1) / 2f, (gridSize.y - 1) / 2f);
            }

            Vector3[] ResizeGrid(Vector2Int gridSize, Matrix4x4 TRS)
            {
                Vector3[] points = new Vector3[(gridSize.x + 1) * (gridSize.y + 1)];
                int n = 0;
                for (float i = -gridSize.x / 2f; i <= gridSize.x / 2f; i++)
                {
                    for (float j = -gridSize.y / 2f; j <= gridSize.y / 2f; j++)
                    {
                        points[n] = new Vector3(i, j);
                        n++;
                    }
                }
                Center = new Vector2((gridSize.x - 1) / 2f, (gridSize.y - 1) / 2f);
                return points;
            }

            Vector3[] RecalculatePositions(Vector3[] positions, Matrix4x4 TRS)
            {
                Vector3[] results = new Vector3[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    results[i] = TRS.MultiplyPoint3x4(positions[i]);
                }
                Cells = new Dictionary<int, Vector3[]>();
                int n = 0;
                for (int i = 0; i < points.Length - GridDimensions.y - 1; i++)
                {
                    if ((i + 1) % (GridDimensions.y + 1) == 0 && i != 0)
                    {
                        continue;
                    }
                    Cells.Add(n, new Vector3[] { results[i], results[i + 1], results[i + GridDimensions.y + 1], results[i + GridDimensions.y + 2] });
                    n++;
                }
                return results;
            }
        }
    }
}