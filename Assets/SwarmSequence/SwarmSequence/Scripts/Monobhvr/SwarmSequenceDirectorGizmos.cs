using System;
using System.Collections.Generic;
using UnityEngine;
using SwarmSequencer.EditorTools;
using SwarmSequencer.MathTools;

#if UNITY_EDITOR
namespace SwarmSequencer
{
    public partial class SwarmSequenceDirector
    {
        [SerializeField] Vector3Int GridSize;

        [SerializeField] Vector3 center;

        [SerializeField] bool shouldDrawGrid;
        [SerializeField] bool DrawTragectories;

        [SerializeField] List<ProjectilePathColor> pathColors = new List<ProjectilePathColor>();

        internal Dictionary<ProjectileKey, List<Vector3>> sequencePath = new Dictionary<ProjectileKey, List<Vector3>>();



        void DrawPaths(Dictionary<ProjectileKey, List<Vector3>> positions)
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
            if (DrawTragectories && FrameDatas != null)
            {
                DrawPaths(sequencePath);
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