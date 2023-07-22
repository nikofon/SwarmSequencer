using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SwarmSequencer
{
    [CreateAssetMenu(menuName = "ProjectileAnimator/ProjectileDriverData")]
    public class AdditionalSequenceData : ScriptableObject
    {
        public Vector3 gridOrigin;
        public Quaternion gridRotation;
        [Min(0.01f)]
        public float gridScale;
        public Vector3Int gridDimensions;
        public List<InstanceLookUp> ProjectileLookUps;
        [Min(0.01f)]
        public float TimeBetweenFrames = 0.01f;
        public List<FrameTimeOverride> FrameTimeOverrides;
    }
}