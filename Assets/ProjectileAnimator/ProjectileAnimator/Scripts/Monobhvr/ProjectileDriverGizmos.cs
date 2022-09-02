using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
namespace ProjectileAnimator
{
    public partial class ProjectileDriver
    {
        [SerializeField] Vector3Int GridSize;

        [SerializeField] Vector3 center;

        [SerializeField] bool shouldDrawGrid;
        [SerializeField] bool DrawTragectories;

        [SerializeField] List<ProjectilePathColor> pathColors = new List<ProjectilePathColor>();

        Dictionary<ProjectileKey, List<Vector3>> positions = new Dictionary<ProjectileKey, List<Vector3>>();

        /// <summary>
        /// Returns path for each projectile
        /// </summary>
        /// <returns></returns>
        public Dictionary<ProjectileKey, List<Vector3>> GetPaths()
        {
            Dictionary<ProjectileKey, List<Vector3>> res = new Dictionary<ProjectileKey, List<Vector3>>();
            if (FrameDatas == null) FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            foreach (var data in FrameDatas)
            {
                foreach (var pos in data.ProjectilePositionData)
                {
                    if (!res.ContainsKey(pos.Key))
                    {
                        List<Vector3> toAdd = new List<Vector3>();
                        toAdd.Add(pos.Value.Item1);
                        toAdd.Add(pos.Value.Item2);
                        res.Add(pos.Key, toAdd);
                    }
                    else
                    {
                        res[pos.Key].Add(pos.Value.Item1);
                        res[pos.Key].Add(pos.Value.Item2);
                    }
                }
            }
            Dictionary<ProjectileKey, List<Vector3>> bezierRes = new Dictionary<ProjectileKey, List<Vector3>>();
            foreach (var KVPair in res)
            {
                List<Vector3> drawPoints = new List<Vector3>();
                for (int i = 1; i < KVPair.Value.Count - 1; i += 2)
                {
                    Vector3 pZero = KVPair.Value[i - 1];
                    Vector3 pOne = KVPair.Value[i];
                    Vector3 pTwo = KVPair.Value[i + 1];
                    if (float.IsInfinity(pOne.x))
                    {
                        pOne = (-pZero + pTwo) / 2;
                    }
                    drawPoints.AddRange(MathHelper.BezierAproximation(pZero, pTwo, pOne));
                }
                bezierRes.Add(KVPair.Key, drawPoints);
            }

            positions = bezierRes;

            return bezierRes;
        }

        /// <summary>
        /// Editor function that draws gizmos for projectile tragectories
        /// </summary>
        void DrawPaths()
        {
            foreach (var path in positions)
            {
                Nullable<Color> color = pathColors.Find(x => x.keys.Contains(path.Key))?.color;
                GizmoHelper.DrawPath(color.HasValue ? color.Value : Gizmos.color, UseWorldSpace ? Matrix4x4.identity : transform.localToWorldMatrix, path.Value.ToArray());
            }
        }


        private void OnDrawGizmos()
        {
            if (shouldDrawGrid)
            {
                GizmoHelper.DrawGrid3DWithHandles(GridSize, CellSize, center, UseWorldSpace ? Matrix4x4.identity : transform.localToWorldMatrix);
            }
            if (DrawTragectories)
            {
                DrawPaths();
            }
        }
    }

    [System.Serializable]
    public class ProjectilePathColor
    {
        public List<ProjectileKey> keys;
        public Color color;
    }
}
#endif