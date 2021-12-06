using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectileAnimator
{
    [CreateAssetMenu(menuName = "ProjectileAnimator/ProjectileDriverData")]
    public class ProjectileDataScriptable : ScriptableObject
    {
        public List<ProjectileLookUp> ProjectileLookUps;
        [Min(0.01f)]
        public float TimeBetweenFrames = 0.01f;
        public List<FrameTimeOverride> FrameTimeOverrides;
    }
}