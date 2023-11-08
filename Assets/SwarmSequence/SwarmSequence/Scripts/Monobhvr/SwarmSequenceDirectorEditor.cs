#if UNITY_EDITOR
using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace SwarmSequencer
{
    public partial class SwarmSequenceDirector
    {
        public void PlayAnimationEditor()
        {
            LoadFrameData();
            if (paused) { paused = false; return; }
            ChangeFrame(0);
            active = true;
            EditorCoroutineUtility.StartCoroutine(RunProjectileMovementEditor(0.02f), this);
        }

        IEnumerator RunProjectileMovementEditor(float delta)
        {
            var waitAmount = new EditorWaitForSeconds(delta);
            bool shouldContinue = true;
            Action onCycleEnd = () => { throw new SequenceRuncicleExcpetion(); };
            while (active)
            {
                if (!paused)
                {
                    switch (AnimationType)
                    {
                        case AnimationTypes.Pingpong:
                            onCycleEnd = () => { order *= -1; skipFrame = true; };
                            if (!skipFrame)
                                InstanceMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame += order;
                                    DisposeNativeCollections();
                                    DisposeBezierPoints();
                                }
                                else { skipFrame = false; }
                                shouldContinue = ChangeFrame(currentFrame, DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            break;
                        case AnimationTypes.Repeat:
                            onCycleEnd = () => { skipFrame = true; currentFrame = 0; };
                            if (!skipFrame) InstanceMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame++;
                                    DisposeNativeCollections();
                                    DisposeBezierPoints();
                                }
                                else { skipFrame = false; }
                                shouldContinue = ChangeFrame(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            break;
                        case AnimationTypes.Single:
                            onCycleEnd = () => { Stop(DisposalMode.Immediate); };
                            InstanceMovement();
                            if (t >= 1)
                            {
                                currentFrame++;
                                DisposeNativeCollections();
                                DisposeBezierPoints();
                                shouldContinue = ChangeFrame(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            break;
                    }
                    if (!shouldContinue) onCycleEnd();
                    t += delta / timeOverrideValue;
                    currentTime += order * delta;
                }
                else yield return waitAmount;
            }
        }

        void CleanUp(PlayModeStateChange x)
        {
            if (x == PlayModeStateChange.ExitingEditMode)
            {
                if (Active)
                {
                    Stop(DisposalMode.Immediate);
                }
            }
        }
    }
}
#endif
