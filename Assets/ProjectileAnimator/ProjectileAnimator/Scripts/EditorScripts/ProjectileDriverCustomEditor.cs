using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
namespace ProjectileAnimator
{
    [CustomEditor(typeof(ProjectileDriver))]
    public class ProjectileDriverCustomEditor: Editor
    {
        SerializedProperty projectileDataAsset;
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
        SerializedProperty t;

        ProjectileDataScriptable scriptable;

        ProjectileDriver targetDriver;

        SerializedProperty loadTimeBetweenTurns;
        SerializedProperty loadTurnTimeOverrides;
        SerializedProperty loadProjectileLookUps;

        bool showPosition;

        bool showSettings = true;
        bool showGizmosSettings;

        bool copyData = true;
        bool loadData;

        private void OnEnable()
        {
            targetDriver = (ProjectileDriver)target;

            loadTimeBetweenTurns = serializedObject.FindProperty("loadTimeBetweenTurns");
            t = serializedObject.FindProperty("t");
            loadTurnTimeOverrides = serializedObject.FindProperty("loadTurnTimeOverrides");
            loadProjectileLookUps = serializedObject.FindProperty("loadProjectileLookUps");

            if (targetDriver.ProjectileDataScriptable != null)
            {
                copyData = false;
                loadData = true;
                scriptable = targetDriver.ProjectileDataScriptable;
            }

            projectileDataAsset = serializedObject.FindProperty("projectileDataAsset");
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
            showSettings = GUILayout.Button("Settings")? true:showSettings;
            if (showSettings) { showGizmosSettings = false; }
            GUI.enabled = !showGizmosSettings;
            showGizmosSettings = GUILayout.Button("Gizmos") ? true : showGizmosSettings; ;
            GUI.enabled = true;
            if (showGizmosSettings) { showSettings = false; }
            EditorGUILayout.EndHorizontal();
            if (showSettings) { SettingsGUI(); }
            else if(showGizmosSettings) { GizmoGUI(); }
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
            if(dw != DrawTragectories.boolValue && DrawTragectories.boolValue)
            {
                targetDriver.GetPaths();
            }
        }

        void SettingsGUI()
        {
            EditorGUILayout.PropertyField(projectileDataAsset);
            if (Application.isPlaying || !(loadData && loadProjectileLookUps.boolValue))
                EditorGUILayout.PropertyField(projectileLookUps);
            EditorGUILayout.PropertyField(animationType);
            EditorGUILayout.PropertyField(t);
            EditorGUILayout.PropertyField(UseWorldSpace);
            EditorGUILayout.PropertyField(CellSize);
            if (Application.isPlaying || !(loadData && loadTimeBetweenTurns.boolValue))
                EditorGUILayout.PropertyField(TimeBetweenTurns);
            if (Application.isPlaying || !(loadData && loadTurnTimeOverrides.boolValue))
                EditorGUILayout.PropertyField(turnTimeOverrides);
            EditorGUILayout.PropertyField(CompletedObjectsHandling);
            EditorGUILayout.PropertyField(UnassignedObjectsHandling);
            float tempTime = EditorGUILayout.Slider(targetDriver.CurrentTime, 0, targetDriver.Duration - 0.001f);
            if(tempTime != targetDriver.CurrentTime)
            {
                targetDriver.CurrentTime = tempTime;
            }
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
            EditorGUILayout.PropertyField(loadTimeBetweenTurns);
            EditorGUILayout.PropertyField(loadTurnTimeOverrides);

            serializedObject.ApplyModifiedProperties();
            if (copyData)
            {
                targetDriver.ProjectileDataScriptable = null;
                GUI.enabled = scriptable != null;
                if (GUILayout.Button("Copy data from scriptable"))
                {
                    if (scriptable != null)
                    {
                        targetDriver.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenTurns.boolValue, loadTurnTimeOverrides.boolValue, loadProjectileLookUps.boolValue);
                    }
                }
            }
            else
            {
                targetDriver.ProjectileDataScriptable = scriptable;
            }
            GUI.enabled = true;
            GUILayout.Space(3);
            GUI.enabled = !targetDriver.Playing;
            if (GUILayout.Button("Play"))
            {
                if (!Application.isPlaying) { if (loadData && scriptable != null) { 
                        targetDriver.LoadSettingsFromScriptableObject(scriptable, loadTimeBetweenTurns.boolValue, loadTurnTimeOverrides.boolValue, loadProjectileLookUps.boolValue); } 
                    targetDriver.PlayAnimationEditor(); }
                else targetDriver.Play();
            }
            GUI.enabled = ((ProjectileDriver)target).Running;
            if (GUILayout.Button("Pause"))
            {
                ((ProjectileDriver)target).Pause();
            }
            if (GUILayout.Button("Stop"))
            {
                if (!Application.isPlaying) targetDriver.StopAnimationEditor();
                else targetDriver.Stop();
            }
            GUI.enabled = true;
        }

    }
}
