using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ProjectileAnimator {

    namespace Timeline {
        public class ProjectileAnimationBehaviour : PlayableBehaviour
        {
            public TextAsset asset;
            public override void ProcessFrame(Playable playable, UnityEngine.Playables.FrameData info, object playerData)
            {
                Debug.Log(playable.GetTime());
                var pjd = playerData as ProjectileDriver;
                if(pjd.ActiveFrameDatas == null)
                {
                    pjd.ChangeAsset(asset.text);
                }
                pjd.CurrentTime = (float) playable.GetTime();
            }

        }
    }
}
