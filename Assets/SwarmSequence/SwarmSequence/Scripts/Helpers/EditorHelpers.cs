using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using UnityEditor;
using SwarmSequencer.MathTools;

namespace SwarmSequencer
{
    namespace EditorTools
    {
        public class ProjectileGroupContainer
        {
            public GameObject prefab
            {
                get => m_prefab;
                private set
                {
                    if (value != null)
                    {
                        prefabMesh = value.GetComponent<MeshFilter>()?.sharedMesh;
                        prefabMaterial = value.GetComponent<Renderer>()?.sharedMaterial;
                    }
                    m_prefab = value;
                }
            }
            GameObject m_prefab;
            public Mesh prefabMesh;
            public Material prefabMaterial;
            public VisualElement root;
            public int projectileGroupID;

            public bool visability = true;

            public VisualElement projectileInstanceContainer;

            public Color trailColor;

            public readonly SequenceCreator parent;

            public Dictionary<int, ProjectileInstanceContainer> projectileInstances = new Dictionary<int, ProjectileInstanceContainer>();

            ToolbarButton visibilityButton;

            VisualTreeAsset projectileInstanceAsset;
            Button showProjectielListButton;
            ObjectField gof;

            public ProjectileGroupContainer(VisualElement root, int projectileGroupID, VisualElement projectileInstanceContainer, SequenceCreator parent, VisualTreeAsset projectileInstanceAsset)
            {
                this.root = root;
                this.projectileGroupID = projectileGroupID;
                this.projectileInstanceContainer = projectileInstanceContainer;
                this.parent = parent;
                this.projectileInstanceAsset = projectileInstanceAsset;
                showProjectielListButton = root.Q<Button>("DisplayProjectileList");
                showProjectielListButton.clicked += () =>
                {
                    if (projectileInstances.Count != 0)
                    {
                        ChangeProjectilesVisability();
                        if (visability) showProjectielListButton.text = "Hide projectile list";
                        else showProjectielListButton.text = "Show projectile list";
                    }
                };
                trailColor = Color.green;
                var cf = root.Q<ColorField>("trailColorField");
                cf.value = trailColor;
                cf.RegisterValueChangedCallback(
                    (v) =>
                    {
                        trailColor = v.newValue;
                        foreach (var inst in projectileInstances)
                        {
                            if (inst.Value.trailColor == v.previousValue)
                            {
                                inst.Value.trailColorField.value = v.newValue;
                            }
                        }
                        parent.SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                    }
                    );
                gof = root.Q<ObjectField>("projectilePrefabField");
                gof.RegisterValueChangedCallback((v) =>
                {
                    prefab = (GameObject)v.newValue;
                    parent.SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                });

                visibilityButton = root.Q<ToolbarButton>("ChangeVisibilityButton");

                visibilityButton.clicked += () =>
                {
                    ChangeVisibility(!visability);
                };

                visibilityButton.style.backgroundColor = SequenceCreator.indicatingGreen;
            }

            public void ChangePrefab(GameObject newPrefab)
            {
                prefab = newPrefab;
                gof.value = newPrefab;
            }

            public void ChangeVisibility(bool visible)
            {
                visability = visible;
                foreach (var i in projectileInstances)
                {
                    i.Value.ChangeVisibility(visible);
                }
                visibilityButton.style.backgroundColor = visible ? SequenceCreator.indicatingGreen : SequenceCreator.indicatingRed;
            }

            public void DeleteProjectileInstance(int projectileIndex)
            {
                projectileInstanceContainer.Remove(projectileInstances[projectileIndex].root);
                projectileInstances.Remove(projectileIndex);
            }

            public ProjectileInstanceContainer AddProjectileInstance()
            {
                return AddProjectileInstance(FindFreeIndex());
            }

            public ProjectileInstanceContainer AddProjectileInstance(int projectileIndex)
            {
                VisualElement newInstance = projectileInstanceAsset.CloneTree();
                newInstance.style.minHeight = new StyleLength(new Length(ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                newInstance.style.height = new StyleLength(new Length(ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                newInstance.Q<IntegerField>().value = projectileIndex;
                var projInstance = new ProjectileInstanceContainer(this, newInstance, projectileIndex, projectileGroupID, trailColor);
                projectileInstances.Add(projectileIndex, projInstance);
                if (visability)
                {
                    projectileInstanceContainer.Add(newInstance);
                    root.style.height = new StyleLength(new Length(root.resolvedStyle.height + ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                }
                else
                {
                    ChangeProjectilesVisability();
                }
                return projInstance;
            }

            public void ChangeProjectilesVisability()
            {
                ChangeProjectilesVisability(!visability);
            }

            int FindFreeIndex()
            {
                int i = 0;
                while (projectileInstances.ContainsKey(i))
                {
                    i++;
                }
                return i;
            }

            public void ChangeProjectilesVisability(bool changeTo)
            {
                if (projectileInstances.Count == 0) return;
                visability = changeTo;
                if (changeTo)
                {
                    foreach (var i in projectileInstances)
                    {
                        projectileInstanceContainer.Add(i.Value.root);
                    }
                    root.style.height = new StyleLength(new Length(root.resolvedStyle.height + projectileInstances.Count * ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                }
                else
                {
                    foreach (var i in projectileInstances)
                    {
                        projectileInstanceContainer.Remove(i.Value.root);
                    }
                    root.style.height = new StyleLength(new Length(root.resolvedStyle.height - projectileInstances.Count * ProjectileInstanceContainer.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
                }
                if (visability) showProjectielListButton.text = "Hide instance list";
                else showProjectielListButton.text = "Show instance list";
            }
        }

        public class ProjectileInstanceContainer
        {
            public static readonly Color NORMAL_BORDER_COLOR = new Color(0.4941176f, 0.4941176f, 0.4941176f);
            public static readonly Color SELECTED_BORDER_COLOR = new Color(0.04313726F, 0.7058824f, 0f);
            public const int BLOCK_PIXEL_HEIGHT = 100;
            public bool visible = true;
            public Color trailColor;
            public VisualElement root;

            public Vector3Field positionField;

            public readonly ProjectileKey instanceID;

            public VisualElement[] borderElements;
            public ProjectileGroupContainer parent;
            public ColorField trailColorField;
            Dictionary<int, Vector2> viewportPositionInFrame = new Dictionary<int, Vector2>();

            public Dictionary<int, Tuple<SerializableVector3, SerializableVector3>> FramePositionAndBezier = new Dictionary<int, Tuple<SerializableVector3, SerializableVector3>>();

            ToolbarButton visibilityButton;



            /// <summary>
            /// Deletes this projectile from the given frame
            /// </summary>
            /// <param name="frameIndex"></param>
            public void ClearFrame(int frameIndex)
            {
                ClearPosition(frameIndex);
                ClearScreenPosition(frameIndex);
                if (parent.parent.SelectedFrame == frameIndex) UpdatePositionUI();
            }


            /// <summary>
            /// Sets position in frame by adding an entry to FramePositionAndBezier dictionary.
            /// Also calculates cell position.
            /// </summary>
            /// <param name="frame"></param>
            /// <param name="position"></param>
            /// <param name="bezierControl"></param>
            /// <param name="grid"></param>
            internal void SetPositionInFrame(int frame, SerializableVector3 position, SerializableVector3 bezierControl, Vector2 screenPosition)
            {
                FramePositionAndBezier[frame] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
                viewportPositionInFrame[frame] = screenPosition;
                if (frame == parent.parent.SelectedFrame) UpdatePositionUI();
            }

            internal void SetBezierInFrame(int frame, SerializableVector3 bezierControl)
            {
                FramePositionAndBezier[frame] = new Tuple<SerializableVector3, SerializableVector3>(FramePositionAndBezier[frame].Item1, bezierControl);
            }

            internal void SetScreenPositionInFrame(int frame, Vector2 position)
            {
                viewportPositionInFrame[frame] = position;
            }

            void ClearPosition(int frame)
            {
                FramePositionAndBezier.Remove(frame);
            }

            void ClearScreenPosition(int frame)
            {
                viewportPositionInFrame.Remove(frame);
            }

            public Vector2 GetViewportPosition(int frame)
            {
                if (!viewportPositionInFrame.ContainsKey(frame)) return MathHelper.NaNVector3;
                return viewportPositionInFrame[frame];
            }


            public void ResetBezierPos(int frameIndex)
            {
                if (FramePositionAndBezier.ContainsKey(frameIndex + 1) && FramePositionAndBezier.ContainsKey(frameIndex))
                {
                    SerializableVector3 b = (FramePositionAndBezier[frameIndex + 1].Item1 + FramePositionAndBezier[frameIndex].Item1) / 2;
                    FramePositionAndBezier[frameIndex] =
                    new Tuple<SerializableVector3, SerializableVector3>(FramePositionAndBezier[frameIndex].Item1, b);
                }
            }
            public ProjectileInstanceContainer(ProjectileGroupContainer parent, VisualElement root, int projectileInstanceID, int projectileGroupID, Color trailColor)
            {
                this.parent = parent;
                this.root = root;
                this.trailColor = trailColor;
                this.instanceID = new ProjectileKey(projectileGroupID, projectileInstanceID);
                trailColorField = root.Q<ColorField>("trailColorOverrideField");
                trailColorField.value = this.trailColor;
                trailColorField.RegisterValueChangedCallback(
                    (v) =>
                    {
                        this.trailColor = v.newValue;
                        if (this.parent.parent.SelectedProjectileInstance == this)
                        {
                            this.parent.parent.SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                        }
                    }
                    );

                positionField = root.Q<Vector3Field>("projectilePositionField");
                positionField.value = MathHelper.NaNVector3;
                positionField.RegisterValueChangedCallback((v) =>
                {
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.parent.SelectedFrame;
                    Vector3 newPosition = MathHelper.NanToZero(v.newValue);
                    SerializableVector3 bezier = MathHelper.NaNVector3;
                    if (FramePositionAndBezier.ContainsKey(selectedFrame))
                    {
                        if (FramePositionAndBezier.ContainsKey(selectedFrame + 1) && MathHelper.IsNaNVector3(FramePositionAndBezier[selectedFrame].Item2))
                        {
                            bezier = (FramePositionAndBezier[selectedFrame + 1].Item1 + (SerializableVector3)newPosition) / 2;
                        }
                        else { bezier = FramePositionAndBezier[selectedFrame].Item2; }
                    }
                    SetPositionInFrame(selectedFrame, newPosition, bezier, parent.parent.GetViewportFromRelativePosition(newPosition));
                    positionField.SetValueWithoutNotify(newPosition);
                    parent.parent.SelectedProjectileInstanceUI.UpdateSelectedInstanceUI();
                    SceneView.RepaintAll();
                });

                root.Q<ToolbarButton>("DeleteInstanceButton").clicked += () =>
                {
                    parent.parent.DeleteProjectileInstance(parent, projectileInstanceID);
                };

                root.Q<Button>("SelectInstanceButton").clicked += () =>
                {
                    parent.parent.SelectProjectileInstance(this);
                };

                visibilityButton = root.Q<ToolbarButton>("ChangeVisabilityButton");

                visibilityButton.clicked += () =>
                {
                    ChangeVisibility(!visible);
                };

                ChangeVisibility(true);

                borderElements = new VisualElement[]{
                root.Q<VisualElement>("BorderContainerOne"),
                root.Q<VisualElement>("BorderContainerTwo"),
                root.Q<VisualElement>("BorderContainerThree"),
                };
            }

            public void ChangeVisibility(bool visibility)
            {
                visible = visibility;
                visibilityButton.style.backgroundColor = visible ? SequenceCreator.indicatingGreen : SequenceCreator.indicatingRed;
                SceneView.RepaintAll();
            }

            internal void UpdatePositionUI()
            {
                int selectedFrame = parent.parent.SelectedFrame;
                if (FramePositionAndBezier.ContainsKey(selectedFrame))
                {
                    positionField.SetValueWithoutNotify(FramePositionAndBezier[selectedFrame].Item1);
                }
                else
                    positionField.SetValueWithoutNotify(MathHelper.NaNVector3);
            }
        }

        public class ButtonGroup
        {
            List<Button> Buttons;

            public int Value { get => m_value; set { OnValueChange?.Invoke(m_value, value); m_value = value; } }
            private int m_value;
            public Color selectedBGColor;

            public Color standartBGColor;

            public event Action<int, int> OnValueChange;

            public ButtonGroup(Color selectedBGColor, Color standartBGColor, int initValue = 0, params Button[] buttons)
            {
                this.selectedBGColor = selectedBGColor;
                this.standartBGColor = standartBGColor;
                Buttons = new List<Button>(buttons);
                for (int i = 0; i < Buttons.Count; i++)
                {
                    int buttonValue = i;
                    Buttons[i].clicked += () => SetValue(buttonValue);
                }
                OnValueChange += UpdateUI;
                SetValue(initValue);
            }

            void AddButtonToGroup(Button button)
            {
                Buttons.Add(button);
                int buttonValue = Buttons.Count - 1;
                button.clicked += () => SetValue(buttonValue);
            }

            void SetValue(int newValue)
            {
                if (newValue < Buttons.Count)
                {
                    Value = newValue;
                }
                else throw new IndexOutOfRangeException("You are trying to assign a value which is out of range");
            }

            void UpdateUI(int oldValue, int newValue)
            {
                Buttons[oldValue].style.backgroundColor = standartBGColor;
                Buttons[newValue].style.backgroundColor = selectedBGColor;
            }
        }

        public class SelectedProjectileInstanceUI
        {
            VisualElement bezierControlParent;
            VisualElement positionControlParent;

            Label selectedProjectileGroupLabel;
            Label selectedProjectileInstanceLabel;
            ColorField selectedProjectileInstanceColorField;
            ObjectField selectedProjectilePrefabField;
            Vector3Field selectedInstPosInCurrentFrame;
            Vector3Field selectedInstPosInNextFrame;
            Vector3Field selectedInstPosInPrevFrame;
            Vector3Field selectedBezierPrevToCurrent;
            Vector3Field selectedBezierCurrentToNext;
            IMGUIContainer selectedProjectilePreview;
            Editor selectedInstancePrefabEditor;

            SequenceCreator parent;

            public SelectedProjectileInstanceUI(VisualElement rootVisualElement, SequenceCreator parent)
            {
                this.parent = parent;
                selectedProjectileGroupLabel = rootVisualElement.Q<Label>("GroupIndexCounter");
                selectedProjectileInstanceLabel = rootVisualElement.Q<Label>("InstanceIndexCounter");
                selectedProjectileInstanceColorField = rootVisualElement.Q<ColorField>("SelectedProjectileColorField");
                selectedProjectileInstanceColorField.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance != null)
                    {
                        parent.SelectedProjectileInstance.trailColor = v.newValue;
                        parent.SelectedProjectileInstance.trailColorField.value = v.newValue;
                    }
                });
                selectedProjectilePrefabField = rootVisualElement.Q<ObjectField>("SelectedInstancePrefabField");
                selectedProjectilePrefabField.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    this.parent.projectileGroupContainerDict[parent.SelectedProjectileInstance.instanceID.GroupIndex].ChangePrefab((GameObject)v.newValue);
                });
                rootVisualElement.Q<Button>("ClearInstanceSelectionButton").clicked += () => parent.SelectProjectileInstance(null);

                selectedInstPosInCurrentFrame = rootVisualElement.Q<Vector3Field>("selectedPosInCurrentFrame");
                selectedInstPosInCurrentFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                selectedInstPosInCurrentFrame.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.SelectedFrame;
                    Vector3 newPosition = MathHelper.NanToZero(v.newValue);
                    SerializableVector3 bezier = MathHelper.NaNVector3;
                    if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame))
                    {
                        if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame + 1) && MathHelper.IsNaNVector3(parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2))
                        {
                            bezier = (parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame + 1].Item1 + (SerializableVector3)newPosition) / 2;
                        }
                        else { bezier = parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2; }
                    }
                    parent.SelectedProjectileInstance.SetPositionInFrame(selectedFrame, newPosition, bezier, parent.GetViewportFromRelativePosition(newPosition));
                    selectedInstPosInCurrentFrame.SetValueWithoutNotify(newPosition);
                    SceneView.RepaintAll();
                });

                selectedInstPosInNextFrame = rootVisualElement.Q<Vector3Field>("selectedInstPosInNextFrame");
                selectedInstPosInNextFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                selectedInstPosInNextFrame.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.SelectedFrame + 1;
                    if (selectedFrame >= parent.FrameCount) parent.AddFrame(selectedFrame, false);
                    Vector3 newPosition = MathHelper.NanToZero(v.newValue);
                    SerializableVector3 bezier = MathHelper.NaNVector3;
                    if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame))
                    {
                        if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame + 1) && MathHelper.IsNaNVector3(parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2))
                        {
                            bezier = (parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame + 1].Item1 + (SerializableVector3)newPosition) / 2;
                        }
                        else { bezier = parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2; }
                    }
                    parent.SelectedProjectileInstance.SetPositionInFrame(selectedFrame, newPosition, bezier, parent.GetViewportFromRelativePosition(newPosition));
                    selectedInstPosInNextFrame.SetValueWithoutNotify(newPosition);
                    SceneView.RepaintAll();
                });

                selectedInstPosInPrevFrame = rootVisualElement.Q<Vector3Field>("selectedInstPosInPrevFrame");
                selectedInstPosInPrevFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                selectedInstPosInPrevFrame.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.SelectedFrame - 1;
                    if (selectedFrame < 0)
                    {
                        selectedInstPosInPrevFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                        return;
                    }
                    Vector3 newPosition = MathHelper.NanToZero(v.newValue);
                    SerializableVector3 bezier = MathHelper.NaNVector3;
                    if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame))
                    {
                        if (parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(selectedFrame + 1) && MathHelper.IsNaNVector3(parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2))
                        {
                            bezier = (parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame + 1].Item1 + (SerializableVector3)newPosition) / 2;
                        }
                        else { bezier = parent.SelectedProjectileInstance.FramePositionAndBezier[selectedFrame].Item2; }
                    }
                    parent.SelectedProjectileInstance.SetPositionInFrame(selectedFrame, newPosition, bezier, parent.GetViewportFromRelativePosition(newPosition));
                    selectedInstPosInPrevFrame.SetValueWithoutNotify(newPosition);
                    SceneView.RepaintAll();
                });

                selectedBezierCurrentToNext = rootVisualElement.Q<Vector3Field>("bezierCurrentFrame");
                selectedBezierCurrentToNext.SetValueWithoutNotify(MathHelper.NaNVector3);
                selectedBezierCurrentToNext.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    if (!parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(parent.SelectedFrame + 1)) return;
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.SelectedFrame;
                    Vector3 newBezier = MathHelper.NanToZero(v.newValue);
                    parent.SelectedProjectileInstance.SetBezierInFrame(parent.SelectedFrame, newBezier);
                    selectedBezierCurrentToNext.SetValueWithoutNotify(newBezier);
                    SceneView.RepaintAll();
                });
                selectedBezierCurrentToNext.MakeDelayed();
                selectedBezierPrevToCurrent = rootVisualElement.Q<Vector3Field>("bezierPrevFrame");
                selectedBezierPrevToCurrent.SetValueWithoutNotify(MathHelper.NaNVector3);
                selectedBezierPrevToCurrent.RegisterValueChangedCallback((v) =>
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    if (!parent.SelectedProjectileInstance.FramePositionAndBezier.ContainsKey(parent.SelectedFrame - 1)) return;
                    if (MathHelper.IsNaNVector3(v.newValue)) return;
                    int selectedFrame = parent.SelectedFrame - 1;
                    Vector3 newBezier = MathHelper.NanToZero(v.newValue);
                    parent.SelectedProjectileInstance.SetBezierInFrame(parent.SelectedFrame - 1, newBezier);
                    selectedBezierPrevToCurrent.SetValueWithoutNotify(newBezier);
                    SceneView.RepaintAll();
                });
                selectedBezierPrevToCurrent.MakeDelayed();

                selectedProjectilePreview = rootVisualElement.Q<IMGUIContainer>("selectedProjectilePreview");

                bezierControlParent = rootVisualElement.Q<VisualElement>("bezierControl");
                positionControlParent = rootVisualElement.Q<VisualElement>("positionControl");

                selectedProjectilePreview.onGUIHandler += ProjectileAssetPreviewIMGUI;

                ChangeDisplayMode(SequenceCreator.ModificationMode.Position);
            }

            public void ChangeDisplayMode(SequenceCreator.ModificationMode mode)
            {
                if (mode == SequenceCreator.ModificationMode.Position || mode == SequenceCreator.ModificationMode.Eraser)
                {
                    positionControlParent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    bezierControlParent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
                else if (mode == SequenceCreator.ModificationMode.Bezier)
                {
                    positionControlParent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    bezierControlParent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                }
            }

            public void UpdateSelectedInstanceUI()
            {
                UpdateSelectedInstanceUI(parent.SelectedProjectileInstance);
            }

            void ProjectileAssetPreviewIMGUI()
            {
                if (selectedInstancePrefabEditor != null)
                    selectedInstancePrefabEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(150, 150), null);
            }
            public void UpdateSelectedInstanceUI(ProjectileInstanceContainer selected)
            {
                if (parent.SelectedProjectileInstance != null)
                {
                    foreach (var v in parent.SelectedProjectileInstance.borderElements)
                    {
                        v.style.borderTopColor = ProjectileInstanceContainer.NORMAL_BORDER_COLOR;
                        v.style.borderLeftColor = ProjectileInstanceContainer.NORMAL_BORDER_COLOR;
                        v.style.borderBottomColor = ProjectileInstanceContainer.NORMAL_BORDER_COLOR;
                        v.style.borderRightColor = ProjectileInstanceContainer.NORMAL_BORDER_COLOR;
                    }
                }
                if (selected == null)
                {
                    if (parent.SelectedProjectileInstance == null) return;
                    selectedProjectileGroupLabel.text = "-";
                    selectedProjectileInstanceLabel.text = "-";
                    selectedProjectileInstanceColorField.value = Color.black;
                    selectedInstancePrefabEditor = null;
                    selectedProjectilePrefabField.value = null;
                    selectedInstPosInCurrentFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedInstPosInNextFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedInstPosInPrevFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedBezierCurrentToNext.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedBezierPrevToCurrent.SetValueWithoutNotify(MathHelper.NaNVector3);
                    return;
                }
                selectedProjectileGroupLabel.text = selected.instanceID.GroupIndex.ToString();
                selectedProjectileInstanceLabel.text = selected.instanceID.InstanceIndex.ToString();
                selectedProjectileInstanceColorField.value = selected.trailColor;
                selectedProjectilePrefabField.value = selected.parent.prefab;
                if (parent.projectileGroupContainerDict[selected.instanceID.GroupIndex].prefab != null)
                    selectedInstancePrefabEditor = Editor.CreateEditor(parent.projectileGroupContainerDict[selected.instanceID.GroupIndex].prefab);
                if (selected.FramePositionAndBezier.ContainsKey(parent.SelectedFrame))
                {
                    selectedInstPosInCurrentFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[parent.SelectedFrame].Item1);
                    selectedBezierCurrentToNext.SetValueWithoutNotify(selected.FramePositionAndBezier[parent.SelectedFrame].Item2);
                }
                else
                {
                    selectedInstPosInCurrentFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedBezierCurrentToNext.SetValueWithoutNotify(MathHelper.NaNVector3);
                }
                if (selected.FramePositionAndBezier.ContainsKey(parent.SelectedFrame - 1))
                {
                    selectedInstPosInPrevFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[parent.SelectedFrame - 1].Item1);
                    selectedBezierPrevToCurrent.SetValueWithoutNotify(selected.FramePositionAndBezier[parent.SelectedFrame - 1].Item2);
                }
                else
                {
                    selectedInstPosInPrevFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                    selectedBezierPrevToCurrent.SetValueWithoutNotify(MathHelper.NaNVector3);
                }
                if (selected.FramePositionAndBezier.ContainsKey(parent.SelectedFrame + 1))
                    selectedInstPosInNextFrame.SetValueWithoutNotify(selected.FramePositionAndBezier[parent.SelectedFrame + 1].Item1);
                else
                    selectedInstPosInNextFrame.SetValueWithoutNotify(MathHelper.NaNVector3);
                foreach (var v in selected.borderElements)
                {
                    v.style.borderTopColor = ProjectileInstanceContainer.SELECTED_BORDER_COLOR;
                    v.style.borderLeftColor = ProjectileInstanceContainer.SELECTED_BORDER_COLOR;
                    v.style.borderBottomColor = ProjectileInstanceContainer.SELECTED_BORDER_COLOR;
                    v.style.borderRightColor = ProjectileInstanceContainer.SELECTED_BORDER_COLOR;
                }
            }
        }
    }
}
