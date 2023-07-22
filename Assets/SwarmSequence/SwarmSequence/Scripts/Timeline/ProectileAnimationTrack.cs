using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SwarmSequencer.Timeline
{
    [TrackClipType(typeof(SwarmSequenceAsset))]
    [TrackBindingType(typeof(SwarmSequenceDirector), TrackBindingFlags.AllowCreateComponent)]
    public class SwarmSequenceAnimationTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            foreach (var c in GetClips())
            {
                (c.asset as SwarmSequenceAsset).driver = (SwarmSequenceDirector)go.GetComponent<PlayableDirector>().GetGenericBinding(this);
            }

            return base.CreateTrackMixer(graph, go, inputCount);

        }

    }
}

