using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor;

namespace SwarmSequencer
{
    namespace EditorTools
    {
        public class ImportAdditionalSettingsEditorWindow : EditorWindow
        {
            AdditionalSequenceData additionalSettings;
            bool loadGridSettings;
            bool loadPrefabs;


            public void Init(VisualTreeAsset tree, SequenceCreator parent)
            {
                this.titleContent = new GUIContent("ImportSequenceWindow");
                this.maxSize = new Vector2(250, 125);

                VisualElement root = tree.CloneTree();

                root.Q<Toggle>("importGridSettingsToggle").RegisterValueChangedCallback((v) => loadGridSettings = v.newValue);
                root.Q<Toggle>("importPrefabsToggle").RegisterValueChangedCallback((v) => loadPrefabs = v.newValue);

                var adsobjf = root.Q<ObjectField>("additionalData");
                adsobjf.objectType = typeof(AdditionalSequenceData);
                adsobjf.RegisterValueChangedCallback((v) => additionalSettings = (AdditionalSequenceData)v.newValue);

                root.Q<Button>("import").clicked += () =>
                {
                    if (additionalSettings == null) { Debug.LogWarning("No data to load!"); return; }
                    parent.ImportAdditionalSettings(additionalSettings, loadGridSettings, loadPrefabs);
                    Close();
                };
                root.Q<Button>("back").clicked += Close;

                rootVisualElement.Add(root);
            }

        }
    }
}

