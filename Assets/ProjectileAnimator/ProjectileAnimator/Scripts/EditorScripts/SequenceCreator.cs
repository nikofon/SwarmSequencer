using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ProjectileAnimator
{
    public class SequenceCreator : EditorWindow
    {

        SerializedObject so;

        //Grid params
        [SerializeField]
        Vector3 gridOrigin = Vector3.zero;
        [SerializeField]
        Quaternion gridRotation = Quaternion.identity;
        [SerializeField]
        float gridCellSize = 1f;
        Vector2Int gridDimensions = new Vector2Int(2, 2);
        int GridYLevel = 0;

        Grid grid;

        SerializedProperty gridOriginSerialized;
        SerializedProperty gridRotationSerialized;
        SerializedProperty gridUniformScaleSerialized;

        //Projectile Info
        List<ProjectileKey> ProjectileKeys = new List<ProjectileKey>();
        List<FrameData> GeneratedFrameDatas = new List<FrameData>();

        Nullable<ProjectileKey> selectedProjectileKey;

        int currentFrame;

        //Visual Elements
        SliderInt YLevelSlider;
        Vector2IntField GridSizeField;
        event Action<int, int> OnFrameChanged;

        [MenuItem("/Window/ProjectileAnimator/SequenceCreator")]
        private static void ShowWindow()
        {
            var window = GetWindow<SequenceCreator>();
            window.titleContent = new GUIContent("SequenceCreator");
            window.minSize = new Vector2(678, 450);
            window.Show();
        }

        private void OnEnable()
        {
            //setup
            so = new SerializedObject(this);
            VisualTreeAsset origin = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/SequenceCreatorUXML.uxml");
            TemplateContainer container = origin.CloneTree();
            rootVisualElement.Add(container);
            //ValueChange bindings
            YLevelSlider = rootVisualElement.Q<SliderInt>("YLevelSlider");
            YLevelSlider.RegisterValueChangedCallback((v) => { GridYLevel = v.newValue; SceneView.RepaintAll(); });
            GridSizeField = rootVisualElement.Q<Vector2IntField>("GridSize");
            GridSizeField.RegisterValueChangedCallback((v) =>
            {
                gridDimensions.x = Mathf.Max(2, v.newValue.x);
                gridDimensions.y = Mathf.Max(2, v.newValue.y);
                GridSizeField.value = gridDimensions;
            });
            //Serialized properties
            gridOriginSerialized = so.FindProperty("gridOrigin");
            gridRotationSerialized = so.FindProperty("gridRotation");
            gridUniformScaleSerialized = so.FindProperty("gridCellSize");

            grid = new Grid(gridDimensions, gridCellSize, gridOrigin, gridRotation);

            SceneView.duringSceneGui += DuringSceneGUI;
        }

        void ChangeCurrentFrame(int changeTo)
        {
            int oldFrame = currentFrame;
            currentFrame = changeTo;
            OnFrameChanged?.Invoke(oldFrame, changeTo);
        }


        void AddProjectileInfoToFrame(int frameCount, ProjectileKey key, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                Debug.LogWarning($"A frame you trying to add info to: {frameCount} doesn't exist");
                return;
            }
            if (GeneratedFrameDatas[frameCount] == null) { Debug.LogWarning($"A frame you trying to add info to: {frameCount} is null, you should initialize it first"); return; }
            if (GeneratedFrameDatas[frameCount].ProjectilePositionData.ContainsKey(key))
                GeneratedFrameDatas[frameCount].ProjectilePositionData[key] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            else
                GeneratedFrameDatas[frameCount].ProjectilePositionData.Add(key, new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl));


        }

        void CreateNewFrame(int frameCount, Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> projectilePositionData)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                frameCount = GeneratedFrameDatas.Count;
                GeneratedFrameDatas.Add(new FrameData(projectilePositionData, frameCount));
            }
            else
            {
                List<FrameData> newFrameDatas = new List<FrameData>(GeneratedFrameDatas.Count + 1);
                for (int i = 0; i < frameCount; i++)
                {
                    newFrameDatas[i] = GeneratedFrameDatas[i];
                }
                newFrameDatas[frameCount].ProjectilePositionData = projectilePositionData;
                for (int i = frameCount + 2; i <= GeneratedFrameDatas.Count; i++)
                {
                    newFrameDatas[i] = GeneratedFrameDatas[i - 1];
                    newFrameDatas[i].Order = i;
                }
                GeneratedFrameDatas = newFrameDatas;
            }
        }

        void DeleteFrame(int frameCount)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                Debug.LogWarning($"You are trying to delete an unexisting frame: {frameCount}");
                return;
            }
            GeneratedFrameDatas.RemoveAt(frameCount);
            for (int i = frameCount; i < GeneratedFrameDatas.Count; i++)
            {
                GeneratedFrameDatas[i].Order = i;
            }
        }

        void DrawGrid(Vector2Int gridSize, float cellSize, float yLevel, Vector3 zero, Quaternion gridRotation)
        {
            Vector3 localZero = new Vector3(zero.x, zero.y, zero.z);
            var TRSMatrix = Matrix4x4.TRS(localZero, gridRotation, Vector3.one);
            /*
            Vector3 localForward = TRSMatrix * (yLevel * cellSize * Vector3.forward);
            localZero += localForward;
            TRSMatrix = Matrix4x4.TRS(localZero, gridRotation, Vector3.one);
            */
            TRSMatrix *= Matrix4x4.Translate(yLevel * cellSize * Vector3.forward);
            GizmoHelper.DrawGrid3DWithHandles((Vector3Int)gridSize, cellSize, Vector3.zero, TRSMatrix);
        }

        void DuringSceneGUI(SceneView sceneView)
        {
            Vector3 gridZero = this.gridOrigin;
            Quaternion gridRotation = this.gridRotation;
            float gridUniformScale = this.gridCellSize;
            Handles.TransformHandle(ref gridZero, ref gridRotation, ref gridUniformScale);
            so.Update();
            gridRotationSerialized.quaternionValue = gridRotation;
            gridOriginSerialized.vector3Value = gridZero;
            gridUniformScaleSerialized.floatValue = gridUniformScale;
            so.ApplyModifiedProperties();
            if (grid.GridRotation != this.gridRotation || grid.GridOrigin != this.gridOrigin + GridYLevel * Vector3.forward || grid.CellSize != this.gridCellSize || grid.GridSize != gridDimensions)
            {
                grid = new Grid(gridDimensions, gridCellSize, gridOrigin + GridYLevel * Vector3.forward, gridRotation);
            }
            if (Event.current.type == EventType.Repaint)
            {
                GizmoHelper.DrawGridWithHandles(grid);
                //DrawGrid(gridSize, gridUniformScale, GridYLevel, gridZero, gridRotation);
            }

        }


        private void OnGUI()
        {

        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
        }
    }
}