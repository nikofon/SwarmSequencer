#if UNITY_EDITOR
using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor;

namespace ProjectileAnimator
{
    public partial class ProjectileDriver
    {
        public void PlayAnimationEditor()
        {
            if (paused) { paused = false; return; }
            if (FrameDatas == null) FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            ChangeFrame(0);
            running = true;
            EditorCoroutineUtility.StartCoroutine(RunProjectileMovementEditor(0.02f), this);
        }
        public void StopAnimationEditor()
        {
            running = false;
            paused = false;
            foreach (var obj in projectilePositions) DestroyImmediate(obj.Value.gameObject);
            projectilePositions.Clear();
            order = 1;
            skipFrame = false;
            currentFrame = 0;
            DisposeNativeCollections();
            FrameDatas = null;
        }

        IEnumerator RunProjectileMovementEditor(float delta)
        {
            var waitAmount = new EditorWaitForSeconds(delta);
            while (running)
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
                                ChangeFrame(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= stop;
                            break;
                    }
                    t += delta / timeOverrideValue; 
                    currentTime += order * delta; 
                }
            }
        }

        void CleanUp(PlayModeStateChange x)
        {
            if (x == PlayModeStateChange.ExitingEditMode)
            {
                if (Running)
                {
                    StopAnimationEditor();
                }
            }
        }
    }
}
#endif
