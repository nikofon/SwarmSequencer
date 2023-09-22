using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System;
using Unity.Collections;
using UnityEditor;
using SwarmSequencer.MathTools;

namespace SwarmSequencer
{
    [ExecuteInEditMode]
    public partial class SwarmSequenceDirector : MonoBehaviour
    {
        //data to load
        /// <summary>
        /// Projectile data scriptable used by this object. To set it and apply settings, call ChangeAsset(string, ProjectileDataScriptable,...)
        /// </summary>
        public AdditionalSequenceData ProjectileDataScriptable { get => projectileDataScriptable; set => projectileDataScriptable = value; }
        [SerializeField] AdditionalSequenceData projectileDataScriptable;

        [SerializeField] bool loadTimeBetweenTurns;
        [SerializeField] bool loadFrameTimeOverrides;
        [SerializeField] bool loadInstanceLookUps;

        [SerializeField] SwarmSequence swarmSequenceData;

        /// <summary>
        /// Collection of prefabs to be used
        /// </summary>
        public List<InstanceLookUp> Instances = new List<InstanceLookUp>();

        public bool PlayOnAwake = false;

        [Tooltip("Pixel to local units")]
        [Min(0.1f)]
        public float CellSize = 1f;

        [Tooltip("By default projectiles move in local space, check this to use world space instead")]
        public bool UseWorldSpace;

        [Min(0.01f)]
        public float TimeBetweenFrames = 0.1f;

        [Tooltip("How to handle objects that disappeare on the next frame?")]
        public UnassignedObjectsHandlingType UnassignedObjectsHandling = UnassignedObjectsHandlingType.Destroy;
        [Tooltip("How to handle objects that have completed the animation?")]
        public UnassignedObjectsHandlingType CompletedObjectsHandling = UnassignedObjectsHandlingType.Destroy;

        public AnimationTypes AnimationType { get => animationType; set { animationType = value; } }
        [SerializeField] AnimationTypes animationType = AnimationTypes.Single;

        public List<FrameTimeOverride> frameTimeOverrides = new List<FrameTimeOverride>();

        //run data
        /// <summary>
        /// List of current frame datas
        /// </summary>
        public ReadOnlyCollection<FrameData> ActiveFrameDatas { get { if (readonlyFrameData == null) { if (FrameDatas == null) return null; readonlyFrameData = new ReadOnlyCollection<FrameData>(FrameDatas); } return readonlyFrameData; } }
        ReadOnlyCollection<FrameData> readonlyFrameData;

        private List<FrameData> FrameDatas;


        Dictionary<ProjectileKey, Transform> projectilePositions = new Dictionary<ProjectileKey, Transform>();

        //Events
        public event Action OnAnimationFinished;
        /// <summary>
        /// Now projectiles are interpolating between frame (argument 1) and frame (argument 2)
        /// </summary>
        public event Action<int, int> OnFrameChanged;

        //Native collections
        NativeArray<Vector3> currentPositions;
        NativeArray<Vector3> originalPositions;
        NativeArray<Vector3> bezierInterpolationPoints;
        NativeArray<Vector3> targetPositions;

        //Frame info
        public int CurrentFrame { get => currentFrame; }
        int currentFrame = 0;
        /// <summary>
        /// current lerp time
        /// </summary>
        float t;

        //Status variables
        /// <summary>
        /// Is an animation currently playing?
        /// </summary>
        public bool Playing { get => active && !paused; }
        public bool Paused { get => paused; }
        /// <summary>
        /// Is animation active? (returns true even if paused)
        /// </summary>
        public bool Active { get => active; }

        public float CurrentTime
        {
            get => currentTime; set
            {
                if (FrameDatas == null) return;
                int frame = GetFrameByTime(value);
                int maxFrame = FrameDatas.Count - 1;
                int newFrame = Mathf.Clamp(frame, 0, maxFrame);
                if (newFrame != currentFrame || newFrame == 0) { currentFrame = newFrame; ChangeFrame(newFrame); }
                currentTime = value;
                t = DetermineTByTime(value);
                if (frame < maxFrame)
                    InstanceMovement();
            }
        }
        float currentTime;

        /// <summary>
        /// Gets or sets the duration of one complete animation. If you want to set it, be sure that there is a desserialized seqeunce in use.
        /// </summary>
        public float Duration { get => CalculateDuration(); set { if (FrameDatas != null) TimeBetweenFrames = CalculateTimeBetweenFrames(value, FrameDatas.Count, frameTimeOverrides); } }

        bool paused;

        bool active;
        bool skipFrame;
        int order = 1;
        float timeOverrideValue = -1;

        //Advanced

        [Tooltip("Determines batch size for IJobParallelFor.Schedule()")]
        [Min(1)]
        public int batchSize = 1;

        void ChangeFrame(int newFrame, DisposalMode dsplM = DisposalMode.Normal)
        {
            DisposeNativeCollections();
            DisposeBezierPoints();
            if ((order > 0 && newFrame + 1 >= FrameDatas.Count) || (order < 0 && newFrame - 1 < 0))
            {
                OnAnimationFinished?.Invoke();
                return;
            }
            t = 0;
            var timeOverride = frameTimeOverrides.Find(x => x.Equals(new FrameTimeOverride() { FrameOne = currentFrame, FrameTwo = currentFrame + order }));
            timeOverrideValue = timeOverride == null ? TimeBetweenFrames : timeOverride.value;
            var pos = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>(FrameDatas[newFrame].ProjectilePositionData);
            Dictionary<ProjectileKey, Vector3> toInstantiate = new Dictionary<ProjectileKey, Vector3>();
            List<ProjectileKey> toRemove = new List<ProjectileKey>();
            var nextPos = FrameDatas[newFrame + order].ProjectilePositionData;
            foreach (var v in pos)
            {
                var matrix = UseWorldSpace ? Matrix4x4.identity : transform.localToWorldMatrix;
                if (!projectilePositions.ContainsKey(v.Key)) toInstantiate.Add(v.Key, matrix.MultiplyPoint3x4(v.Value.Item1));
            }
            if (toInstantiate.Count > 0)
                InstantiateProjectiles(ref toInstantiate);
            foreach (var p in projectilePositions)
            {
                if (!nextPos.ContainsKey(p.Key) || !pos.ContainsKey(p.Key)) toRemove.Add(p.Key);
            }
            foreach (var trmv in toRemove)
            {
                var trvmGO = projectilePositions[trmv];
                projectilePositions.Remove(trmv);
                pos.Remove(trmv);
                if (UnassignedObjectsHandling == UnassignedObjectsHandlingType.Destroy && trvmGO != null)
                {
                    if (Application.isPlaying && dsplM == DisposalMode.Normal) { Destroy(trvmGO.gameObject); }
                    else DestroyImmediate(trvmGO.gameObject);
                }
            }
            bezierInterpolationPoints = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            currentPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            originalPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            targetPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            int i = 0;
            foreach (var p in pos)
            {
                currentPositions[i] = CellSize * (Vector3)p.Value.Item1;
                originalPositions[i] = CellSize * (Vector3)p.Value.Item1;
                if (order > 0)
                {
                    bezierInterpolationPoints[i] = CellSize * (Vector3)p.Value.Item2;
                }
                else
                {
                    bezierInterpolationPoints[i] = CellSize * (Vector3)nextPos[p.Key].Item2;
                }
                targetPositions[i] = CellSize * (Vector3)nextPos[p.Key].Item1;
                i++;
            }

            OnFrameChanged?.Invoke(newFrame, newFrame + order);
        }

        /// <summary>
        /// Returns a dictionary containing a path for each projectile
        /// </summary>
        /// <returns></returns>
        public Dictionary<ProjectileKey, List<Vector3>> GetPaths()
        {
            if (FrameDatas == null) return null;
            Dictionary<ProjectileKey, List<Vector3>> res = new Dictionary<ProjectileKey, List<Vector3>>();
            foreach (var data in FrameDatas)
            {
                foreach (var pos in data.ProjectilePositionData)
                {
                    if (!res.ContainsKey(pos.Key))
                    {
                        List<Vector3> toAdd = new List<Vector3>();
                        toAdd.Add(pos.Value.Item1);
                        toAdd.Add(pos.Value.Item2);
                        res.Add(pos.Key, toAdd);
                    }
                    else
                    {
                        res[pos.Key].Add(pos.Value.Item1);
                        res[pos.Key].Add(pos.Value.Item2);
                    }
                }
            }
            Dictionary<ProjectileKey, List<Vector3>> bezierRes = new Dictionary<ProjectileKey, List<Vector3>>();
            foreach (var KVPair in res)
            {
                List<Vector3> drawPoints = new List<Vector3>();
                for (int i = 1; i < KVPair.Value.Count - 1; i += 2)
                {
                    Vector3 pZero = KVPair.Value[i - 1];
                    Vector3 pOne = KVPair.Value[i];
                    Vector3 pTwo = KVPair.Value[i + 1];
                    if (MathHelper.IsNaNVector3(pOne))
                    {
                        pOne = (-pZero + pTwo) / 2;
                    }
                    drawPoints.AddRange(MathHelper.BezierAproximation(pZero, pTwo, pOne));
                }
                bezierRes.Add(KVPair.Key, drawPoints);
            }

            return bezierRes;
        }

        public int GetFrameByTime(float time)
        {
            int frame = 0;
            if (frameTimeOverrides == null || frameTimeOverrides.Count == 0)
            {
                frame = Mathf.FloorToInt(time / TimeBetweenFrames);
            }
            else
            {
                float curTime = 0;
                for (int i = 0; i < FrameDatas.Count - 1; i++)
                {
                    var timeOverride = frameTimeOverrides.Find(x => x.Equals(new FrameTimeOverride { FrameOne = i, FrameTwo = i + 1 }));
                    if (timeOverride != null)
                    {
                        curTime += timeOverride.value;
                    }
                    else curTime += TimeBetweenFrames;
                    if (time < curTime) break;
                    else frame++;
                }
            }
            return frame;
        }


        void LoadFrameData()
        {
            if (swarmSequenceData == null)
            {
                Debug.LogError(new NullReferenceException("No seqence provided"));
                return;
            }
            FrameDatas = swarmSequenceData.Frames;
        }

        float CalculateDuration()
        {
            float res = 0;
            for (int i = 0; i < FrameDatas.Count - 1; i++)
            {
                var timeOverride = frameTimeOverrides.Find(x => x.Equals(new FrameTimeOverride { FrameOne = i, FrameTwo = i + 1 }));
                if (timeOverride != null)
                {
                    res += timeOverride.value;
                }
                else res += TimeBetweenFrames;
            }
            return res;
        }

        float DetermineTByTime(float time)
        {
            float t = 0;
            if (frameTimeOverrides.Count == 0)
            {
                t = (time % TimeBetweenFrames) / TimeBetweenFrames;
            }
            else
            {
                float curTime = 0;
                for (int i = 0; i < FrameDatas.Count - 1; i++)
                {
                    var timeOverride = frameTimeOverrides.Find(x => x.Equals(new FrameTimeOverride { FrameOne = i, FrameTwo = i + 1 }));
                    if (timeOverride != null)
                    {
                        curTime += timeOverride.value;
                    }
                    else curTime += TimeBetweenFrames;
                    if (time < curTime)
                    {
                        float overrideValue = timeOverride == null ? TimeBetweenFrames : timeOverride.value;
                        t = (overrideValue - (curTime - time)) / overrideValue; break;
                    }
                }
            }
            return t;
        }

        void InstanceMovement()
        {
            JobHandle h = new MoveProjectiles()
            {
                transform = UseWorldSpace ? Matrix4x4.identity : transform.localToWorldMatrix,
                positions = currentPositions,
                bezierInterpolationPoints = bezierInterpolationPoints,
                originalPositions = originalPositions,
                targets = targetPositions,
                t = t
            }.Schedule(currentPositions.Length, batchSize);
            h.Complete();
            int i = 0;
            foreach (var s in projectilePositions)
            {
                if (s.Value != null)
                {
                    s.Value.position = currentPositions[i];
                    i++;
                }
            }
        }

        /// <summary>
        /// Instantiates given projectiles
        /// </summary>
        /// <param name="toInstantiate"> key - position, value.item1 = projectile id, value.item2 internalID</param>
        void InstantiateProjectiles(ref Dictionary<ProjectileKey, Vector3> toInstantiate)
        {
            foreach (var v in toInstantiate)
            {
                var foundObj = Instances.Find(x => x.groupIndex == v.Key.GroupIndex);
                if (foundObj == null)
                {
                    Clear();
                    throw new NoPrefabWithGivenIdFoundException($"No prefab for group {v.Key.GroupIndex} found");
                }
                GameObject go = foundObj.prefab;
                if (go == null) throw new NullReferenceException($"Prefab for group {v.Key.GroupIndex} is null");
                var g = Instantiate(go, v.Value, Quaternion.identity);
                projectilePositions.Add(new ProjectileKey(v.Key.GroupIndex, v.Key.InstanceIndex), g.transform);
            }
            projectilePositions.SortProjectileDictionary();
        }

        /// <summary>
        /// Set settings from a scriptable object. Gameobjects will not be reinstantiated if an animation is already running (even if it's paused)! 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="loadTimeBetweenFrames"></param>
        /// <param name="loadFrameTimeOverrides"></param>
        /// <param name="loadProjectileLookUps"></param>
        public void LoadSettingsFromScriptableObject(AdditionalSequenceData settings, bool loadTimeBetweenFrames = true, bool loadFrameTimeOverrides = true, bool loadProjectileLookUps = true, bool saveScriptable = true)
        {
            if (saveScriptable) ProjectileDataScriptable = settings;
            if (loadTimeBetweenFrames) TimeBetweenFrames = settings.TimeBetweenFrames;
            if (loadFrameTimeOverrides) frameTimeOverrides = settings.FrameTimeOverrides;
            if (loadProjectileLookUps) Instances = settings.ProjectileLookUps;
        }

        /// <summary>
        /// Set new sequence
        /// </summary>
        /// <param name="new sequence"></param>
        /// <returns>Succesfully changed asset</returns>
        public bool SetSequence(SwarmSequence sequence)
        {
            if (active) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                if (sequence == null)
                {
                    swarmSequenceData = null;
                    FrameDatas = null;
                }
                else { swarmSequenceData = sequence; FrameDatas = sequence.Frames; }
                return true;
            }
        }

        /// <summary>
        /// Set new seqeunce with provided settings. If the animation can not be changed, provided settings won't be applied.
        /// </summary>
        /// <param name="new sequence"></param>
        /// <param name="settings"></param>
        /// <param name="loadTimeBetweenFrames"></param>
        /// <param name="loadFrameTimeOverrides"></param>
        /// <param name="loadProjectileLookUps"></param>
        /// <returns>Succesfully changed asset</returns>
        public bool SetSequence(SwarmSequence sequence, AdditionalSequenceData settings, bool loadTimeBetweenFrames = true, bool loadFrameTimeOverrides = true, bool loadProjectileLookUps = true)
        {
            if (active) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                this.ProjectileDataScriptable = settings;
                LoadSettingsFromScriptableObject(settings, loadTimeBetweenFrames, loadFrameTimeOverrides, loadProjectileLookUps);
                SetSequence(sequence);
                return true;
            }
        }


        internal SwarmSequence GetSwarmSequence()
        {
            return swarmSequenceData;
        }

        public string GetActiveSequenceName()
        {
            return swarmSequenceData.sequenceName;
        }

        float CalculateTimeBetweenFrames(float duration, int frameCount, List<FrameTimeOverride> frameTimeOverrides)
        {
            int i = 0;
            foreach (var v in frameTimeOverrides)
            {
                duration -= v.value;
                i++;
            }
            return duration / (frameCount - 1 - i);
        }

        /// <summary>
        /// Plays or unpauses an animation
        /// </summary>
        /// <param name="order"></param>
        public void Play()
        {
            LoadFrameData();
            Debug.Log($"Frame data: {swarmSequenceData.Frames == null}");
            if (paused) { paused = false; return; }
            if (active) return;
            ChangeFrame(currentFrame);
            active = true;
            StartCoroutine(RunSequence());
        }

        /// <summary>
        /// plays animation from given frame. If animation is currently paused it will restart from given frame.
        /// </summary>
        /// <param name="fromFrame"></param>
        /// <param name="order"> if order = 1, plays animation forward, if = -1 plays animation in reverse </param>
        public void Play(int fromFrame, int order)
        {
            LoadFrameData();
            DisposeBezierPoints();
            DisposeNativeCollections();
            if (Playing) return;
            if (paused) Stop();
            currentFrame = fromFrame;
            this.order = order;
            ChangeFrame(currentFrame);
            active = true;
            StartCoroutine(RunSequence());
        }
        /// <summary>
        /// Stops current animation, applying "completed objects handling" procedure to remaining objects
        /// </summary>
        public void Stop()
        {
            active = false;
            if (CompletedObjectsHandling == UnassignedObjectsHandlingType.Destroy)
            {
                foreach (var obj in projectilePositions)
                    if (obj.Value != null) Destroy(obj.Value.gameObject);
            }
            projectilePositions.Clear();
            order = 1;
            currentFrame = 0;
            t = 0;
            currentTime = 0;
            skipFrame = false;
            DisposeNativeCollections();
            DisposeBezierPoints();
        }

        public void Pause()
        {
            paused = true;
        }

        IEnumerator RunSequence(bool useFixedTime = false, float fixedTime = 0.002f)
        {
            Debug.Log("started sequence");
            YieldInstruction waitAmount;
            YieldInstruction endOfFrame = new WaitForEndOfFrame();

            if (!useFixedTime) waitAmount = endOfFrame;
            else waitAmount = new WaitForSeconds(fixedTime);
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
                                InstanceMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame += order;
                                }
                                else { skipFrame = false; }
                                ChangeFrame(currentFrame, DisposalMode.Normal);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= increment;
                            break;
                        case AnimationTypes.Repeat:
                            Action reset = () => { skipFrame = true; currentFrame = 0; };
                            OnAnimationFinished += reset;
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
                                ChangeFrame(currentFrame, DisposalMode.Normal);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= reset;
                            break;
                        case AnimationTypes.Single:
                            Action stop = () => { Stop(); };
                            OnAnimationFinished += stop;
                            InstanceMovement();
                            if (t >= 1)
                            {
                                currentFrame++;
                                ChangeFrame(currentFrame, DisposalMode.Normal);
                            }
                            yield return waitAmount;
                            OnAnimationFinished -= stop;
                            break;
                    }
                    if (!useFixedTime) { t += Time.deltaTime / timeOverrideValue; currentTime += order * Time.deltaTime; }
                    else { currentTime += order * fixedTime; t += fixedTime / timeOverrideValue; }
                }
                else { yield return endOfFrame; }
            }
        }

        public void Clear()
        {
            active = false;
            foreach (var v in projectilePositions)
            {
                DestroyImmediate(v.Value.gameObject);
            }
            projectilePositions = new Dictionary<ProjectileKey, Transform>();
            t = 0;
            skipFrame = false;
            FrameDatas = null;
            DisposeNativeCollections();
            DisposeBezierPoints();
        }

        private void Awake()
        {

#if UNITY_EDITOR
            if (!Application.isPlaying) EditorApplication.playModeStateChanged += CleanUp;
#endif
            if (ProjectileDataScriptable != null)
            {
                if (loadInstanceLookUps)
                    Instances = ProjectileDataScriptable.ProjectileLookUps;
                if (loadFrameTimeOverrides)
                    frameTimeOverrides = ProjectileDataScriptable.FrameTimeOverrides;
                if (loadTimeBetweenTurns)
                {
                    TimeBetweenFrames = ProjectileDataScriptable.TimeBetweenFrames;
                }
            }
            if (Application.isPlaying && PlayOnAwake)
                Play();
        }

        void DisposeNativeCollections()
        {
            if (currentPositions.IsCreated)
            {
                currentPositions.Dispose();
                targetPositions.Dispose();
                originalPositions.Dispose();
            }
        }

        void DisposeBezierPoints()
        {
            if (bezierInterpolationPoints.IsCreated)
                bezierInterpolationPoints.Dispose();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            DisposeNativeCollections();
            DisposeBezierPoints();
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorApplication.playModeStateChanged -= CleanUp;
#endif
        }
        private void OnApplicationQuit()
        {
            DisposeNativeCollections();
            DisposeBezierPoints();
        }
        public enum AnimationTypes
        {
            Single,
            Repeat,
            Pingpong,
        }

        /// <summary>
        /// How to handle objects that are no longer a part of the seqeunce?
        /// </summary>
        public enum UnassignedObjectsHandlingType
        {
            /// <summary>
            /// Unwanted objects will be destroyed
            /// </summary>
            Destroy,
            /// <summary>
            /// Unwanted objects will be left at their last known position
            /// </summary>
            Ignore
        }

        public enum DisposalMode { Normal, Immediate }
    }

    public struct MoveProjectiles : IJobParallelFor
    {
        public Matrix4x4 transform;
        public NativeArray<Vector3> positions;
        [ReadOnly] public NativeArray<Vector3> originalPositions;
        [ReadOnly] public NativeArray<Vector3> bezierInterpolationPoints;
        [ReadOnly] public NativeArray<Vector3> targets;
        public float t;
        public void Execute(int index)
        {
            if (MathHelper.IsNaNVector3(bezierInterpolationPoints[index]))
            {
                positions[index] = Vector3.Lerp(transform.MultiplyPoint3x4(originalPositions[index]), transform.MultiplyPoint3x4(targets[index]), t);
            }
            else
            {
                positions[index] = MathHelper.BezierInterpolation(transform.MultiplyPoint3x4(originalPositions[index]), transform.MultiplyPoint3x4(targets[index]), transform.MultiplyPoint3x4(bezierInterpolationPoints[index]), t);
            }
        }
    }
}
