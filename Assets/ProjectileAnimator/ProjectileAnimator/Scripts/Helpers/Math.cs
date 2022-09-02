using UnityEngine;
namespace ProjectileAnimator
{
    public static class MathHelper
    {
        public static Vector3 BezierInterpolation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float t)
        {
            Vector3 pQZero = Vector3.Lerp(pZero, pOne, t);
            Vector3 pQOne = Vector3.Lerp(pOne, pTwo, t);
            return Vector3.Lerp(pQZero, pQOne, t);
        }

        public static Vector3[] BezierAproximation(Vector3 pZero, Vector3 pTwo, Vector3 pOne, float step = 0.02f)
        {
            int length = Mathf.CeilToInt(1 / step);
            Vector3[] result = new Vector3[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = BezierInterpolation(pZero, pTwo, pOne, Mathf.Min(1, i * step));
            }
            return result;
        }
    }

    public class Grid
    {
        Vector3[] points;

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
        public Vector2Int GridSize
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
        Matrix4x4 m_transformMatrix;

        public Grid(Vector2Int gridSize, float cellSize, Vector3 gridOrigin, Quaternion gridRotation)
        {
            m_transformMatrix = Matrix4x4.TRS(gridOrigin, gridRotation, cellSize * Vector3.one);
            GridSize = gridSize;
            m_gridOrigin = gridOrigin;
            m_gridRotation = gridRotation;
            m_cellSize = cellSize;
            m_worldPositions = RecalculatePositions(points, Matrix4x4.TRS(GridOrigin, GridRotation, CellSize * Vector3.one));
        }

        Vector3[] ResizeGrid(Vector2Int gridSize, Matrix4x4 TRS)
        {
            Vector3[] points = new Vector3[(gridSize.x + 1) * (gridSize.y + 1)];
            int n = 0;
            Debug.Log($"Resizing grid to size {gridSize}");
            for (float i = -gridSize.x / 2; i <= gridSize.x / 2; i++)
            {
                for (float j = -gridSize.y / 2; j <= gridSize.y / 2; j++)
                {
                    points[n] = new Vector3(i, j);
                    Debug.Log($"Adding point {i} {j} to index {n}");
                    n++;
                }
            }
            /*
            n = 0;
            foreach (var p in points)
            {
                Debug.Log(p);
                n++;
            }
            */
            return points;
        }

        Vector3[] RecalculatePositions(Vector3[] positions, Matrix4x4 TRS)
        {
            Vector3[] results = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                results[i] = TRS.MultiplyPoint3x4(positions[i]);
            }
            return results;
        }
    }
}