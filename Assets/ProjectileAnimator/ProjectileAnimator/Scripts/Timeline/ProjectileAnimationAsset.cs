using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace ProjectileAnimator.Timeline
{
    public class ProjectileAnimationAsset : PlayableAsset
    {
        public TextAsset asset;
        public List<FrameTimeOverride> frameTimeOverrides = new List<FrameTimeOverride>();
        [HideInInspector]public ProjectileDriver driver;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ProjectileAnimationBehaviour>.Create(graph);
            var projectileAnimationBehaviour = playable.GetBehaviour();
            projectileAnimationBehaviour.asset = FrameDataSerializer.DeserializeFrameData(asset.text);
            projectileAnimationBehaviour.frameTimeOverrides = frameTimeOverrides;
            projectileAnimationBehaviour.owner = driver;

            return playable;
        }

       
    }
}
