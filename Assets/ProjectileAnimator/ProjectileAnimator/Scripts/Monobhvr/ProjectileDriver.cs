using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using System;
using Unity.Collections;
using UnityEditor;
using System.Threading;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
#endif

namespace ProjectileAnimator
{
    [ExecuteInEditMode]
    public partial class ProjectileDriver : MonoBehaviour
    {
        //data to load
        /// <summary>
        /// Projectile data scriptablel, used by this object. To set it and apply settings, call ChangeAsset(string, ProjectileDataScriptable,...)
        /// </summary>
        public ProjectileDataScriptable ProjectileDataScriptable { get => projectileDataScriptable; set => projectileDataScriptable = value; }
        [SerializeField] ProjectileDataScriptable projectileDataScriptable;

        [SerializeField]bool loadTimeBetweenTurns;
        [SerializeField]bool loadTurnTimeOverrides;
        [SerializeField]bool loadProjectileLookUps;

        [SerializeField] TextAsset projectileDataAsset;

        public List<ProjectileLookUp> projectileLookUps = new List<ProjectileLookUp>();

        public bool PlayOnAwake = false;

        [Tooltip("Pixel to local units")]
        public float CellSize;
        [Tooltip("By default projectiles move in local space, check this to use world space instead")]
        public bool UseWorldSpace;

        [Min(0.01f)]
        public float TimeBetweenFrames;

        [Tooltip("How to handle objects that disappeare on the next frame?")]
        public UnwantedObjectsHandlingType UnassignedObjectsHandling = UnwantedObjectsHandlingType.Destroy;
        [Tooltip("How to handle objects that have completed the animation?")]
        public UnwantedObjectsHandlingType CompletedObjectsHandling = UnwantedObjectsHandlingType.Destroy;

        public AnimationTypes AnimationType { get => animationType; set { if (Playing) { Debug.LogWarning("Can't change AnimationType while the animation is playing."); return; } animationType = value; } }
        [SerializeField] AnimationTypes animationType = AnimationTypes.Single;

        public List<FrameTimeOverride> frameTimeOverrides = new List<FrameTimeOverride>();

        //run data
        /// <summary>
        /// List of current frame datas
        /// </summary>
        public ReadOnlyCollection<FrameData> ActiveFrameDatas { get { if (readonlyFrameData == null) readonlyFrameData = new ReadOnlyCollection<FrameData>(FrameDatas); return readonlyFrameData; } }
        ReadOnlyCollection<FrameData> readonlyFrameData;

        private List<FrameData> FrameDatas;

        /// <summary>
        /// Holds deserialized data that is not currently active
        /// </summary>
        public Dictionary<string, List<FrameData>> DeserializedFrameDatas { get { if (deserializedFrameDatas == null) deserializedFrameDatas = new Dictionary<string, List<FrameData>>(); return deserializedFrameDatas; } 
            set => deserializedFrameDatas = value; }
        Dictionary<string, List<FrameData>> deserializedFrameDatas;

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
        NativeArray<Vector3> targetPositions;

        //Turn info
        public int CurrentFrame { get => currentFrame; }
        int currentFrame = 0;
        /// <summary>
        /// current lerp time
        /// </summary>
        float t;

        //Status variables
        /// <summary>
        /// Is an animation is currently playing?
        /// </summary>
        public bool Playing { get => running && !paused; }
        public bool Paused { get => paused; }
        /// <summary>
        /// Is animation active? (returns true even if paused)
        /// </summary>
        public bool Running { get => running; }

        bool paused;

        bool running;
        bool skipFrame;
        int order = 1;
        float timeOverrideValue = -1;

        //Advanced

        [Tooltip("If checked, provided textasset will be deserealized on awake")]
        public bool deserializeOnAwake = true;

        [Tooltip("Determines batch size for IJobParallelFor.Schedule()")]
        [Min(1)]
        public int batchSize = 1;

        void ChangeTurn(int newTurn, int order = 1, DisposalMode dsplM = DisposalMode.Normal)
        {
            if ((order > 0 && newTurn + 1 >= FrameDatas.Count) || (order < 0 && newTurn - 1 < 0)) {
                OnAnimationFinished?.Invoke();
                return; }
            t = 0;
            var timeOverride = frameTimeOverrides.Find(x => x.Equals(new FrameTimeOverride() { FrameOne = currentFrame, FrameTwo = currentFrame + order }));
            timeOverrideValue = timeOverride == null ? TimeBetweenFrames : timeOverride.value;
            var pos = new Dictionary<ProjectileKey, SerializableVector3>(FrameDatas[newTurn].ProjectilePositionData);
            Dictionary<Vector3, ProjectileKey> toInstantiate = new Dictionary<Vector3, ProjectileKey>();
            List<ProjectileKey> toRemove = new List<ProjectileKey>();
            var nextPos = FrameDatas[newTurn + order].ProjectilePositionData;
            foreach (var p in projectilePositions)
            {
                if (!nextPos.ContainsKey(p.Key)) toRemove.Add(p.Key);
            }
            foreach (var trmv in toRemove)
            {
                var trvmGO = projectilePositions[trmv];
                projectilePositions.Remove(trmv);
                pos.Remove(trmv);
                if (UnassignedObjectsHandling == UnwantedObjectsHandlingType.Destroy && trvmGO != null) {
                    if (dsplM == DisposalMode.Normal) { Destroy(trvmGO.gameObject); }
                    else DestroyImmediate(trvmGO.gameObject); }
            }
            foreach (var v in pos)
            {
                if (!projectilePositions.ContainsKey(v.Key)) toInstantiate.Add(v.Value, v.Key);
            }
            InstantiateProjectiles(toInstantiate);
            currentPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            originalPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            targetPositions = new NativeArray<Vector3>(pos.Count, Allocator.Persistent);
            int i = 0;
            foreach (var p in pos)
            {
                currentPositions[i] = CellSize * (Vector3)p.Value;
                originalPositions[i] = CellSize * (Vector3)p.Value;
                targetPositions[i] = CellSize * (Vector3)nextPos[p.Key];
                i++;
            }

            OnFrameChanged?.Invoke(newTurn, newTurn + 1);
        }


        void ProjectileMovement(bool useFixedTime = false, float fixedTime = 0)
        {
            if (!useFixedTime) t += Time.deltaTime / timeOverrideValue;
            else t += fixedTime / timeOverrideValue;
            JobHandle h = new MoveProjectiles() { transform = UseWorldSpace? Matrix4x4.identity: transform.localToWorldMatrix, positions = currentPositions, originalPositions = originalPositions, targets = targetPositions, t = t }.Schedule(currentPositions.Length, batchSize);
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
        void InstantiateProjectiles(Dictionary<Vector3, ProjectileKey> toInstantiate)
        {
            foreach (var v in toInstantiate)
            {
                var foundObj = projectileLookUps.Find(x => x.id == v.Value.ProjectilePrefabId );
                if (foundObj == null) { Clear();
                    throw new NoPrefabWithGivenIdFoundException($"No prefab with id {v.Value.ProjectilePrefabId} found, add prefab with this id to PrefabLookUps"); }
                GameObject go = foundObj.prefab;
                var g = Instantiate(go, v.Key, Quaternion.identity);
                projectilePositions.Add(new ProjectileKey(v.Value.ProjectilePrefabId, v.Value.ProjectileInternalId), g.transform);
            }
            projectilePositions.SortProjectileDictionary();
        }

        /// <summary>
        /// set settings from a scriptable object. Gameobjects will not be reinstantiated if an animation is already running (even if it's paused)! 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="loadTimeBetweenFrames"></param>
        /// <param name="loadFrameTimeOverrides"></param>
        /// <param name="loadProjectileLookUps"></param>
        public void LoadSettingsFromScriptableObject(ProjectileDataScriptable settings, bool loadTimeBetweenFrames = true, bool loadFrameTimeOverrides = true, bool loadProjectileLookUps = true, bool saveScriptable = true)
        {
            if (saveScriptable) ProjectileDataScriptable = settings;
            if (loadTimeBetweenFrames) TimeBetweenFrames = settings.TimeBetweenFrames;
            if (loadFrameTimeOverrides) frameTimeOverrides = settings.FrameTimeOverrides;
            if (loadProjectileLookUps) projectileLookUps = settings.ProjectileLookUps;
        }

        /// <summary>
        /// Set new animation from string
        /// </summary>
        /// <param name="newAssetText"></param>
        /// <returns>Succesfully changed asset</returns>
        public bool ChangeAsset(string newAssetText)
        {
            if (running) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                FrameDatas = FrameDataSerializer.DeserializeFrameData(newAssetText);
                return true;
            }
        }

        /// <summary>
        /// Set new animation from string with provided settings. If the animation can not be changed, provided settings won't be applied.
        /// </summary>
        /// <param name="newAsset"></param>
        /// <param name="settings"></param>
        /// <param name="loadTimeBetweenFrames"></param>
        /// <param name="loadFrameTimeOverrides"></param>
        /// <param name="loadProjectileLookUps"></param>
        /// <returns>Succesfully changed asset</returns>
        public bool ChangeAsset(string newAsset, ProjectileDataScriptable settings, bool loadTimeBetweenFrames = true, bool loadFrameTimeOverrides = true, bool loadProjectileLookUps = true)
        {
            if (running) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                this.ProjectileDataScriptable = settings;
                LoadSettingsFromScriptableObject(settings, loadTimeBetweenFrames, loadFrameTimeOverrides, loadProjectileLookUps);
                ChangeAsset(newAsset);
                return true;
            }            
        }
   

        /// <summary>
        /// Set new animation from data
        /// </summary>
        /// <param name="newData"></param>
        /// <returns></returns>
        public bool ChangeAsset(List<FrameData> newData)
        {
            if (running) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                FrameDatas = newData;
                return true;
            }
        }
        public bool ChangeAsset(List<FrameData> newData, ProjectileDataScriptable settings, bool loadTimeBetweenFrames = true, bool loadFrameTimeOverrides = true, bool loadProjectileLookUps = true)
        {
            if (running) { Debug.LogWarning("You can't change animation while the other one is running, stop the animation first and try again."); return false; }
            else
            {
                this.ProjectileDataScriptable = settings;
                LoadSettingsFromScriptableObject(settings, loadTimeBetweenFrames, loadFrameTimeOverrides, loadProjectileLookUps);
                ChangeAsset(newData);
                return true;
            }
        }

        /// <summary>
        /// Call this function to asynchronously deserealize a string and add it to DeserializedFrameDatas with given name being the key.
        /// </summary>
        /// <param name="newAssetText"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public void DeserializeAssetToQueue(string newAssetText, string name)
        {
            Thread thread = new Thread(new ThreadStart(() => {
                DeserializedFrameDatas.Add(name, FrameDataSerializer.DeserializeFrameData(newAssetText));
            }));
            thread.Start();
        }

        /// <summary>
        /// Plays or unpauses an animation
        /// </summary>
        /// <param name="order"></param>
        public void Play()
        {
            if (paused) { paused = false; return; }
            if (running) return;
            if (FrameDatas == null) FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            ChangeTurn(currentFrame);
            running = true;
            StartCoroutine(RunProjectileMovement());
        }

        /// <summary>
        /// plays animation from given frame. If animation is currently paused it will restart from given frame.
        /// </summary>
        /// <param name="fromFrame"></param>
        /// <param name="order"> if order = 1, plays animation forward, if = -1 plays animation in reverse </param>
        public void Play(int fromFrame, int order)
        {
            if (paused) Stop();
            if (running) return;
            currentFrame = fromFrame;
            if (FrameDatas == null) FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            this.order = order;
            ChangeTurn(currentFrame);
            running = true;
            StartCoroutine(RunProjectileMovement());
        }
        /// <summary>
        /// Stops current animation, applying "completed objects handling" procedure to remaining objects
        /// </summary>
        public void Stop()
        {
            running = false;
            if(CompletedObjectsHandling == UnwantedObjectsHandlingType.Destroy) {
                foreach (var obj in projectilePositions)
                    if(obj.Value != null) Destroy(obj.Value.gameObject);
                    }
            projectilePositions.Clear();
            order = 1;
            currentFrame = 0;
            t = 0;
            skipFrame = false;
            skipFrame = false;
            DisposeNativeCollections();
        }

        public void Pause()
        {
            paused = true;
        }

        IEnumerator RunProjectileMovement(bool useFixedTime = false, float fixedTime = 0)
        {
            YieldInstruction waitAmount;
            if (!useFixedTime) waitAmount = new WaitForEndOfFrame();
            else waitAmount = new WaitForSeconds(fixedTime);
            switch (AnimationType) {
                case AnimationTypes.Pingpong:
                    Action increment = () => { order *= -1; skipFrame = true; };
                    OnAnimationFinished += increment;
                    while (running) {
                        if (!paused)
                        {
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
                                ChangeTurn(currentFrame, order);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= increment;
                    break;
                case AnimationTypes.Repeat:
                    Action reset = () => { skipFrame = true; currentFrame = 0; };
                    OnAnimationFinished += reset;
                    while (running)
                    {
                        if (!paused)
                        {
                            if (!skipFrame) ProjectileMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame++;
                                    DisposeNativeCollections();
                                }
                                else { skipFrame = false; }
                                ChangeTurn(currentFrame);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= reset;
                    break;
                case AnimationTypes.Single:
                    Action stop = () => { Stop(); };
                    OnAnimationFinished += stop;
                    while (running)
                    {
                        if (!paused)
                        {
                            ProjectileMovement();
                            if (t >= 1)
                            {
                                currentFrame++;
                                DisposeNativeCollections();
                                ChangeTurn(currentFrame);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= stop;
                    break;
            }
        }

        public void Clear()
        {
            running = false;
            foreach (var v in projectilePositions)
            {
                DestroyImmediate(v.Value.gameObject);
            }
            projectilePositions = new Dictionary<ProjectileKey, Transform>();
            t = 0;
            skipFrame = false;
            FrameDatas = null;
            DisposeNativeCollections();
        }

        private void Awake()
        {
#if UNITY_EDITOR
            if(!Application.isPlaying) EditorApplication.playModeStateChanged += CleanUp;
#endif
            if(ProjectileDataScriptable != null)
            {
                if(loadProjectileLookUps)
                    projectileLookUps = ProjectileDataScriptable.ProjectileLookUps;
                if(loadTurnTimeOverrides)
                    frameTimeOverrides = ProjectileDataScriptable.FrameTimeOverrides;
                if(loadTimeBetweenTurns)
                {
                    TimeBetweenFrames = ProjectileDataScriptable.TimeBetweenFrames;
                }
            }
            if (deserializeOnAwake)
                if(projectileDataAsset != null)
                    FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            if (Application.isPlaying && PlayOnAwake)
                Play();
        }

#if UNITY_EDITOR

        public void PlayAnimationEditor()
        {
            if (paused) { paused = false; return; }
            if (FrameDatas == null) FrameDatas = FrameDataSerializer.DeserializeFrameData(projectileDataAsset.text);
            ChangeTurn(0);
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
            switch (AnimationType)
            {
                case AnimationTypes.Pingpong:
                    Action increment = () => { order *= -1; skipFrame = true; };
                    OnAnimationFinished += increment;
                    while (running)
                    {
                        if (!paused)
                        {
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
                                ChangeTurn(currentFrame, order, DisposalMode.Immediate);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= increment;
                    break;
                case AnimationTypes.Repeat:
                    Action reset = () => { skipFrame = true; currentFrame = 0; };
                    OnAnimationFinished += reset;
                    while (running)
                    {
                        if (!paused)
                        {
                            if (!skipFrame) ProjectileMovement();
                            if (t >= 1)
                            {
                                if (!skipFrame)
                                {
                                    currentFrame++;
                                    DisposeNativeCollections();
                                }
                                else { skipFrame = false; }
                                ChangeTurn(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= reset;
                    break;
                case AnimationTypes.Single:
                    Action stop = () => { StopAnimationEditor(); };
                    OnAnimationFinished += stop;
                    while (running)
                    {
                        if (!paused)
                        {
                            ProjectileMovement();
                            if (t >= 1)
                            {
                                currentFrame++;
                                DisposeNativeCollections();
                                ChangeTurn(currentFrame, dsplM: DisposalMode.Immediate);
                            }
                        }
                        yield return waitAmount;
                    }
                    OnAnimationFinished -= stop;
                    break;
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
#endif

        void DisposeNativeCollections()
        {
            if (currentPositions.IsCreated)
            {
                currentPositions.Dispose();
                targetPositions.Dispose();
                originalPositions.Dispose();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            DisposeNativeCollections();
#if UNITY_EDITOR
            if(!Application.isPlaying)EditorApplication.playModeStateChanged -= CleanUp;
#endif
        }
        private void OnApplicationQuit()
        {
            DisposeNativeCollections();
        }
        public enum AnimationTypes
        {
            Single,
            Repeat,
            Pingpong,
        }
        
        /// <summary>
        /// How to handle objects that are no longer part of animation?
        /// </summary>
        public enum UnwantedObjectsHandlingType
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

        public enum DisposalMode { Normal, Immediate}
    }

    public struct MoveProjectiles : IJobParallelFor
    {
        public Matrix4x4 transform;
        public NativeArray<Vector3> positions;
        [ReadOnly]public NativeArray<Vector3> originalPositions;
        [ReadOnly]public NativeArray<Vector3> targets;
        public float t;
        public void Execute(int index)
        {
            positions[index] = Vector3.Lerp(transform.MultiplyPoint(originalPositions[index]), transform.MultiplyPoint(targets[index]), t);
        }
    }
}
