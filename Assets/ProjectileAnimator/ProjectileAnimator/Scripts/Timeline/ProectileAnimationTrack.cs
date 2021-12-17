using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ProjectileAnimator.Timeline {
    [TrackClipType(typeof(ProjectileAnimationAsset))]
    [TrackBindingType(typeof(ProjectileDriver), TrackBindingFlags.AllowCreateComponent)]
    public class ProectileAnimationTrack : TrackAsset {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            foreach (var c in GetClips())
            {
                (c.asset as ProjectileAnimationAsset).driver = (ProjectileDriver) go.GetComponent<PlayableDirector>().GetGenericBinding(this);
            }

            return base.CreateTrackMixer(graph, go, inputCount);

        }

    }
}

