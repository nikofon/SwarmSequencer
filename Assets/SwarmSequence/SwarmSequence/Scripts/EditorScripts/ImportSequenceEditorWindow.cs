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
        public class ImportSequenceEditorWindow : EditorWindow
        {
            SwarmSequence sequence;
            AdditionalSequenceData additionalSettings;
            bool loadGridSettings = true;
            bool loadPrefabs = true;


            public void Init(VisualTreeAsset tree, SequenceCreator parent)
            {
                this.titleContent = new GUIContent("ImportSequenceWindow");
                this.maxSize = new Vector2(250, 125);

                VisualElement root = tree.CloneTree();

                root.Q<Toggle>("importGridSettingsToggle").RegisterValueChangedCallback((v) => loadGridSettings = v.newValue);
                root.Q<Toggle>("importPrefabsToggle").RegisterValueChangedCallback((v) => loadPrefabs = v.newValue);

                var sobjf = root.Q<ObjectField>("sequenceField");
                sobjf.objectType = typeof(SwarmSequence);
                sobjf.RegisterValueChangedCallback((v) => sequence = (SwarmSequence)v.newValue);

                var adsobjf = root.Q<ObjectField>("additionalData");
                adsobjf.objectType = typeof(AdditionalSequenceData);
                adsobjf.RegisterValueChangedCallback((v) => additionalSettings = (AdditionalSequenceData)v.newValue);

                root.Q<Button>("import").clicked += () =>
                {
                    if (sequence == null) { Debug.LogWarning("No sequence to load!"); return; }
                    if (additionalSettings != null)
                    {
                        parent.ImportAdditionalSettings(additionalSettings, loadGridSettings, loadPrefabs);
                    }
                    parent.ImportSequence(sequence);
                    Close();
                };
                root.Q<Button>("back").clicked += Close;

                rootVisualElement.Add(root);
            }

        }
    }
}

