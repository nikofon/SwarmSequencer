using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace ProjectileAnimator.Timeline {
    [TrackClipType(typeof(ProjectileAnimationAsset))]
    [TrackBindingType(typeof(ProjectileDriver), TrackBindingFlags.AllowCreateComponent)]
    public class ProectileAnimationTrack : TrackAsset { }
}

