using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;

namespace SwarmSequencer
{
    namespace EditorTools
    {
        public class ExportAdditionalSettingsEditorWindow : EditorWindow
        {
            SequenceCreator parent;
            AdditionalSequenceData additionalSettings;
            bool loadGridSettings;
            bool loadPrefabs;


            public void Init(VisualTreeAsset tree, SequenceCreator parent)
            {
                this.parent = parent;
                this.titleContent = new GUIContent("Export settings");
                this.maxSize = new Vector2(250, 125);

                VisualElement root = tree.CloneTree();

                root.Q<Toggle>("exportGridSettingsToggle").RegisterValueChangedCallback((v) => loadGridSettings = v.newValue);
                root.Q<Toggle>("exportPrefabsToggle").RegisterValueChangedCallback((v) => loadPrefabs = v.newValue);

                root.Q<Button>("save").clicked += () =>
                {
                    ExportAdditionalSettings();
                };
                root.Q<Button>("back").clicked += Close;

                rootVisualElement.Add(root);
            }

            bool ExportAdditionalSettings()
            {
                string path = EditorUtility.SaveFilePanel("Export sequence additional data", Application.dataPath, "SequenceAdditionalData", "asset");
                if (path.Length == 0)
                    return false;
                int keywordIndex = path.IndexOf("Assets");
                if (keywordIndex != -1)
                {
                    path = path.Substring(keywordIndex);
                }
                AdditionalSequenceData data = ScriptableObject.CreateInstance<AdditionalSequenceData>();
                if (loadGridSettings)
                {
                    data.gridOrigin = parent.gridOrigin;
                    data.gridDimensions = parent.gridDimensions;
                    data.gridRotation = parent.gridRotation;
                    data.gridScale = parent.gridCellSize;
                }
                if (loadPrefabs)
                {
                    data.ProjectileLookUps = parent.GetPrefabs();
                }
                Debug.Log(path);
                AssetDatabase.CreateAsset(data, path);
                return true;
            }


        }
    }
}
