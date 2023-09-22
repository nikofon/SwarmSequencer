using UnityEditor;
using UnityEngine;
using UnityEditor.AssetImporters;
using System.IO;
using UnityEditor.Callbacks;
using SwarmSequencer.EditorTools;

namespace SwarmSequencer
{
    namespace Serialization
    {
        [ScriptedImporter(1, "ss")]
        public class SSImporter : ScriptedImporter
        {
            SwarmSequence asset;
            public override void OnImportAsset(AssetImportContext ctx)
            {
                var data = File.ReadAllText(ctx.assetPath);
                asset = ScriptableObject.CreateInstance<SwarmSequence>();
                asset.sequenceName = ctx.assetPath;
                asset.rawData = data;
                ctx.AddObjectToAsset("Main", asset);
                ctx.SetMainObject(asset);
            }

            [OnOpenAsset]
            public static bool OpenSwarmSequenceInEditor(int instanceID, int line)
            {
                bool windowIsOpen = EditorWindow.HasOpenInstances<SequenceCreator>();
                var path = AssetDatabase.GetAssetPath(instanceID);
                if (Path.GetExtension(path) != ".ss") return false;
                Object asset = EditorUtility.InstanceIDToObject(instanceID);
                if (!windowIsOpen)
                {
                    var window = EditorWindow.CreateWindow<SequenceCreator>();
                    window.ImportSequence((SwarmSequence)asset);
                }
                else
                {
                    EditorWindow.FocusWindowIfItsOpen<SequenceCreator>();
                    Debug.Log("It seems you already have a SequenceCreator open. Use load button to load the sequence you want to modify");
                    return true;
                }
                return true;
            }



        }
    }

}
