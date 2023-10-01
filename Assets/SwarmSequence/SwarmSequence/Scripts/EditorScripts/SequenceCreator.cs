using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using SwarmSequencer.MathTools;
using SwarmSequencer.Serialization;

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

            public Dictionary<int, ProjectileGroupContainer> projectileGroupContainerDict = new Dictionary<int, ProjectileGroupContainer>();

            public SelectedProjectileInstanceUI SelectedProjectileInstanceUI { get; private set; }

            //Edit mode

            public ModificationMode CurrentMode { get; private set; }

            ButtonGroup selectModeButtonGroup;

            public enum ModificationMode
            {
                Position,
                Bezier,
                Eraser,
                Freehand,
                Corner
            }

            const float editorPrecision = 1e-4f;

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
                    rootVisualElement.Q<Button>("selectEditModeButton"), rootVisualElement.Q<Button>("selectEditBezierModeButton"),
                    rootVisualElement.Q<Button>("selectEraserModeButton"), rootVisualElement.Q<Button>("selectPositionModeButton"),
                    rootVisualElement.Q<Button>("selectCornerModeButton"));
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
                key.SetPositionInFrame(frameIndex, position, bezierControl, GetViewportFromRelativePosition(position));
                if (key.FramePositionAndBezier.ContainsKey(frameIndex - 1))
                {
                    key.ResetBezierPos(frameIndex - 1);
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            ProjectileGroupContainer AddNewProjectileGroupContainer(int containerIndex)
            {
                VisualElement newContainer = projectileContainerAsset.CloneTree();
                ProjectileGroupContainer projGroup = new ProjectileGroupContainer(newContainer, containerIndex, newContainer.Q<VisualElement>("root"), this, projectileInstanceAsset);
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
            void AddProjectileInstance(ProjectileGroupContainer group)
            {
                group.AddProjectileInstance();
            }

            internal void DeleteProjectileInstance(ProjectileGroupContainer group, int projectileInstanceIndex)
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
                    AddFrame(i + currentCount, false);
                }
            }

            public void AddFrame(int frameIndex, bool selectNewFrame = true)
            {
                if (frameIndex != frameCount)
                {
                    Debug.Log($"shifting frames for frame {frameIndex}");
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

            internal Vector3 GetViewportFromRelativePosition(Vector3 relativePosition)
            {
                var position = grid.RelativeToWorldPos(relativePosition);
                if (SceneView.currentDrawingSceneView == null) return MathHelper.NaNVector3;
                return SceneView.currentDrawingSceneView.camera.WorldToViewportPoint(position);
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
                                prj.Value.SetPositionInFrame(keys[i] + offset, tempStorage[keys[i]].Item1, tempStorage[keys[i]].Item2, GetViewportFromRelativePosition(tempStorage[keys[i]].Item1));
                                tempStorage.Remove(keys[i]);
                            }
                            else
                                prj.Value.SetPositionInFrame(keys[i] + offset, prj.Value.FramePositionAndBezier[keys[i]].Item1, prj.Value.FramePositionAndBezier[keys[i]].Item2, GetViewportFromRelativePosition(prj.Value.FramePositionAndBezier[keys[i]].Item1));
                        }
                    }
                }
            }

            void ModifyBezierControls(int frameIndex)
            {
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    if (!prjGrp.Value.visability) continue;
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (!prj.Value.visible) continue;
                        if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                        {
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex + 1) && !MathHelper.IsNaNVector3(prj.Value.FramePositionAndBezier[frameIndex].Item2))
                            {
                                Vector3 newBezier = grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex].Item2), gridRotation));
                                float delta = (newBezier - (Vector3)prj.Value.FramePositionAndBezier[frameIndex].Item2).sqrMagnitude;
                                if ((newBezier - (Vector3)prj.Value.FramePositionAndBezier[frameIndex].Item2).sqrMagnitude > Vector3.kEpsilon)
                                {
                                    prj.Value.SetPositionInFrame(frameIndex, prj.Value.FramePositionAndBezier[frameIndex].Item1, newBezier, GetViewportFromRelativePosition(prj.Value.FramePositionAndBezier[frameIndex].Item1));
                                }
                            }
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex - 1) && !MathHelper.IsNaNVector3(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2))
                            {
                                Vector3 newBezier = grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2), gridRotation));
                                if ((newBezier - (Vector3)prj.Value.FramePositionAndBezier[frameIndex - 1].Item2).sqrMagnitude > Vector3.kEpsilon)
                                {
                                    prj.Value.SetPositionInFrame(frameIndex - 1, prj.Value.FramePositionAndBezier[frameIndex - 1].Item1, newBezier, GetViewportFromRelativePosition(prj.Value.FramePositionAndBezier[frameIndex - 1].Item1));
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
                    if (!prjGrp.Value.visability) continue;
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (!prj.Value.visible) continue;
                        if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                        {
                            Vector3 oldPos = grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex].Item1);
                            Vector3 newPos = Handles.PositionHandle(oldPos, gridRotation);
                            if ((newPos - oldPos).sqrMagnitude < Vector3.kEpsilon) continue;
                            SerializableVector3 newPosition = grid.WorldToRelativePos(newPos);
                            prj.Value.SetPositionInFrame(frameIndex, newPosition, prj.Value.FramePositionAndBezier[frameIndex].Item2, GetViewportFromRelativePosition(newPosition));

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
                                UpdateInstanceViewportPositions();
                            }
                        }
                        break;
                    case EventType.MouseDown:
                        if (!showGrid) break;
                        if (SceneView.mouseOverWindow == sceneView)
                        {
                            if (Event.current.button == 0)
                            {
                                ProcessMouseLeftClick(CurrentMode, sceneView.camera);
                            }
                            else if (Event.current.button == 1)
                            {
                                Vector2 mouseViewportPosition = sceneView.camera.ScreenToViewportPoint(Event.current.mousePosition);
                                Vector2 mousePositionCorrected = new Vector2(mouseViewportPosition.x, 1 - mouseViewportPosition.y);
                                if (CurrentMode == ModificationMode.Eraser)
                                {
                                    foreach (var v in projectileGroupContainerDict)
                                    {
                                        foreach (var vv in v.Value.projectileInstances)
                                        {
                                            var vpPos = vv.Value.GetViewportPosition(SelectedFrame);
                                            if (MathHelper.IsNaNVector3(vpPos)) continue;
                                            if ((vpPos - mousePositionCorrected).sqrMagnitude < GetClickDetectionPrecision(sceneView.camera.transform.position, sceneView.camera.orthographicSize, sceneView.camera.orthographic))
                                            {
                                                vv.Value.ClearFrame(SelectedFrame);
                                                if (vv.Value == SelectedProjectileInstance)
                                                    SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                                            }
                                        }
                                    }
                                }
                                else if (CurrentMode == ModificationMode.Position && SelectedProjectileInstance != null)
                                {
                                    var vpPos = SelectedProjectileInstance.GetViewportPosition(SelectedFrame);
                                    if (!MathHelper.IsNaNVector3(vpPos) && (vpPos - mousePositionCorrected).sqrMagnitude < GetClickDetectionPrecision(sceneView.camera.transform.position, sceneView.camera.orthographicSize))
                                    {
                                        SelectedProjectileInstance.ClearFrame(SelectedFrame);
                                        SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                                    }
                                }
                            }
                        }
                        break;
                }

            }

            void ProcessMouseLeftClick(ModificationMode mode, Camera sceneCamera)
            {
                Vector2 clickPosition = sceneCamera.ScreenToViewportPoint(Event.current.mousePosition);
                Vector2 mousePositionCorrected = new Vector2(clickPosition.x, 1 - clickPosition.y);
                if (SelectedProjectileInstance != null)
                {
                    if (CurrentMode == ModificationMode.Position)
                    {
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
                    else if (CurrentMode == ModificationMode.Eraser)
                    {
                        var vpPos = SelectedProjectileInstance.GetViewportPosition(SelectedFrame);
                        float detectionPrecision = sceneCamera.orthographic ? GetClickDetectionPrecision(sceneCamera.transform.position, sceneCamera.orthographicSize) : GetClickDetectionPrecision(sceneCamera.transform.position, sceneCamera.fieldOfView, false);
                        if (!MathHelper.IsNaNVector3(vpPos) && (vpPos - mousePositionCorrected).sqrMagnitude < detectionPrecision)
                        {
                            SelectedProjectileInstance.ClearFrame(SelectedFrame);
                            SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                        }
                    }
                    else if (CurrentMode == ModificationMode.Corner)
                    {
                        var corner = MathHelper.FindClosestCellCornerIgnoreZ(mousePositionCorrected, gridPointsScreenPosition);
                        var projRelativePosition = grid.WorldToRelativePos(grid.Cells[corner.Item1][corner.Item2]);
                        SerializableVector3 bezier = SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(SelectedFrame + 1) ? (SelectedProjectileInstance.FramePositionAndBezier[SelectedFrame + 1].Item1 + (SerializableVector3)projRelativePosition) / 2 :
                            MathHelper.NaNVector3;
                        AddInstanceInfoToFrame(SelectedFrame, SelectedProjectileInstance,
                        projRelativePosition, bezier);
                    }
                }
            }

            float GetClickDetectionPrecision(Vector3 cameraPosition, float cameraSize, bool orthographic = true)
            {
                float cameraDistance = MathF.Abs(grid.TRSInverse.MultiplyPoint3x4(cameraPosition).z);
                if (orthographic)
                    return 1 / MathF.Max(1, MathF.Pow(cameraDistance, 2)) / cameraSize * grid.CellSize;
                else
                {
                    float mult = cameraDistance * MathF.Atan(cameraSize);
                    return 1 / MathF.Max(1, MathF.Pow(cameraDistance, 2)) / mult * grid.CellSize;
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

            void DrawFramesRecursively(int frameToDraw, Dictionary<ProjectileKey, Tuple<Vector3, Vector3>> connectionPositions, int propogationAmount, bool skipFirstDraw, int direction, float meshSize, float meshSizeDecay)
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
                            if (connectionPositions != null && connectionPositions.ContainsKey(prj.Value.instanceID))
                            {
                                Vector3 bezierToUse = direction == -1 ? thisFrameBezier : connectionPositions[prj.Value.instanceID].Item2;
                                Handles.DrawAAPolyLine(5f,
                                    MathHelper.BezierAproximation(thisFramePos, connectionPositions[prj.Value.instanceID].Item1, bezierToUse));

                            }
                        }
                        projPositions.Add(prj.Value.instanceID, new Tuple<Vector3, Vector3>(thisFramePos, thisFrameBezier));
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
                        if (projectileGroupContainerDict.ContainsKey(v.groupIndex))
                        {
                            projectileGroupContainerDict[v.groupIndex].ChangePrefab(v.prefab);
                        }
                        else
                        {
                            var g = AddNewProjectileGroupContainer(v.groupIndex);
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
                    foreach (var inst in f.ProjectilePositionData)
                    {
                        Vector3 bezier = inst.Value.Item2;
                        if (MathHelper.IsNaNVector3(bezier))
                        {
                            if (frameCount < sequence.Frames.Count - 1)
                            {
                                if (sequence.Frames[frameCount + 1].ProjectilePositionData.ContainsKey(inst.Key))
                                {
                                    bezier = (sequence.Frames[frameCount + 1].ProjectilePositionData[inst.Key].Item1 + sequence.Frames[frameCount].ProjectilePositionData[inst.Key].Item1) / 2;
                                }
                            }
                        }
                        if (!projectileGroupContainerDict.ContainsKey(inst.Key.GroupIndex))
                        {
                            var g = AddNewProjectileGroupContainer(inst.Key.GroupIndex);
                            var i = g.AddProjectileInstance(inst.Key.InstanceIndex);
                            i.SetPositionInFrame(f.Order, inst.Value.Item1, bezier, GetViewportFromRelativePosition(inst.Value.Item1));
                        }
                        else
                        {
                            var g = projectileGroupContainerDict[inst.Key.GroupIndex];
                            if (!g.projectileInstances.ContainsKey(inst.Key.InstanceIndex))
                            {
                                g.AddProjectileInstance(inst.Key.InstanceIndex);
                            }
                            g.projectileInstances[inst.Key.InstanceIndex].SetPositionInFrame(f.Order, inst.Value.Item1, bezier, GetViewportFromRelativePosition(inst.Value.Item1));
                        }
                    }
                    frameCount++;
                }
                projectileGroupIntField.SetValueWithoutNotify(FindFreeProjectileGroupIndex());
                if (frameCount > this.frameCount)
                {
                    AddMultipleFrames(frameCount - this.frameCount);
                }
            }

            void UpdateInstanceViewportPositions()
            {
                foreach (var v in projectileGroupContainerDict)
                {
                    foreach (var vv in v.Value.projectileInstances)
                    {
                        foreach (var frame in vv.Value.FramePositionAndBezier)
                        {
                            vv.Value.SetScreenPositionInFrame(frame.Key, GetViewportFromRelativePosition(frame.Value.Item1));
                        }
                    }
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
                                activeFrame.ProjectilePositionData.Add(prj.Value.instanceID, res);
                            }
                            else
                            {
                                var dict = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>();
                                dict.Add(prj.Value.instanceID, res);
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
                        res.Add(new InstanceLookUp() { prefab = g.Value.prefab, groupIndex = g.Key });
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