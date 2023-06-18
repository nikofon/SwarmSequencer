using UnityEngine;
using UnityEditor;


namespace ProjectileAnimator
{
    public static class GizmoHelper
    {
        public static void DrawPath(Color color, Matrix4x4 convert, params Vector3[] points)
        {
            Color standart = Gizmos.color;
            Gizmos.color = color;
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (points[i] != points[i + 1])
                    Gizmos.DrawLine(convert.MultiplyPoint(points[i]), convert.MultiplyPoint(points[i + 1]));
            }
            Gizmos.color = standart;
        }

        public static void DrawGrid3D(Vector3Int gridSize, float cellSize, Vector3 zero, Matrix4x4 convert = default)
        {
            float halfX = gridSize.x * cellSize / 2;
            float halfY = gridSize.y * cellSize / 2;
            float halfZ = gridSize.z * cellSize / 2;

            if (cellSize == 0) return;

            for (float z = -halfZ; z <= halfZ; z += cellSize)
            {
                for (float x = -halfX; x <= halfX; x += cellSize)
                {
                    Gizmos.DrawLine(convert.MultiplyPoint(new Vector3(x, -halfY, z) + zero), convert.MultiplyPoint(new Vector3(x, halfY, z) + zero));
                }
                for (float y = -halfY; y <= halfY; y += cellSize)
                {
                    Gizmos.DrawLine(convert.MultiplyPoint(new Vector3(-halfX, y, z) + zero), convert.MultiplyPoint(new Vector3(halfX, y, z) + zero));
                }
            }
            for (float x = -halfX; x <= halfX; x += cellSize)
            {
                for (float y = -halfY; y <= halfY; y += cellSize)
                {
                    Gizmos.DrawLine(convert.MultiplyPoint(new Vector3(x, y, -halfZ) + zero), convert.MultiplyPoint(new Vector3(x, y, halfZ) + zero));
                }
            }
        }

        public static void DrawGridWithHandles(Grid grid)
        {
            for (int i = 0; i < grid.WorldPositions.Length; i++)
            {
                if (i + 1 < grid.WorldPositions.Length && (i + 1) % (grid.GridDimensions.y + 1) != 0)
                {
                    Handles.DrawLine(grid.WorldPositions[i], grid.WorldPositions[i + 1]);
                }
                if (i + grid.GridDimensions.y + 1 < grid.WorldPositions.Length)
                {
                    Handles.DrawLine(grid.WorldPositions[i], grid.WorldPositions[i + grid.GridDimensions.y + 1]);
                }
            }
        }

        public static void DrawGrid3DWithHandles(Vector3Int gridSize, float cellSize, Vector3 zero, Matrix4x4 convert = default)
        {
            float halfX = gridSize.x * cellSize / 2;
            float halfY = gridSize.y * cellSize / 2;
            float halfZ = gridSize.z * cellSize / 2;

            if (cellSize == 0) return;

            for (float z = -halfZ; z <= halfZ; z += cellSize)
            {
                for (float x = -halfX; x <= halfX; x += cellSize)
                {
                    Handles.DrawLine(convert.MultiplyPoint(new Vector3(x, -halfY, z) + zero), convert.MultiplyPoint(new Vector3(x, halfY, z) + zero));
                }
                for (float y = -halfY; y <= halfY; y += cellSize)
                {
                    Handles.DrawLine(convert.MultiplyPoint(new Vector3(-halfX, y, z) + zero), convert.MultiplyPoint(new Vector3(halfX, y, z) + zero));
                }
            }
            for (float x = -halfX; x <= halfX; x += cellSize)
            {
                for (float y = -halfY; y <= halfY; y += cellSize)
                {
                    Handles.DrawLine(convert.MultiplyPoint(new Vector3(x, y, -halfZ) + zero), convert.MultiplyPoint(new Vector3(x, y, halfZ) + zero));
                }
            }
        }
    }
}