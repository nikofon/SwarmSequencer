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
        public void StopAnimationEditor()
        {
            active = false;
            paused = false;
            foreach (var obj in projectilePositions) DestroyImmediate(obj.Value.gameObject);
            projectilePositions.Clear();
            order = 1;
            skipFrame = false;
            currentFrame = 0;
            currentTime = 0;
            DisposeNativeCollections();
            DisposeBezierPoints();
        }

        IEnumerator RunProjectileMovementEditor(float delta)
        {
            var waitAmount = new EditorWaitForSeconds(delta);
            while (active)
            {
                if (!paused)
                {
                    switch (AnimationType)
                    {
                        case AnimationTypes.Pingpong:
                            Action increment = () => { order *= -1; skipFrame = true; };
                            OnAnimationFinished += increment;
                            if (!skipFrame)
                                ProjectileMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame += order;
                                    DisposeNativeCollections();
                                    DisposeBezierPoints();
                                }
                                else { skipFrame = false; }
                                ChangeFrame(currentFrame, DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= increment;
                            break;
                        case AnimationTypes.Repeat:
                            Action reset = () => { skipFrame = true; currentFrame = 0; };
                            OnAnimationFinished += reset;
                            if (!skipFrame) ProjectileMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame++;
                                    DisposeNativeCollections();
                                    DisposeBezierPoints();
                                }
                                else { skipFrame = false; }
                                ChangeFrame(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= reset;
                            break;
                        case AnimationTypes.Single:
                            Action stop = () => { StopAnimationEditor(); };
                            OnAnimationFinished += stop;
                            ProjectileMovement();
                            if (t >= 1)
                            {
                                currentFrame++;
                                DisposeNativeCollections();
                                DisposeBezierPoints();
                                ChangeFrame(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= stop;
                            break;
                    }
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
                    StopAnimationEditor();
                }
            }
        }
    }
}
#endif
