using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SwarmSequencer.Timeline
{
    [TrackClipType(typeof(ProjectileAnimationAsset))]
    [TrackBindingType(typeof(SwarmSequenceDirector), TrackBindingFlags.AllowCreateComponent)]
    public class ProectileAnimationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            foreach (var c in GetClips())
            {
                (c.asset as ProjectileAnimationAsset).driver = (SwarmSequenceDirector)go.GetComponent<PlayableDirector>().GetGenericBinding(this);
            }

            return base.CreateTrackMixer(graph, go, inputCount);

        }

    }
}

