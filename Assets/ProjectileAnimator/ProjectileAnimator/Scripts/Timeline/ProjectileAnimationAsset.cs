using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace ProjectileAnimator.Timeline
{
    public class ProjectileAnimationAsset : PlayableAsset
    {
        public TextAsset asset;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ProjectileAnimationBehaviour>.Create(graph);

            var projectileAnimationBehaviour = playable.GetBehaviour();
            //lightControlBehaviour.light = light.Resolve(graph.GetResolver());
            projectileAnimationBehaviour.asset = asset;

            return playable;
        }
    }
}
