using UnityEngine;
using UnityEditor;
using SwarmSequencer.Serialization;
namespace SwarmSequencer
{
    [CustomEditor(typeof(SwarmSequenceDirector))]
    public class SqarmSequenceDirectorCustomEditor : Editor
    {
        SerializedProperty instances;
        SerializedProperty CellSize;
        SerializedProperty TimeBetweenTurns;
        SerializedProperty animationType;
        SerializedProperty turnTimeOverrides;
        SerializedProperty UnassignedObjectsHandling;
        SerializedProperty CompletedObjectsHandling;
        SerializedProperty UseWorldSpace;
        SerializedProperty batchSize;
        SerializedProperty PlayOnAwake;
        SerializedProperty GridSize;
        SerializedProperty center;
        SerializedProperty shouldDrawGrid;
        SerializedProperty DrawTragectories;
        SerializedProperty pathColors;

        AdditionalSequenceData scriptable;

        SwarmSequenceDirector targetDirector;

        SerializedProperty loadTimeBetweenFrames;
        SerializedProperty loadFrameTimeOverrides;
        SerializedProperty loadInstanceLookUps;

        bool showPosition;

        bool showSettings = true;
        bool showGizmosSettings;

        bool copyData = true;
        bool loadData;

        private void OnEnable()
        {
            targetDirector = (SwarmSequenceDirector)target;

            loadTimeBetweenFrames = serializedObject.FindProperty("loadTimeBetweenTurns");
            loadFrameTimeOverrides = serializedObject.FindProperty("loadFrameTimeOverrides");
            loadInstanceLookUps = serializedObject.FindProperty("loadInstanceLookUps");

            if (targetDirector.ProjectileDataScriptable != null)
            {
                copyData = false;
                loadData = true;
                scriptable = targetDirector.ProjectileDataScriptable;
            }

            center = serializedObject.FindProperty("center");
            shouldDrawGrid = serializedObject.FindProperty("shouldDrawGrid");
            instances = serializedObject.FindProperty("Instances");
            DrawTragectories = serializedObject.FindProperty("DrawTragectories");
            CellSize = serializedObject.FindProperty("CellSize");
            GridSize = serializedObject.FindProperty("GridSize");
            batchSize = serializedObject.FindProperty("batchSize");
            PlayOnAwake = serializedObject.FindProperty("PlayOnAwake");
            UseWorldSpace = serializedObject.FindProperty("UseWorldSpace");
            TimeBetweenTurns = serializedObject.FindProperty("TimeBetweenFrames");
            animationType = serializedObject.FindProperty("animationType");
            turnTimeOverrides = serializedObject.FindProperty("frameTimeOverrides");
            UnassignedObjectsHandling = serializedObject.FindProperty("UnassignedObjectsHandling");
            CompletedObjectsHandling = serializedObject.FindProperty("CompletedObjectsHandling");
            pathColors = serializedObject.FindProperty("pathColors");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !showSettings;
            showSettings = GUILayout.Button("Settings") ? true : showSettings;
            if (showSettings) { showGizmosSettings = false; }
            GUI.enabled = !showGizmosSettings;
            showGizmosSettings = GUILayout.Button("Gizmos") ? true : showGizmosSettings; ;
            GUI.enabled = true;
            if (showGizmosSettings) { showSettings = false; }
            EditorGUILayout.EndHorizontal();
            if (showSettings) { SettingsGUI(); }
            else if (showGizmosSettings) { GizmoGUI(); }
        }

        void GizmoGUI()
        {
            bool dw = DrawTragectories.boolValue;
            EditorGUILayout.LabelField("Grid");
            EditorGUILayout.PropertyField(shouldDrawGrid);
            EditorGUILayout.PropertyField(GridSize);
            EditorGUILayout.PropertyField(center);
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Tragectories");
            EditorGUILayout.PropertyField(DrawTragectories);
            EditorGUILayout.PropertyField(pathColors);
            serializedObject.ApplyModifiedProperties();
            if (dw != DrawTragectories.boolValue && DrawTragectories.boolValue)
            {
                targetDirector.sequencePath = targetDirector.GetPaths();
            }
        }

        void SettingsGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("SwarmSequence");
            var s = (SwarmSequence)EditorGUILayout.ObjectField(targetDirector.GetSwarmSequence(), typeof(SwarmSequence), false, GUILayout.Height(50));
            if (s != targetDirector.GetSwarmSequence())
            {
                targetDirector.SetSequence(s);
                if (DrawTragectories.boolValue)
                {
                    targetDirector.sequencePath = targetDirector.GetPaths();
                }
            }
            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(instances);
            EditorGUILayout.Space(10);
            EditorGUILayout.PropertyField(animationType);
            EditorGUILayout.PropertyField(UseWorldSpace);
            EditorGUILayout.PropertyField(CellSize);
            if (Application.isPlaying || !(loadData && loadTimeBetweenFrames.boolValue))
                EditorGUILayout.PropertyField(TimeBetweenTurns);
            if (Application.isPlaying || !(loadData && loadFrameTimeOverrides.boolValue))
                EditorGUILayout.PropertyField(turnTimeOverrides);
            EditorGUILayout.PropertyField(CompletedObjectsHandling);
            EditorGUILayout.PropertyField(UnassignedObjectsHandling);
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(PlayOnAwake);
            showPosition = EditorGUILayout.BeginFoldoutHeaderGroup(showPosition, "Advanced");
            if (showPosition)
            {
                EditorGUILayout.PropertyField(batchSize);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();

            GUILayout.Box("Import data from sctiptable object");

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !copyData;
            copyData = GUILayout.Button(new GUIContent("Copy")) ? true : copyData;
            if (copyData) { loadData = false; }
            GUI.enabled = !loadData;
            loadData = GUILayout.Button("Load") ? true : loadData; ;
            GUI.enabled = true;
            if (loadData) { copyData = false; }
            EditorGUILayout.EndHorizontal();

            scriptable = (AdditionalSequenceData)EditorGUILayout.ObjectField("ProjectileData Sciptable", scriptable, typeof(AdditionalSequenceData), false);
            EditorGUILayout.PropertyField(loadInstanceLookUps);
            EditorGUILayout.PropertyField(loadTimeBetweenFrames);
            EditorGUILayout.PropertyField(loadFrameTimeOverrides);

            serializedObject.ApplyModifiedProperties();
            if (copyData)
            {
                targetDirector.ProjectileDataScriptable = null;
                GUI.enabled = scriptable != null;
                if (GUILayout.Button("Copy data from scriptable"))
                {
                    if (scriptable != null)
                    {
                        targetDirector.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenFrames.boolValue, loadFrameTimeOverrides.boolValue, loadInstanceLookUps.boolValue);
                    }
                }
            }
            else
            {
                targetDirector.ProjectileDataScriptable = scriptable;
            }
            GUI.enabled = true;
            GUILayout.Space(3);
            GUI.enabled = !targetDirector.Playing;
            if (GUILayout.Button("Play"))
            {
                if (!Application.isPlaying)
                {
                    if (loadData && scriptable != null)
                    {
                        targetDirector.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenFrames.boolValue, loadFrameTimeOverrides.boolValue, loadInstanceLookUps.boolValue);
                    }
                    targetDirector.PlayAnimationEditor();
                }
                else targetDirector.Play();
            }
            GUI.enabled = ((SwarmSequenceDirector)target).Active;
            if (GUILayout.Button("Pause"))
            {
                ((SwarmSequenceDirector)target).Pause();
            }
            if (GUILayout.Button("Stop"))
            {
                if (!Application.isPlaying) targetDirector.Stop(SwarmSequenceDirector.DisposalMode.Immediate);
                else targetDirector.Stop();
            }
            GUI.enabled = true;
        }

    }
}
