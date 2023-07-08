using UnityEngine;
using UnityEditor;
using SwarmSequencer.Serialization;
namespace SwarmSequencer
{
    [CustomEditor(typeof(SwarmSequenceDirector))]
    public class SqarmSequenceDirectorCustomEditor : Editor
    {
        SerializedProperty projectileLookUps;
        SerializedProperty CellSize;
        SerializedProperty TimeBetweenTurns;
        SerializedProperty animationType;
        SerializedProperty turnTimeOverrides;
        SerializedProperty UnassignedObjectsHandling;
        SerializedProperty CompletedObjectsHandling;
        SerializedProperty UseWorldSpace;
        SerializedProperty batchSize;
        SerializedProperty deserializeOnAwake;
        SerializedProperty PlayOnAwake;
        SerializedProperty GridSize;
        SerializedProperty center;
        SerializedProperty shouldDrawGrid;
        SerializedProperty DrawTragectories;
        SerializedProperty pathColors;

        ProjectileDataScriptable scriptable;

        SwarmSequenceDirector targetDirector;

        SerializedProperty loadTimeBetweenFrames;
        SerializedProperty loadTurnTimeOverrides;
        SerializedProperty loadProjectileLookUps;

        bool showPosition;

        bool showSettings = true;
        bool showGizmosSettings;

        bool copyData = true;
        bool loadData;

        private void OnEnable()
        {
            targetDirector = (SwarmSequenceDirector)target;

            loadTimeBetweenFrames = serializedObject.FindProperty("loadTimeBetweenTurns");
            loadTurnTimeOverrides = serializedObject.FindProperty("loadTurnTimeOverrides");
            loadProjectileLookUps = serializedObject.FindProperty("loadProjectileLookUps");

            if (targetDirector.ProjectileDataScriptable != null)
            {
                copyData = false;
                loadData = true;
                scriptable = targetDirector.ProjectileDataScriptable;
            }

            center = serializedObject.FindProperty("center");
            shouldDrawGrid = serializedObject.FindProperty("shouldDrawGrid");
            projectileLookUps = serializedObject.FindProperty("projectileLookUps");
            DrawTragectories = serializedObject.FindProperty("DrawTragectories");
            CellSize = serializedObject.FindProperty("CellSize");
            GridSize = serializedObject.FindProperty("GridSize");
            batchSize = serializedObject.FindProperty("batchSize");
            PlayOnAwake = serializedObject.FindProperty("PlayOnAwake");
            deserializeOnAwake = serializedObject.FindProperty("deserializeOnAwake");
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
                targetDirector.GetPaths();
            }
        }

        void SettingsGUI()
        {
            if (Application.isPlaying || !(loadData && loadProjectileLookUps.boolValue))
                EditorGUILayout.PropertyField(projectileLookUps);
            var s = (SwarmSequence)EditorGUILayout.ObjectField(targetDirector.GetSwarmSequence(), typeof(SwarmSequence), false);
            if (s != targetDirector.GetSwarmSequence())
            {
                targetDirector.SetSequence(s);
            }
            EditorGUILayout.PropertyField(animationType);
            EditorGUILayout.PropertyField(UseWorldSpace);
            EditorGUILayout.PropertyField(CellSize);
            if (Application.isPlaying || !(loadData && loadTimeBetweenFrames.boolValue))
                EditorGUILayout.PropertyField(TimeBetweenTurns);
            if (Application.isPlaying || !(loadData && loadTurnTimeOverrides.boolValue))
                EditorGUILayout.PropertyField(turnTimeOverrides);
            EditorGUILayout.PropertyField(CompletedObjectsHandling);
            EditorGUILayout.PropertyField(UnassignedObjectsHandling);
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(PlayOnAwake);
            showPosition = EditorGUILayout.BeginFoldoutHeaderGroup(showPosition, "Advanced");
            if (showPosition)
            {
                EditorGUILayout.PropertyField(deserializeOnAwake);
                EditorGUILayout.PropertyField(batchSize);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();

            GUILayout.Box("Import data from sctiptable object");

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !copyData;
            copyData = GUILayout.Button("Copy") ? true : copyData;
            if (copyData) { loadData = false; }
            GUI.enabled = !loadData;
            loadData = GUILayout.Button("Load") ? true : loadData; ;
            GUI.enabled = true;
            if (loadData) { copyData = false; }
            EditorGUILayout.EndHorizontal();

            scriptable = (ProjectileDataScriptable)EditorGUILayout.ObjectField("ProjectileData Sciptable", scriptable, typeof(ProjectileDataScriptable), false);
            EditorGUILayout.PropertyField(loadProjectileLookUps);
            EditorGUILayout.PropertyField(loadTimeBetweenFrames);
            EditorGUILayout.PropertyField(loadTurnTimeOverrides);

            serializedObject.ApplyModifiedProperties();
            if (copyData)
            {
                targetDirector.ProjectileDataScriptable = null;
                GUI.enabled = scriptable != null;
                if (GUILayout.Button("Copy data from scriptable"))
                {
                    if (scriptable != null)
                    {
                        targetDirector.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenFrames.boolValue, loadTurnTimeOverrides.boolValue, loadProjectileLookUps.boolValue);
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
                        targetDirector.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenFrames.boolValue, loadTurnTimeOverrides.boolValue, loadProjectileLookUps.boolValue);
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
                if (!Application.isPlaying) targetDirector.StopAnimationEditor();
                else targetDirector.Stop();
            }
            GUI.enabled = true;
        }

    }
}
