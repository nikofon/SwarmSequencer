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

            SwarmSequencer.MathTools.Grid grid;

            SerializedProperty gridOriginSerialized;
            SerializedProperty gridRotationSerialized;
            SerializedProperty gridCellSizeSerialized;

            //Projectile Info

            int frameCount = 0;
            public int SelectedFrame { get; private set; }

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

            VisualTreeAsset projectileContainerPrefab;
            VisualTreeAsset projectileInstancePrefab;

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
                Eraser
            }

            const float editorPrecision = 1e-2f;

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
                gridRotationField.value = gridRotationSerialized.quaternionValue.eulerAngles;
                gridRotationField.RegisterValueChangedCallback((v) =>
                {
                    Vector3 newVal = MathHelper.ConfineVector3ToPrecision(v.newValue, editorPrecision);
                    gridRotationField.SetValueWithoutNotify(newVal);
                    so.Update();
                    gridRotationSerialized.quaternionValue = Quaternion.Euler(newVal);
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
                gridOriginField.value = gridOriginSerialized.vector3Value;
                gridOriginField.RegisterValueChangedCallback((v) =>
                {
                    so.Update();
                    gridOriginSerialized.vector3Value = v.newValue;
                    so.ApplyModifiedProperties();
                    SceneView.RepaintAll();
                });
                grid = new SwarmSequencer.MathTools.Grid((Vector2Int)gridDimensions, gridCellSizeSerialized.floatValue, gridOriginSerialized.vector3Value, gridRotationSerialized.quaternionValue);

                var tb = rootVisualElement.Q<ToolbarButton>("EnableGridEditingButton");
                tb.clicked += () => { modifyingGridInSceneView = !modifyingGridInSceneView; tb.style.backgroundColor = modifyingGridInSceneView ? indicatingGreen : indicatingRed; SceneView.RepaintAll(); };

                #endregion

                #region Frame settings
                projectileGroupIntField = rootVisualElement.Q<IntegerField>("ProjectileGroupIndexField");
                selectedFrameSlider = rootVisualElement.Q<SliderInt>("ChangeCurrentFrameSlider");
                selectedFrameSlider.RegisterValueChangedCallback((v) => { Debug.Log("slider"); SelectFrame(v.newValue); SceneView.RepaintAll(); });
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
                    rootVisualElement.Q<Button>("selectEditModeButton"), rootVisualElement.Q<Button>("selectEditBezierModeButton"), rootVisualElement.Q<Button>("selectEraserModeButton"));
                selectModeButtonGroup.OnValueChange += (oldValue, newValue) => ChangeModificationModeWithoutNotify((ModificationMode)newValue);

                rootVisualElement.Q<Button>("AddProjectileGroupButton").clicked += () =>
                {
                    AddNewProjectileGroupContainer(projectileGroupIntField.value);
                    int newProjInd = FindFreeProjectileGroupIndex();
                    projectileGroupIntField.value = newProjInd;
                };

                rootVisualElement.Q<Button>("save").clicked += () => SerializeResult();

                projectileContainerPrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileGroup.uxml");
                projectileInstancePrefab = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/ProjectileInstance.uxml");


                SceneView.duringSceneGui += DuringSceneGUI;

                if (frameCount == 0)
                {
                    AddFrame(0);
                }
                SequenceCreatorShortcutManager.activeSequenceCreator = this;
            }


            public void SelectProjectileInstance(ProjectileInstanceContainer instance)
            {
                Debug.Log("SelectProjectileInstance function called");
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI(instance);
                SelectedProjectileInstance = instance;
            }

            int FindFreeProjectileGroupIndex()
            {
                Debug.Log("FindFreeProjectileGroupIndex function called");
                int newProjInd = 0;
                while (projectileGroupContainerDict.ContainsKey(newProjInd))
                {
                    newProjInd++;
                }
                return newProjInd;
            }

            public void SelectFrame(int frameIndex)
            {
                Debug.Log("SelectFrame function called");
                if (frameIndex >= frameCount || frameIndex < 0) return;
                SelectedFrame = frameIndex;
                UpdateCurrentFrameCounterUI();
                if (SelectedProjectileInstance != null)
                    SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            void AddInstanceInfoToFrame(int frameIndex, ProjectileInstanceContainer key, SerializableVector3 position, SerializableVector3 bezierControl)
            {
                Debug.Log("AddInstanceInfoToFrame function called");
                key.SetPositionInFrame(frameIndex, position, bezierControl, grid);
                if (key.useBezier)
                {
                    if (key.FramePositionAndBezier.ContainsKey(frameIndex - 1))
                    {
                        key.ResetBezierPos(frameIndex - 1);
                    }
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            void AddInstanceInfoToFrame(int frameIndex, ProjectileInstanceContainer key, int cellPosition, int depth, SerializableVector3 bezierControl)
            {
                Debug.Log("AddInstanceInfoToFrame (by cell) function called");
                key.SetPositionInFrameByCell(frameIndex, cellPosition, depth, bezierControl, grid);
                if (key.useBezier && key.FramePositionAndBezier.ContainsKey(frameIndex - 1) && key.FramePositionAndBezier[frameIndex - 1].Item2.x == float.NaN)
                {
                    key.ResetBezierPos(frameIndex - 1);
                }
                SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
            }

            ProjectileGroupUI AddNewProjectileGroupContainer(int containerIndex)
            {
                Debug.Log("AddNewProjectileGroupContainer function called");
                VisualElement newContainer = projectileContainerPrefab.CloneTree();
                ProjectileGroupUI projGroup = new ProjectileGroupUI(newContainer, containerIndex, newContainer.Q<VisualElement>("root"), this, projectileInstancePrefab);
                newContainer.Q<IntegerField>("projectileGroupIndexField").value = containerIndex;
                newContainer.Q<ToolbarButton>("DeleteButton").clicked += () => DeleteProjectielGroupContainer(containerIndex);
                newContainer.Q<Button>("AddProjectileButton").clicked += () => AddProjectileInstance(projGroup);
                projectileGroupContainerDict.Add(containerIndex, projGroup);
                projectileGroupScrollView.Add(newContainer);
                return projGroup;
            }

            void DeleteProjectielGroupContainer(int containerIndex)
            {
                Debug.Log("DeleteProjectielGroupContainer function called");
                projectileGroupScrollView.Remove(projectileGroupContainerDict[containerIndex].root);
                projectileGroupContainerDict.Remove(containerIndex);
                projectileGroupIntField.value = FindFreeProjectileGroupIndex();
            }
            void AddProjectileInstance(ProjectileGroupUI group)
            {
                Debug.Log("AddProjectileInstance function called");
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
                Debug.Log("DeleteProjectileInstance function called");
                if (SelectedProjectileInstance == group.projectileInstances[projectileInstanceIndex]) SelectProjectileInstance(null);
                group.root.style.height = new StyleLength(new Length(group.root.resolvedStyle.height - ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                group.DeleteProjectileInstance(projectileInstanceIndex);
            }

            void AddMultipleFrames(int amountToAdd)
            {
                int currentCount = frameCount;
                for (int i = 0; i < frameCount - currentCount; i++)
                {
                    AddFrame(i + currentCount);
                }
            }

            void AddFrame(int frameIndex)
            {
                Debug.Log("AddFrame function called");
                if (frameIndex != frameCount)
                {
                    ShiftFramesInProjectileInstances(frameIndex, 1);
                    ClearFrame(frameIndex);
                }
                frameCount++;
                UpdateFrameCountCounterUI();
                SelectFrame(frameIndex);
                SceneView.RepaintAll();
            }

            void UpdateFrameCountCounterUI()
            {
                Debug.Log("UpdateFrameCountCounterUI function called");
                selectedFrameSlider.highValue = frameCount - 1;
                frameCountCounter.SetValueWithoutNotify(frameCount);
            }

            void UpdateCurrentFrameCounterUI()
            {
                Debug.Log("UpdateCurrentFrameCounterUI function called");
                currentFrameCounter.SetValueWithoutNotify(SelectedFrame + 1);
                selectedFrameSlider.SetValueWithoutNotify(SelectedFrame);
            }

            void DeleteFrame(int frameIndex)
            {
                Debug.Log("DeleteFrame function called");
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
                Debug.Log("ClearFrame function called");
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
                Debug.Log("ShiftFramesInProjectileInstances function called");
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
                Debug.Log("ModifyBezierControls function called");
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        if (!prj.Value.useBezier) continue;
                        if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex))
                        {
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex + 1) && !float.IsNaN(prj.Value.FramePositionAndBezier[frameIndex].Item2.x))
                            {
                                prj.Value.FramePositionAndBezier[frameIndex] =
                                new Tuple<SerializableVector3, SerializableVector3>(prj.Value.FramePositionAndBezier[frameIndex].Item1,
                                grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex].Item2), Quaternion.identity)));
                            }
                            if (prj.Value.FramePositionAndBezier.ContainsKey(frameIndex - 1) && !float.IsNaN(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2.x))
                            {
                                prj.Value.FramePositionAndBezier[frameIndex - 1] =
                                new Tuple<SerializableVector3, SerializableVector3>(prj.Value.FramePositionAndBezier[frameIndex - 1].Item1,
                                grid.WorldToRelativePos(Handles.PositionHandle(grid.RelativeToWorldPos(prj.Value.FramePositionAndBezier[frameIndex - 1].Item2), Quaternion.identity)));
                            }
                        }
                    }
                }
                RadioButton b = new RadioButton();
            }

            Matrix4x4 CalculateTRS(float cellSize, Vector3 zero, Quaternion gridRotation)
            {
                Debug.Log("CalculateTRS function called");
                var TRSMatrix = Matrix4x4.TRS(zero, gridRotation, cellSize * Vector3.one);
                return TRSMatrix;
            }
            void DuringSceneGUI(SceneView sceneView)
            {
                if (modifyingGridInSceneView)
                {
                    Vector3 gridZero = gridOriginSerialized.vector3Value;
                    Quaternion gridRotation = gridRotationSerialized.quaternionValue;
                    float gridUniformScale = gridCellSize;
                    Handles.TransformHandle(ref gridZero, ref gridRotation, ref gridUniformScale);
                    gridRotationField.SetValueWithoutNotify(gridRotation.eulerAngles);
                    gridOriginField.SetValueWithoutNotify(gridZero);
                    gridCellSizeField.SetValueWithoutNotify(gridUniformScale);
                    gridCellSizeSerialized.floatValue = gridUniformScale;
                    gridRotationSerialized.quaternionValue = gridRotation;
                    gridOriginSerialized.vector3Value = gridZero;
                }
                if (grid.GridRotation != gridRotationSerialized.quaternionValue ||
                    grid.GridOrigin != gridOriginSerialized.vector3Value ||
                    grid.CellSize != gridCellSizeSerialized.floatValue || grid.GridDimensions != (Vector2Int)gridDimensions)
                {
                    grid = new SwarmSequencer.MathTools.Grid((Vector2Int)gridDimensions, CalculateTRS(gridCellSize, gridOriginSerialized.vector3Value, gridRotationSerialized.quaternionValue));
                    gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                }
                if (CurrentMode == ModificationMode.Bezier)
                {
                    ModifyBezierControls(SelectedFrame);
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
                                SwarmSequencer.MathTools.Grid adjustedGrid = new SwarmSequencer.MathTools.Grid(grid.GridDimensions, Matrix4x4.Translate(Vector3.forward * gridDepthLevel * gridCellSize) * grid.TRS);
                                GizmoHelper.DrawGridWithHandles(adjustedGrid);
                            }
                            if (sceneView.camera.worldToCameraMatrix != sceneCameraMatrix)
                            {
                                gridPointsScreenPosition = FindWorldToScreenSpaceProjection(sceneView, grid.Cells);
                                sceneCameraMatrix = sceneView.camera.worldToCameraMatrix;
                            }
                            DrawFramesOnGrid(SelectedFrame, 0.8f, 0.5f);
                        }
                        break;
                    case EventType.MouseDown:
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
                                                }
                                            }
                                        }
                                    }
                                    else if (CurrentMode == ModificationMode.Position && SelectedProjectileInstance != null)
                                    {
                                        if (SelectedProjectileInstance.GetCellIndex(SelectedFrame) == gridCell)
                                        {
                                            SelectedProjectileInstance.ClearFrame(SelectedFrame);
                                        }
                                    }
                                }
                            }
                        }
                        break;
                }

            }

            public void ChangeModificationModeWithoutNotify(ModificationMode mode)
            {
                CurrentMode = mode;
                SelectedProjectileInstanceUI.ChangeDisplayMode(mode);
            }

            public void SelectModificationMode(ModificationMode mode)
            {
                Debug.Log($"selected mode: {(int)mode}");
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
                            Handles.DrawWireCube(thisFramePos, gridCellSize * meshSizeDecay * meshSize * Vector3.one);
                            Handles.BeginGUI();
                            Handles.Label(thisFramePos, (frameToDraw + 1).ToString());
                            Handles.EndGUI();
                            if (connexionPositions != null && connexionPositions.ContainsKey(prj.Value.projectileInstanceID))
                            {
                                if (prj.Value.useBezier)
                                {
                                    Vector3 bezierToUse = direction == -1 ? thisFrameBezier : connexionPositions[prj.Value.projectileInstanceID].Item2;
                                    Handles.DrawAAPolyLine(5f,
                                        MathHelper.BezierAproximation(thisFramePos, connexionPositions[prj.Value.projectileInstanceID].Item1, bezierToUse));
                                }
                                else
                                    Handles.DrawAAPolyLine(5f, thisFramePos, connexionPositions[prj.Value.projectileInstanceID].Item1);

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

            void LoadSequence(SwarmSequence sequence)
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

            }

            bool SerializeResult()
            {
                if (projectileGroupContainerDict.Count == 0) return false;
                string path = EditorUtility.SaveFilePanel("SaveBakedData", Application.dataPath, "SequenceData", "ss");
                if (path.Length == 0)
                {
                    return false;
                }
                List<FrameData> data = new List<FrameData>();
                foreach (var prjGrp in projectileGroupContainerDict)
                {
                    foreach (var prj in prjGrp.Value.projectileInstances)
                    {
                        foreach (var frame in prj.Value.FramePositionAndBezier)
                        {
                            var activeFrame = data.Find(x => x.Order == frame.Key);
                            if (activeFrame != null)
                            {
                                activeFrame.ProjectilePositionData.Add(prj.Value.projectileInstanceID, frame.Value);
                            }
                            else
                            {
                                var dict = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>();
                                dict.Add(prj.Value.projectileInstanceID, frame.Value);
                                data.Add(new FrameData(dict, frame.Key));
                            }
                        }
                    }
                }
                data.Sort();
                FrameDataSerializer.SaveFrameData(path, data);
                return true;
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