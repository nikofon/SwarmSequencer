using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Linq;
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

        int frameCount = 0;
        int selectedFrame;

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
        Vector3Field selectedInstPosInPrevFrame;
        Vector3Field selectedInstPosInCurrentFrame;
        Vector3Field selectedInstPosInNextFrame;


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
            gridRotationField.MakeDelayed();
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
                if (v.newValue - 1 < frameCount)
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
                if (v.newValue - 1 <= 0)
                {
                    frameCountCounter.SetValueWithoutNotify(v.previousValue);
                    Debug.LogWarning("You can't have less then one frame");
                    return;
                }
                if (v.newValue - 1 >= frameCount)
                {
                    int currentCount = frameCount;
                    for (int i = 0; i < v.newValue - currentCount; i++)
                    {
                        AddFrame(i + currentCount, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>());
                    }
                    UpdateFrameCountCounterUI();
                }
                if (v.newValue == frameCount) return;
                else
                {
                    int currentCount = frameCount;
                    for (int i = currentCount - 1; i > v.newValue - 1; i--)
                    {
                        DeleteFrame(i);
                    }
                    UpdateFrameCountCounterUI();
                }
            });
            rootVisualElement.Q<Button>("AddFrameButton").clicked += () => { AddFrame(selectedFrame + 1, new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>()); UpdateFrameCountCounterUI(); };
            rootVisualElement.Q<Button>("DeleteFrameButton").clicked += () => { DeleteFrame(selectedFrame); UpdateFrameCountCounterUI(); };
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
            selectedInstPosInCurrentFrame = rootVisualElement.Q<Vector3Field>("selectedInstPosInCurrentFrame");
            // selectedInstPosInCurrentFrame.RegisterValueChangedCallback((v) =>
            // {
            //     SelectedProjectileInstance.SetPositionInFrame(selectedFrame, v.newValue, new SerializableVector3(1, 1, 1), grid);
            // });
            selectedInstPosInNextFrame = rootVisualElement.Q<Vector3Field>("selectedInstPosInNextFrame");
            selectedInstPosInPrevFrame = rootVisualElement.Q<Vector3Field>("selectedInstPosInPrevFrame");
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
            if (selected.FramePositionAndBezier.ContainsKey(selectedFrame))
                selectedInstPosInCurrentFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[selectedFrame].Item1);
            else
                selectedInstPosInCurrentFrame.SetValueWithoutNotify(new Vector3(float.NaN, float.NaN, float.NaN));
            if (selected.FramePositionAndBezier.ContainsKey(selectedFrame - 1))
                selectedInstPosInPrevFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[selectedFrame - 1].Item1);
            else
                selectedInstPosInPrevFrame.SetValueWithoutNotify(new Vector3(float.NaN, float.NaN, float.NaN));
            if (selected.FramePositionAndBezier.ContainsKey(selectedFrame + 1))
                selectedInstPosInNextFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[selectedFrame + 1].Item1);
            else
                selectedInstPosInNextFrame.SetValueWithoutNotify(new Vector3(float.NaN, float.NaN, float.NaN));
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
            Debug.Log($"selecting frame {frameIndex}");
            if (frameIndex >= frameCount) return;
            selectedFrame = frameIndex;
            UpdateCurrentFrameCounterUI();
            if (SelectedProjectileInstance != null)
                UpdateSelectedInstanceUI();
        }


        void AddProjectileInfoToFrame(int frameIndex, ProjectileInstanceGUI key, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            key.SetPositionInFrame(frameIndex, position, bezierControl, grid);
            UpdateSelectedInstanceUI();
        }

        void AddProjectileInfoToFrame(int frameIndex, ProjectileInstanceGUI key, int cellPosition, int depth, SerializableVector3 bezierControl)
        {
            key.SetPositionInFrameByCell(frameIndex, cellPosition, depth, bezierControl, grid);
            UpdateSelectedInstanceUI();
        }

        void RemoveProjectileInfoFromFrame(int frameIndex, ProjectileInstanceGUI key)
        {
            key.ClearFrame(frameIndex);
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

        void AddFrame(int frameIndex, Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> projectilePositionData)
        {
            Debug.Log($"Adding frame {frameIndex} framecount: {frameCount}");
            if (frameIndex != frameCount)
            {
                ShiftFramesInProjectileInstances(frameIndex, 1);
            }
            frameCount++;
            SelectFrame(frameIndex);
        }

        void UpdateFrameCountCounterUI()
        {
            selectedFrameSlider.highValue = frameCount - 1;
            frameCountCounter.SetValueWithoutNotify(frameCount);
        }

        void UpdateCurrentFrameCounterUI()
        {
            currentFrameCounter.SetValueWithoutNotify(selectedFrame + 1);
            selectedFrameSlider.SetValueWithoutNotify(selectedFrame);
        }

        void DeleteFrame(int frameIndex)
        {
            foreach (var prjGrp in projectileGroupContainerDict)
            {
                foreach (var prj in prjGrp.Value.projectileInstances)
                {
                    if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                    {
                        prj.Value.ClearFrame(frameIndex);
                    }
                }
            }
            if (frameIndex != frameCount - 1)
            {
                ShiftFramesInProjectileInstances(frameIndex, -1);
                SelectFrame(frameIndex - 1);
            }
            else
                SelectFrame(frameIndex);
            frameCount--;
        }

        /// <summary>
        /// Shifts all frames from starting to frame + offset. For example for startingFrame = 1 and offset = 1: frame 1 becomes frame 2, frame 2 becomes frame 3 and so on. Used to handle adding and deleting frames.
        /// </summary>
        /// <param name="startingFrame"></param>
        /// <param name="offset"></param>
        void ShiftFramesInProjectileInstances(int startingFrame, int offset)
        {
            foreach (var prjGrp in projectileGroupContainerDict)
            {
                foreach (var prj in prjGrp.Value.projectileInstances)
                {
                    var keys = prj.Value.FramePositionAndBezier.Keys.ToList();
                    keys.Sort();
                    int index = keys.FindIndex(x => x >= startingFrame);
                    if (index == -1) continue;
                    Dictionary<int, Tuple<SerializableVector3, SerializableVector3>> tempStorage = new Dictionary<int, Tuple<SerializableVector3, SerializableVector3>>();
                    for (int i = index; i < keys.Count; i++)
                    {
                        if (keys.Contains(keys[i] + offset))
                        {
                            tempStorage.Add(keys[i] + offset, prj.Value.FramePositionAndBezier[keys[i] + offset]);
                        }
                        if (tempStorage.ContainsKey(keys[i]))
                        {
                            prj.Value.SetPositionInFrame(keys[i] + offset, tempStorage[keys[i]].Item1, tempStorage[keys[i]].Item2, grid);
                            tempStorage.Remove(keys[i]);
                        }
                        else
                            prj.Value.SetPositionInFrame(keys[i] + offset, prj.Value.FramePositionAndBezier[keys[i]].Item1, prj.Value.FramePositionAndBezier[keys[i]].Item2, grid);
                    }
                    prj.Value.ClearFrame(startingFrame);
                }
            }
        }


        Matrix4x4 CalculateTRS(float cellSize, Vector3 zero, Quaternion gridRotation)
        {
            var TRSMatrix = Matrix4x4.TRS(zero, gridRotation, cellSize * Vector3.one);
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
                gridRotationField.value = gridRotation.eulerAngles;
                gridOriginField.value = gridZero;
                gridCellSizeField.value = gridUniformScale;
            }
            if (grid.GridRotation != gridRotationSerialized.quaternionValue ||
                grid.GridOrigin != gridOriginSerialized.vector3Value ||
                grid.CellSize != gridCellSizeSerialized.floatValue || grid.GridDimensions != (Vector2Int)gridDimensions)
            {
                grid = new Grid((Vector2Int)gridDimensions, CalculateTRS(gridCellSize, this.gridOrigin, this.gridRotation));
                gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
            }
            switch (Event.current.type)
            {
                case EventType.Repaint:
                    if (grid != null)
                    {
                        if (gridDepthLevel == 0)
                        {
                            GizmoHelper.DrawGridWithHandles(grid);
                        }
                        else
                        {
                            Grid adjustedGrid = new Grid(grid.GridDimensions, Matrix4x4.Translate(Vector3.forward * gridDepthLevel * gridCellSize) * grid.TRS);
                            GizmoHelper.DrawGridWithHandles(adjustedGrid);
                        }
                        if (sceneView.camera.worldToCameraMatrix != sceneCameraMatrix)
                        {
                            gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                            sceneCameraMatrix = sceneView.camera.worldToCameraMatrix;
                        }
                        DrawFramesOnGrid(selectedFrame, 0.8f, 0.5f);
                    }
                    break;
                case EventType.MouseDown:
                    if (SceneView.mouseOverWindow == sceneView)
                    {
                        if (Event.current.button == 0)
                        {
                            if (SelectedProjectileInstance != null)
                            {
                                Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                                Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                                int gridCell = MathHelper.FindCellContainingPointIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                                if (gridCell != -1)
                                {
                                    AddProjectileInfoToFrame(selectedFrame, SelectedProjectileInstance,
                                    gridCell, gridDepthLevel, new SerializableVector3(1, 1, 1));
                                }
                            }
                        }
                        else if (Event.current.button == 1)
                        {
                            Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                            Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                            int gridCell = MathHelper.FindCellContainingPointIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                            if (gridCell != -1)
                            {
                                ProjectileInstanceGUI keyToRemove = null;
                                foreach (var v in projectileGroupContainerDict)
                                {
                                    foreach (var vv in v.Value.projectileInstances)
                                    {
                                        if (vv.Value.GetCellIndex(selectedFrame) == gridCell)
                                        {
                                            keyToRemove = vv.Value; break;
                                        }
                                    }
                                }
                                if (keyToRemove != null)
                                {
                                    RemoveProjectileInfoFromFrame(selectedFrame, keyToRemove);
                                }
                            }
                        }
                    }
                    break;
                case EventType.KeyDown:
                    if (Event.current.keyCode == KeyCode.D)
                    {
                        SelectFrame(selectedFrame + 1);
                    }
                    else if (Event.current.keyCode == KeyCode.A)
                    {
                        if (selectedFrame - 1 >= 0)
                        {
                            SelectFrame(selectedFrame - 1);
                        }
                    }
                    break;
            }

        }

        void DrawFramesOnGrid(int frameToDraw, float meshSize, float meshSizeDecay, int lookForwardAmount = 1, int lookBackwardsAmount = 1)
        {
            DrawFramesRecursively(frameToDraw, null, lookForwardAmount, false, 1, meshSize, meshSizeDecay);
            DrawFramesRecursively(frameToDraw, null, lookBackwardsAmount, true, -1, meshSize, meshSizeDecay);
        }

        void DrawFramesRecursively(int frameToDraw, Dictionary<ProjectileKey, Vector3> connexionPositions, int propogationAmount, bool skipFirstDraw, int direction, float meshSize, float meshSizeDecay)
        {
            Dictionary<ProjectileKey, Vector3> projPositions = new Dictionary<ProjectileKey, Vector3>();
            foreach (var prjGrp in projectileGroupContainerDict)
            {
                foreach (var prj in prjGrp.Value.projectileInstances)
                {
                    if (!prj.Value.visible || !prj.Value.FramePositionAndBezier.ContainsKey(frameToDraw)) continue;
                    Vector3 thisFramePos = grid.TRS.MultiplyPoint3x4((Vector3)prj.Value.FramePositionAndBezier[frameToDraw].Item1);
                    if (!skipFirstDraw)
                    {
                        Handles.color = prj.Value.trailColor;
                        Handles.DrawWireCube(thisFramePos, gridCellSize * meshSizeDecay * meshSize * Vector3.one);
                        Handles.BeginGUI();
                        Handles.Label(thisFramePos, (frameToDraw + 1).ToString());
                        Handles.EndGUI();
                        if (connexionPositions != null && connexionPositions.ContainsKey(prj.Value.projectileInstanceID))
                            Handles.DrawAAPolyLine(5f, thisFramePos, connexionPositions[prj.Value.projectileInstanceID]);
                    }
                    projPositions.Add(prj.Value.projectileInstanceID, thisFramePos);
                }
            }
            if (propogationAmount != 0)
            {
                if (frameToDraw + direction >= 0 && frameToDraw + direction < frameCount)
                    DrawFramesRecursively(frameToDraw + direction, projPositions, propogationAmount - 1, false, direction, meshSizeDecay * meshSize, meshSizeDecay);
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