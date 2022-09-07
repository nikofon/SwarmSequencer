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
        [SerializeField]
        Vector3Int gridDimensions = new Vector3Int(2, 2, 2);
        int GridYLevel = 0;

        Grid grid;

        SerializedProperty gridOriginSerialized;
        SerializedProperty gridRotationSerialized;
        SerializedProperty gridCellSizeSerialized;

        //Projectile Info
        List<ProjectileKey> ProjectileKeys = new List<ProjectileKey>();
        List<FrameData> GeneratedFrameDatas = new List<FrameData>();

        Nullable<ProjectileKey> selectedProjectileKey;

        int currentFrame;

        //Visual Elements
        SliderInt YLevelSlider;
        Vector3IntField GridSizeField;
        Vector3Field GridOriginField;
        Vector3Field GridRotationField;
        FloatField CellSizeField;
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
            //Serialized properties
            gridOriginSerialized = so.FindProperty("gridOrigin");
            gridRotationSerialized = so.FindProperty("gridRotation");
            gridCellSizeSerialized = so.FindProperty("gridCellSize");
            //ValueChange bindings
            YLevelSlider = rootVisualElement.Q<SliderInt>("YLevelSlider");
            YLevelSlider.RegisterValueChangedCallback((v) => { GridYLevel = v.newValue; SceneView.RepaintAll(); });
            YLevelSlider.highValue = gridDimensions.z;

            GridSizeField = rootVisualElement.Q<Vector3IntField>("GridDimensions");
            GridSizeField.value = (Vector3Int)gridDimensions;
            GridSizeField.RegisterValueChangedCallback((v) =>
            {
                gridDimensions.x = Mathf.Max(2, v.newValue.x);
                gridDimensions.y = Mathf.Max(2, v.newValue.y);
                gridDimensions.z = Mathf.Max(0, v.newValue.z);
                YLevelSlider.highValue = gridDimensions.z;
                GridSizeField.value = gridDimensions;
                SceneView.RepaintAll();
            });

            GridRotationField = rootVisualElement.Q<Vector3Field>("GridRotationField");
            GridRotationField.value = gridRotation.eulerAngles;
            GridRotationField.RegisterValueChangedCallback((v) =>
            {
                //Debug.Log($"new value: {v.newValue} grid rotation: {gridRotation.eulerAngles} Distance: {Vector3.Distance(v.newValue, gridRotation.eulerAngles)}");
                so.Update();
                gridRotationSerialized.quaternionValue = Quaternion.Euler(v.newValue);
                so.ApplyModifiedProperties();
                SceneView.RepaintAll();
            });

            CellSizeField = rootVisualElement.Q<FloatField>("CellSizeField");
            CellSizeField.value = gridCellSize;
            CellSizeField.TrackPropertyValue(gridCellSizeSerialized, (a) => CellSizeField.value = a.floatValue);
            CellSizeField.RegisterValueChangedCallback((v) =>
            {
                so.Update();
                gridCellSizeSerialized.floatValue = MathF.Max(0.0001f, v.newValue);
                so.ApplyModifiedProperties();
                CellSizeField.value = gridCellSizeSerialized.floatValue;
                SceneView.RepaintAll();
            });

            GridOriginField = rootVisualElement.Q<Vector3Field>("GridOriginField");
            GridOriginField.value = gridOrigin;
            GridOriginField.TrackPropertyValue(gridOriginSerialized, (a) => GridOriginField.value = a.vector3Value);
            GridOriginField.RegisterValueChangedCallback((v) =>
            {
                so.Update();
                gridOriginSerialized.vector3Value = v.newValue;
                so.ApplyModifiedProperties();
                SceneView.RepaintAll();
            });
            grid = new Grid((Vector2Int)gridDimensions, gridCellSize, gridOrigin, gridRotation);

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

        Matrix4x4 GetMatrixWithYOffset(float cellSize, float yLevel, Vector3 zero, Quaternion gridRotation)
        {
            var TRSMatrix = Matrix4x4.TRS(zero, gridRotation, cellSize * Vector3.one);
            TRSMatrix *= Matrix4x4.Translate(yLevel * cellSize * Vector3.forward);
            return TRSMatrix;
        }
        void DuringSceneGUI(SceneView sceneView)
        {
            Vector3 gridZero = gridOrigin;
            Quaternion gridRotation = this.gridRotation;
            float gridUniformScale = gridCellSize;
            Handles.TransformHandle(ref gridZero, ref gridRotation, ref gridUniformScale);
            so.Update();
            gridRotationSerialized.quaternionValue = gridRotation;
            gridOriginSerialized.vector3Value = gridZero;
            gridCellSizeSerialized.floatValue = gridUniformScale;
            so.ApplyModifiedProperties();
            if (grid.GridRotation != gridRotationSerialized.quaternionValue ||
            grid.GridOrigin != gridOriginSerialized.vector3Value + GridYLevel * Vector3.forward ||
            grid.CellSize != gridCellSizeSerialized.floatValue || grid.GridSize != (Vector2Int)gridDimensions)
            {
                grid = new Grid((Vector2Int)gridDimensions, GetMatrixWithYOffset(gridCellSize, GridYLevel, this.gridOrigin, this.gridRotation));
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