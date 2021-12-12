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
            ProjectileDriver owner;
            public override void ProcessFrame(Playable playable, UnityEngine.Playables.FrameData info, object playerData)
            {
                if(owner == null)
                {
                    owner = playerData as ProjectileDriver;

                    if(owner != null) owner.ChangeAsset(asset.text);
                }
                if (owner == null) return;

                

                owner.CurrentTime = (float) playable.GetTime();
            }


            public override void OnPlayableDestroy(Playable playable)
            {
                if(owner != null)
                {
                    owner.Clear();
                }
            }

        }

        public class ProjectileAnimationMixerBehaviour : PlayableBehaviour
        {

        }
    }
}
