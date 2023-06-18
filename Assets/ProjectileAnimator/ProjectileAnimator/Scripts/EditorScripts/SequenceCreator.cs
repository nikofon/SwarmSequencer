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
        int gridDepthLevel = 0;

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


        //Visual Elements
        Button deleteFrameButton;

        IntegerField currentFrameCounter;
        IntegerField frameCountCounter;
        IMGUIContainer selectedProjectilePreview;
        IntegerField projectileGroupIntField;
        SliderInt gridDepthSlider;
        SliderInt selectedFrameSlider;
        Vector3IntField gridSizeField;
        Vector3Field gridOriginField;
        Vector3Field gridRotationField;
        FloatField gridCellSizeField;
        Color editGridOffButtonColor = new Color(0.7372549f, 0.1529412f, 0.1882353f);
        Color editGridOnButtonColor = new Color(0.1529412f, 0.7372549f, 0.2303215f);

        ScrollView projectileGroupScrollView;

        VisualTreeAsset projectileContainerPrefab;
        VisualTreeAsset projectileInstancePrefab;

        Matrix4x4 sceneCameraMatrix;

        Dictionary<int, ProjectileGroupUI> projectileGroupContainerDict = new Dictionary<int, ProjectileGroupUI>();

        //Selected projectile info

        Label selectedProjectileGroupLabel;
        Label selectedProjectileInstanceLabel;
        ColorField selectedProjectileInstanceColorField;
        ObjectField selectedProjectilePrefabField;
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
            gridDepthSlider = rootVisualElement.Q<SliderInt>("GridDepthSlider");
            gridDepthSlider.RegisterValueChangedCallback((v) => { gridDepthLevel = v.newValue; SceneView.RepaintAll(); });
            gridDepthSlider.highValue = gridDimensions.z;

            gridSizeField = rootVisualElement.Q<Vector3IntField>("GridDimensions");
            gridSizeField.value = (Vector3Int)gridDimensions;
            gridSizeField.RegisterValueChangedCallback((v) =>
            {
                gridDimensions.x = Mathf.Max(2, v.newValue.x);
                gridDimensions.y = Mathf.Max(2, v.newValue.y);
                gridDimensions.z = Mathf.Max(0, v.newValue.z);
                gridDepthSlider.highValue = gridDimensions.z;
                gridSizeField.value = gridDimensions;
                SceneView.RepaintAll();
            });
            gridRotationField = rootVisualElement.Q<Vector3Field>("GridRotationField");
            gridRotationField.value = gridRotation.eulerAngles;
            gridRotationField.RegisterValueChangedCallback((v) =>
            {
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

            gridOriginField = rootVisualElement.Q<Vector3Field>("GridOriginField");
            gridOriginField.value = gridOrigin;
            gridOriginField.RegisterValueChangedCallback((v) =>
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
            #region Frame settings
            selectedFrameSlider = rootVisualElement.Q<SliderInt>("ChangeCurrentFrameSlider");
            selectedFrameSlider.RegisterValueChangedCallback((v) => { SelectFrame(v.newValue); SceneView.RepaintAll(); });
            currentFrameCounter = rootVisualElement.Q<IntegerField>("CurrentFrameCounter");
            currentFrameCounter.RegisterValueChangedCallback((v) =>
            {
                if (v.newValue - 1 < GeneratedFrameDatas.Count)
                {
                    SelectFrame(v.newValue - 1);
                }
                else
                {
                    Debug.LogWarning($"Frame {v.newValue - 1} doesn't exist!");
                    currentFrameCounter.SetValueWithoutNotify(v.previousValue);
                }
            });
            frameCountCounter = rootVisualElement.Q<IntegerField>("FrameCountCounter");
            frameCountCounter.RegisterValueChangedCallback((v) =>
            {
                Debug.Log($"changing value to: {v.newValue}");
                if (v.newValue - 1 <= 0)
                {
                    frameCountCounter.SetValueWithoutNotify(v.previousValue);
                    Debug.LogWarning("You can't have less then one frame");
                    return;
                }
                if (v.newValue - 1 >= GeneratedFrameDatas.Count)
                {
                    int currentCount = GeneratedFrameDatas.Count;
                    for (int i = 0; i < v.newValue - currentCount; i++)
                    {
                        Debug.Log($"commanding to add frame {i + currentCount}");
                        AddFrame(i + currentCount, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>());
                    }
                    UpdateFrameCountCounterUI();
                }
                if (v.newValue == GeneratedFrameDatas.Count) return;
                else
                {
                    int currentCount = GeneratedFrameDatas.Count;
                    for (int i = currentCount - 1; i > v.newValue - 1; i--)
                    {
                        DeleteFrame(i);
                    }
                    UpdateFrameCountCounterUI();
                }
            });
            rootVisualElement.Q<Button>("AddFrameButton").clicked += () => { AddFrame(GeneratedFrameDatas.Count, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>()); UpdateFrameCountCounterUI(); };
            rootVisualElement.Q<Button>("DeleteFrameButton").clicked += () => { DeleteFrame(selectedFrameData.Order); UpdateFrameCountCounterUI(); };
            #endregion
            projectileGroupScrollView = rootVisualElement.Q<ScrollView>("ProjectileGroupsScrollView");

            #region SelectedProjectileInfo
            selectedProjectileGroupLabel = rootVisualElement.Q<Label>("GroupIndexCounter");
            selectedProjectileInstanceLabel = rootVisualElement.Q<Label>("InstanceIndexCounter");
            selectedProjectileInstanceColorField = rootVisualElement.Q<ColorField>("SelectedProjectileColorField");
            selectedProjectileInstanceColorField.RegisterValueChangedCallback((v) =>
            {
                if (SelectedProjectileInstance != null)
                {
                    SelectedProjectileInstance.trailColor = v.newValue;
                    SelectedProjectileInstance.trailColorField.value = v.newValue;
                }
            });
            selectedProjectilePrefabField = rootVisualElement.Q<ObjectField>("SelectedInstancePrefabField");
            selectedProjectilePrefabField.RegisterValueChangedCallback((v) =>
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
            selectedProjectilePreview = rootVisualElement.Q<IMGUIContainer>("selectedProjectilePreview");
            #endregion

            rootVisualElement.Q<Button>("AddProjectileGroupButton").clicked += () =>
            {
                AddNewProjectileGroupContainer(projectileGroupIntField.value);
                int newProjInd = FindFreeProjectileGroupIndex();
                projectileGroupIntField.value = newProjInd;
            };

            projectileContainerPrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileGroup.uxml");
            projectileInstancePrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileInstance.uxml");

            selectedProjectilePreview.onGUIHandler += ProjectileAssetPreviewIMGUI;

            SceneView.duringSceneGui += DuringSceneGUI;

            AddFrame(0, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>());
            SelectFrame(0);
            UpdateFrameCountCounterUI();
            UpdateCurrentFrameCounterUI();
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
                selectedProjectileGroupLabel.text = "-";
                selectedProjectileInstanceLabel.text = "-";
                selectedProjectileInstanceColorField.value = Color.black;
                selectedInstancePrefab = null;
                selectedInstancePrefabEditor = null;
                selectedProjectilePrefabField.value = null;
                return;
            }
            selectedProjectileGroupLabel.text = selected.projectileInstanceID.ProjectilePrefabId.ToString();
            selectedProjectileInstanceLabel.text = selected.projectileInstanceID.ProjectileInstanceID.ToString();
            selectedProjectileInstanceColorField.value = selected.trailColor;
            selectedProjectilePrefabField.value = selected.parent.prefab;
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

        void SelectFrame(int frameIndex)
        {
            selectedFrameData = GeneratedFrameDatas[frameIndex];
            UpdateCurrentFrameCounterUI();
        }


        void AddProjectileInfoToFrame(FrameData frameData, ProjectileKey key, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            if (frameData == null) { Debug.LogWarning("A frame you trying to add info to is null, you should initialize it first"); return; }
            if (frameData.ProjectilePositionData.ContainsKey(key))
                frameData.ProjectilePositionData[key] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            else
                frameData.ProjectilePositionData.Add(key, new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl));
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

        void AddFrame(int frameCount, Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> projectilePositionData)
        {
            Debug.Log($"Adding frame {frameCount}");
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

        void UpdateFrameCountCounterUI()
        {
            selectedFrameSlider.highValue = GeneratedFrameDatas.Count - 1;
            frameCountCounter.SetValueWithoutNotify(GeneratedFrameDatas.Count);
        }

        void UpdateCurrentFrameCounterUI()
        {
            currentFrameCounter.SetValueWithoutNotify(selectedFrameData.Order + 1);
            selectedFrameSlider.SetValueWithoutNotify(selectedFrameData.Order);
        }

        void DeleteFrame(int frameCount)
        {
            GeneratedFrameDatas.RemoveAt(frameCount);
            if (frameCount == GeneratedFrameDatas.Count)
            {
                selectedFrameData = GeneratedFrameDatas[frameCount - 1];
            }
            else
            {
                for (int i = frameCount; i < GeneratedFrameDatas.Count; i++)
                {
                    GeneratedFrameDatas[i].Order = i;
                }
                selectedFrameData = GeneratedFrameDatas[frameCount];
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
                gridRotationField.value = gridRotation.eulerAngles;
                gridOriginSerialized.vector3Value = gridZero;
                gridOriginField.value = gridZero;
                gridCellSizeSerialized.floatValue = gridUniformScale;
                gridCellSizeField.value = gridUniformScale;
                so.ApplyModifiedProperties();
            }
            if (grid.GridRotation != gridRotationSerialized.quaternionValue ||
                grid.GridOrigin != gridOriginSerialized.vector3Value + gridDepthLevel * Vector3.forward ||
                grid.CellSize != gridCellSizeSerialized.floatValue || grid.GridDimensions != (Vector2Int)gridDimensions)
            {
                grid = new Grid((Vector2Int)gridDimensions, GetMatrixWithYOffset(gridCellSize, gridDepthLevel, this.gridOrigin, this.gridRotation));
                gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
            }
            switch (Event.current.type)
            {
                case EventType.Repaint:
                    if (grid != null)
                    {
                        GizmoHelper.DrawGridWithHandles(grid);
                        if (sceneView.camera.worldToCameraMatrix != sceneCameraMatrix)
                        {
                            gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                            sceneCameraMatrix = sceneView.camera.worldToCameraMatrix;
                        }
                        if (selectedFrameData != null)
                        {
                            if (selectedFrameData.ProjectilePositionData.Count != 0)
                            {
                                foreach (var prj in selectedFrameData.ProjectilePositionData)
                                {
                                    DrawProjectilePathOnGrid(prj.Key, selectedFrameData.Order);
                                }
                            }
                        }
                    }
                    break;
                case EventType.MouseDown:
                    if (SceneView.mouseOverWindow == sceneView && Event.current.button == 0)
                    {
                        if (SelectedProjectileInstance != null)
                        {
                            Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                            Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                            int gridCell = MathHelper.FindCellContainingPointIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                            Debug.Log($"grid cell {gridCell}");
                            if (gridCell != -1)
                            {
                                Vector2 relativePosition = grid.CellIndexToRelativePosition(gridCell);
                                Debug.Log($"Relative position: {relativePosition}");
                                AddProjectileInfoToFrame(selectedFrameData, SelectedProjectileInstance.projectileInstanceID,
                                new SerializableVector3(relativePosition.x, relativePosition.y, gridDepthLevel), new SerializableVector3(1, 1, 1));
                            }
                        }
                    }
                    break;
                case EventType.KeyDown:
                    if (Event.current.keyCode == KeyCode.D)
                    {
                        if (selectedFrameData.Order + 1 < GeneratedFrameDatas.Count)
                        {
                            SelectFrame(selectedFrameData.Order + 1);
                        }
                    }
                    else if (Event.current.keyCode == KeyCode.A)
                    {
                        if (selectedFrameData.Order - 1 >= 0)
                        {
                            SelectFrame(selectedFrameData.Order - 1);
                        }
                    }
                    break;
            }

        }

        void DrawProjectilePathOnGrid(ProjectileKey prj, int currentFrame, int lookForwardAmount = 1, int lookBackwardsAmount = 1)
        {
            Handles.color = projectileGroupContainerDict[prj.ProjectilePrefabId].projectileInstances[prj.ProjectileInstanceID].trailColor;
            Color secondaryColor = Handles.color;
            secondaryColor.a = 0.5f;
            Vector3 thisFramePos = gridOrigin + GeneratedFrameDatas[currentFrame].ProjectilePositionData[prj].Item1.ScaleToVector3(gridCellSize);
            Handles.DrawWireCube(thisFramePos, gridCellSize / 2.2f * Vector3.one);
            Handles.color = secondaryColor;
            for (int i = 1; i <= lookBackwardsAmount; i++)
            {
                if (currentFrame - i >= 0 && GeneratedFrameDatas[currentFrame - i].ProjectilePositionData.ContainsKey(prj))
                {
                    Vector3 prevFramePos = gridOrigin + GeneratedFrameDatas[currentFrame - i].ProjectilePositionData[prj].Item1.ScaleToVector3(gridCellSize);
                    Handles.DrawWireCube(prevFramePos, gridCellSize / 3 * Vector3.one);
                    Handles.DrawAAPolyLine(5f, prevFramePos, thisFramePos);
                    Handles.BeginGUI();
                    Handles.Label(prevFramePos, (currentFrame - i).ToString());
                    Handles.EndGUI();
                    //Handles.ArrowHandleCap(0, prevFramePos, Quaternion.FromToRotation(Vector3.forward, thisFramePos - prevFramePos), Vector3.Distance(thisFramePos, prevFramePos) - gridCellSize / 4.2f, EventType.Repaint);
                }
            }
            for (int i = 1; i <= lookForwardAmount; i++)
            {
                if (currentFrame + i < GeneratedFrameDatas.Count && GeneratedFrameDatas[currentFrame + i].ProjectilePositionData.ContainsKey(prj))
                {
                    Vector3 nextFramePos = gridOrigin + GeneratedFrameDatas[currentFrame + i].ProjectilePositionData[prj].Item1.ScaleToVector3(gridCellSize);
                    Handles.DrawWireCube(nextFramePos, gridCellSize / 3 * Vector3.one);
                    Handles.DrawAAPolyLine(5f, nextFramePos, thisFramePos);
                    Handles.BeginGUI();
                    Handles.Label(nextFramePos, (currentFrame + i).ToString());
                    Handles.EndGUI();
                }
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