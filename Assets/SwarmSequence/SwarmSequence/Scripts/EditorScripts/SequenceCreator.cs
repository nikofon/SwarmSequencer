using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using SwarmSequencer.MathTools;
using SwarmSequencer.Serialization;
using System.IO;

namespace SwarmSequencer
{
    namespace EditorTools
    {
        public class SequenceCreator : EditorWindow
        {
            //Grid params
            [SerializeField]
            internal Vector3 gridOrigin = Vector3.zero;
            [SerializeField]
            internal Quaternion gridRotation = Quaternion.identity;
            [SerializeField]
            internal float gridCellSize = 1f;
            [SerializeField]
            internal Vector3Int gridDimensions = new Vector3Int(2, 2, 2);
            [SerializeField]
            int gridDepthLevel = 0;

            Dictionary<int, Vector3[]> gridPointsScreenPosition;

            bool modifyingGridInSceneView;

            internal SwarmSequencer.MathTools.Grid grid;

            bool showGrid = true;


            //Projectile Info

            public int FrameCount { get => frameCount; }

            int frameCount = 0;
            public int SelectedFrame { get => m_selectedFrame; set { SelectFrame(value); } }
            int m_selectedFrame;

            public ProjectileInstanceContainer SelectedProjectileInstance { get; private set; }


            //Visual Elements
            Button deleteFrameButton;

            IntegerField currentFrameCounter;
            IntegerField frameCountCounter;
            IMGUIContainer selectedProjectilePreview;
            IntegerField projectileGroupIntField;
            SliderInt gridDepthSlider;
            SliderInt selectedFrameSlider;
            Vector3IntField gridDimensionsField;
            Vector3Field gridOriginField;
            Vector3Field gridRotationField;
            FloatField gridCellSizeField;
            public readonly static Color indicatingRed = new Color(0.7372549f, 0.1529412f, 0.1882353f);
            public readonly static Color indicatingGreen = new Color(0.1529412f, 0.7372549f, 0.2303215f);
            public readonly static Color neutralGray = new Color(0.345098f, 0.345098f, 0.345098f);

            ScrollView projectileGroupScrollView;

            VisualTreeAsset projectileContainerAsset;
            VisualTreeAsset projectileInstanceAsset;
            VisualTreeAsset importSequenceWindowAsset;
            VisualTreeAsset importGridSettingsWindowAsset;
            VisualTreeAsset exportAdditionalSettingsWindowAsset;

            Matrix4x4 sceneCameraMatrix;

            public Dictionary<int, ProjectileGroupUI> projectileGroupContainerDict = new Dictionary<int, ProjectileGroupUI>();

            public SelectedProjectileInstanceUI SelectedProjectileInstanceUI { get; private set; }

            //Edit mode

            public ModificationMode CurrentMode { get; private set; }

            ButtonGroup selectModeButtonGroup;

            public enum ModificationMode
            {
                Position,
                Bezier,
                Eraser,
                Freehand
            }

            const float editorPrecision = 1e-2f;

            [MenuItem("/Window/SwarmSequence/SequenceCreator")]
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
                VisualTreeAsset origin = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/SequenceCreatorUXML.uxml");
                TemplateContainer container = origin.CloneTree();
                container.style.flexGrow = 1;
                rootVisualElement.Add(container);

                #region GridSettings
                //ValueChange bindings
                gridDepthSlider = rootVisualElement.Q<SliderInt>("GridDepthSlider");
                gridDepthSlider.RegisterValueChangedCallback((v) => { gridDepthLevel = v.newValue; SceneView.RepaintAll(); });
                gridDepthSlider.highValue = gridDimensions.z - 1;

                gridDimensionsField = rootVisualElement.Q<Vector3IntField>("GridDimensions");
                gridDimensionsField.value = (Vector3Int)gridDimensions;
                gridDimensionsField.RegisterValueChangedCallback((v) =>
                {
                    gridDimensions.x = Mathf.Max(2, v.newValue.x);
                    gridDimensions.y = Mathf.Max(2, v.newValue.y);
                    gridDimensions.z = Mathf.Max(0, v.newValue.z);
                    gridDepthSlider.highValue = gridDimensions.z - 1;
                    gridDimensionsField.value = gridDimensions;
                    SceneView.RepaintAll();
                });
                gridDimensionsField.MakeDelayed();
                gridRotationField = rootVisualElement.Q<Vector3Field>("GridRotationField");
                gridRotationField.value = gridRotation.eulerAngles;
                gridRotationField.RegisterValueChangedCallback((v) =>
                {
                    Vector3 newVal = MathHelper.ConfineVector3ToPrecision(v.newValue, editorPrecision);
                    gridRotationField.SetValueWithoutNotify(newVal);
                    Undo.RecordObject(this, "Sequence creator Grid alteration");
                    gridRotation = Quaternion.Euler(newVal);
                    SceneView.RepaintAll();
                });

                gridCellSizeField = rootVisualElement.Q<FloatField>("CellSizeField");
                gridCellSizeField.value = gridCellSize;
                gridCellSizeField.RegisterValueChangedCallback((v) =>
                {
                    Undo.RecordObject(this, "Sequence creator Grid alteration");
                    gridCellSize = MathF.Max(0.0001f, v.newValue);
                    gridCellSizeField.SetValueWithoutNotify(gridCellSize);
                    SceneView.RepaintAll();
                });

                gridOriginField = rootVisualElement.Q<Vector3Field>("GridOriginField");
                gridOriginField.value = gridOrigin;
                gridOriginField.RegisterValueChangedCallback((v) =>
                {
                    Undo.RecordObject(this, "Sequence creator Grid alteration");
                    gridOrigin = v.newValue;
                    SceneView.RepaintAll();
                });
                grid = new SwarmSequencer.MathTools.Grid((Vector2Int)gridDimensions, gridCellSize, gridOrigin, gridRotation);

                var liveEditButton = rootVisualElement.Q<ToolbarButton>("EnableGridEditingButton");
                liveEditButton.clicked += () => { modifyingGridInSceneView = !modifyingGridInSceneView; liveEditButton.style.backgroundColor = modifyingGridInSceneView ? indicatingGreen : indicatingRed; SceneView.RepaintAll(); };

                var gridVisibilityButton = rootVisualElement.Q<ToolbarButton>("showGridButton");
                gridVisibilityButton.clicked += () => { showGrid = !showGrid; gridVisibilityButton.style.backgroundColor = showGrid ? indicatingGreen : indicatingRed; SceneView.RepaintAll(); };

                #endregion

                #region Frame settings
                projectileGroupIntField = rootVisualElement.Q<IntegerField>("ProjectileGroupIndexField");
                selectedFrameSlider = rootVisualElement.Q<SliderInt>("ChangeCurrentFrameSlider");
                selectedFrameSlider.highValue = frameCount;
                selectedFrameSlider.RegisterValueChangedCallback((v) => { SelectedFrame = v.newValue; SceneView.RepaintAll(); });
                currentFrameCounter = rootVisualElement.Q<IntegerField>("CurrentFrameCounter");
                currentFrameCounter.RegisterValueChangedCallback((v) =>
                {
                    if (v.newValue - 1 < frameCount)
                    {
                        SelectedFrame = v.newValue - 1;
                        SceneView.RepaintAll();
                    }
                    else
                    {
                        Debug.LogWarning($"Frame {v.newValue - 1} doesn't exist!");
                        currentFrameCounter.SetValueWithoutNotify(v.previousValue);
                    }
                });
                frameCountCounter = rootVisualElement.Q<IntegerField>("FrameCountCounter");
                frameCountCounter.value = frameCount;
                frameCountCounter.RegisterValueChangedCallback((v) =>
                {
                    if (v.newValue <= 0)
                    {
                        frameCountCounter.SetValueWithoutNotify(v.previousValue);
                        Debug.LogWarning("You can't have less then one frame");
                        return;
                    }
                    if (v.newValue > frameCount)
                    {
                        AddMultipleFrames(v.newValue - frameCount);
                        UpdateFrameCountCounterUI();
                    }
                    else if (v.newValue == frameCount) return;
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
                rootVisualElement.Q<Button>("AddFrameButton").clicked += () => { AddFrame(SelectedFrame + 1); };
                rootVisualElement.Q<Button>("DeleteFrameButton").clicked += () => { DeleteFrame(SelectedFrame); };
                #endregion
                projectileGroupScrollView = rootVisualElement.Q<ScrollView>("ProjectileGroupsScrollView");
                SelectedProjectileInstanceUI = new SelectedProjectileInstanceUI(rootVisualElement, this);

                projectileGroupIntField = rootVisualElement.Q<IntegerField>("ProjectileGroupIndexField");
                projectileGroupIntField.RegisterValueChangedCallback((v) =>
                {
                    if (projectileGroupContainerDict.ContainsKey(v.newValue))
                    {
                        Debug.LogWarning("Projectile group with this index already exists!");
                        projectileGroupIntField.SetValueWithoutNotify(v.previousValue);
                    }
                }
                );

                selectModeButtonGroup = new ButtonGroup(indicatingGreen, neutralGray, 0,
                    rootVisualElement.Q<Button>("selectEditModeButton"), rootVisualElement.Q<Button>("selectEditBezierModeButton"), rootVisualElement.Q<Button>("selectEraserModeButton"), rootVisualElement.Q<Button>("selectPositionModeButton"));
                selectModeButtonGroup.OnValueChange += (oldValue, newValue) => ChangeModificationModeWithoutNotify((ModificationMode)newValue);

                rootVisualElement.Q<Button>("AddProjectileGroupButton").clicked += () =>
                {
                    AddNewProjectileGroupContainer(projectileGroupIntField.value);
                    int newProjInd = FindFreeProjectileGroupIndex();
                    projectileGroupIntField.value = newProjInd;
                };

                rootVisualElement.Q<Button>("save").clicked += () => SerializeResult();
                rootVisualElement.Q<Button>("import").clicked += () => CreateImportSequenceInterface();
                rootVisualElement.Q<Button>("loadGridSettings").clicked += () => CreateImportGridSettingsInterface();
                rootVisualElement.Q<Button>("saveGridSettings").clicked += () => CreateExportAdditionalSettingsWindow();

                projectileContainerAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/ProjectileGroup.uxml");
                projectileInstanceAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/ProjectileInstance.uxml");
                importSequenceWindowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/ImportSequence.uxml");
                importGridSettingsWindowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/ImportSequenceSecondaryData.uxml");
                exportAdditionalSettingsWindowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/SwarmSequence/SwarmSequence/UIDocuments/UXML/ExportSequenceSecondaryData.uxml");


                SceneView.duringSceneGui += DuringSceneGUI;

                if (frameCount == 0)
                {
                    AddFrame(0);
                }
                SelectFrame(0);
                SequenceCreatorShortcutManager.activeSequenceCreator = this;
            }


            internal void SelectProjectileInstance(ProjectileInstanceContainer instance)
            {
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI(instance);
                SelectedProjectileInstance = instance;
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

            public void SelectFrame(int frameIndex)
            {
                if (frameIndex >= frameCount || frameIndex < 0) return;
                m_selectedFrame = frameIndex;
                UpdateCurrentFrameCounterUI();
                if (SelectedProjectileInstance != null)
                    SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                foreach (var g in projectileGroupContainerDict)
                {
                    foreach (var i in g.Value.projectileInstances)
                    {
                        i.Value.UpdatePositionUI();
                    }
                }
            }

            void AddInstanceInfoToFrame(int frameIndex, ProjectileInstanceContainer key, SerializableVector3 position, SerializableVector3 bezierControl)
            {
                key.SetPositionInFrame(frameIndex, position, bezierControl, grid);
                if (key.FramePositionAndBezier.ContainsKey(frameIndex - 1))
                {
                    key.ResetBezierPos(frameIndex - 1);
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            ProjectileGroupUI AddNewProjectileGroupContainer(int containerIndex)
            {
                VisualElement newContainer = projectileContainerAsset.CloneTree();
                ProjectileGroupUI projGroup = new ProjectileGroupUI(newContainer, containerIndex, newContainer.Q<VisualElement>("root"), this, projectileInstanceAsset);
                newContainer.Q<IntegerField>("projectileGroupIndexField").value = containerIndex;
                newContainer.Q<ToolbarButton>("DeleteButton").clicked += () => DeleteProjectielGroupContainer(containerIndex);
                newContainer.Q<Button>("AddProjectileButton").clicked += () => AddProjectileInstance(projGroup);
                projectileGroupContainerDict.Add(containerIndex, projGroup);
                projectileGroupScrollView.Add(newContainer);
                return projGroup;
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
                group.root.style.height = new StyleLength(new Length(group.root.resolvedStyle.height - ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                group.DeleteProjectileInstance(projectileInstanceIndex);
            }

            void AddMultipleFrames(int amountToAdd)
            {
                int currentCount = frameCount;
                for (int i = 0; i < amountToAdd; i++)
                {
                    AddFrame(i + currentCount);
                }
            }

            public void AddFrame(int frameIndex, bool selectNewFrame = true)
            {
                if (frameIndex != frameCount)
                {
                    ShiftFramesInProjectileInstances(frameIndex, 1);
                    ClearFrame(frameIndex);
                }
                frameCount++;
                UpdateFrameCountCounterUI();
                if (selectNewFrame) SelectFrame(frameIndex);
                SceneView.RepaintAll();
            }

            void UpdateFrameCountCounterUI()
            {
                selectedFrameSlider.highValue = frameCount - 1;
                frameCountCounter.SetValueWithoutNotify(frameCount);
            }

            void UpdateCurrentFrameCounterUI()
            {
                currentFrameCounter.SetValueWithoutNotify(SelectedFrame + 1);
                selectedFrameSlider.SetValueWithoutNotify(SelectedFrame);
            }

            void DeleteFrame(int frameIndex)
            {
                ClearFrame(frameIndex);
                if (frameIndex != frameCount - 1)
                {
                    ShiftFramesInProjectileInstances(frameIndex + 1, -1);
                }
                frameCount--;
                SelectFrame(frameIndex - 1);
                UpdateFrameCountCounterUI();
                SceneView.RepaintAll();
            }

            void ClearFrame(int frameIndex)
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
                    }
                }
            }

            void ModifyBezierControls(int frameIndex)
            {
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                        {
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex + 1) && !MathHelper.IsNaNVector3(prj.Value.FramePositionAndBezier[frameIndex].Item2))
                            {
                                SerializableVector3 newBezier = grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex].Item2), gridRotation));
                                if (newBezier != prj.Value.FramePositionAndBezier[frameIndex].Item2)
                                {
                                    prj.Value.SetPositionInFrame(frameIndex, prj.Value.FramePositionAndBezier[frameIndex].Item1, newBezier, grid);
                                }
                            }
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex - 1) && !MathHelper.IsNaNVector3(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2))
                            {
                                SerializableVector3 newBezier = grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2), gridRotation));
                                if (newBezier != prj.Value.FramePositionAndBezier[frameIndex - 1].Item2)
                                {
                                    prj.Value.SetPositionInFrame(frameIndex - 1, prj.Value.FramePositionAndBezier[frameIndex - 1].Item1, newBezier, grid);
                                }
                            }
                        }
                    }
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            void ModifyPosition(int frameIndex)
            {
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                        {
                            SerializableVector3 newPosition = grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex].Item1), gridRotation));
                            prj.Value.SetPositionInFrame(frameIndex, newPosition, prj.Value.FramePositionAndBezier[frameIndex].Item2, grid);

                        }
                    }
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            Matrix4x4 CalculateTRS(float cellSize, Vector3 zero, Quaternion gridRotation)
            {
                var TRSMatrix = Matrix4x4.TRS(zero, gridRotation, cellSize * Vector3.one);
                return TRSMatrix;
            }
            void DuringSceneGUI(SceneView sceneView)
            {
                if (gridPointsScreenPosition == null)
                    gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                if (modifyingGridInSceneView)
                {
                    Vector3 gridZero = gridOrigin;
                    Quaternion gridRotation = this.gridRotation;
                    float gridUniformScale = gridCellSize;
                    EditorGUI.BeginChangeCheck();
                    Handles.TransformHandle(ref gridZero, ref gridRotation, ref gridUniformScale);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this, "Sequence creator Grid alteration");
                        gridRotationField.SetValueWithoutNotify(gridRotation.eulerAngles);
                        gridOriginField.SetValueWithoutNotify(gridZero);
                        gridCellSizeField.SetValueWithoutNotify(gridUniformScale);
                        gridCellSize = gridUniformScale;
                        this.gridRotation = gridRotation;
                        this.gridOrigin = gridZero;
                    }
                }
                if (grid.GridRotation != this.gridRotation ||
                    grid.GridOrigin != this.gridOrigin ||
                    grid.CellSize != gridCellSize || grid.GridDimensions != (Vector2Int)gridDimensions)
                {
                    grid = new SwarmSequencer.MathTools.Grid((Vector2Int)gridDimensions, CalculateTRS(gridCellSize, this.gridOrigin, this.gridRotation));
                    gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                }
                if (CurrentMode == ModificationMode.Bezier)
                {
                    ModifyBezierControls(SelectedFrame);
                }
                else if (CurrentMode == ModificationMode.Freehand)
                {
                    ModifyPosition(SelectedFrame);
                }
                switch (Event.current.type)
                {
                    case EventType.Repaint:
                        if (grid != null)
                        {
                            DrawFramesOnGrid(SelectedFrame, 0.8f, 0.5f);
                            Handles.color = Color.white;
                            if (!showGrid) break;
                            if (gridDepthLevel == 0)
                            {
                                GizmoHelper.DrawGridWithHandles(grid);
                            }
                            else
                            {
                                SwarmSequencer.MathTools.Grid adjustedGrid = new SwarmSequencer.MathTools.Grid(grid.GridDimensions, Matrix4x4.Translate(Vector3.forward * gridDepthLevel * gridCellSize) * grid.TRS);
                                GizmoHelper.DrawGridWithHandles(adjustedGrid);
                            }
                            if (sceneView.camera.worldToCameraMatrix != sceneCameraMatrix)
                            {
                                gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                                sceneCameraMatrix = sceneView.camera.worldToCameraMatrix;
                            }
                        }
                        break;
                    case EventType.MouseDown:
                        if (!showGrid) break;
                        if (SceneView.mouseOverWindow == sceneView)
                        {
                            if (Event.current.button == 0)
                            {
                                if (SelectedProjectileInstance != null && CurrentMode == ModificationMode.Position)
                                {
                                    Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                                    Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                                    int gridCell = MathHelper.FindCellContainingPointIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                                    if (gridCell != -1)
                                    {
                                        var projRelativePosition = grid.CellIndexToRelativePosition(gridCell).ToVector3(gridDepthLevel);
                                        SerializableVector3 bezier = SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(SelectedFrame + 1) ? (SelectedProjectileInstance.FramePositionAndBezier[SelectedFrame + 1].Item1 + (SerializableVector3)projRelativePosition) / 2 :
                                            MathHelper.NaNVector3;
                                        AddInstanceInfoToFrame(SelectedFrame, SelectedProjectileInstance,
                                        projRelativePosition, bezier);
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
                                    if (CurrentMode == ModificationMode.Eraser)
                                    {
                                        foreach (var v in projectileGroupContainerDict)
                                        {
                                            foreach (var vv in v.Value.projectileInstances)
                                            {
                                                if (vv.Value.GetCellIndex(SelectedFrame) == gridCell)
                                                {
                                                    vv.Value.ClearFrame(SelectedFrame);
                                                    SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                                                }
                                            }
                                        }
                                    }
                                    else if (CurrentMode == ModificationMode.Position && SelectedProjectileInstance != null)
                                    {
                                        if (SelectedProjectileInstance.GetCellIndex(SelectedFrame) == gridCell)
                                        {
                                            SelectedProjectileInstance.ClearFrame(SelectedFrame);
                                            SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                                        }
                                    }
                                }
                            }
                        }
                        break;
                }

            }

            void ChangeModificationModeWithoutNotify(ModificationMode mode)
            {
                CurrentMode = mode;
                SelectedProjectileInstanceUI.ChangeDisplayMode(mode);
            }

            internal void SelectModificationMode(ModificationMode mode)
            {
                selectModeButtonGroup.Value = (int)mode;
            }

            void DrawFramesOnGrid(int frameToDraw, float meshSize, float meshSizeDecay, int lookForwardAmount = 1, int lookBackwardsAmount = 1)
            {
                DrawFramesRecursively(frameToDraw, null, lookForwardAmount, false, 1, meshSize, meshSizeDecay);
                DrawFramesRecursively(frameToDraw, null, lookBackwardsAmount, true, -1, meshSize, meshSizeDecay);
            }

            void DrawFramesRecursively(int frameToDraw, Dictionary<ProjectileKey, Tuple<Vector3, Vector3>> connexionPositions, int propogationAmount, bool skipFirstDraw, int direction, float meshSize, float meshSizeDecay)
            {
                Dictionary<ProjectileKey, Tuple<Vector3, Vector3>> projPositions = new Dictionary<ProjectileKey, Tuple<Vector3, Vector3>>();
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (!prj.Value.visible || !prj.Value.FramePositionAndBezier.ContainsKey(frameToDraw)) continue;
                        Vector3 thisFramePos = grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameToDraw].Item1);
                        Vector3 thisFrameBezier = grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameToDraw].Item2);
                        if (!skipFirstDraw)
                        {
                            Handles.color = prj.Value.trailColor;
                            if (prj.Value.parent.prefab == null)
                            {
                                Handles.DrawWireCube(thisFramePos, gridCellSize * meshSizeDecay * meshSize * Vector3.one);
                            }
                            else
                            {
                                Graphics.DrawMesh(prj.Value.parent.prefabMesh, thisFramePos, gridRotation, prj.Value.parent.prefabMaterial, 0);
                            }
                            Handles.BeginGUI();
                            Handles.Label(thisFramePos, (frameToDraw + 1).ToString());
                            Handles.EndGUI();
                            if (connexionPositions != null && connexionPositions.ContainsKey(prj.Value.projectileInstanceID))
                            {
                                Vector3 bezierToUse = direction == -1 ? thisFrameBezier : connexionPositions[prj.Value.projectileInstanceID].Item2;
                                Handles.DrawAAPolyLine(5f,
                                    MathHelper.BezierAproximation(thisFramePos, connexionPositions[prj.Value.projectileInstanceID].Item1, bezierToUse));

                            }
                        }
                        projPositions.Add(prj.Value.projectileInstanceID, new Tuple<Vector3, Vector3>(thisFramePos, thisFrameBezier));
                    }
                }
                if (propogationAmount != 0 && frameToDraw + direction < frameCount && frameToDraw + direction >= 0)
                {
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

            void CreateImportSequenceInterface()
            {
                var window = CreateWindow<ImportSequenceEditorWindow>();
                window.Init(importSequenceWindowAsset, this);

            }

            void CreateImportGridSettingsInterface()
            {
                var window = CreateWindow<ImportAdditionalSettingsEditorWindow>();
                window.Init(importGridSettingsWindowAsset, this);
            }

            internal void ImportAdditionalSettings(AdditionalSequenceData data, bool loadGridSettings, bool loadProjectileLookUps)
            {
                if (loadGridSettings)
                {
                    gridDimensions = data.gridDimensions;
                    gridCellSize = data.gridScale;
                    gridOrigin = data.gridOrigin;
                    gridRotation = data.gridRotation;
                    grid = new MathTools.Grid((Vector2Int)gridDimensions, gridCellSize, gridOrigin, gridRotation);
                }
                if (loadProjectileLookUps)
                {
                    foreach (var v in data.ProjectileLookUps)
                    {
                        if (projectileGroupContainerDict.ContainsKey(v.id))
                        {
                            projectileGroupContainerDict[v.id].ChangePrefab(v.prefab);
                        }
                        else
                        {
                            var g = AddNewProjectileGroupContainer(v.id);
                            g.ChangePrefab(v.prefab);
                        }
                    }
                }
            }

            internal void ImportSequence(SwarmSequence sequence)
            {
                int frameCount = 0;
                foreach (var f in sequence.Frames)
                {
                    frameCount++;
                    foreach (var inst in f.ProjectilePositionData)
                    {
                        if (!projectileGroupContainerDict.ContainsKey(inst.Key.ProjectilePrefabId))
                        {
                            var g = AddNewProjectileGroupContainer(inst.Key.ProjectilePrefabId);
                            var i = g.AddProjectileInstance(inst.Key.ProjectileInstanceID);
                            i.SetPositionInFrame(f.Order, inst.Value.Item1, inst.Value.Item2);
                        }
                        else
                        {
                            var g = projectileGroupContainerDict[inst.Key.ProjectilePrefabId];
                            if (!g.projectileInstances.ContainsKey(inst.Key.ProjectileInstanceID))
                            {
                                g.AddProjectileInstance(inst.Key.ProjectileInstanceID);
                            }
                            g.projectileInstances[inst.Key.ProjectileInstanceID].SetPositionInFrame(f.Order, inst.Value.Item1, inst.Value.Item2);
                        }
                    }
                }
                if (frameCount > this.frameCount)
                {
                    AddMultipleFrames(frameCount - this.frameCount);
                }

            }

            bool SerializeResult()
            {
                if (projectileGroupContainerDict.Count == 0) return false;
                string path = EditorUtility.SaveFilePanel("Save Sequence", Application.dataPath, "SequenceData", "ss");
                if (path.Length == 0)
                    return false;
                List<FrameData> data = new List<FrameData>();
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        foreach (var frame in prj.Value.FramePositionAndBezier)
                        {
                            var activeFrame = data.Find(x => x.Order == frame.Key);
                            SerializableVector3 bezier = MathHelper.NaNVector3;
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frame.Key + 1))
                            {
                                if (frame.Value.Item2 != (frame.Value.Item1 + prj.Value.FramePositionAndBezier[frame.Key + 1].Item1) / 2)
                                    bezier = frame.Value.Item2;
                            }
                            Tuple<SerializableVector3, SerializableVector3> res = new Tuple<SerializableVector3, SerializableVector3>(frame.Value.Item1, bezier);
                            if (activeFrame != null)
                            {
                                activeFrame.ProjectilePositionData.Add(prj.Value.projectileInstanceID, res);
                            }
                            else
                            {
                                var dict = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>();
                                dict.Add(prj.Value.projectileInstanceID, res);
                                data.Add(new FrameData(dict, frame.Key));
                            }
                        }
                    }
                }
                data.Sort();
                FrameDataSerializer.SaveFrameData(path, data);
                return true;
            }

            internal List<InstanceLookUp> GetPrefabs()
            {
                var res = new List<InstanceLookUp>();
                foreach (var g in projectileGroupContainerDict)
                {
                    if (g.Value.prefab != null)
                    {
                        res.Add(new InstanceLookUp() { prefab = g.Value.prefab, id = g.Key });
                    }
                }
                return res;
            }

            void CreateExportAdditionalSettingsWindow()
            {
                var window = CreateWindow<ExportAdditionalSettingsEditorWindow>();
                window.Init(exportAdditionalSettingsWindowAsset, this);
            }

            private void OnDisable()
            {
                SequenceCreatorShortcutManager.activeSequenceCreator = null;
                SceneView.duringSceneGui -= DuringSceneGUI;
                SceneView.RepaintAll();
            }
        }
    }
}