using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

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

        public Dictionary<int, ProjectileInstanceGUI> projectileInstances = new Dictionary<int, ProjectileInstanceGUI>();

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

        public ProjectileInstanceGUI AddProjectileInstance()
        {
            return AddProjectileInstance(FindFreeIndex());
        }

        ProjectileInstanceGUI AddProjectileInstance(int projectileIndex)
        {
            VisualElement newInstance = projectileInstanceAsset.CloneTree();
            newInstance.style.minHeight = new StyleLength(new Length(ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            newInstance.style.height = new StyleLength(new Length(ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            newInstance.Q<IntegerField>().value = projectileIndex;
            var projInstance = new ProjectileInstanceGUI(this, newInstance, newInstance.Q<Vector3IntField>("ProjectilePositionField"), projectileIndex, projectileGroupID, trailColor);
            projectileInstances.Add(projectileIndex, projInstance);
            if (projectilesShown)
            {
                projectileInstanceContainer.Add(newInstance);
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height + ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
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
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height + projectileInstances.Count * ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            }
            else
            {
                foreach (var i in projectileInstances)
                {
                    projectileInstanceContainer.Remove(i.Value.root);
                }
                root.style.height = new StyleLength(new Length(root.resolvedStyle.height - projectileInstances.Count * ProjectileInstanceGUI.BLOCK_PIXEL_HEIGHT, LengthUnit.Pixel));
            }
            if (projectilesShown) showProjectielListButton.text = "Hide projectile list";
            else showProjectielListButton.text = "Show projectile list";
        }
    }

    public class ProjectileInstanceGUI
    {
        public static readonly Color NORMAL_BORDER_COLOR = new Color(0.4941176f, 0.4941176f, 0.4941176f);
        public static readonly Color SELECTED_BORDER_COLOR = new Color(0.04313726F, 0.7058824f, 0f);
        public Color trailColor;
        public const int BLOCK_PIXEL_HEIGHT = 100;
        public VisualElement root;

        public Vector3IntField positionField;

        public readonly ProjectileKey projectileInstanceID;

        public VisualElement[] borderElements;
        public ProjectileGroupUI parent;
        public ColorField trailColorField;

        public ProjectileInstanceGUI(ProjectileGroupUI parent, VisualElement root, Vector3IntField positionField, int projectileInstanceID, int projectileGroupID, Color trailColor)
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
}
