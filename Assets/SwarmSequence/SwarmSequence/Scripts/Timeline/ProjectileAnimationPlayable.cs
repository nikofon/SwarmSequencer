using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SwarmSequencer
{

    namespace Timeline
    {
        public class SwarmSequenceBehaviour : PlayableBehaviour
        {
            public SwarmSequence asset;
            public SwarmSequenceDirector owner;
            public float timeBetweenFrames;
            public List<FrameTimeOverride> frameTimeOverrides = new List<FrameTimeOverride>();
            public override void ProcessFrame(Playable playable, UnityEngine.Playables.FrameData info, object playerData)
            {
                if (owner == null)
                {
                    owner = playerData as SwarmSequenceDirector;
                }
                if (owner == null) return;

                owner.CurrentTime = (float)playable.GetTime();

            }

            public override void OnBehaviourPlay(Playable playable, UnityEngine.Playables.FrameData info)
            {
                if (owner != null) { owner.SetSequence(asset); owner.frameTimeOverrides = frameTimeOverrides; owner.Duration = (float)playable.GetDuration(); }
            }

            public override void OnBehaviourPause(Playable playable, UnityEngine.Playables.FrameData info)
            {
#if UNITY_EDITOR
                if (owner != null && info.effectivePlayState == PlayState.Paused)
                {
                    if (Application.isPlaying) owner.Stop();
                    else owner.StopAnimationEditor();
                }
#else
                if(owner != null && info.effectivePlayState == PlayState.Paused) {
                    owner.Stop();
                }
#endif
            }

            public override void OnPlayableDestroy(Playable playable)
            {
#if UNITY_EDITOR
                if (owner != null)
                {
                    if (Application.isPlaying) owner.Stop();
                    else owner.StopAnimationEditor();
                }
#else
                owner?.Stop();
#endif
            }


        }

        public class SwarmSequenceMixerBehaviour : PlayableBehaviour
        {

        }
    }
}