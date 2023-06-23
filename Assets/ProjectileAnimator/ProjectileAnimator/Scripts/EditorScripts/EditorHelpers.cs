using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using UnityEditor;

namespace ProjectileAnimator
{
    public class ProjectileGroupUI
    {
        public GameObject prefab { get; private set; }
        public VisualElement root;
        public int projectileGroupID;

        public bool projectilesShown = true;

        public VisualElement projectileInstanceContainer;

        public Color trailColor;

        public readonly SequenceCreator parent;

        public Dictionary<int, ProjectileInstanceUI> projectileInstances = new Dictionary<int, ProjectileInstanceUI>();

        VisualTreeAsset projectileInstanceAsset;
        Button showProjectielListButton;
        ObjectField gof;

        public ProjectileGroupUI(VisualElement root, int projectileGroupID, VisualElement projectileInstanceContainer, SequenceCreator parent, VisualTreeAsset projectileInstanceAsset)
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
                    if (projectilesShown) showProjectielListButton.text = "Hide projectile list";
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
                    parent.UpdateSelectedInstanceUI();
                }
                );
            gof = root.Q<ObjectField>("projectilePrefabField");
            gof.RegisterValueChangedCallback((v) =>
            {
                prefab = (GameObject)v.newValue;
                parent.UpdateSelectedInstanceUI();
            });
        }

        public void ChangePrefab(GameObject newPrefab)
        {
            prefab = newPrefab;
            gof.value = newPrefab;
        }

        public void DeleteProjectileInstance(int projectileIndex)
        {
            projectileInstanceContainer.Remove(projectileInstances[projectileIndex].root);
            projectileInstances.Remove(projectileIndex);
        }

        public ProjectileInstanceUI AddProjectileInstance()
        {
            return AddProjectileInstance(FindFreeIndex());
        }

        ProjectileInstanceUI AddProjectileInstance(int projectileIndex)
        {
            VisualElement newInstance = projectileInstanceAsset.CloneTree();
            newInstance.style.minHeight = new StyleLength(new Length(ProjectileInstanceUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            newInstance.style.height = new StyleLength(new Length(ProjectileInstanceUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            newInstance.Q<IntegerField>().value = projectileIndex;
            var projInstance = new ProjectileInstanceUI(this, newInstance, newInstance.Q<Vector3IntField>("ProjectilePositionField"), projectileIndex, projectileGroupID, trailColor);
            projectileInstances.Add(projectileIndex, projInstance);
            if (projectilesShown)
            {
                projectileInstanceContainer.Add(newInstance);
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height + ProjectileInstanceUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            }
            else
            {
                ChangeProjectilesVisability();
            }
            return projInstance;
        }

        public void ChangeProjectilesVisability()
        {
            ChangeProjectilesVisability(!projectilesShown);
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
            projectilesShown = changeTo;
            if (changeTo)
            {
                foreach (var i in projectileInstances)
                {
                    projectileInstanceContainer.Add(i.Value.root);
                }
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height + projectileInstances.Count * ProjectileInstanceUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            }
            else
            {
                foreach (var i in projectileInstances)
                {
                    projectileInstanceContainer.Remove(i.Value.root);
                }
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height - projectileInstances.Count * ProjectileInstanceUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            }
            if (projectilesShown) showProjectielListButton.text = "Hide projectile list";
            else showProjectielListButton.text = "Show projectile list";
        }
    }

    public class ProjectileInstanceUI
    {
        public bool useBezier = true;
        public static readonly Color NORMAL_BORDER_COLOR = new Color(0.4941176f, 0.4941176f, 0.4941176f);
        public static readonly Color SELECTED_BORDER_COLOR = new Color(0.04313726F, 0.7058824f, 0f);
        public const int BLOCK_PIXEL_HEIGHT = 100;
        public bool visible = true;
        public Color trailColor;
        public VisualElement root;

        public Vector3IntField positionField;

        public readonly ProjectileKey projectileInstanceID;

        public VisualElement[] borderElements;
        public ProjectileGroupUI parent;
        public ColorField trailColorField;
        Dictionary<int, int> cellPositionInFrame = new Dictionary<int, int>();

        public Dictionary<int, Tuple<SerializableVector3, SerializableVector3>> FramePositionAndBezier = new Dictionary<int, Tuple<SerializableVector3, SerializableVector3>>();

        /// <summary>
        /// Returns index of a cell in which this projectile is positioned at a given frame
        /// </summary>
        /// <param name="frame"></param>
        /// <returns>Cell index</returns>
        public int GetCellIndex(int frame)
        {
            if (!cellPositionInFrame.ContainsKey(frame)) return int.MinValue;
            return cellPositionInFrame[frame];
        }

        /// <summary>
        /// Deletes this projectile from the given frame
        /// </summary>
        /// <param name="frameIndex"></param>
        public void ClearFrame(int frameIndex)
        {
            ClearCellPosition(frameIndex);
            ClearPosition(frameIndex);
        }

        /// <summary>
        /// Sets position in the given frame by adding an entry to FramePositionAndBezier dictionary.
        /// DOES NOT AUTOMATICALLY CALCULATE CELL POSITION, USE OVERLOAD METHOD WITH GRID PARAMETER TO DO SO!
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="position"></param>
        /// <param name="bezierControl"></param>
        public void SetPositionInFrame(int frame, SerializableVector3 position, SerializableVector3 bezierControl)
        {
            FramePositionAndBezier[frame] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
        }

        public void SetPositionInFrameByCell(int frame, int cellIndex, float depth, SerializableVector3 bezierControl, Grid grid)
        {
            Vector2 pos = grid.CellIndexToRelativePosition(cellIndex);
            Vector3 position = new Vector3(pos.x, pos.y, depth);
            FramePositionAndBezier[frame] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            SetCellIndex(frame, cellIndex);
        }

        /// <summary>
        /// Sets position in frame by adding an entry to FramePositionAndBezier dictionary.
        /// Also calculates cell position.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="position"></param>
        /// <param name="bezierControl"></param>
        /// <param name="grid"></param>
        public void SetPositionInFrame(int frame, SerializableVector3 position, SerializableVector3 bezierControl, Grid grid)
        {
            FramePositionAndBezier[frame] = new Tuple<SerializableVector3, SerializableVector3>(position, bezierControl);
            SetCellIndex(frame, grid.RelativePositionToCellIndex(position.x, position.y));
        }


        /// <summary>
        /// Sets in what cell the projectile is located in the given frame
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="cellIndex"></param>
        public void SetCellIndex(int frame, int cellIndex)
        {
            cellPositionInFrame[frame] = cellIndex;
        }

        void ClearCellPosition(int frame)
        {
            cellPositionInFrame.Remove(frame);
        }

        void ClearPosition(int frame)
        {
            FramePositionAndBezier.Remove(frame);
        }


        public void ResetBezierPos(int frameIndex)
        {
            if (FramePositionAndBezier.ContainsKey(frameIndex + 1) && FramePositionAndBezier.ContainsKey(frameIndex))
            {
                Debug.Log($"Actually resetting bezier in {frameIndex}");
                SerializableVector3 b = (FramePositionAndBezier[frameIndex + 1].Item1 + FramePositionAndBezier[frameIndex].Item1) / 2;
                FramePositionAndBezier[frameIndex] =
                new Tuple<SerializableVector3, SerializableVector3>(FramePositionAndBezier[frameIndex].Item1, b);
            }
            foreach (var v in FramePositionAndBezier)
            {
                Debug.Log($"{v.Key}: bezier {(Vector3)v.Value.Item2}");
            }
        }
        public ProjectileInstanceUI(ProjectileGroupUI parent, VisualElement root, Vector3IntField positionField, int projectileInstanceID, int projectileGroupID, Color trailColor)
        {
            this.parent = parent;
            this.root = root;
            this.positionField = positionField;
            this.trailColor = trailColor;
            this.projectileInstanceID = new ProjectileKey(projectileGroupID, projectileInstanceID);
            trailColorField = root.Q<ColorField>("trailColorOverrideField");
            trailColorField.value = this.trailColor;
            trailColorField.RegisterValueChangedCallback(
                (v) =>
                {
                    this.trailColor = v.newValue;
                    if (this.parent.parent.SelectedProjectileInstance == this)
                    {
                        this.parent.parent.UpdateSelectedInstanceUI();
                    }
                }
                );
            borderElements = new VisualElement[]{
                root.Q<VisualElement>("BorderContainerOne"),
                root.Q<VisualElement>("BorderContainerTwo"),
                root.Q<VisualElement>("BorderContainerThree"),
        };
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
}
