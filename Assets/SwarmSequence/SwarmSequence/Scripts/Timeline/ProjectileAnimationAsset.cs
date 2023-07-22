using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace SwarmSequencer.Timeline
{
    public class SwarmSequenceAsset : PlayableAsset
    {
        public SwarmSequence asset;
        public List<FrameTimeOverride> frameTimeOverrides = new List<FrameTimeOverride>();
        [HideInInspector] public SwarmSequenceDirector driver;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SwarmSequenceBehaviour>.Create(graph);
            var projectileAnimationBehaviour = playable.GetBehaviour();
            projectileAnimationBehaviour.asset = asset;
            projectileAnimationBehaviour.frameTimeOverrides = frameTimeOverrides;
            projectileAnimationBehaviour.owner = driver;

            return playable;
        }


    }
}
