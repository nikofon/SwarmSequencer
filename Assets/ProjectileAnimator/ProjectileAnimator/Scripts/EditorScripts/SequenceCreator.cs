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

        Dictionary<int, Vector3[]> gridPointsScreenPosition;

        bool modifyingGridInSceneView;

        Grid grid;

        SerializedProperty gridOriginSerialized;
        SerializedProperty gridRotationSerialized;
        SerializedProperty gridCellSizeSerialized;

        //Projectile Info
        List<ProjectileKey> ProjectileKeys = new List<ProjectileKey>();
        List<FrameData> GeneratedFrameDatas = new List<FrameData>();
        FrameData selectedFrameData;

        public ProjectileInstanceGUI SelectedProjectileInstance { get; private set; }

        int currentFrame;

        //Visual Elements
        IMGUIContainer selectedProjectilePreview;
        IntegerField projectileGroupIntField;
        SliderInt YLevelSlider;
        Vector3IntField GridSizeField;
        Vector3Field GridOriginField;
        Vector3Field GridRotationField;
        FloatField gridCellSizeField;
        Color editGridOffButtonColor = new Color(0.7372549f, 0.1529412f, 0.1882353f);
        Color editGridOnButtonColor = new Color(0.1529412f, 0.7372549f, 0.2303215f);

        ScrollView projectileGroupScrollView;

        VisualTreeAsset projectileContainerPrefab;
        VisualTreeAsset projectileInstancePrefab;

        Matrix4x4 sceneCameraMatrix;

        Dictionary<int, ProjectileGroupUI> projectileGroupContainerDict = new Dictionary<int, ProjectileGroupUI>();

        //Selected projectile info

        Label SelectedProjectileGroupLabel;
        Label SelectedProjectileInstanceLabel;
        ColorField SelectedProjectileInstanceColorField;
        ObjectField SelectedProjectilePrefabField;
        GameObject selectedInstancePrefab;
        Editor selectedInstancePrefabEditor;

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
            container.style.flexGrow = 1;
            rootVisualElement.Add(container);
            //Serialized properties
            gridOriginSerialized = so.FindProperty("gridOrigin");
            gridRotationSerialized = so.FindProperty("gridRotation");
            gridCellSizeSerialized = so.FindProperty("gridCellSize");
            #region GridSettings
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
                Debug.Log($"new value: {v.newValue} grid rotation: {gridRotation.eulerAngles} Distance: {Vector3.Distance(v.newValue, gridRotation.eulerAngles)}");
                so.Update();
                gridRotationSerialized.quaternionValue = Quaternion.Euler(v.newValue);
                so.ApplyModifiedProperties();
                SceneView.RepaintAll();
            });

            gridCellSizeField = rootVisualElement.Q<FloatField>("CellSizeField");
            gridCellSizeField.value = gridCellSize;
            gridCellSizeField.RegisterValueChangedCallback((v) =>
            {
                so.Update();
                gridCellSizeSerialized.floatValue = MathF.Max(0.0001f, v.newValue);
                so.ApplyModifiedProperties();
                gridCellSizeField.value = gridCellSizeSerialized.floatValue;
                SceneView.RepaintAll();
            });

            GridOriginField = rootVisualElement.Q<Vector3Field>("GridOriginField");
            GridOriginField.value = gridOrigin;
            GridOriginField.RegisterValueChangedCallback((v) =>
            {
                so.Update();
                gridOriginSerialized.vector3Value = v.newValue;
                so.ApplyModifiedProperties();
                SceneView.RepaintAll();
            });
            grid = new Grid((Vector2Int)gridDimensions, gridCellSize, gridOrigin, gridRotation);

            var tb = rootVisualElement.Q<ToolbarButton>("EnableGridEditingButton");
            tb.clicked += () => { modifyingGridInSceneView = !modifyingGridInSceneView; tb.style.backgroundColor = modifyingGridInSceneView ? editGridOnButtonColor : editGridOffButtonColor; SceneView.RepaintAll(); };

            #endregion
            projectileContainerPrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileGroup.uxml");
            projectileInstancePrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileInstance.uxml");

            projectileGroupScrollView = rootVisualElement.Q<ScrollView>("ProjectileGroupsScrollView");

            SelectedProjectileGroupLabel = rootVisualElement.Q<Label>("GroupIndexCounter");
            SelectedProjectileInstanceLabel = rootVisualElement.Q<Label>("InstanceIndexCounter");
            SelectedProjectileInstanceColorField = rootVisualElement.Q<ColorField>("SelectedProjectileColorField");
            SelectedProjectileInstanceColorField.RegisterValueChangedCallback((v) =>
            {
                if (SelectedProjectileInstance != null)
                {
                    SelectedProjectileInstance.trailColor = v.newValue;
                    SelectedProjectileInstance.trailColorField.value = v.newValue;
                }
            });
            SelectedProjectilePrefabField = rootVisualElement.Q<ObjectField>("SelectedInstancePrefabField");
            SelectedProjectilePrefabField.RegisterValueChangedCallback((v) =>
            {
                if (SelectedProjectileInstance == null) return;
                projectileGroupContainerDict[SelectedProjectileInstance.projectileInstanceID.ProjectilePrefabId].ChangePrefab((GameObject)v.newValue);
            });
            rootVisualElement.Q<Button>("ClearInstanceSelectionButton").clicked += () => SelectProjectileInstance(null);
            projectileGroupIntField = rootVisualElement.Q<IntegerField>("ProjectileGroupIndexField");
            projectileGroupIntField.RegisterValueChangedCallback((v) =>
            {
                if (projectileGroupContainerDict.ContainsKey(v.newValue))
                {
                    Debug.LogWarning("Projectile group with this index already exists!");
                    projectileGroupIntField.value = v.previousValue;
                }
            }
            );
            rootVisualElement.Q<Button>("AddProjectileGroupButton").clicked += () =>
            {
                AddNewProjectileGroupContainer(projectileGroupIntField.value);
                int newProjInd = FindFreeProjectileGroupIndex();
                projectileGroupIntField.value = newProjInd;
            };

            selectedProjectilePreview = rootVisualElement.Q<IMGUIContainer>("selectedProjectilePreview");

            selectedProjectilePreview.onGUIHandler += ProjectileAssetPreviewIMGUI;

            SceneView.duringSceneGui += DuringSceneGUI;

            CreateNewFrame(0, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>());
            SelectFrame(0);
        }

        void ProjectileAssetPreviewIMGUI()
        {

            if (selectedInstancePrefab != null)
            {

                selectedInstancePrefabEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(150, 150), null);
            }

        }

        public void SelectProjectileInstance(ProjectileInstanceGUI instance)
        {
            UpdateSelectedInstanceUI(instance);
            SelectedProjectileInstance = instance;
        }

        public void UpdateSelectedInstanceUI()
        {
            UpdateSelectedInstanceUI(SelectedProjectileInstance);
        }

        void UpdateSelectedInstanceUI(ProjectileInstanceGUI selected)
        {
            if (SelectedProjectileInstance != null)
            {
                foreach (var v in SelectedProjectileInstance.borderElements)
                {
                    v.style.borderTopColor = ProjectileInstanceGUI.NORMAL_BORDER_COLOR;
                    v.style.borderLeftColor = ProjectileInstanceGUI.NORMAL_BORDER_COLOR;
                    v.style.borderBottomColor = ProjectileInstanceGUI.NORMAL_BORDER_COLOR;
                    v.style.borderRightColor = ProjectileInstanceGUI.NORMAL_BORDER_COLOR;
                }
            }
            if (selected == null)
            {
                if (SelectedProjectileInstance == null) return;
                SelectedProjectileGroupLabel.text = "-";
                SelectedProjectileInstanceLabel.text = "-";
                SelectedProjectileInstanceColorField.value = Color.black;
                selectedInstancePrefab = null;
                selectedInstancePrefabEditor = null;
                SelectedProjectilePrefabField.value = null;
                return;
            }
            SelectedProjectileGroupLabel.text = selected.projectileInstanceID.ProjectilePrefabId.ToString();
            SelectedProjectileInstanceLabel.text = selected.projectileInstanceID.ProjectileInstanceID.ToString();
            SelectedProjectileInstanceColorField.value = selected.trailColor;
            SelectedProjectilePrefabField.value = selected.parent.prefab;
            selectedInstancePrefab = selected.parent.prefab;
            selectedInstancePrefabEditor = Editor.CreateEditor(selectedInstancePrefab);
            foreach (var v in selected.borderElements)
            {
                v.style.borderTopColor = ProjectileInstanceGUI.SELECTED_BORDER_COLOR;
                v.style.borderLeftColor = ProjectileInstanceGUI.SELECTED_BORDER_COLOR;
                v.style.borderBottomColor = ProjectileInstanceGUI.SELECTED_BORDER_COLOR;
                v.style.borderRightColor = ProjectileInstanceGUI.SELECTED_BORDER_COLOR;
            }

        }

        int FindFreeProjectileGroupIndex()
        {
            int newProjInd = 0;
            while (projectileGroupContainerDict.ContainsKey(newProjInd))
            {
                newProjInd++;
            }
            return newProjInd;
        }
        void ChangeCurrentFrame(int changeTo)
        {
            int oldFrame = currentFrame;
            currentFrame = changeTo;
            OnFrameChanged?.Invoke(oldFrame, changeTo);
        }

        void SelectFrame(int frameIndex)
        {
            var newSelectedFrameData = GeneratedFrameDatas.Find(x => x.Order == frameIndex);
            if (newSelectedFrameData != null) selectedFrameData = newSelectedFrameData;
            else Debug.LogWarning("You are trying to select a frame that doesn't exist");
        }


        void AddProjectileInfoToFrame(int frameCount, ProjectileKey key, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            if (frameCount >= GeneratedFrameDatas.Count)
            {
                Debug.LogWarning($"A frame you trying to add info to ({frameCount}) doesn't exist");
                return;
            }
            if (GeneratedFrameDatas[frameCount] == null) { Debug.LogWarning($"A frame you trying to add info to ({frameCount}) is null, you should initialize it first"); return; }
            if (GeneratedFrameDatas[frameCount].ProjectilePositionData.ContainsKey(key))
                GeneratedFrameDatas[frameCount].ProjectilePositionData[key] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            else
                GeneratedFrameDatas[frameCount].ProjectilePositionData.Add(key, new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl));
        }

        VisualElement AddNewProjectileGroupContainer(int containerIndex)
        {
            VisualElement newContainer = projectileContainerPrefab.CloneTree();
            ProjectileGroupUI projGroup = new ProjectileGroupUI(newContainer, containerIndex, newContainer.Q<VisualElement>("root"), this, projectileInstancePrefab);
            newContainer.Q<IntegerField>("projectileGroupIndexField").value = containerIndex;
            newContainer.Q<ToolbarButton>("DeleteButton").clicked += () => DeleteProjectielGroupContainer(containerIndex);
            newContainer.Q<Button>("AddProjectileButton").clicked += () => AddProjectileInstance(projGroup);
            projectileGroupContainerDict.Add(containerIndex, projGroup);
            projectileGroupScrollView.Add(newContainer);
            return newContainer;
        }

        void DeleteProjectielGroupContainer(int containerIndex)
        {
            projectileGroupScrollView.Remove(projectileGroupContainerDict[containerIndex].root);
            projectileGroupContainerDict.Remove(containerIndex);
            projectileGroupIntField.value = FindFreeProjectileGroupIndex();

        }
        void AddProjectileInstance(ProjectileGroupUI group)
        {
            var i = group.AddProjectileInstance();
            i.root.Q<ToolbarButton>("DeleteInstanceButton").clicked += () =>
            {
                DeleteProjectileInstance(group, i.projectileInstanceID.ProjectileInstanceID);
            };

            i.root.Q<Button>("SelectInstanceButton").clicked += () =>
            {
                SelectProjectileInstance(i);
            };

        }

        void DeleteProjectileInstance(ProjectileGroupUI group, int projectileInstanceIndex)
        {
            if (SelectedProjectileInstance == group.projectileInstances[projectileInstanceIndex]) SelectProjectileInstance(null);
            group.root.style.height = new StyleLength(new Length(group.root.resolvedStyle.height - ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            group.DeleteProjectileInstance(projectileInstanceIndex);
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
            if (modifyingGridInSceneView)
            {
                Vector3 gridZero = gridOrigin;
                Quaternion gridRotation = this.gridRotation;
                float gridUniformScale = gridCellSize;
                Handles.TransformHandle(ref gridZero, ref gridRotation, ref gridUniformScale);
                so.Update();
                gridRotationSerialized.quaternionValue = gridRotation;
                GridRotationField.value = gridRotation.eulerAngles;
                gridOriginSerialized.vector3Value = gridZero;
                GridOriginField.value = gridZero;
                gridCellSizeSerialized.floatValue = gridUniformScale;
                gridCellSizeField.value = gridUniformScale;
                so.ApplyModifiedProperties();
            }
            if (grid.GridRotation != gridRotationSerialized.quaternionValue ||
                grid.GridOrigin != gridOriginSerialized.vector3Value + GridYLevel * Vector3.forward ||
                grid.CellSize != gridCellSizeSerialized.floatValue || grid.GridSize != (Vector2Int)gridDimensions)
            {
                grid = new Grid((Vector2Int)gridDimensions, GetMatrixWithYOffset(gridCellSize, GridYLevel, this.gridOrigin, this.gridRotation));
                gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
            }
            switch (Event.current.type)
            {
                case EventType.Repaint:
                    GizmoHelper.DrawGridWithHandles(grid);
                    if (grid != null)
                    {
                        if (sceneView.camera.worldToCameraMatrix != sceneCameraMatrix)
                        {
                            gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                            sceneCameraMatrix = sceneView.camera.worldToCameraMatrix;
                        }
                    }
                    break;
                case EventType.MouseDown:
                    if (SceneView.mouseOverWindow == sceneView && Event.current.button == 0)
                    {
                        Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                        Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                        int gridCell = MathHelper.FindCellContainingPointIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                        Debug.Log(
                            $"mouse position: {mousePositionCorrected} gridCell: {gridCell}");
                    }
                    break;
            }

        }

        Dictionary<int, Vector3[]> FindWorldToScreenSpaceProjection(SceneView view, Dictionary<int, Vector3[]> worldPoints)
        {
            Dictionary<int, Vector3[]> result = new Dictionary<int, Vector3[]>();
            foreach (var v in worldPoints)
            {
                result.Add(v.Key, new Vector3[] { view.camera.WorldToViewportPoint(v.Value[0]), view.camera.WorldToViewportPoint(v.Value[1]),
                view.camera.WorldToViewportPoint(v.Value[2]), view.camera.WorldToViewportPoint(v.Value[3]) });
            }
            return result;
        }

        Vector3[] FindWorldToScreenSpaceProjection(SceneView view, params Vector3[] worldPoints)
        {
            Vector3[] result = new Vector3[worldPoints.Length];
            for (int i = 0; i < worldPoints.Length; i++)
            {
                result[i] = view.camera.WorldToViewportPoint(worldPoints[i]);
                //Debug.Log(result[i]);
            }
            return result;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            SceneView.RepaintAll();
        }
    }
}